using BlazorIdle.Server.Application.Abstractions;
using BlazorIdle.Server.Domain.Characters;
using BlazorIdle.Server.Domain.Combat.Enemies;
using BlazorIdle.Server.Domain.Combat.Professions;
using BlazorIdle.Server.Domain.Economy;
using BlazorIdle.Shared.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using static BlazorIdle.Server.Domain.Economy.EconomyCalculator;

namespace BlazorIdle.Server.Application.Battles.Step;

public sealed class StepBattleCoordinator
{
    private readonly ConcurrentDictionary<Guid, RunningBattle> _running = new();
    private readonly ConcurrentDictionary<Guid, DateTime> _completedAtUtc = new();

    private readonly IServiceScopeFactory _scopeFactory; // 改为作用域工厂
    
    // 边打边发配置：奖励发放间隔（模拟时间）
    private readonly double _rewardFlushIntervalSimSeconds;
    private readonly bool _enablePeriodicRewards;

    public StepBattleCoordinator(IServiceScopeFactory scopeFactory, IConfiguration config)
    {
        _scopeFactory = scopeFactory;
        _rewardFlushIntervalSimSeconds = config.GetValue<double>("Combat:RewardFlushIntervalSeconds", 10.0);
        _enablePeriodicRewards = config.GetValue<bool>("Combat:EnablePeriodicRewards", true);
    }

    // 新增覆盖：continuousRespawnDelaySeconds / dungeonWaveDelaySeconds / dungeonRunDelaySeconds
    // battleState: 用于恢复离线/在线切换时的战斗状态
    // stamina: 角色耐力，用于计算最大血量
    public Guid Start(Guid characterId, Profession profession, CharacterStats stats, double seconds, ulong seed, string? enemyId, int enemyCount,
        StepBattleMode mode = StepBattleMode.Duration, string? dungeonId = null,
        double? continuousRespawnDelaySeconds = null, double? dungeonWaveDelaySeconds = null, double? dungeonRunDelaySeconds = null,
        Offline.BattleState? battleState = null, int stamina = 10)
    {
        var eid = EnemyRegistry.Resolve(enemyId).Id;
        var enemy = EnemyRegistry.Resolve(eid);
        var id = Guid.NewGuid();

        var rb = new RunningBattle(
            id: id,
            characterId: characterId,
            profession: profession,
            seed: seed,
            targetSeconds: seconds,
            enemyDef: enemy,
            enemyCount: enemyCount,
            stats: stats,
            mode: mode,
            dungeonId: dungeonId,
            continuousRespawnDelaySeconds: continuousRespawnDelaySeconds,
            dungeonWaveDelaySeconds: dungeonWaveDelaySeconds,
            dungeonRunDelaySeconds: dungeonRunDelaySeconds,
            stamina: stamina
        );

        // 恢复战斗状态（如果有）
        if (battleState != null)
        {
            rb.Engine.RestoreBattleState(battleState);
        }

        if (!_running.TryAdd(id, rb))
            throw new InvalidOperationException("Failed to register running battle.");

        return id;
    }

    internal IEnumerable<Guid> InternalIdsSnapshot() => _running.Keys.ToArray();

