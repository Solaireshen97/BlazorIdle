using BlazorIdle.Server.Application.Abstractions;
using BlazorIdle.Server.Config;
using BlazorIdle.Server.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;

namespace BlazorIdle.Server.Services;

/// <summary>
/// 战斗通知服务实现
/// </summary>
public sealed class BattleNotificationService : IBattleNotificationService
{
    private readonly IHubContext<BattleNotificationHub> _hubContext;
    private readonly ILogger<BattleNotificationService> _logger;
    private readonly SignalROptions _options;

    public BattleNotificationService(
        IHubContext<BattleNotificationHub> hubContext,
        ILogger<BattleNotificationService> logger,
        IOptions<SignalROptions> options)
    {
        _hubContext = hubContext;
        _logger = logger;
        _options = options.Value;
    }

    /// <summary>
    /// 发送状态变更通知（Phase 1：简化版本）
    /// </summary>
    public async Task NotifyStateChangeAsync(Guid battleId, string eventType)
    {
        if (!_options.Enabled)
        {
            _logger.LogDebug("SignalR is disabled, skipping notification for battle {BattleId}", battleId);
            return;
        }

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
                "Sent {EventType} notification to battle {BattleId}",
                eventType,
                battleId
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to send notification for battle {BattleId}, eventType: {EventType}",
                battleId,
                eventType
            );
            // 不抛出异常，避免影响主业务流程
        }
    }

    /// <summary>
    /// 发送详细事件数据（Phase 2：扩展版本）
    /// </summary>
    public async Task NotifyEventAsync(Guid battleId, object eventData)
    {
        if (!_options.Enabled)
        {
            _logger.LogDebug("SignalR is disabled, skipping event notification for battle {BattleId}", battleId);
            return;
        }

        try
        {
            var groupName = GetBattleGroupName(battleId);
            await _hubContext.Clients
                .Group(groupName)
                .SendAsync("BattleEvent", eventData);

            _logger.LogDebug(
                "Sent detailed event notification to battle {BattleId}",
                battleId
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to send detailed event notification for battle {BattleId}",
                battleId
            );
            // 不抛出异常，避免影响主业务流程
        }
    }

    /// <summary>
    /// 获取战斗组名称（与 Hub 保持一致）
    /// </summary>
    private static string GetBattleGroupName(Guid battleId) => $"battle_{battleId}";
}
