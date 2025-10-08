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
/// 离线战斗快进结果
/// </summary>
public sealed class OfflineFastForwardResult
{
    public Guid CharacterId { get; init; }
    public Guid PlanId { get; init; }
    public double SimulatedSeconds { get; init; }      // 实际模拟时长
    public bool PlanCompleted { get; init; }           // 计划是否完成
    public long TotalDamage { get; init; }
    public int TotalKills { get; init; }
    public long Gold { get; init; }                    // 金币收益
    public long Exp { get; init; }                     // 经验收益
    public Dictionary<string, double> LootExpected { get; init; } = new();
    public Dictionary<string, int> LootSampled { get; init; } = new();
    public double UpdatedExecutedSeconds { get; init; } // 更新后的已执行时长
    public string DropMode { get; init; } = "expected";
}

/// <summary>
/// 离线战斗快进引擎
/// 专门处理离线期间的战斗计算，支持从离线时刻的进度无感继承
/// </summary>
public class OfflineFastForwardEngine
{
    private readonly BattleSimulator _simulator;

    public OfflineFastForwardEngine(BattleSimulator simulator)
    {
        _simulator = simulator;
    }

    /// <summary>
    /// 快进模拟离线战斗
    /// </summary>
    /// <param name="character">角色数据</param>
    /// <param name="plan">活动计划（包含已执行时长）</param>
    /// <param name="offlineSeconds">离线时长（秒）</param>
    /// <param name="maxCapSeconds">离线时长上限（默认12小时）</param>
    /// <param name="dropMode">掉落模式：expected 或 sampled</param>
    /// <returns>离线快进结果</returns>
    public OfflineFastForwardResult FastForward(
        Character character,
        ActivityPlan plan,
        double offlineSeconds,
        double maxCapSeconds = 43200, // 12小时
        string dropMode = "expected")
    {
        // 允许处理 Running 或 Paused 状态的计划
        if (plan.State != ActivityState.Running && plan.State != ActivityState.Paused)
        {
            throw new InvalidOperationException($"Cannot fast forward plan in state {plan.State}");
        }

        // 1. 应用离线时长上限
        var cappedOfflineSeconds = Math.Min(offlineSeconds, maxCapSeconds);

        // 2. 计算计划剩余时长（考虑已执行时长，实现无感继承）
        var remainingSeconds = CalculateRemainingSeconds(plan, cappedOfflineSeconds);

        // 3. 如果没有剩余时长可执行，直接返回空结果
        if (remainingSeconds <= 0)
        {
            return new OfflineFastForwardResult
            {
                CharacterId = character.Id,
                PlanId = plan.Id,
                SimulatedSeconds = 0,
                PlanCompleted = plan.IsLimitReached(),
                UpdatedExecutedSeconds = plan.ExecutedSeconds,
                DropMode = dropMode
            };
        }

        // 4. 快进模拟战斗（传入战斗状态快照以继承进度）
        var simulationResult = SimulateBattle(character, plan, remainingSeconds, dropMode);

        // 5. 更新计划的已执行时长
        var updatedExecutedSeconds = plan.ExecutedSeconds + remainingSeconds;
        plan.ExecutedSeconds = updatedExecutedSeconds;

        // 6. 判断计划是否完成
        var planCompleted = plan.IsLimitReached();
        if (planCompleted)
        {
            plan.State = ActivityState.Completed;
            plan.CompletedAt = DateTime.UtcNow;
            // 清空战斗状态，计划已完成
            plan.BattleStateJson = null;
        }

        // 7. 返回结果
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
            LootExpected = simulationResult.LootExpected,
            LootSampled = simulationResult.LootSampled,
            UpdatedExecutedSeconds = updatedExecutedSeconds,
            DropMode = simulationResult.DropMode
        };
    }

    /// <summary>
    /// 计算计划剩余时长
    /// </summary>
    private double CalculateRemainingSeconds(ActivityPlan plan, double cappedOfflineSeconds)
    {
        if (plan.LimitType == LimitType.Infinite)
        {
            // 无限计划：模拟全部离线时长（已受上限约束）
            return cappedOfflineSeconds;
        }

        if (plan.LimitType == LimitType.Duration && plan.LimitValue.HasValue)
        {
            // Duration 计划：计算剩余时长 = 限制值 - 已执行时长
            var remaining = plan.LimitValue.Value - plan.ExecutedSeconds;
            
            // 取离线时长和剩余时长的最小值
            return Math.Max(0, Math.Min(remaining, cappedOfflineSeconds));
        }

        // 其他情况返回 0
        return 0;
    }

    /// <summary>
    /// 模拟战斗并计算收益
    /// </summary>
    private SimulationResultWithEconomy SimulateBattle(
        Character character, 
        ActivityPlan plan, 
        double durationSeconds,
        string dropMode)
    {
        // 加载之前的战斗状态快照（如果有）
        BattleState? previousState = null;
        if (!string.IsNullOrWhiteSpace(plan.BattleStateJson))
        {
            try
            {
                previousState = JsonSerializer.Deserialize<BattleState>(plan.BattleStateJson);
            }
            catch
            {
                // 如果反序列化失败，忽略快照，从头开始
                previousState = null;
            }
        }

        // 构建角色战斗数据
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

        // 根据活动类型构建配置
        BattleSimulator.BattleConfig config;
        EconomyContext economyContext;

        if (plan.Type == ActivityType.Combat)
        {
            var payload = JsonSerializer.Deserialize<CombatActivityPayload>(plan.PayloadJson);
            if (payload is null)
                throw new InvalidOperationException("Invalid combat payload");

            var enemyDef = EnemyRegistry.Resolve(payload.EnemyId ?? "dummy");
            var seed = payload.Seed ?? DeriveSeed(character.Id);

            config = new BattleSimulator.BattleConfig
            {
                BattleId = plan.BattleId ?? Guid.NewGuid(),
                CharacterId = character.Id,
                Profession = profession,
                Stats = stats,
                Seed = seed,
                EnemyDef = enemyDef,
                EnemyCount = payload.EnemyCount,
                Mode = "continuous",
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

            // For dungeons, we need a dummy enemy for the RunningBattle constructor
            // even though it won't be used (dungeon uses DungeonEncounterProvider)
            var dummyEnemy = EnemyRegistry.Resolve("dummy");

            config = new BattleSimulator.BattleConfig
            {
                BattleId = plan.BattleId ?? Guid.NewGuid(),
                CharacterId = character.Id,
                Profession = profession,
                Stats = stats,
                Seed = seed,
                EnemyDef = dummyEnemy,
                EnemyCount = 1,
                Mode = payload.Loop ? "dungeonloop" : "dungeonsingle",
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
            throw new NotImplementedException($"Activity type {plan.Type} is not implemented for offline fast forward");
        }

        // 执行战斗模拟
        var runningBattle = _simulator.CreateRunningBattle(config, durationSeconds);
        
        // 恢复之前的战斗状态（敌人血量、波次等）
        if (previousState != null)
        {
            runningBattle.Engine.RestoreBattleState(previousState);
        }
        
        runningBattle.FastForwardTo(durationSeconds);

        // 捕获当前战斗状态并保存到计划中（用于下次继续）
        var currentState = runningBattle.Engine.CaptureBattleState();
        plan.BattleStateJson = JsonSerializer.Serialize(currentState);

        // 统计击杀数据
        var killCount = new Dictionary<string, int>(StringComparer.Ordinal);
        int runCompleted = 0;
        
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

        // 更新经济上下文的 runCompleted
        economyContext.RunCompletedCount = runCompleted;

        // 计算经济收益
        long gold, exp;
        Dictionary<string, double> lootExpected = new();
        Dictionary<string, int> lootSampled = new();
        string finalDropMode;

        var dm = dropMode.Trim().ToLowerInvariant();
        if (dm == "sampled")
        {
            var result = EconomyCalculator.ComputeSampledWithContext(killCount, economyContext);
            gold = result.Gold;
            exp = result.Exp;
            lootSampled = result.Items.ToDictionary(kv => kv.Key, kv => (int)Math.Round(kv.Value));
            finalDropMode = "sampled";
        }
        else
        {
            var result = EconomyCalculator.ComputeExpectedWithContext(killCount, economyContext);
            gold = result.Gold;
            exp = result.Exp;
            lootExpected = result.Items;
            finalDropMode = "expected";
        }

        // 计算总击杀数
        int totalKills = killCount.Values.Sum();

        return new SimulationResultWithEconomy
        {
            TotalDamage = 0, // 可以从 segments 中累计，这里简化处理
            TotalKills = totalKills,
            Gold = gold,
            Exp = exp,
            LootExpected = lootExpected,
            LootSampled = lootSampled,
            DropMode = finalDropMode
        };
    }

    /// <summary>
    /// 生成随机种子
    /// </summary>
    private static ulong DeriveSeed(Guid characterId)
    {
        var baseRng = RngContext.FromGuid(characterId);
        baseRng.Skip(4);
        ulong salt = (ulong)DateTime.UtcNow.Ticks;
        return RngContext.Hash64(baseRng.NextUInt64() ^ salt);
    }

    /// <summary>
    /// 模拟结果（带经济数据）
    /// </summary>
    private sealed class SimulationResultWithEconomy
    {
        public long TotalDamage { get; init; }
        public int TotalKills { get; init; }
        public long Gold { get; init; }
        public long Exp { get; init; }
        public Dictionary<string, double> LootExpected { get; init; } = new();
        public Dictionary<string, int> LootSampled { get; init; } = new();
        public string DropMode { get; init; } = "expected";
    }
}