    public (bool found, StepBattleStatusDto status) GetStatus(Guid id, string? dropMode = null)
    {
        if (!_running.TryGetValue(id, out var rb))
            return (false, default!);

        var totalDamage = rb.Segments.Sum(s => s.TotalDamage);
        var simulated = rb.Clock.CurrentTime;
        var effectiveDuration = rb.Completed
            ? Math.Min(rb.TargetDurationSeconds, rb.Battle.EndedAt ?? (rb.KillTime ?? simulated))
            : simulated;

        var dps = totalDamage / Math.Max(0.0001, effectiveDuration);

        // 聚合 kill.* 与 dungeon_run_complete
        var killCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        int runCompleted = 0;
        foreach (var seg in rb.Segments)
        {
            if (seg.TagCounters.TryGetValue("dungeon_run_complete", out var rc))
                runCompleted += rc;

            foreach (var (tag, val) in seg.TagCounters)
            {
                if (!tag.StartsWith("kill.", StringComparison.Ordinal)) continue;
                if (!killCounts.ContainsKey(tag)) killCounts[tag] = 0;
                killCounts[tag] += val;
            }
        }

        // 构建经济上下文（若是地城模式，读取 dungeon 配置；否则倍率=1）
        var ctx = new EconomyContext { Seed = rb.Seed };
        if (!string.IsNullOrWhiteSpace(rb.DungeonId))
        {
            var d = DungeonRegistry.Resolve(rb.DungeonId!);
            // Phase 6: 应用强化掉落倍率
            var finalDropMultiplier = d.DropChanceMultiplier * d.EnhancedDropMultiplier;
            ctx = new EconomyContext
            {
                GoldMultiplier = d.GoldMultiplier,
                ExpMultiplier = d.ExpMultiplier,
                DropChanceMultiplier = finalDropMultiplier,
                RunCompletedCount = runCompleted,
                RunRewardGold = d.RunRewardGold,
                RunRewardExp = d.RunRewardExp,
                RunRewardLootTableId = d.RunRewardLootTableId,
                RunRewardLootRolls = d.RunRewardLootRolls,
                Seed = rb.Seed
            };
        }

        var mode = (dropMode ?? "expected").Trim().ToLowerInvariant();
        long gold; long exp; Dictionary<string, double>? lootExp = null; Dictionary<string, int>? lootSampled = null;
        if (mode == "sampled")
        {
            var r = EconomyCalculator.ComputeSampledWithContext(killCounts, ctx);
            gold = r.Gold; exp = r.Exp;
            lootSampled = r.Items.ToDictionary(kv => kv.Key, kv => (int)Math.Round(kv.Value));
        }
        else
        {
            var r = EconomyCalculator.ComputeExpectedWithContext(killCounts, ctx);
            gold = r.Gold; exp = r.Exp;
            lootExp = r.Items;
            mode = "expected";
        }

        // Phase 3: 获取玩家状态信息
        var player = rb.Context.Player;
        int playerMaxHp = player.MaxHp;
        int playerCurrentHp = player.CurrentHp;
        double playerHpPercent = playerMaxHp > 0 ? (double)playerCurrentHp / playerMaxHp : 1.0;
        bool playerIsDead = player.IsDead;
        double? playerReviveAt = player.ReviveAt;
        
        // 计算复活倒计时（如果玩家死亡且有复活时间）
        double? reviveCountdownSeconds = null;
        if (playerIsDead && playerReviveAt.HasValue)
        {
            reviveCountdownSeconds = Math.Max(0, playerReviveAt.Value - rb.Clock.CurrentTime);
        }
        
        // 收集敌人血量状态
        var enemyHealthList = new List<EnemyHealthStatusDto>();
        var ctx2 = rb.Context;
        if (ctx2.EncounterGroup != null)
        {
            foreach (var enc in ctx2.EncounterGroup.All)
            {
                enemyHealthList.Add(new EnemyHealthStatusDto
                {
                    EnemyId = enc.Enemy.Id,
                    EnemyName = enc.Enemy.Name,
                    CurrentHp = enc.CurrentHp,
                    MaxHp = enc.Enemy.MaxHp,
                    HpPercent = enc.Enemy.MaxHp > 0 ? (double)enc.CurrentHp / enc.Enemy.MaxHp : 0,
                    IsDead = enc.IsDead
                });
            }
        }
        else if (ctx2.Encounter != null)
        {
            var enc = ctx2.Encounter;
            enemyHealthList.Add(new EnemyHealthStatusDto
            {
                EnemyId = enc.Enemy.Id,
                EnemyName = enc.Enemy.Name,
                CurrentHp = enc.CurrentHp,
                MaxHp = enc.Enemy.MaxHp,
                HpPercent = enc.Enemy.MaxHp > 0 ? (double)enc.CurrentHp / enc.Enemy.MaxHp : 0,
                IsDead = enc.IsDead
            });
        }
        
        // 获取下次攻击时间
        double? nextAttackAt = null;
        double? nextSpecialAt = null;
        foreach (var track in ctx2.Tracks)
        {
            if (track.TrackType == Domain.Combat.TrackType.Attack)
                nextAttackAt = track.NextTriggerAt;
            else if (track.TrackType == Domain.Combat.TrackType.Special)
                nextSpecialAt = track.NextTriggerAt;
        }
        
        // 计算轮询提示（智能轮询策略）
        PollingHint? pollingHint = CalculatePollingHint(
            rb.Completed,
            playerHpPercent,
            playerIsDead,
            nextAttackAt,
            nextSpecialAt,
            rb.Clock.CurrentTime
        );

        return (true, new StepBattleStatusDto
        {
            Id = rb.Id,
            CharacterId = rb.CharacterId,
            Profession = rb.Profession,
            EnemyId = rb.EnemyId,
            EnemyCount = rb.EnemyCount,
            SimulatedSeconds = rb.Clock.CurrentTime,
            TargetSeconds = rb.TargetDurationSeconds,
            Completed = rb.Completed,
            TotalDamage = totalDamage,
            Dps = Math.Round(dps, 2),
            SegmentCount = rb.Segments.Count,
            Seed = rb.Seed.ToString(),
            SeedIndexStart = rb.SeedIndexStart,
            SeedIndexEnd = rb.SeedIndexEnd,
            Killed = rb.Killed,
            KillTimeSeconds = rb.KillTime,
            OverkillDamage = rb.Overkill,
            PersistedBattleId = rb.PersistedBattleId,
            Mode = rb.Mode.ToString().ToLowerInvariant(),
            WaveIndex = rb.WaveIndex,
            RunCount = rb.RunCount,
            DungeonId = rb.DungeonId,

            // 奖励
            DropMode = mode,
            Gold = gold,
            Exp = exp,
            LootExpected = lootExp ?? new(),
            LootSampled = lootSampled ?? new(),
            
            // Phase 3: 实时战斗信息（包含玩家死亡状态）
            PlayerMaxHp = playerMaxHp,
            PlayerCurrentHp = playerCurrentHp,
            PlayerHpPercent = playerHpPercent,
            PlayerIsDead = playerIsDead,
            PlayerReviveCountdownSeconds = reviveCountdownSeconds,
            Enemies = enemyHealthList,
            NextAttackAt = nextAttackAt,
            NextSpecialAt = nextSpecialAt,
            CurrentTime = rb.Clock.CurrentTime,
            PollingHint = pollingHint
        });
    }
    
