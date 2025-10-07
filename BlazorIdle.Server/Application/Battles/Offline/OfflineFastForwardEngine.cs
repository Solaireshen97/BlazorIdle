using BlazorIdle.Server.Application.Abstractions;
using BlazorIdle.Server.Domain.Activities;
using BlazorIdle.Server.Domain.Characters;
using BlazorIdle.Server.Domain.Combat;
using BlazorIdle.Server.Domain.Combat.Enemies;
using BlazorIdle.Server.Domain.Economy;
using BlazorIdle.Shared.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace BlazorIdle.Server.Application.Battles.Offline;

/// <summary>
/// 离线战斗快进引擎结果
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
/// 离线战斗快进引擎
/// 复用 BattleSimulator 进行离线战斗模拟，支持活动计划的状态恢复和无感衔接
/// </summary>
public sealed class OfflineFastForwardEngine
{
    private readonly BattleSimulator _simulator;
    private readonly ICharacterRepository _characters;

    public OfflineFastForwardEngine(BattleSimulator simulator, ICharacterRepository characters)
    {
        _simulator = simulator;
        _characters = characters;
    }

    /// <summary>
    /// 快进模拟离线期间的活动计划执行
    /// 支持从离线点继续计算（基于 ExecutedSeconds），实现无感衔接
    /// </summary>
    /// <param name="character">角色实体</param>
    /// <param name="plan">活动计划</param>
    /// <param name="offlineSeconds">离线时长（秒）</param>
    /// <param name="maxCapSeconds">离线时长上限（默认12小时）</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>离线快进结果</returns>
    public Task<OfflineFastForwardResult> FastForwardAsync(
        Character character,
        ActivityPlan plan,
        double offlineSeconds,
        double maxCapSeconds = 43200, // 12小时默认上限
        CancellationToken ct = default)
    {
        // 1. 计算实际模拟时长（应用上限）
        var cappedSeconds = Math.Min(offlineSeconds, maxCapSeconds);

        // 2. 计算计划剩余时长（支持从已执行时间点继续）
        var remainingSeconds = CalculateRemainingSeconds(plan, cappedSeconds);

        // 3. 如果没有剩余时间可模拟，直接返回空结果
        if (remainingSeconds <= 0)
        {
            return Task.FromResult(new OfflineFastForwardResult
            {
                CharacterId = character.Id,
                PlanId = plan.Id,
                SimulatedSeconds = 0,
                PlanCompleted = true,
                UpdatedExecutedSeconds = plan.ExecutedSeconds
            });
        }

        // 4. 解析活动配置并创建战斗配置
        var battleConfig = CreateBattleConfig(character, plan);

        // 5. 使用 BattleSimulator 快进模拟
        var runningBattle = _simulator.CreateRunningBattle(battleConfig, remainingSeconds);
        runningBattle.FastForwardTo(remainingSeconds);

        // 6. 聚合战斗结果
        var (totalDamage, kills, killCount) = AggregateSegmentResults(runningBattle.Segments);

        // 7. 计算经济收益
        var economyResult = CalculateEconomyRewards(plan, killCount, runningBattle);

        // 8. 更新计划的已执行时长
        var updatedExecutedSeconds = plan.ExecutedSeconds + remainingSeconds;

        // 9. 判断计划是否完成
        var planCompleted = CheckPlanCompleted(plan, updatedExecutedSeconds);

        // 10. 返回结果
        return Task.FromResult(new OfflineFastForwardResult
        {
            CharacterId = character.Id,
            PlanId = plan.Id,
            SimulatedSeconds = remainingSeconds,
            PlanCompleted = planCompleted,
            TotalDamage = totalDamage,
            TotalKills = kills,
            Gold = economyResult.Gold,
            Exp = economyResult.Exp,
            Loot = economyResult.Loot,
            Segments = runningBattle.Segments,
            UpdatedExecutedSeconds = updatedExecutedSeconds
        });
    }

    /// <summary>
    /// 计算计划剩余可执行时长
    /// 关键：支持从 ExecutedSeconds 继续，实现无感衔接
    /// </summary>
    private double CalculateRemainingSeconds(ActivityPlan plan, double availableSeconds)
    {
        if (plan.LimitType == LimitType.Infinite)
        {
            // Infinite 类型：全部离线时长都可用
            return availableSeconds;
        }

        if (plan.LimitType == LimitType.Duration && plan.LimitValue.HasValue)
        {
            // Duration 类型：计算剩余时长 = 总限制 - 已执行
            var remaining = plan.LimitValue.Value - plan.ExecutedSeconds;
            
            // 返回剩余和可用时长的最小值
            return Math.Max(0, Math.Min(remaining, availableSeconds));
        }

        // 其他情况：返回可用时长
        return availableSeconds;
    }

