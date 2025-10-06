using BlazorIdle.Server.Application.Abstractions;
using BlazorIdle.Server.Application.Battles.Step;
using BlazorIdle.Server.Domain.Characters;
using BlazorIdle.Server.Domain.Combat.Enemies;
using BlazorIdle.Server.Domain.Combat.Rng;
using BlazorIdle.Server.Domain.Economy;

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

    // 新增：经济收益
    public long Gold { get; init; }
    public long Exp { get; init; }
    public Dictionary<string, double> LootExpected { get; init; } = new();
}

public sealed class OfflineSettlementService
{
    private readonly ICharacterRepository _characters;

    public OfflineSettlementService(ICharacterRepository characters) => _characters = characters;

    public async Task<OfflineSettleResult> SimulateAsync(
        Guid characterId,
        TimeSpan offlineDuration,
        string? mode = "continuous",
        string? enemyId = "dummy",
        int enemyCount = 1,
        string? dungeonId = null,
        ulong? seed = null,
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

        // 聚合：总伤害 + 击杀 + 经济
        long totalDamage = 0;
        int kills = 0;
        var killCount = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var s in rb.Segments)
        {
            totalDamage += s.TotalDamage;
            if (s.TagCounters.TryGetValue("encounter_cleared", out var kc))
                kills += kc;

            foreach (var (tag, val) in s.TagCounters)
            {
                if (!tag.StartsWith("kill.", StringComparison.Ordinal)) continue;
                if (!killCount.ContainsKey(tag)) killCount[tag] = 0;
                killCount[tag] += val;
            }
        }

        var reward = EconomyCalculator.ComputeExpected(killCount);

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
            Gold = reward.Gold,
            Exp = reward.Exp,
            LootExpected = reward.Items
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