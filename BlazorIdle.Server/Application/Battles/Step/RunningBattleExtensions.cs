using BlazorIdle.Server.Domain.Combat;
using BlazorIdle.Server.Domain.Combat.Buffs;
using BlazorIdle.Shared.Messages.Battle;
using System.Threading;

namespace BlazorIdle.Server.Application.Battles.Step;

/// <summary>
/// RunningBattle扩展类
/// 为战斗实例添加SignalR帧生成和版本管理能力
/// 支持实时战斗状态推送和断线重连
/// </summary>
public static class RunningBattleExtensions
{
    // 使用线程本地存储来存储每个战斗的版本号和统计数据
    // 避免修改RunningBattle原有结构
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<Guid, BattleFrameState> _battleStates = new();

    /// <summary>
    /// 生成战斗帧数据（增量更新）
    /// 包含自上一帧以来的状态变化，用于高频推送
    /// </summary>
    /// <param name="battle">战斗实例</param>
    /// <returns>帧数据对象</returns>
    public static FrameTick GenerateFrameTick(this RunningBattle battle)
    {
        if (battle == null)
            throw new ArgumentNullException(nameof(battle));

        // 获取或创建战斗状态
        var state = _battleStates.GetOrAdd(battle.Id, _ => new BattleFrameState());

        // 递增版本号（线程安全）
        var version = Interlocked.Increment(ref state.Version);
        var now = DateTime.UtcNow;
        var currentTime = battle.Clock.CurrentTime;

        // 构建帧数据
        var frame = new FrameTick
        {
            Version = version,
            ServerTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            BattleId = battle.Id.ToString(),
            Phase = GetBattlePhase(battle),
            Metrics = GenerateFrameMetrics(battle, state, currentTime),
            Aggregates = GenerateFrameAggregates(battle, state, currentTime)
        };

        // 更新状态用于下一帧
        state.LastFrameTime = currentTime;
        state.LastUpdateTime = now;

        return frame;
    }

    /// <summary>
    /// 生成战斗快照（完整状态）
    /// 包含完整的战斗状态，用于断线重连或定期同步
    /// </summary>
    /// <param name="battle">战斗实例</param>
    /// <returns>快照数据对象</returns>
    public static BattleSnapshot GenerateSnapshot(this RunningBattle battle)
    {
        if (battle == null)
            throw new ArgumentNullException(nameof(battle));

        // 获取当前版本号
        var state = _battleStates.GetOrAdd(battle.Id, _ => new BattleFrameState());
        var version = state.Version;

        var snapshot = new BattleSnapshot
        {
            Version = version,
            ServerTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            BattleId = battle.Id.ToString(),
            State = new BattleState
            {
                Phase = GetBattlePhase(battle),
                ElapsedTime = battle.Clock.CurrentTime,
                Player = GeneratePlayerState(battle),
                Enemies = GenerateEnemyStates(battle),
                Statistics = GenerateStatistics(battle)
            }
        };

        return snapshot;
    }

    /// <summary>
    /// 获取当前版本号
    /// 用于查询战斗的当前帧版本
    /// </summary>
    public static long GetCurrentVersion(this RunningBattle battle)
    {
        if (battle == null)
            throw new ArgumentNullException(nameof(battle));

        var state = _battleStates.GetOrAdd(battle.Id, _ => new BattleFrameState());
        return state.Version;
    }

    /// <summary>
    /// 清理战斗状态
    /// 在战斗结束后调用，释放相关资源
    /// </summary>
    public static void CleanupFrameState(this RunningBattle battle)
    {
        if (battle == null)
            return;

        _battleStates.TryRemove(battle.Id, out _);
    }

    #region 私有辅助方法

    /// <summary>
    /// 获取战斗阶段
    /// </summary>
    private static BattlePhase GetBattlePhase(RunningBattle battle)
    {
        if (battle.Completed)
            return BattlePhase.Ended;

        // 检查是否暂停（目前没有暂停机制，保留用于未来扩展）
        return BattlePhase.Active;
    }