    /// <summary>
    /// 根据战斗状态计算建议的轮询间隔
    /// </summary>
    private static PollingHint? CalculatePollingHint(
        bool isCompleted,
        double playerHpPercent,
        bool playerIsDead,
        double? nextAttackAt,
        double? nextSpecialAt,
        double currentTime)
    {
        int suggestedIntervalMs;
        bool isStable;
        double? nextSignificantEventAt = null;

        if (isCompleted)
        {
            // 战斗已完成 - 使用较长轮询间隔或停止轮询
            suggestedIntervalMs = 5000;
            isStable = true;
        }
        else if (playerIsDead)
        {
            // 玩家死亡 - 中等轮询间隔（等待复活）
            suggestedIntervalMs = 2000;
            isStable = true;
        }
        else if (playerHpPercent < 0.5)
        {
            // 玩家血量低于50% - 激烈战斗，较短轮询间隔
            suggestedIntervalMs = 1000;
            isStable = false;
            
            // 计算下次重要事件时间（最早的攻击时间）
            if (nextAttackAt.HasValue && nextSpecialAt.HasValue)
                nextSignificantEventAt = Math.Min(nextAttackAt.Value, nextSpecialAt.Value);
            else if (nextAttackAt.HasValue)
                nextSignificantEventAt = nextAttackAt.Value;
            else if (nextSpecialAt.HasValue)
                nextSignificantEventAt = nextSpecialAt.Value;
        }
        else
        {
            // 正常战斗 - 标准轮询间隔
            suggestedIntervalMs = 2000;
            isStable = true;
            
            // 计算下次重要事件时间
            if (nextAttackAt.HasValue && nextSpecialAt.HasValue)
                nextSignificantEventAt = Math.Min(nextAttackAt.Value, nextSpecialAt.Value);
            else if (nextAttackAt.HasValue)
                nextSignificantEventAt = nextAttackAt.Value;
            else if (nextSpecialAt.HasValue)
                nextSignificantEventAt = nextSpecialAt.Value;
        }

        return new PollingHint
        {
            SuggestedIntervalMs = suggestedIntervalMs,
            NextSignificantEventAt = nextSignificantEventAt,
            IsStable = isStable
        };
    }

