namespace BlazorIdle.Models;

/// <summary>
/// 战斗日志条目
/// </summary>
public class BattleLogEntry
{
    public Guid Id { get; set; }
    public string Message { get; set; } = "";
    public BattleLogEntryType Type { get; set; }
    public DateTime Timestamp { get; set; }
}

/// <summary>
/// 战斗日志条目类型
/// </summary>
public enum BattleLogEntryType
{
    AttackStarted,
    DamageDealt,
    DamageReceived,
    CriticalHit,
    EnemyAttack,
    Other
}