    /// <summary>
    /// 生成帧指标数据
    /// 包含自上一帧以来的增量变化
    /// </summary>
    private static FrameMetrics GenerateFrameMetrics(RunningBattle battle, BattleFrameState state, double currentTime)
    {
        var context = battle.Context;
        var collector = battle.Collector;

        // 计算DPS（基于SegmentCollector的数据）
        var playerDps = CalculatePlayerDps(collector, currentTime);
        var receivedDps = 0.0; // TODO: 实现受伤DPS计算

        // 获取生命值数据
        // 注意：当前系统中玩家没有受伤机制，使用固定值
        // TODO: 从实际的玩家状态获取生命值
        var currentHp = 1000; // 临时固定值
        var maxHp = 1000; // 临时固定值
        var hpDelta = currentHp - state.LastHp;
        state.LastHp = currentHp;

        // 获取护盾数据（如果有护盾系统）
        var currentShield = 0; // TODO: 从Buff系统获取护盾值
        var shieldDelta = currentShield - state.LastShield;
        state.LastShield = currentShield;

        var metrics = new FrameMetrics
        {
            CastProgress = null, // TODO: 实现施法进度检测
            Dps = new DpsMetrics
            {
                Player = playerDps,
                Received = receivedDps
            },
            Health = new HealthMetrics
            {
                Current = currentHp,
                Max = maxHp,
                Delta = hpDelta
            },
            Shield = new ShieldMetrics
            {
                Current = currentShield,
                Delta = shieldDelta
            },
            Buffs = GetBuffChanges(context, state, currentTime),
            ExpiredBuffs = GetExpiredBuffIds(context, state, currentTime)
        };

        return metrics;
    }

    /// <summary>
    /// 生成聚合统计数据
    /// 在时间窗口内的累计数据
    /// </summary>
    private static FrameAggregates GenerateFrameAggregates(RunningBattle battle, BattleFrameState state, double currentTime)
    {
        var collector = battle.Collector;
        
        // 计算窗口内的统计数据
        var windowDamage = 0.0;
        var windowHealing = 0.0;
        var windowHits = 0;

        // 从collector获取最近的段数据
        foreach (var segment in battle.Segments)
        {
            if (segment.EndTime >= state.LastFrameTime && segment.StartTime <= currentTime)
            {
                windowDamage += segment.TotalDamage;
                // 当前系统没有治疗，保留0
                // windowHealing += segment.TotalHealing;
                
                // 当前CombatSegment没有HitCount字段
                // 可以使用EventCount作为替代，或者基于DamageBySource计算
                windowHits += segment.EventCount;
            }
        }

        return new FrameAggregates
        {
            WindowStart = state.LastFrameTime,
            WindowEnd = currentTime,
            Damage = windowDamage,
            Healing = windowHealing,
            Hits = windowHits
        };
    }

    /// <summary>
    /// 计算玩家DPS
    /// 基于最近5秒的伤害数据
    /// </summary>
    private static double CalculatePlayerDps(SegmentCollector collector, double currentTime)
    {
        // DPS计算窗口：5秒
        const double dpsWindow = 5.0;
        var windowStart = Math.Max(0, currentTime - dpsWindow);

        // TODO: 实现基于collector的DPS计算
        // 目前返回0，待集成后实现
        return 0.0;
    }

    /// <summary>
    /// 获取Buff变化
    /// 返回自上一帧以来新增或刷新的Buff
    /// </summary>
    private static List<BuffChange>? GetBuffChanges(BattleContext context, BattleFrameState state, double currentTime)
    {
        // TODO: 实现Buff变化检测
        // 需要跟踪BuffManager的状态变化
        return null;
    }

    /// <summary>
    /// 获取过期的Buff ID列表
    /// 返回自上一帧以来过期或被移除的Buff
    /// </summary>
    private static List<string>? GetExpiredBuffIds(BattleContext context, BattleFrameState state, double currentTime)
    {
        // TODO: 实现过期Buff检测
        // 需要跟踪BuffManager的状态变化
        return null;
    }