    public (bool found, List<StepBattleSegmentDto> segments) GetSegments(Guid id, int sinceIndex)
    {
        if (!_running.TryGetValue(id, out var rb))
            return (false, new());

        var list = new List<StepBattleSegmentDto>();
        for (int i = sinceIndex; i < rb.Segments.Count; i++)
        {
            var s = rb.Segments[i];
            list.Add(new StepBattleSegmentDto
            {
                Index = i,
                StartTime = s.StartTime,
                EndTime = s.EndTime,
                EventCount = s.EventCount,
                TotalDamage = s.TotalDamage,
                DamageBySource = s.DamageBySource,
                DamageByType = s.DamageByType,
                ResourceFlow = s.ResourceFlow
            });
        }
        return (true, list);
    }

    public void AdvanceAll(int maxEventsPerBattle = 500, double maxSliceSeconds = 0.25, CancellationToken ct = default)
    {
        foreach (var kv in _running.ToArray())
        {
            if (ct.IsCancellationRequested) break;

            var rb = kv.Value;
            if (!rb.Completed)
            {
                rb.Advance(maxEvents: maxEventsPerBattle, maxSimSecondsSlice: maxSliceSeconds);

                if (rb.Completed)
                {
                    _completedAtUtc.TryAdd(rb.Id, DateTime.UtcNow);
                }
                
                // 边打边发：周期性发放奖励（仅 sampled 模式）
                if (_enablePeriodicRewards)
                {
                    TryFlushPeriodicRewards(rb, ct);
                }
            }

            if (rb.Completed && !rb.Persisted)
            {
                try
                {
                    // 在需要用到仓储/Finalizer 时，临时创建一个作用域，解析 Scoped 的 Finalizer
                    using var scope = _scopeFactory.CreateScope();
                    var finalizer = scope.ServiceProvider.GetRequiredService<StepBattleFinalizer>();
                    var persistedId = finalizer.FinalizeAsync(rb, ct).GetAwaiter().GetResult();

                    rb.Persisted = true;
                    rb.PersistedBattleId = persistedId;
                }
                catch
                {
                    // TODO: log
                }
            }
        }
    }

    public bool TryGet(Guid id, out RunningBattle? rb) => _running.TryGetValue(id, out rb);

    public int PruneCompleted(TimeSpan ttl)
    {
        var now = DateTime.UtcNow;
        int removed = 0;
        foreach (var kv in _completedAtUtc.ToArray())
        {
            if ((now - kv.Value) > ttl)
            {
                if (_running.TryRemove(kv.Key, out _))
                {
                    _completedAtUtc.TryRemove(kv.Key, out _);
                    removed++;
                }
            }
        }
        return removed;
    }

