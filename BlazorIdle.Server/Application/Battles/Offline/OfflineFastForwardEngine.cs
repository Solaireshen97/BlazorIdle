using BlazorIdle.Server.Domain.Activities;
using BlazorIdle.Server.Domain.Characters;
using BlazorIdle.Server.Domain.Combat.Enemies;
using BlazorIdle.Server.Domain.Combat.Rng;
using BlazorIdle.Server.Domain.Economy;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace BlazorIdle.Server.Application.Battles.Offline;

/// <summary>
/// 离线快进引擎
/// 负责模拟离线期间的活动计划执行，复用 BattleSimulator 进行战斗模拟
/// </summary>
public sealed class OfflineFastForwardEngine
{
    private readonly BattleSimulator _simulator;

    public OfflineFastForwardEngine(BattleSimulator simulator)
    {
        _simulator = simulator ?? throw new ArgumentNullException(nameof(simulator));
    }

    /// <summary>
    /// 快进模拟离线期间的活动计划执行
    /// </summary>
    /// <param name="character">角色实体</param>
    /// <param name="plan">活动计划</param>
    /// <param name="offlineSeconds">离线时长（秒）</param>
    /// <param name="maxCapSeconds">最大离线时长上限（默认12小时）</param>
    /// <returns>离线快进结果</returns>
    public OfflineFastForwardResult FastForward(
        Character character,
        ActivityPlan plan,
        double offlineSeconds,
        double maxCapSeconds = 43200.0) // 12小时 = 43200秒
    {
        if (character == null)
            throw new ArgumentNullException(nameof(character));
        if (plan == null)
            throw new ArgumentNullException(nameof(plan));
        if (offlineSeconds < 0)
            throw new ArgumentOutOfRangeException(nameof(offlineSeconds), "离线时长不能为负数");

        // 1. 限制离线时长（不超过上限）
        var cappedOfflineSeconds = Math.Min(offlineSeconds, maxCapSeconds);

        // 2. 计算计划剩余时长
        var remainingSeconds = CalculateRemainingSeconds(plan, cappedOfflineSeconds);

        // 3. 快进模拟战斗
        var simulationResult = SimulateBattle(character, plan, remainingSeconds);

        // 4. 更新计划的已执行时长
        var updatedExecutedSeconds = plan.ExecutedSeconds + simulationResult.SimulatedSeconds;

        // 5. 判断计划是否完成
        var planCompleted = CheckPlanCompleted(plan, updatedExecutedSeconds);

        return new OfflineFastForwardResult
        {
            CharacterId = character.Id,
            PlanId = plan.Id,
            SimulatedSeconds = simulationResult.SimulatedSeconds,
            PlanCompleted = planCompleted,
            TotalDamage = simulationResult.TotalDamage,
            TotalKills = simulationResult.TotalKills,
            Gold = simulationResult.Gold,
            Exp = simulationResult.Exp,
            Loot = simulationResult.Loot,
            UpdatedExecutedSeconds = updatedExecutedSeconds
        };
    }

    /// <summary>
    /// 计算计划剩余时长
    /// </summary>
    private double CalculateRemainingSeconds(ActivityPlan plan, double cappedOfflineSeconds)
    {
        if (plan.LimitType == LimitType.Infinite)
        {
            // 无限制计划：使用全部离线时长
            return cappedOfflineSeconds;
        }

        if (plan.LimitType == LimitType.Duration && plan.LimitValue.HasValue)
        {
            // 时长限制计划：计算剩余时长
            var remaining = plan.LimitValue.Value - plan.ExecutedSeconds;

            // 确保不超过实际离线时长
            return Math.Min(Math.Max(0, remaining), cappedOfflineSeconds);
        }

        // 其他情况默认返回0
        return 0;
    }

    /// <summary>
    /// 判断计划是否已完成
    /// </summary>
    private bool CheckPlanCompleted(ActivityPlan plan, double updatedExecutedSeconds)
    {
        if (plan.LimitType == LimitType.Infinite)
            return false;

        if (plan.LimitType == LimitType.Duration && plan.LimitValue.HasValue)
            return updatedExecutedSeconds >= plan.LimitValue.Value;

        return false;
    }