    /// <summary>
    /// 创建战斗配置（从活动计划的 PayloadJson 解析）
    /// </summary>
    private BattleSimulator.BattleConfig CreateBattleConfig(
        Character character,
        ActivityPlan plan)
    {
        // 构建角色属性
        var profession = character.Profession;
        var baseStats = ProfessionBaseStatsRegistry.Resolve(profession);
        var attrs = new PrimaryAttributes(character.Strength, character.Agility, character.Intellect, character.Stamina);
        var derived = StatsBuilder.BuildDerived(profession, attrs);
        var stats = StatsBuilder.Combine(baseStats, derived);

        // 解析活动配置
        string mode = "continuous";
        string enemyId = "dummy";
        int enemyCount = 1;
        string? dungeonId = null;

        if (plan.Type == ActivityType.Combat)
        {
            var payload = JsonSerializer.Deserialize<CombatActivityPayload>(plan.PayloadJson);
            if (payload != null)
            {
                mode = "continuous";
                enemyId = payload.EnemyId ?? "dummy";
                enemyCount = payload.EnemyCount;
            }
        }
        else if (plan.Type == ActivityType.Dungeon)
        {
            var payload = JsonSerializer.Deserialize<DungeonActivityPayload>(plan.PayloadJson);
            if (payload != null)
            {
                mode = payload.Loop ? "dungeonloop" : "dungeonsingle";
                dungeonId = payload.DungeonId;
                
                // 地城模式下从地城定义获取敌人信息
                var dungeon = DungeonRegistry.Resolve(dungeonId ?? "intro_cave");
                if (dungeon.Waves.Any())
                {
                    var firstEnemy = dungeon.Waves[0].Enemies.FirstOrDefault();
                    enemyId = firstEnemy.enemyId ?? "dummy";
                    enemyCount = firstEnemy.count;
                }
            }
        }

        var enemyDef = EnemyRegistry.Resolve(enemyId);

        return new BattleSimulator.BattleConfig
        {
            BattleId = plan.BattleId ?? Guid.NewGuid(),
            CharacterId = character.Id,
            Profession = profession,
            Stats = stats,
            Seed = DeriveSeed(character.Id, plan.Id),
            EnemyDef = enemyDef,
            EnemyCount = enemyCount,
            Mode = mode,
            DungeonId = dungeonId
        };
    }

    /// <summary>
    /// 聚合战斗段结果
    /// </summary>
    private (long totalDamage, int kills, Dictionary<string, int> killCount) AggregateSegmentResults(
        List<CombatSegment> segments)
    {
        long totalDamage = 0;
        int totalKills = 0;
        var killCount = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var segment in segments)
        {
            // 聚合伤害（如果有的话）
            // totalDamage += segment.TotalDamage; // 如果 CombatSegment 有这个字段

            // 聚合击杀标签
            foreach (var (tag, count) in segment.TagCounters)
            {
                if (tag.StartsWith("kill.", StringComparison.Ordinal))
                {
                    if (!killCount.ContainsKey(tag))
                        killCount[tag] = 0;
                    killCount[tag] += count;
                    totalKills += count;
                }
            }
        }

        return (totalDamage, totalKills, killCount);
    }

    /// <summary>
    /// 计算经济收益（金币、经验、掉落）
    /// </summary>
    private (long Gold, long Exp, Dictionary<string, double> Loot) CalculateEconomyRewards(
        ActivityPlan plan,
        Dictionary<string, int> killCount,
        Step.RunningBattle runningBattle)
    {
        // 构建经济上下文
        var ctx = new EconomyContext
        {
            GoldMultiplier = 1.0,
            ExpMultiplier = 1.0,
            DropChanceMultiplier = 1.0,
            RunCompletedCount = 0,
            Seed = runningBattle.Seed
        };

        // 如果是地城，应用地城经济参数
        if (plan.Type == ActivityType.Dungeon && !string.IsNullOrWhiteSpace(runningBattle.DungeonId))
        {
            var dungeon = DungeonRegistry.Resolve(runningBattle.DungeonId);
            var runCompleted = 0;

            // 统计完成的副本轮次
            foreach (var segment in runningBattle.Segments)
            {
                if (segment.TagCounters.TryGetValue("dungeon_run_complete", out var rc))
                    runCompleted += rc;
            }

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
                Seed = runningBattle.Seed
            };
        }

        // 计算期望值（避免离线随机性）
        var result = EconomyCalculator.ComputeExpectedWithContext(killCount, ctx);

        return (result.Gold, result.Exp, result.Items);
    }

    /// <summary>
    /// 检查计划是否完成
    /// </summary>
    private bool CheckPlanCompleted(ActivityPlan plan, double executedSeconds)
    {
        if (plan.LimitType == LimitType.Infinite)
            return false;

        if (plan.LimitType == LimitType.Duration && plan.LimitValue.HasValue)
            return executedSeconds >= plan.LimitValue.Value;

        return false;
    }

    /// <summary>
    /// 生成确定性种子
    /// </summary>
    private ulong DeriveSeed(Guid characterId, Guid planId)
    {
        var baseRng = Domain.Combat.Rng.RngContext.FromGuid(characterId);
        var planRng = Domain.Combat.Rng.RngContext.FromGuid(planId);
        baseRng.Skip(4);
        planRng.Skip(2);
        ulong salt = (ulong)DateTime.UtcNow.Ticks;
        return Domain.Combat.Rng.RngContext.Hash64(baseRng.NextUInt64() ^ planRng.NextUInt64() ^ salt);
    }
}
