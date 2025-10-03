namespace BlazorIdle.Server.Domain.Records;

/// <summary>
/// 持久化层（数据库映射）里的“战斗主记录”
/// 注意：它与运行时模拟用的 Battle (Domain/Combat/Battle) 不同：
///   * Battle (模拟) ：逻辑时间（double）、事件调度、仅内存存在
///   * BattleRecord  ：真实时间戳(DateTime)、汇总统计、用于保存与查询
/// </summary>
public class BattleRecord
{
    /// <summary>
    /// 战斗唯一 Id（与模拟 Battle.Id 对应）
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// 关联的角色 Id（外键，方便按角色筛选战斗历史）
    /// </summary>
    public Guid CharacterId { get; set; }

    /// <summary>
    /// 真实世界开始时间（创建记录时的 UtcNow）
    /// </summary>
    public DateTime StartedAt { get; set; }

    /// <summary>
    /// 真实世界结束时间（同步一次性模拟 → 与 StartedAt 通常接近）
    /// 若未来改成异步/实时推进，会拉开时间差
    /// </summary>
    public DateTime EndedAt { get; set; }

    /// <summary>
    /// 战斗总伤害（所有段 TotalDamage 之和，写冗余方便统计）
    /// </summary>
    public int TotalDamage { get; set; }

    /// <summary>
    /// 逻辑战斗时长（秒，来自模拟参数或真实计算）
    /// 不等同于 (EndedAt - StartedAt).TotalSeconds
    /// </summary>
    public double DurationSeconds { get; set; }

    public double AttackIntervalSeconds { get; set; }  // 新增（如果还没）
    public double SpecialIntervalSeconds { get; set; } // 新增

    /// <summary>
    /// 导航属性：战斗被切片后的段列表
    /// EF Core Include(b => b.Segments) 读取
    /// </summary>
    public ICollection<BattleSegmentRecord> Segments { get; set; } = new List<BattleSegmentRecord>();
}

/// <summary>
/// 战斗分段持久化记录：
/// 与运行时的 CombatSegment 对应（StartTime / EndTime 都是逻辑时间）
/// 每段聚合：事件数、伤害总量、来源分布（序列化为 JSON）
/// </summary>
public class BattleSegmentRecord
{
    public Guid Id { get; set; }

    /// <summary>
    /// 外键 → BattleRecord.Id
    /// </summary>
    public Guid BattleId { get; set; }

    /// <summary>
    /// 逻辑段起始时间（秒，模拟里 GameClock 的时间）
    /// </summary>
    public double StartTime { get; set; }

    /// <summary>
    /// 逻辑段结束时间（秒）
    /// </summary>
    public double EndTime { get; set; }

    /// <summary>
    /// 段内收集到的伤害事件数量
    /// </summary>
    public int EventCount { get; set; }

    /// <summary>
    /// 段内伤害总和
    /// </summary>
    public int TotalDamage { get; set; }

    /// <summary>
    /// 伤害来源分布（字典序列化后的 JSON）
    /// 例如：{"basic_attack":120,"skill_fire":300}
    /// 可后续改为：单独子表或 JSON 列（不同数据库支持差异）
    /// </summary>
    public string DamageBySourceJson { get; set; } = "{}";

    public string TagCountersJson { get; set; } = "{}";        // 新增（若之前没有）
    public string ResourceFlowJson { get; set; } = "{}";       // 新增
}