    /// <summary>
    /// 模拟战斗并计算收益
    /// </summary>
    private SimulationSummary SimulateBattle(Character character, ActivityPlan plan, double durationSeconds)
    {
        // 如果时长为0，直接返回空结果
        if (durationSeconds <= 0)
        {
            return new SimulationSummary
            {
                SimulatedSeconds = 0,
                TotalDamage = 0,
                TotalKills = 0,
                Gold = 0,
                Exp = 0,
                Loot = new Dictionary<string, double>()
            };
        }

        // 解析活动配置
        var payload = ParseCombatPayload(plan.PayloadJson);
        if (payload == null)
        {
            throw new InvalidOperationException($"无法解析活动计划 {plan.Id} 的配置数据");
        }

        // 构建角色属性
        var profession = character.Profession;
        var baseStats = ProfessionBaseStatsRegistry.Resolve(profession);
        var attrs = new PrimaryAttributes(
            character.Strength,
            character.Agility,
            character.Intellect,
            character.Stamina
        );
        var derived = StatsBuilder.BuildDerived(profession, attrs);
        var stats = StatsBuilder.Combine(baseStats, derived);

        // 解析敌人定义
        var enemyDef = EnemyRegistry.Resolve(payload.EnemyId ?? "dummy");

        // 生成随机种子
        var seed = DeriveSeed(character.Id, plan.Id);

        // 创建战斗配置
        var config = new BattleSimulator.BattleConfig
        {
            BattleId = Guid.NewGuid(),
            CharacterId = character.Id,
            Profession = profession,
            Stats = stats,
            Seed = seed,
            EnemyDef = enemyDef,
            EnemyCount = payload.EnemyCount > 0 ? payload.EnemyCount : 1,
            Mode = payload.Mode ?? "continuous",
            DungeonId = payload.DungeonId
        };

        // 执行战斗模拟
        var runningBattle = _simulator.CreateRunningBattle(config, durationSeconds);
        runningBattle.FastForwardTo(durationSeconds);

        // 统计战斗结果
        int totalKills = 0;
        int runCompleted = 0;
        var killCount = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var segment in runningBattle.Segments)
        {
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

        // 计算总击杀数
        totalKills = killCount.Values.Sum();

        // 构建经济上下文
        var economyContext = BuildEconomyContext(payload, seed, runCompleted);

        // 计算收益（使用期望值模式）
        var economyResult = EconomyCalculator.ComputeExpectedWithContext(killCount, economyContext);

        return new SimulationSummary
        {
            SimulatedSeconds = durationSeconds,
            TotalDamage = 0, // 可以从 segments 计算，暂时返回0
            TotalKills = totalKills,
            Gold = economyResult.Gold,
            Exp = economyResult.Exp,
            Loot = economyResult.Items
        };
    }

    /// <summary>
    /// 解析战斗活动配置
    /// </summary>
    private CombatPayloadData? ParseCombatPayload(string payloadJson)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(payloadJson) || payloadJson == "{}")
            {
                // 返回默认配置
                return new CombatPayloadData
                {
                    EnemyId = "dummy",
                    EnemyCount = 1,
                    Mode = "continuous"
                };
            }

            return JsonSerializer.Deserialize<CombatPayloadData>(payloadJson);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 构建经济上下文
    /// </summary>
    private EconomyContext BuildEconomyContext(CombatPayloadData payload, ulong seed, int runCompleted)
    {
        var ctx = new EconomyContext
        {
            GoldMultiplier = 1.0,
            ExpMultiplier = 1.0,
            DropChanceMultiplier = 1.0,
            RunCompletedCount = runCompleted,
            Seed = seed
        };

        // 如果是副本模式，应用副本的经济系数
        if (!string.IsNullOrWhiteSpace(payload.DungeonId))
        {
            var dungeon = DungeonRegistry.Resolve(payload.DungeonId);
            ctx = new EconomyContext
            {
                GoldMultiplier = dungeon.GoldMultiplier,
                ExpMultiplier = dungeon.ExpMultiplier,
                DropChanceMultiplier = dungeon.DropChanceMultiplier,
                RunCompletedCount = runCompleted,
                RunRewardGold = dungeon.RunRewardGold,
                RunRewardExp = dungeon.RunRewardExp,
                RunRewardLootTableId = dungeon.RunRewardLootTableId,
                RunRewardLootRolls = dungeon.RunRewardLootRolls,
                Seed = seed
            };
        }

        return ctx;
    }

    /// <summary>
    /// 生成随机种子
    /// </summary>
    private static ulong DeriveSeed(Guid characterId, Guid planId)
    {
        var charRng = RngContext.FromGuid(characterId);
        var planRng = RngContext.FromGuid(planId);
        charRng.Skip(2);
        planRng.Skip(3);
        ulong salt = (ulong)DateTime.UtcNow.Ticks;
        return RngContext.Hash64(charRng.NextUInt64() ^ planRng.NextUInt64() ^ salt);
    }

    /// <summary>
    /// 战斗配置数据（从 PayloadJson 解析）
    /// </summary>
    private sealed class CombatPayloadData
    {
        public string? EnemyId { get; set; }
        public int EnemyCount { get; set; }
        public string? Mode { get; set; }
        public string? DungeonId { get; set; }
    }

    /// <summary>
    /// 模拟结果摘要（内部使用）
    /// </summary>
    private sealed class SimulationSummary
    {
        public double SimulatedSeconds { get; init; }
        public long TotalDamage { get; init; }
        public int TotalKills { get; init; }
        public long Gold { get; init; }
        public long Exp { get; init; }
        public Dictionary<string, double> Loot { get; init; } = new();
    }
}
