using System.Collections.Generic;

namespace BlazorIdle.Server.Domain.Combat.Enemies;

public sealed class DungeonDefinition
{
    public string Id { get; }
    public string Name { get; }
    public IReadOnlyList<Wave> Waves { get; }

    // 现有：波次/整轮刷新延迟
    public double WaveRespawnDelaySeconds { get; }
    public double RunRespawnDelaySeconds { get; }

    // 新增：经济倍率（默认 1.0）
    public double GoldMultiplier { get; }
    public double ExpMultiplier { get; }
    public double DropChanceMultiplier { get; }

    // 新增：整轮完成奖励（固定 + 掉落表按 rolls）
    public int RunRewardGold { get; }
    public int RunRewardExp { get; }
    public string? RunRewardLootTableId { get; }
    public int RunRewardLootRolls { get; }
    
    // Phase 6: 强化型地下城预留
    /// <summary>是否允许玩家自动复活（默认 true）</summary>
    public bool AllowAutoRevive { get; }
    /// <summary>强化掉落倍率（默认 1.0）</summary>
    public double EnhancedDropMultiplier { get; }
    /// <summary>玩家死亡时是否重置副本（默认 false）</summary>
    public bool ResetOnPlayerDeath { get; }

    public DungeonDefinition(
        string id,
        string name,
        IReadOnlyList<Wave> waves,
        double waveRespawnDelaySeconds = 3.0,
        double runRespawnDelaySeconds = 10.0,
        // 经济：默认 1 倍
        double goldMultiplier = 1.0,
        double expMultiplier = 1.0,
        double dropChanceMultiplier = 1.0,
        // 整轮奖励（默认无）
        int runRewardGold = 0,
        int runRewardExp = 0,
        string? runRewardLootTableId = null,
        int runRewardLootRolls = 0,
        // Phase 6: 强化型地下城
        bool allowAutoRevive = true,
        double enhancedDropMultiplier = 1.0,
        bool resetOnPlayerDeath = false)
    {
        Id = id;
        Name = name;
        Waves = waves;
        WaveRespawnDelaySeconds = waveRespawnDelaySeconds <= 0 ? 0 : waveRespawnDelaySeconds;
        RunRespawnDelaySeconds = runRespawnDelaySeconds <= 0 ? 0 : runRespawnDelaySeconds;

        GoldMultiplier = goldMultiplier <= 0 ? 1.0 : goldMultiplier;
        ExpMultiplier = expMultiplier <= 0 ? 1.0 : expMultiplier;
        DropChanceMultiplier = dropChanceMultiplier <= 0 ? 1.0 : dropChanceMultiplier;

        RunRewardGold = runRewardGold < 0 ? 0 : runRewardGold;
        RunRewardExp = runRewardExp < 0 ? 0 : runRewardExp;
        RunRewardLootTableId = runRewardLootTableId;
        RunRewardLootRolls = runRewardLootRolls < 0 ? 0 : runRewardLootRolls;
        
        // Phase 6: 强化型地下城
        AllowAutoRevive = allowAutoRevive;
        EnhancedDropMultiplier = enhancedDropMultiplier <= 0 ? 1.0 : enhancedDropMultiplier;
        ResetOnPlayerDeath = resetOnPlayerDeath;
    }

    public sealed class Wave
    {
        public IReadOnlyList<(string enemyId, int count)> Enemies { get; }
        public Wave(IReadOnlyList<(string enemyId, int count)> enemies) => Enemies = enemies;
    }
}