    /// <summary>
    /// 生成玩家状态（用于快照）
    /// </summary>
    private static PlayerState GeneratePlayerState(RunningBattle battle)
    {
        var context = battle.Context;
        var stats = context.Stats;

        return new PlayerState
        {
            Health = new HealthSnapshot
            {
                Current = 1000, // TODO: 从实际玩家状态获取
                Max = 1000 // TODO: 从实际玩家状态获取
            },
            Shield = 0, // TODO: 从Buff系统获取护盾值
            Resources = new Dictionary<string, int>
            {
                // TODO: 添加资源数据（如法力、能量等）
                // 当前系统可能没有资源系统，保留用于未来扩展
            },
            Buffs = GetBuffSnapshots(context, isDebuff: false),
            Debuffs = GetBuffSnapshots(context, isDebuff: true)
        };
    }

    /// <summary>
    /// 获取Buff快照列表
    /// </summary>
    private static BuffSnapshot[] GetBuffSnapshots(BattleContext context, bool isDebuff)
    {
        // TODO: 实现从BuffManager获取Buff列表
        // 需要区分增益和减益
        return Array.Empty<BuffSnapshot>();
    }

    /// <summary>
    /// 生成敌人状态列表（用于快照）
    /// </summary>
    private static EnemyState[] GenerateEnemyStates(RunningBattle battle)
    {
        var encounterGroup = battle.Context.EncounterGroup;
        if (encounterGroup == null)
            return Array.Empty<EnemyState>();

        var enemyStates = new List<EnemyState>();
        
        // 遍历所有敌人（All是属性不是方法）
        foreach (var encounter in encounterGroup.All)
        {
            if (encounter == null || encounter.Enemy == null)
                continue;

            enemyStates.Add(new EnemyState
            {
                Id = encounter.Enemy.Id,
                Name = encounter.Enemy.Name,
                Health = new HealthSnapshot
                {
                    Current = encounter.CurrentHp,
                    Max = encounter.Enemy.MaxHp
                },
                Buffs = Array.Empty<BuffSnapshot>() // TODO: 敌人Buff系统
            });
        }

        return enemyStates.ToArray();
    }

    /// <summary>
    /// 生成战斗统计数据（用于快照）
    /// </summary>
    private static BattleStatistics GenerateStatistics(RunningBattle battle)
    {
        var collector = battle.Collector;
        
        // 计算总统计数据
        var totalDamage = 0.0;
        var totalHealing = 0.0;
        var totalHits = 0;
        var enemiesKilled = 0;

        // 从segments聚合统计
        foreach (var segment in battle.Segments)
        {
            totalDamage += segment.TotalDamage;
            // 当前系统没有治疗统计
            // totalHealing += segment.TotalHealing;
            
            // 使用EventCount作为命中次数的近似值
            totalHits += segment.EventCount;
        }
        
        // 统计击杀数：检查击杀的敌人
        if (battle.Context.EncounterGroup != null)
        {
            enemiesKilled = battle.Context.EncounterGroup.All.Count(e => e.IsDead);
        }

        return new BattleStatistics
        {
            TotalDamage = totalDamage,
            TotalHealing = totalHealing,
            TotalHits = totalHits,
            EnemiesKilled = enemiesKilled
        };
    }

    #endregion
}

/// <summary>
/// 战斗帧状态
/// 存储每个战斗的版本号和上一帧的数据，用于计算增量
/// </summary>
internal class BattleFrameState
{
    /// <summary>
    /// 当前版本号（原子操作保证线程安全）
    /// </summary>
    public long Version;

    /// <summary>
    /// 上一帧的战斗时间
    /// </summary>
    public double LastFrameTime;

    /// <summary>
    /// 上次更新的墙钟时间
    /// </summary>
    public DateTime LastUpdateTime = DateTime.UtcNow;

    /// <summary>
    /// 上一帧的生命值
    /// </summary>
    public int LastHp;

    /// <summary>
    /// 上一帧的护盾值
    /// </summary>
    public int LastShield;

    /// <summary>
    /// 已知的Buff ID集合（用于检测新增和过期）
    /// </summary>
    public HashSet<string> KnownBuffIds = new();
}
