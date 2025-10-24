namespace BlazorIdle.Shared.Messages.Battle;

/// <summary>
/// 战斗帧数据 - 轻量级增量更新
/// 用于高频率推送战斗状态变化，每帧仅包含增量数据以减少带宽消耗
/// </summary>
public class FrameTick
{
    /// <summary>
    /// 版本号（单调递增）
    /// 用于检测丢包、乱序和实现断线重连
    /// </summary>
    public long Version { get; set; }
    
    /// <summary>
    /// 服务器时间戳（Unix毫秒）
    /// 用于客户端时间同步和延迟补偿
    /// </summary>
    public long ServerTime { get; set; }
    
    /// <summary>
    /// 战斗ID
    /// 用于区分不同的战斗实例
    /// </summary>
    public string BattleId { get; set; } = string.Empty;
    
    /// <summary>
    /// 战斗阶段
    /// Active: 战斗进行中
    /// Paused: 战斗暂停
    /// Ended: 战斗结束
    /// </summary>
    public BattlePhase Phase { get; set; }
    
    /// <summary>
    /// 指标增量数据
    /// 包含自上一帧以来的变化数据（DPS、生命值、护盾、Buff等）
    /// </summary>
    public FrameMetrics Metrics { get; set; } = new();
    
    /// <summary>
    /// 聚合统计数据（可选）
    /// 在时间窗口内的聚合统计（伤害、治疗、命中次数等）
    /// </summary>
    public FrameAggregates? Aggregates { get; set; }
    
    /// <summary>
    /// 关键事件列表（可选）
    /// 当前帧发生的重要事件（技能释放、击杀等）
    /// </summary>
    public KeyEvent[]? Events { get; set; }
}

/// <summary>
/// 战斗阶段枚举
/// </summary>
public enum BattlePhase
{
    /// <summary>
    /// 战斗进行中
    /// </summary>
    Active = 0,
    
    /// <summary>
    /// 战斗暂停
    /// </summary>
    Paused = 1,
    
    /// <summary>
    /// 战斗结束
    /// </summary>
    Ended = 2
}

/// <summary>
/// 帧指标数据
/// 包含自上一帧以来的所有状态变化
/// </summary>
public class FrameMetrics
{
    /// <summary>
    /// 施法进度（可选）
    /// 如果正在读条，则包含当前施法进度
    /// </summary>
    public CastProgress? CastProgress { get; set; }
    
    /// <summary>
    /// DPS数据
    /// 包含玩家输出和受到的DPS
    /// </summary>
    public DpsMetrics Dps { get; set; } = new();
    
    /// <summary>
    /// 生命值变化
    /// 当前生命值、最大生命值和变化量
    /// </summary>
    public HealthMetrics Health { get; set; } = new();
    
    /// <summary>
    /// 护盾变化
    /// 当前护盾值和变化量
    /// </summary>
    public ShieldMetrics Shield { get; set; } = new();
    
    /// <summary>
    /// Buff变化列表（可选）
    /// 自上一帧以来新增或刷新的Buff
    /// </summary>
    public List<BuffChange>? Buffs { get; set; }
    
    /// <summary>
    /// 过期的Buff ID列表（可选）
    /// 自上一帧以来过期或被移除的Buff
    /// </summary>
    public List<string>? ExpiredBuffs { get; set; }
}

/// <summary>
/// 施法进度
/// 用于显示技能读条进度
/// </summary>
public class CastProgress
{
    /// <summary>
    /// 技能ID
    /// </summary>
    public string SkillId { get; set; } = string.Empty;
    
    /// <summary>
    /// 施法进度（0.0-1.0）
    /// </summary>
    public double Progress { get; set; }
    
    /// <summary>
    /// 剩余时间（秒）
    /// </summary>
    public double Remaining { get; set; }
}

/// <summary>
/// DPS数据
/// 基于滑动窗口计算的每秒伤害
/// </summary>
public class DpsMetrics
{
    /// <summary>
    /// 玩家输出DPS
    /// </summary>
    public double Player { get; set; }
    
    /// <summary>
    /// 玩家受到的DPS
    /// </summary>
    public double Received { get; set; }
}

/// <summary>
/// 生命值数据
/// </summary>
public class HealthMetrics
{
    /// <summary>
    /// 当前生命值
    /// </summary>
    public int Current { get; set; }
    
    /// <summary>
    /// 最大生命值
    /// </summary>
    public int Max { get; set; }
    
    /// <summary>
    /// 生命值变化量（相对上一帧）
    /// 正数表示治疗，负数表示受伤
    /// </summary>
    public int Delta { get; set; }
}

/// <summary>
/// 护盾数据
/// </summary>
public class ShieldMetrics
{
    /// <summary>
    /// 当前护盾值
    /// </summary>
    public int Current { get; set; }
    
    /// <summary>
    /// 护盾变化量（相对上一帧）
    /// 正数表示获得护盾，负数表示护盾被打破
    /// </summary>
    public int Delta { get; set; }
}

/// <summary>
/// Buff变化
/// 表示Buff的新增或刷新
/// </summary>
public class BuffChange
{
    /// <summary>
    /// Buff ID
    /// </summary>
    public string BuffId { get; set; } = string.Empty;
    
    /// <summary>
    /// 当前层数
    /// </summary>
    public int Stacks { get; set; }
    
    /// <summary>
    /// 剩余持续时间（秒）
    /// </summary>
    public double Duration { get; set; }
    
    /// <summary>
    /// 应用时间戳（Unix毫秒）
    /// </summary>
    public long AppliedAt { get; set; }
}

/// <summary>
/// 聚合统计数据
/// 在指定时间窗口内的累计统计
/// </summary>
public class FrameAggregates
{
    /// <summary>
    /// 时间窗口开始时间（战斗内时间，秒）
    /// </summary>
    public double WindowStart { get; set; }
    
    /// <summary>
    /// 时间窗口结束时间（战斗内时间，秒）
    /// </summary>
    public double WindowEnd { get; set; }
    
    /// <summary>
    /// 窗口内总伤害
    /// </summary>
    public double Damage { get; set; }
    
    /// <summary>
    /// 窗口内总治疗
    /// </summary>
    public double Healing { get; set; }
    
    /// <summary>
    /// 窗口内命中次数
    /// </summary>
    public int Hits { get; set; }
}