    public async Task<(bool ok, Guid persistedId)> StopAndFinalizeAsync(Guid id, CancellationToken ct = default)
    {
        if (!_running.TryGetValue(id, out var rb))
            return (false, Guid.Empty);

        lock (rb)
        {
            if (!rb.Completed)
                rb.ForceStopAndSeal();
        }

        if (!rb.Persisted)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var finalizer = scope.ServiceProvider.GetRequiredService<StepBattleFinalizer>();
                var persistedId = await finalizer.FinalizeAsync(rb, ct);

                rb.Persisted = true;
                rb.PersistedBattleId = persistedId;
            }
            catch
            {
                return (false, Guid.Empty);
            }
        }

        _completedAtUtc.TryAdd(rb.Id, DateTime.UtcNow);
        return (true, rb.PersistedBattleId!.Value);
    }
    
    /// <summary>
    /// 周期性发放奖励（边打边发）。
    /// 仅对 sampled 模式有效，每隔 rewardFlushIntervalSimSeconds 检查一次新 segments。
    /// </summary>
    private void TryFlushPeriodicRewards(RunningBattle rb, CancellationToken ct)
    {
        var currentSimTime = rb.Clock.CurrentTime;
        
        // 检查是否到达下一个发放周期
        if (currentSimTime - rb.LastRewardFlushSimTime < _rewardFlushIntervalSimSeconds)
            return;
        
        // 检查是否有新 segments 需要发放
        var segmentCount = rb.Segments.Count;
        if (segmentCount <= rb.LastFlushedSegmentIndex + 1)
            return;
        
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var rewardService = scope.ServiceProvider.GetRequiredService<IRewardGrantService>();
            
            // 计算当前周期内新增的 segments 的奖励
            var killCounts = new Dictionary<string, int>(StringComparer.Ordinal);
            int runCompleted = 0;
            
            for (int i = rb.LastFlushedSegmentIndex + 1; i < segmentCount; i++)
            {
                var seg = rb.Segments[i];
                if (seg.TagCounters.TryGetValue("dungeon_run_complete", out var rc))
                    runCompleted += rc;
                
                foreach (var (tag, val) in seg.TagCounters)
                {
                    if (tag.StartsWith("kill.", StringComparison.Ordinal))
                    {
                        if (!killCounts.ContainsKey(tag)) killCounts[tag] = 0;
                        killCounts[tag] += val;
                    }
                }
            }
            
            // 只有当有实际击杀或完成副本轮次时才发放
            if (killCounts.Count == 0 && runCompleted == 0)
            {
                rb.LastRewardFlushSimTime = currentSimTime;
                rb.LastFlushedSegmentIndex = segmentCount - 1;
                return;
            }
            
            // 构建经济上下文
            var ctx = new EconomyContext { Seed = rb.Seed };
            if (!string.IsNullOrWhiteSpace(rb.DungeonId))
            {
                var d = DungeonRegistry.Resolve(rb.DungeonId!);
                // Phase 6: 应用强化掉落倍率
                var finalDropMultiplier = d.DropChanceMultiplier * d.EnhancedDropMultiplier;
                ctx = new EconomyContext
                {
                    GoldMultiplier = d.GoldMultiplier,
                    ExpMultiplier = d.ExpMultiplier,
                    DropChanceMultiplier = finalDropMultiplier,
                    RunCompletedCount = runCompleted,
                    RunRewardGold = d.RunRewardGold,
                    RunRewardExp = d.RunRewardExp,
                    RunRewardLootTableId = d.RunRewardLootTableId,
                    RunRewardLootRolls = d.RunRewardLootRolls,
                    Seed = rb.Seed
                };
            }
            else
            {
                ctx.RunCompletedCount = runCompleted;
            }
            
            // 计算奖励（使用 sampled 模式）
            var reward = EconomyCalculator.ComputeSampledWithContext(killCounts, ctx);
            
            // 构建幂等键
            var idempotencyKey = $"battle:{rb.Id}:periodic:sim{currentSimTime:F2}:seg{rb.LastFlushedSegmentIndex + 1}-{segmentCount - 1}";
            
            // 发放奖励
            var granted = rewardService.GrantRewardsAsync(
                rb.CharacterId,
                reward.Gold,
                reward.Exp,
                reward.Items.ToDictionary(kv => kv.Key, kv => (int)Math.Round(kv.Value)),
                idempotencyKey,
                "battle_periodic_reward",
                rb.Id,
                ct
            ).GetAwaiter().GetResult();
            
            // 更新发放状态
            rb.LastRewardFlushSimTime = currentSimTime;
            rb.LastFlushedSegmentIndex = segmentCount - 1;
        }
        catch (Exception)
        {
            // 静默失败，避免影响战斗推进
            // TODO: 可以考虑记录日志
        }
    }
}

