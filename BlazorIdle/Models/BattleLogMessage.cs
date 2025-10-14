namespace BlazorIdle.Models;

/// <summary>
/// 战斗日志消息
/// </summary>
public class BattleLogMessage
{
    /// <summary>
    /// 消息时间戳
    /// </summary>
    public DateTime Timestamp { get; set; }
    
    /// <summary>
    /// 消息文本
    /// </summary>
    public string Text { get; set; } = "";
    
    /// <summary>
    /// 消息类型
    /// </summary>
    public BattleLogMessageType Type { get; set; }
    
    /// <summary>
    /// 是否暴击（用于造成伤害消息）
    /// </summary>
    public bool IsCrit { get; set; }
    
    /// <summary>
    /// 战斗ID（可选）
    /// </summary>
    public Guid? BattleId { get; set; }
}

/// <summary>
/// 战斗日志消息类型
/// </summary>
public enum BattleLogMessageType
{
    /// <summary>
    /// 玩家攻击开始
    /// </summary>
    AttackStarted,
    
    /// <summary>
    /// 造成伤害
    /// </summary>
    DamageDealt,
    
    /// <summary>
    /// 受到伤害
    /// </summary>
    DamageReceived,
    
    /// <summary>
    /// 敌人攻击开始
    /// </summary>
    EnemyAttackStarted
}
