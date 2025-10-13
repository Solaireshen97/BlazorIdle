using BlazorIdle.Server.Application.Abstractions;
using BlazorIdle.Server.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace BlazorIdle.Server.Application.SignalR;

/// <summary>
/// 战斗通知服务实现
/// 负责通过SignalR向订阅的客户端发送战斗事件通知
/// </summary>
public class BattleNotificationService : IBattleNotificationService
{
    private readonly IHubContext<BattleNotificationHub> _hubContext;
    private readonly ILogger<BattleNotificationService> _logger;

    public BattleNotificationService(
        IHubContext<BattleNotificationHub> hubContext,
        ILogger<BattleNotificationService> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    /// <summary>
    /// 发送战斗状态变更通知
    /// </summary>
    public async Task NotifyStateChangeAsync(Guid battleId, string eventType)
    {
        try
        {
            var groupName = GetBattleGroupName(battleId);
            var notification = new
            {
                eventType,
                timestamp = DateTime.UtcNow
            };

            await _hubContext.Clients
                .Group(groupName)
                .SendAsync("StateChanged", notification);

            _logger.LogDebug(
                "已发送状态变更通知: BattleId={BattleId}, EventType={EventType}",
                battleId,
                eventType);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "发送状态变更通知失败: BattleId={BattleId}, EventType={EventType}",
                battleId,
                eventType);
        }
    }

    /// <summary>
    /// 发送详细事件数据通知（Phase 2预留）
    /// </summary>
    public async Task NotifyEventAsync(Guid battleId, object eventData)
    {
        try
        {
            var groupName = GetBattleGroupName(battleId);
            
            await _hubContext.Clients
                .Group(groupName)
                .SendAsync("BattleEvent", eventData);

            _logger.LogDebug(
                "已发送详细事件通知: BattleId={BattleId}, EventType={EventType}",
                battleId,
                eventData.GetType().Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "发送详细事件通知失败: BattleId={BattleId}",
                battleId);
        }
    }

    /// <summary>
    /// 获取战斗组名称
    /// </summary>
    private static string GetBattleGroupName(Guid battleId) => $"battle_{battleId}";
}
