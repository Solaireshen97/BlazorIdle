namespace BlazorIdle.Shared.Messages.Battle;

/// <summary>
/// 战斗快照 - 完整的战斗状态
/// 用于客户端初始化或断线重连后的完整状态恢复
/// 快照包含战斗的所有必要信息，客户端接收后可直接渲染完整状态
/// </summary>
public class BattleSnapshot
{
    /// <summary>
    /// 快照版本号
    /// 与FrameTick共享版本空间，用于断线重连时确定同步点
    /// </summary>
    public long Version { get; set; }
    
    /// <summary>
    /// 服务器时间戳（Unix毫秒）
    /// 快照生成的时间点
    /// </summary>
    public long ServerTime { get; set; }
    
    /// <summary>
    /// 战斗ID
    /// 快照所属的战斗实例
    /// </summary>
    public string BattleId { get; set; } = string.Empty;
    
    /// <summary>
    /// 战斗状态
    /// 包含战斗的完整状态信息
    /// </summary>
    public BattleState State { get; set; } = new();
}

/// <summary>
/// 战斗状态
/// 包含战斗的所有实体和统计信息
/// </summary>
public class BattleState
{
    /// <summary>
    /// 战斗阶段
    /// </summary>
    public BattlePhase Phase { get; set; }
    
    /// <summary>
    /// 已经过的战斗时间（秒）
    /// 从战斗开始到当前的总时长
    /// </summary>
    public double ElapsedTime { get; set; }
    
    /// <summary>
    /// 玩家状态
    /// 包含玩家的所有属性和Buff
    /// </summary>
    public PlayerState Player { get; set; } = new();
    
    /// <summary>
    /// 敌人状态列表
    /// 包含所有存活敌人的状态
    /// </summary>
    public EnemyState[] Enemies { get; set; } = Array.Empty<EnemyState>();
    
    /// <summary>
    /// 战斗统计
    /// 累计的战斗数据
    /// </summary>
    public BattleStatistics Statistics { get; set; } = new();
}

/// <summary>
/// 玩家状态
/// </summary>
public class PlayerState
{
    /// <summary>
    /// 生命值
    /// </summary>
    public HealthSnapshot Health { get; set; } = new();
    
    /// <summary>
    /// 护盾值
    /// </summary>
    public int Shield { get; set; }
    
    /// <summary>
    /// 资源值（法力、能量等）
    /// Key为资源类型，Value为当前值
    /// </summary>
    public Dictionary<string, int> Resources { get; set; } = new();
    
    /// <summary>
    /// Buff列表
    /// 玩家当前所有的增益效果
    /// </summary>
    public BuffSnapshot[] Buffs { get; set; } = Array.Empty<BuffSnapshot>();
    
    /// <summary>
    /// Debuff列表
    /// 玩家当前所有的减益效果
    /// </summary>
    public BuffSnapshot[] Debuffs { get; set; } = Array.Empty<BuffSnapshot>();
}

/// <summary>
/// 生命值快照
/// </summary>
public class HealthSnapshot
{
    /// <summary>
    /// 当前生命值
    /// </summary>
    public int Current { get; set; }
    
    /// <summary>
    /// 最大生命值
    /// </summary>
    public int Max { get; set; }
}

/// <summary>
/// Buff快照
/// 表示某个时刻Buff的完整状态
/// </summary>
public class BuffSnapshot
{
    /// <summary>
    /// Buff ID
    /// </summary>
    public string Id { get; set; } = string.Empty;
    
    /// <summary>
    /// Buff名称
    /// 用于UI显示
    /// </summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// 当前层数
    /// 可叠加Buff的层数
    /// </summary>
    public int Stacks { get; set; }
    
    /// <summary>
    /// 剩余持续时间（秒）
    /// -1表示永久Buff
    /// </summary>
    public double Duration { get; set; }
}

/// <summary>
/// 敌人状态
/// </summary>
public class EnemyState
{
    /// <summary>
    /// 敌人ID
    /// 在战斗内唯一标识敌人
    /// </summary>
    public string Id { get; set; } = string.Empty;
    
    /// <summary>
    /// 敌人名称
    /// 用于UI显示
    /// </summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// 生命值
    /// </summary>
    public HealthSnapshot Health { get; set; } = new();
    
    /// <summary>
    /// Buff列表
    /// 敌人当前的所有效果
    /// </summary>
    public BuffSnapshot[] Buffs { get; set; } = Array.Empty<BuffSnapshot>();
}

/// <summary>
/// 战斗统计
/// 累计的战斗数据，用于结算和显示
/// </summary>
public class BattleStatistics
{
    /// <summary>
    /// 总伤害
    /// 玩家造成的累计伤害
    /// </summary>
    public double TotalDamage { get; set; }
    
    /// <summary>
    /// 总治疗
    /// 玩家接受的累计治疗
    /// </summary>
    public double TotalHealing { get; set; }
    
    /// <summary>
    /// 总命中次数
    /// 包括所有攻击和技能的命中
    /// </summary>
    public int TotalHits { get; set; }
    
    /// <summary>
    /// 击杀敌人数量
    /// 已击败的敌人总数
    /// </summary>
    public int EnemiesKilled { get; set; }
}
