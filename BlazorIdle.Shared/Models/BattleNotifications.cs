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
/// 攻击触发轻量事件（用于前端进度条增量更新）
/// </summary>
public sealed class AttackTickEventDto : BattleEventDto
{
    /// <summary>
    /// 下次攻击触发时间（战斗内时间）
    /// </summary>
    public double NextTriggerAt { get; set; }
    
    /// <summary>
    /// 攻击间隔
    /// </summary>
    public double Interval { get; set; }
}

/// <summary>
/// 技能施放完成轻量事件（用于前端进度条增量更新）
/// </summary>
public sealed class SkillCastCompleteEventDto : BattleEventDto
{
    /// <summary>
    /// 技能 ID
    /// </summary>
    public string SkillId { get; set; } = "";
    
    /// <summary>
    /// 技能施放完成时间（战斗内时间）
    /// </summary>
    public double CastCompleteAt { get; set; }
}

/// <summary>
/// 伤害应用轻量事件（用于前端实时反馈）
/// </summary>
public sealed class DamageAppliedEventDto : BattleEventDto
{
    /// <summary>
    /// 伤害来源（如 "basic_attack", "skill:fireball"）
    /// </summary>
    public string Source { get; set; } = "";
    
    /// <summary>
    /// 伤害值
    /// </summary>
    public int Damage { get; set; }
    
    /// <summary>
    /// 是否暴击
    /// </summary>
    public bool IsCrit { get; set; }
    
    /// <summary>
    /// 目标当前血量
    /// </summary>
    public int TargetCurrentHp { get; set; }
    
    /// <summary>
    /// 目标最大血量
    /// </summary>
    public int TargetMaxHp { get; set; }
}

/// <summary>
/// 攻击开始事件（用于显示战斗消息）
/// </summary>
public sealed class AttackStartEventDto : BattleEventDto
{
    /// <summary>
    /// 攻击者名称
    /// </summary>
    public string AttackerName { get; set; } = "";
    
    /// <summary>
    /// 攻击者类型（Player/Enemy）
    /// </summary>
    public string AttackerType { get; set; } = "";
    
    /// <summary>
    /// 目标名称
    /// </summary>
    public string TargetName { get; set; } = "";
    
    /// <summary>
    /// 目标类型（Player/Enemy）
    /// </summary>
    public string TargetType { get; set; } = "";
    
    /// <summary>
    /// 攻击类型（basic_attack/skill）
    /// </summary>
    public string AttackType { get; set; } = "";
}

/// <summary>
/// 伤害造成事件（用于显示战斗消息）
/// </summary>
public sealed class DamageDealtEventDto : BattleEventDto
{
    /// <summary>
    /// 攻击者名称
    /// </summary>
    public string AttackerName { get; set; } = "";
    
    /// <summary>
    /// 目标名称
    /// </summary>
    public string TargetName { get; set; } = "";
    
    /// <summary>
    /// 伤害值
    /// </summary>
    public int Damage { get; set; }
    
    /// <summary>
    /// 是否暴击
    /// </summary>
    public bool IsCrit { get; set; }
    
    /// <summary>
    /// 伤害类型（Physical/Magic/True）
    /// </summary>
    public string DamageType { get; set; } = "";
    
    /// <summary>
    /// 目标剩余血量
    /// </summary>
    public int TargetCurrentHp { get; set; }
    
    /// <summary>
    /// 目标最大血量
    /// </summary>
    public int TargetMaxHp { get; set; }
}

/// <summary>
/// 受到伤害事件（用于显示战斗消息）
/// </summary>
public sealed class DamageReceivedEventDto : BattleEventDto
{
    /// <summary>
    /// 接收者名称
    /// </summary>
    public string ReceiverName { get; set; } = "";
    
    /// <summary>
    /// 攻击者名称
    /// </summary>
    public string AttackerName { get; set; } = "";
    
    /// <summary>
    /// 受到的伤害值
    /// </summary>
    public int Damage { get; set; }
    
    /// <summary>
    /// 伤害类型（Physical/Magic/True）
    /// </summary>
    public string DamageType { get; set; } = "";
    
    /// <summary>
    /// 接收者剩余血量
    /// </summary>
    public int CurrentHp { get; set; }
    
    /// <summary>
    /// 接收者最大血量
    /// </summary>
    public int MaxHp { get; set; }
}
