using BlazorIdle.Server.Services;

namespace BlazorIdle.Server.Application.Abstractions;

/// <summary>
/// 战斗事件通知服务接口
/// 提供战斗状态变更的实时通知功能
/// </summary>
public interface IBattleNotificationService
{
    /// <summary>
    /// 发送状态变更通知（Phase 1 实现）
    /// </summary>
    /// <param name="battleId">战斗 ID</param>
    /// <param name="eventType">事件类型（如 "PlayerDeath", "EnemyKilled", "TargetSwitched"）</param>
    Task NotifyStateChangeAsync(Guid battleId, string eventType);
    
    /// <summary>
    /// 发送详细事件数据通知（Phase 2 扩展预留）
    /// </summary>
    /// <param name="battleId">战斗 ID</param>
    /// <param name="eventData">事件数据对象</param>
    Task NotifyEventAsync(Guid battleId, object eventData);
    
    /// <summary>
    /// 检查 SignalR 服务是否可用
    /// </summary>
    bool IsAvailable { get; }
    
    /// <summary>
    /// 获取性能指标摘要
    /// </summary>
    SignalRMetricsSummary GetMetrics();
}