public sealed class StepBattleStatusDto
{
    public Guid Id { get; set; }
    public Guid CharacterId { get; set; }
    public Profession Profession { get; set; }
    public string EnemyId { get; set; } = "dummy";
    public int EnemyCount { get; set; }
    public double SimulatedSeconds { get; set; }
    public double TargetSeconds { get; set; }
    public bool Completed { get; set; }
    public int TotalDamage { get; set; }
    public double Dps { get; set; }
    public int SegmentCount { get; set; }
    public string Seed { get; set; } = "0";
    public long SeedIndexStart { get; set; }
    public long SeedIndexEnd { get; set; }
    public bool Killed { get; set; }
    public double? KillTimeSeconds { get; set; }
    public int OverkillDamage { get; set; }
    public Guid? PersistedBattleId { get; set; }

    // 附加：持续/地城展示（前端可忽略这些字段）
    public string? Mode { get; set; }            // "duration"|"continuous"|"dungeonsingle"|"dungeonloop"
    public int? WaveIndex { get; set; }
    public int? RunCount { get; set; }
    public string? DungeonId { get; set; }

    // 新增：期望值奖励
    public string? DropMode { get; set; } // "expected" | "sampled"
    public long Gold { get; set; }
    public long Exp { get; set; }
    public Dictionary<string, double> LootExpected { get; set; } = new();
    public Dictionary<string, int> LootSampled { get; set; } = new();
    
    // 新增：实时战斗信息
    /// <summary>玩家最大血量（基于耐力计算，用于显示）</summary>
    public int PlayerMaxHp { get; set; }
    
    /// <summary>玩家当前血量（Phase 3: 实际血量）</summary>
    public int PlayerCurrentHp { get; set; }
    
    /// <summary>玩家当前血量百分比（Phase 3: 实际百分比）</summary>
    public double PlayerHpPercent { get; set; } = 1.0;
    
    /// <summary>玩家是否死亡（Phase 3）</summary>
    public bool PlayerIsDead { get; set; }
    
    /// <summary>玩家复活倒计时（秒）（Phase 3: 如果死亡且有复活时间）</summary>
    public double? PlayerReviveCountdownSeconds { get; set; }
    
    /// <summary>敌人血量状态列表（支持多怪物）</summary>
    public List<EnemyHealthStatusDto> Enemies { get; set; } = new();
    
    /// <summary>下次普通攻击时间</summary>
    public double? NextAttackAt { get; set; }
    
    /// <summary>下次特殊攻击时间</summary>
    public double? NextSpecialAt { get; set; }
    
    /// <summary>当前战斗时间</summary>
    public double CurrentTime { get; set; }
    
    /// <summary>轮询提示（服务器建议的轮询间隔）</summary>
    public PollingHint? PollingHint { get; set; }
}

/// <summary>
/// 单个敌人的血量状态（用于前端显示）
/// </summary>
public sealed class EnemyHealthStatusDto
{
    /// <summary>敌人ID</summary>
    public string EnemyId { get; set; } = "dummy";
    
    /// <summary>敌人名称</summary>
    public string EnemyName { get; set; } = "";
    
    /// <summary>当前血量</summary>
    public int CurrentHp { get; set; }
    
    /// <summary>最大血量</summary>
    public int MaxHp { get; set; }
    
    /// <summary>血量百分比</summary>
    public double HpPercent { get; set; }
    
    /// <summary>是否已死亡</summary>
    public bool IsDead { get; set; }
}

public sealed class StepBattleSegmentDto
{
    public int Index { get; set; }
    public double StartTime { get; set; }
    public double EndTime { get; set; }
    public int EventCount { get; set; }
    public int TotalDamage { get; set; }
    public Dictionary<string, int> DamageBySource { get; set; } = new();
    public Dictionary<string, int> DamageByType { get; set; } = new();
    public Dictionary<string, int> ResourceFlow { get; set; } = new();
}

/// <summary>
/// 轮询提示（服务器建议的轮询间隔）
/// </summary>
public sealed class PollingHint
{
    /// <summary>建议的轮询间隔（毫秒）</summary>
    public int SuggestedIntervalMs { get; set; }
    
    /// <summary>下次重要事件发生的时间（战斗时间，秒）</summary>
    public double? NextSignificantEventAt { get; set; }
    
    /// <summary>战斗状态是否稳定（true表示可以使用较长轮询间隔）</summary>
    public bool IsStable { get; set; }
}