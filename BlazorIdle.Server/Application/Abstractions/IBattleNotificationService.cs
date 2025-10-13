namespace BlazorIdle.Server.Application.Abstractions;

/// <summary>
/// 战斗通知服务接口
/// 提供业务层调用的通知发送接口，解耦战斗逻辑与 SignalR 实现
/// </summary>
public interface IBattleNotificationService
{
    /// <summary>
    /// 发送状态变更通知（Phase 1：简化版本）
    /// </summary>
    /// <param name="battleId">战斗ID</param>
    /// <param name="eventType">事件类型（如：PlayerDeath, EnemyKilled, TargetSwitched）</param>
    Task NotifyStateChangeAsync(Guid battleId, string eventType);

    /// <summary>
    /// 发送详细事件数据（Phase 2：扩展版本，预留接口）
    /// </summary>
    /// <param name="battleId">战斗ID</param>
    /// <param name="eventData">事件详细数据</param>
    Task NotifyEventAsync(Guid battleId, object eventData);
}
