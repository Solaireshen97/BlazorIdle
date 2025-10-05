using BlazorIdle.Server.Application.Abstractions;
using BlazorIdle.Server.Application.Battles.Step;
using BlazorIdle.Server.Domain.Characters;
using BlazorIdle.Server.Domain.Combat.Enemies;
using BlazorIdle.Server.Domain.Combat.Rng;

namespace BlazorIdle.Server.Application.Battles.Offline;

// 离线结算结果（最小版）：聚合时长/伤害/击杀（基于 encounter_cleared Tag）
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
}

public sealed class OfflineSettlementService
{
    private readonly ICharacterRepository _characters;

    public OfflineSettlementService(ICharacterRepository characters)
    {
        _characters = characters;
    }

    // mode: "continuous" | "dungeon" | "dungeonloop"
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

        // 快速推进到离线秒数
        rb.FastForwardTo(seconds);

        // 聚合：总伤害 + 基于 Tag 的击杀数（encounter_cleared）
        long totalDamage = 0;
        int kills = 0;
        foreach (var s in rb.Segments)
        {
            totalDamage += s.TotalDamage;
            if (s.TagCounters.TryGetValue("encounter_cleared", out var kc))
                kills += kc;
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
            DungeonId = dungeonId
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