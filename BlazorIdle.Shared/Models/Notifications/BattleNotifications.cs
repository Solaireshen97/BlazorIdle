namespace BlazorIdle.Shared.Models.Notifications;

/// <summary>
/// 战斗状态变更事件通知（Phase 1简化版）
/// </summary>
public record StateChangedEvent(
    string EventType,        // "PlayerDeath", "EnemyKilled", "TargetSwitched", "WaveCleared", "BattleCompleted"
    DateTime Timestamp
);

/// <summary>
/// 战斗事件类型常量
/// </summary>
public static class BattleEventTypes
{
    /// <summary>
    /// 玩家死亡事件
    /// </summary>
    public const string PlayerDeath = "PlayerDeath";

    /// <summary>
    /// 玩家复活事件
    /// </summary>
    public const string PlayerRevive = "PlayerRevive";

    /// <summary>
    /// 怪物击杀事件
    /// </summary>
    public const string EnemyKilled = "EnemyKilled";

    /// <summary>
    /// 目标切换事件
    /// </summary>
    public const string TargetSwitched = "TargetSwitched";

    /// <summary>
    /// 波次清除事件
    /// </summary>
    public const string WaveCleared = "WaveCleared";

    /// <summary>
    /// 战斗完成事件
    /// </summary>
    public const string BattleCompleted = "BattleCompleted";
}
