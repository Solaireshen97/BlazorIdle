namespace BlazorIdle.Shared.Models;

/// <summary>
/// 战斗状态变更事件（Phase 1 简化版本）
/// </summary>
public sealed class StateChangedEvent
{
    /// <summary>
    /// 事件类型：PlayerDeath, PlayerRevive, EnemyKilled, TargetSwitched
    /// </summary>
    public string EventType { get; set; } = "";
    
    /// <summary>
    /// 事件发生的时间戳
    /// </summary>
    public DateTime Timestamp { get; set; }
    
    /// <summary>
    /// 战斗 ID
    /// </summary>
    public Guid BattleId { get; set; }
}

/// <summary>
/// 战斗事件基类（Phase 2 扩展预留）
/// </summary>
public abstract class BattleEventDto
{
    public Guid BattleId { get; set; }
    public double EventTime { get; set; }
    public string EventType { get; set; } = "";
}

/// <summary>
/// 玩家死亡事件详情（Phase 2）
/// </summary>
public sealed class PlayerDeathEventDto : BattleEventDto
{
    public double ReviveAt { get; set; }
    public string CauseOfDeath { get; set; } = "";
}

/// <summary>
/// 怪物击杀事件详情（Phase 2）
/// </summary>
public sealed class EnemyKilledEventDto : BattleEventDto
{
    public string EnemyId { get; set; } = "";
    public string EnemyName { get; set; } = "";
    public int Overkill { get; set; }
}

/// <summary>
/// 目标切换事件详情（Phase 2）
/// </summary>
public sealed class TargetSwitchedEventDto : BattleEventDto
{
    public string NewTargetId { get; set; } = "";
    public string NewTargetName { get; set; } = "";
    public int NewTargetHp { get; set; }
    public int NewTargetMaxHp { get; set; }
    public double? NextAttackAt { get; set; }
}

/// <summary>
/// 攻击触发轻量事件（Phase 2.5 优化）
/// 用于前端进度条精准同步
/// </summary>
public sealed class AttackTickEventDto : BattleEventDto
{
    /// <summary>
    /// 下次攻击触发时间（游戏时间）
    /// </summary>
    public double NextAttackAt { get; set; }
    
    /// <summary>
    /// 当前攻击间隔（秒）
    /// </summary>
    public double AttackInterval { get; set; }
    
    /// <summary>
    /// 是否暴击
    /// </summary>
    public bool IsCrit { get; set; }
}

/// <summary>
/// 技能施放轻量事件（Phase 2.5 优化）
/// 用于前端技能状态同步和进度条更新
/// </summary>
public sealed class SkillCastEventDto : BattleEventDto
{
    /// <summary>
    /// 技能ID
    /// </summary>
    public string SkillId { get; set; } = "";
    
    /// <summary>
    /// 技能名称
    /// </summary>
    public string SkillName { get; set; } = "";
    
    /// <summary>
    /// 是否为施放开始（true）或施放完成（false）
    /// </summary>
    public bool IsCastStart { get; set; }
    
    /// <summary>
    /// 施放时长（秒），0表示瞬发
    /// </summary>
    public double CastDuration { get; set; }
    
    /// <summary>
    /// 技能冷却时长（秒）
    /// </summary>
    public double CooldownDuration { get; set; }
    
    /// <summary>
    /// 冷却就绪时间（游戏时间）
    /// </summary>
    public double CooldownReadyAt { get; set; }
}

/// <summary>
/// 伤害应用轻量事件（Phase 2.5 优化）
/// 用于前端血量即时更新
/// </summary>
public sealed class DamageAppliedEventDto : BattleEventDto
{
    /// <summary>
    /// 伤害来源（skill:xxx, basic_attack, dot:xxx等）
    /// </summary>
    public string SourceId { get; set; } = "";
    
    /// <summary>
    /// 实际造成的伤害值
    /// </summary>
    public int DamageAmount { get; set; }
    
    /// <summary>
    /// 伤害类型（Physical, Magic, True）
    /// </summary>
    public string DamageType { get; set; } = "";
    
    /// <summary>
    /// 目标当前生命值
    /// </summary>
    public int TargetCurrentHp { get; set; }
    
    /// <summary>
    /// 目标最大生命值
    /// </summary>
    public int TargetMaxHp { get; set; }
    
    /// <summary>
    /// 目标是否死亡
    /// </summary>
    public bool TargetDied { get; set; }
}
