using BlazorIdle.Server.Application.Abstractions;
using BlazorIdle.Server.Application.Battles.Step;
using BlazorIdle.Server.Domain.Characters;
using BlazorIdle.Server.Domain.Combat.Enemies;
using BlazorIdle.Server.Domain.Combat.Rng;
using BlazorIdle.Server.Domain.Economy;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace BlazorIdle.Server.Application.Battles.Offline;

public sealed class OfflineSettleResult
{
    public Guid CharacterId { get; init; }
    public double SimulatedSeconds { get; init; }
    public long TotalDamage { get; init; }
    public int TotalKills { get; init; }
    public string Mode { get; init; } = "continuous";
    public string EnemyId { get; init; } = "dummy";
    public int EnemyCount { get; init; } = 1;
    public string? DungeonId { get; init; }

    public string DropMode { get; init; } = "expected";
    public long Gold { get; init; }
    public long Exp { get; init; }
    public Dictionary<string, double> LootExpected { get; init; } = new();
    public Dictionary<string, int> LootSampled { get; init; } = new();
}

public sealed class OfflineSettlementService
{
    private readonly ICharacterRepository _characters;

    public OfflineSettlementService(ICharacterRepository characters) => _characters = characters;

    // 新增 dropMode: "expected" | "sampled"
    public async Task<OfflineSettleResult> SimulateAsync(
        Guid characterId,
        TimeSpan offlineDuration,
        string? mode = "continuous",
        string? enemyId = "dummy",
        int enemyCount = 1,
        string? dungeonId = null,
        ulong? seed = null,
        string? dropMode = "expected",
        CancellationToken ct = default)
    {
        var c = await _characters.GetAsync(characterId, ct) ?? throw new InvalidOperationException("Character not found");

        var profession = c.Profession;
        var baseStats = ProfessionBaseStatsRegistry.Resolve(profession);
        var attrs = new PrimaryAttributes(c.Strength, c.Agility, c.Intellect, c.Stamina);
        var derived = StatsBuilder.BuildDerived(profession, attrs);
        var stats = StatsBuilder.Combine(baseStats, derived);

        var enemyDef = EnemyRegistry.Resolve(enemyId);
        var seconds = Math.Max(1.0, offlineDuration.TotalSeconds);

        var stepMode = (mode ?? "continuous").ToLowerInvariant() switch
        {
            "continuous" => StepBattleMode.Continuous,
            "dungeon" => StepBattleMode.DungeonSingle,
            "dungeonloop" => StepBattleMode.DungeonLoop,
            _ => StepBattleMode.Continuous
        };

        ulong finalSeed = seed ?? DeriveSeed(characterId);
        var rb = new RunningBattle(
            id: Guid.NewGuid(),
            characterId: characterId,
            profession: profession,
            seed: finalSeed,
            targetSeconds: seconds,
            enemyDef: enemyDef,
            enemyCount: Math.Max(1, enemyCount),
            stats: stats,
            mode: stepMode,
            dungeonId: dungeonId
        );

        rb.FastForwardTo(seconds);

        long totalDamage = 0;
        int kills = 0;
        int runCompleted = 0;
        var killCount = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var s in rb.Segments)
        {
            if (s.TagCounters.TryGetValue("dungeon_run_complete", out var rc)) runCompleted += rc;
            foreach (var (tag, val) in s.TagCounters)
            {
                if (!tag.StartsWith("kill.", StringComparison.Ordinal)) continue;
                if (!killCount.ContainsKey(tag)) killCount[tag] = 0;
                killCount[tag] += val;
            }
        }

        // 构建 ctx
        var ctx = new EconomyContext
        {
            GoldMultiplier = 1.0,
            ExpMultiplier = 1.0,
            DropChanceMultiplier = 1.0,
            RunCompletedCount = runCompleted,
            Seed = finalSeed
        };

        if (!string.IsNullOrWhiteSpace(dungeonId))
        {
            var d = DungeonRegistry.Resolve(dungeonId!);
            ctx = new EconomyContext
            {
                GoldMultiplier = d.GoldMultiplier,
                ExpMultiplier = d.ExpMultiplier,
                DropChanceMultiplier = d.DropChanceMultiplier,
                RunCompletedCount = runCompleted,
                RunRewardGold = d.RunRewardGold,
                RunRewardExp = d.RunRewardExp,
                RunRewardLootTableId = d.RunRewardLootTableId,
                RunRewardLootRolls = d.RunRewardLootRolls,
                Seed = finalSeed
            };
        }

        var dm = (dropMode ?? "expected").Trim().ToLowerInvariant();
        long gold; long exp; Dictionary<string, double> lootExp = new(); Dictionary<string, int> lootSmp = new();
        if (dm == "sampled")
        {
            var r = EconomyCalculator.ComputeSampledWithContext(killCount, ctx);
            gold = r.Gold; exp = r.Exp;
            lootSmp = r.Items.ToDictionary(kv => kv.Key, kv => (int)Math.Round(kv.Value));
            dm = "sampled";
        }
        else
        {
            var r = EconomyCalculator.ComputeExpectedWithContext(killCount, ctx);
            gold = r.Gold; exp = r.Exp;
            lootExp = r.Items;
            dm = "expected";
        }

        return new OfflineSettleResult
        {
            CharacterId = characterId,
            SimulatedSeconds = seconds,
            TotalDamage = totalDamage,
            TotalKills = kills,
            Mode = mode ?? "continuous",
            EnemyId = enemyDef.Id,
            EnemyCount = Math.Max(1, enemyCount),
            DungeonId = dungeonId,
            DropMode = dm,
            Gold = gold,
            Exp = exp,
            LootExpected = lootExp,
            LootSampled = lootSmp
        };
    }

    private static ulong DeriveSeed(Guid characterId)
    {
        var baseRng = RngContext.FromGuid(characterId);
        baseRng.Skip(4);
        ulong salt = (ulong)DateTime.UtcNow.Ticks;
        return RngContext.Hash64(baseRng.NextUInt64() ^ salt);
    }
}