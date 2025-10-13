namespace BlazorIdle.Server.Application.Abstractions;

/// <summary>
/// 战斗通知服务接口
/// 提供业务层调用的通知发送接口，解耦战斗逻辑与SignalR实现
/// </summary>
public interface IBattleNotificationService
{
    /// <summary>
    /// 发送战斗状态变更通知（Phase 1实现）
    /// </summary>
    /// <param name="battleId">战斗ID</param>
    /// <param name="eventType">事件类型（如"PlayerDeath", "EnemyKilled"等）</param>
    Task NotifyStateChangeAsync(Guid battleId, string eventType);

    /// <summary>
    /// 发送详细事件数据通知（Phase 2预留接口）
    /// </summary>
    /// <param name="battleId">战斗ID</param>
    /// <param name="eventData">事件数据对象</param>
    Task NotifyEventAsync(Guid battleId, object eventData);
}
