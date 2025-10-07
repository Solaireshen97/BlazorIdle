using BlazorIdle.Server.Domain.Activities;
using BlazorIdle.Server.Domain.Characters;
using BlazorIdle.Server.Domain.Combat;
using BlazorIdle.Server.Domain.Combat.Enemies;
using BlazorIdle.Server.Domain.Combat.Professions;
using BlazorIdle.Server.Domain.Combat.Rng;
using BlazorIdle.Server.Domain.Economy;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace BlazorIdle.Server.Application.Battles.Offline;

/// <summary>
/// 离线快进引擎结果
/// </summary>
public sealed class OfflineFastForwardResult
{
    public Guid CharacterId { get; init; }
    public Guid PlanId { get; init; }
    public double SimulatedSeconds { get; init; }
    public bool PlanCompleted { get; init; }
    public long TotalDamage { get; init; }
    public int TotalKills { get; init; }
    public long Gold { get; init; }
    public long Exp { get; init; }
    public Dictionary<string, double> Loot { get; init; } = new();
    public List<CombatSegment> Segments { get; init; } = new();
    public double UpdatedExecutedSeconds { get; init; }
}

/// <summary>
/// 离线快进引擎：用于快速模拟离线期间的战斗收益
/// 核心设计：
/// - 支持从离线点继续（通过 ExecutedSeconds 继承状态）
/// - 复用 BattleSimulator 进行快进
/// - 返回聚合的 CombatSegment 用于回顾
/// - 更新计划的 ExecutedSeconds，实现无感切换
/// </summary>
public sealed class OfflineFastForwardEngine
{
    private readonly BattleSimulator _simulator;

    public OfflineFastForwardEngine(BattleSimulator simulator)
    {
        _simulator = simulator;
    }

    /// <summary>
    /// 快进模拟离线期间的战斗
    /// </summary>
    /// <param name="character">角色信息</param>
    /// <param name="plan">活动计划</param>
    /// <param name="offlineSeconds">离线时长（秒）</param>
    /// <param name="maxCapSeconds">离线上限（默认12小时）</param>
    /// <returns>离线快进结果，包含收益和更新后的计划状态</returns>
    public OfflineFastForwardResult FastForward(
        Character character,
        ActivityPlan plan,
        double offlineSeconds,
        double maxCapSeconds = 43200.0) // 12小时默认上限
    {
        // 1. 限制离线时长
        var cappedSeconds = Math.Min(offlineSeconds, maxCapSeconds);

        // 2. 计算计划剩余时长
        var remainingSeconds = CalculateRemainingSeconds(plan, cappedSeconds);

        // 3. 如果没有剩余时间，直接返回空结果
        if (remainingSeconds <= 0)
        {
            return new OfflineFastForwardResult
            {
                CharacterId = character.Id,
                PlanId = plan.Id,
                SimulatedSeconds = 0,
                PlanCompleted = true,
                UpdatedExecutedSeconds = plan.ExecutedSeconds
            };
        }

        // 4. 使用 BattleSimulator 快进模拟
        var simulationResult = SimulatePlan(character, plan, remainingSeconds);

        // 5. 更新计划状态
        var updatedExecutedSeconds = plan.ExecutedSeconds + remainingSeconds;
        plan.ExecutedSeconds = updatedExecutedSeconds;
        var planCompleted = plan.IsLimitReached();

        // 6. 返回结果
        return new OfflineFastForwardResult
        {
            CharacterId = character.Id,
            PlanId = plan.Id,
            SimulatedSeconds = remainingSeconds,
            PlanCompleted = planCompleted,
            TotalDamage = simulationResult.TotalDamage,
            TotalKills = simulationResult.TotalKills,
            Gold = simulationResult.Gold,
            Exp = simulationResult.Exp,
            Loot = simulationResult.Loot,
            Segments = simulationResult.Segments,
            UpdatedExecutedSeconds = updatedExecutedSeconds
        };
    }

    /// <summary>
    /// 计算计划剩余时长
    /// </summary>
    private double CalculateRemainingSeconds(ActivityPlan plan, double availableSeconds)
    {
        if (plan.LimitType == LimitType.Infinite)
        {
            // 无限模式：使用全部离线时长
            return availableSeconds;
        }

        if (plan.LimitType == LimitType.Duration && plan.LimitValue.HasValue)
        {
            // 限时模式：计算剩余时长
            var remaining = plan.LimitValue.Value - plan.ExecutedSeconds;
            return Math.Max(0, Math.Min(remaining, availableSeconds));
        }

        return availableSeconds;
    }

    /// <summary>
    /// 模拟计划执行
    /// </summary>
    private SimulationResultWithEconomy SimulatePlan(
        Character character,
        ActivityPlan plan,
        double seconds)
    {
        // 构建角色数据
        var profession = character.Profession;
        var baseStats = ProfessionBaseStatsRegistry.Resolve(profession);
        var attrs = new PrimaryAttributes(character.Strength, character.Agility, character.Intellect, character.Stamina);
        var derived = StatsBuilder.BuildDerived(profession, attrs);
        var stats = StatsBuilder.Combine(baseStats, derived);

        // 根据计划类型构建配置
        BattleSimulator.BattleConfig config;
        EconomyContext economyContext;

        if (plan.Type == ActivityType.Combat)
        {
            var payload = JsonSerializer.Deserialize<CombatActivityPayload>(plan.PayloadJson);
            if (payload is null)
                throw new InvalidOperationException("Invalid combat payload");

            var enemyId = payload.EnemyId ?? "dummy";
            var enemyDef = EnemyRegistry.Resolve(enemyId);
            var seed = payload.Seed ?? DeriveSeed(character.Id);

            config = new BattleSimulator.BattleConfig
            {
                BattleId = Guid.NewGuid(),
                CharacterId = character.Id,
                Profession = profession,
                Stats = stats,
                Seed = seed,
                EnemyDef = enemyDef,
                EnemyCount = Math.Max(1, payload.EnemyCount),
                Mode = "continuous",
                DungeonId = null,
                ContinuousRespawnDelaySeconds = payload.RespawnDelay
            };

            economyContext = new EconomyContext
            {
                GoldMultiplier = 1.0,
                ExpMultiplier = 1.0,
                DropChanceMultiplier = 1.0,
                Seed = seed
            };
        }
        else if (plan.Type == ActivityType.Dungeon)
        {
            var payload = JsonSerializer.Deserialize<DungeonActivityPayload>(plan.PayloadJson);
            if (payload is null)
                throw new InvalidOperationException("Invalid dungeon payload");

            var dungeonId = payload.DungeonId;
            var dungeon = DungeonRegistry.Resolve(dungeonId);
            var seed = payload.Seed ?? DeriveSeed(character.Id);

            // 使用地下城的第一波第一个敌人作为占位符（实际由 provider 处理）
            var firstWave = dungeon.Waves.FirstOrDefault();
            var firstEnemy = firstWave?.Enemies.FirstOrDefault();
            var enemyDef = firstEnemy.HasValue && !string.IsNullOrEmpty(firstEnemy.Value.enemyId)
                ? EnemyRegistry.Resolve(firstEnemy.Value.enemyId)
                : EnemyRegistry.Resolve("dummy");

            config = new BattleSimulator.BattleConfig
            {
                BattleId = Guid.NewGuid(),
                CharacterId = character.Id,
                Profession = profession,
                Stats = stats,
                Seed = seed,
                EnemyDef = enemyDef,
                EnemyCount = 1,
                Mode = payload.Loop ? "dungeon_loop" : "dungeon_single",
                DungeonId = dungeonId,
                DungeonWaveDelaySeconds = payload.WaveDelay,
                DungeonRunDelaySeconds = payload.RunDelay
            };

            economyContext = new EconomyContext
            {
                GoldMultiplier = dungeon.GoldMultiplier,
                ExpMultiplier = dungeon.ExpMultiplier,
                DropChanceMultiplier = dungeon.DropChanceMultiplier,
                RunRewardGold = dungeon.RunRewardGold,
                RunRewardExp = dungeon.RunRewardExp,
                RunRewardLootTableId = dungeon.RunRewardLootTableId,
                RunRewardLootRolls = dungeon.RunRewardLootRolls,
                Seed = seed
            };
        }
        else
        {
            throw new NotImplementedException($"Activity type {plan.Type} is not implemented");
        }

        // 执行快进模拟
        var rb = _simulator.CreateRunningBattle(config, seconds);
        rb.FastForwardTo(seconds);

        // 聚合结果
        return AggregateResults(rb, economyContext);
    }

    /// <summary>
    /// 聚合模拟结果和经济收益
    /// </summary>
    private SimulationResultWithEconomy AggregateResults(
        Step.RunningBattle rb,
        EconomyContext context)
    {
        long totalDamage = 0;
        int runCompleted = 0;
        var killCount = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var segment in rb.Segments)
        {
            totalDamage += segment.TotalDamage;

            if (segment.TagCounters.TryGetValue("dungeon_run_complete", out var rc))
                runCompleted += rc;

            foreach (var (tag, val) in segment.TagCounters)
            {
                if (!tag.StartsWith("kill.", StringComparison.Ordinal))
                    continue;

                if (!killCount.ContainsKey(tag))
                    killCount[tag] = 0;
                killCount[tag] += val;
            }
        }

        // 更新上下文
        context.RunCompletedCount = runCompleted;

        // 计算经济收益（使用期望值模式）
        var economyResult = EconomyCalculator.ComputeExpectedWithContext(killCount, context);

        return new SimulationResultWithEconomy
        {
            TotalDamage = totalDamage,
            TotalKills = killCount.Values.Sum(),
            Gold = economyResult.Gold,
            Exp = economyResult.Exp,
            Loot = economyResult.Items,
            Segments = rb.Segments
        };
    }

    /// <summary>
    /// 生成种子
    /// </summary>
    private static ulong DeriveSeed(Guid characterId)
    {
        var baseRng = RngContext.FromGuid(characterId);
        baseRng.Skip(4);
        ulong salt = (ulong)DateTime.UtcNow.Ticks;
        return RngContext.Hash64(baseRng.NextUInt64() ^ salt);
    }

    /// <summary>
    /// 内部结果结构
    /// </summary>
    private sealed class SimulationResultWithEconomy
    {
        public long TotalDamage { get; init; }
        public int TotalKills { get; init; }
        public long Gold { get; init; }
        public long Exp { get; init; }
        public Dictionary<string, double> Loot { get; init; } = new();
        public List<CombatSegment> Segments { get; init; } = new();
    }
}
