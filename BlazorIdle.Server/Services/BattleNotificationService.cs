using BlazorIdle.Server.Application.Abstractions;
using BlazorIdle.Server.Config;
using BlazorIdle.Server.Hubs;
using BlazorIdle.Shared.Models;
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
    private readonly NotificationFilterPipeline? _filterPipeline;

    public BattleNotificationService(
        IHubContext<BattleNotificationHub> hubContext,
        ILogger<BattleNotificationService> logger,
        IOptions<SignalROptions> options,
        NotificationFilterPipeline? filterPipeline = null)
    {
        _hubContext = hubContext;
        _logger = logger;
        _options = options.Value;
        _filterPipeline = filterPipeline;
    }

    public bool IsAvailable => _options.EnableSignalR;

    /// <summary>
    /// 发送状态变更通知
    /// </summary>
    public async Task NotifyStateChangeAsync(Guid battleId, string eventType)
    {
        if (!_options.EnableSignalR)
        {
            if (_options.EnableDetailedLogging)
            {
                _logger.LogDebug("SignalR is disabled, skipping notification for battle {BattleId}", battleId);
            }
            return;
        }

        // 使用过滤器管道进行过滤决策
        if (_filterPipeline != null)
        {
            var context = new NotificationFilterContext
            {
                BattleId = battleId,
                EventType = eventType
            };

            if (!_filterPipeline.Execute(context))
            {
                if (_options.EnableDetailedLogging)
                {
                    _logger.LogDebug(
                        "Notification blocked by filter pipeline: Battle={BattleId}, EventType={EventType}",
                        battleId,
                        eventType
                    );
                }
                return;
            }
        }

        try
        {
            var groupName = $"battle_{battleId}";
            var notification = new StateChangedEvent
            {
                BattleId = battleId,
                EventType = eventType,
                Timestamp = DateTime.UtcNow
            };

            await _hubContext.Clients
                .Group(groupName)
                .SendAsync("StateChanged", notification);

            if (_options.EnableDetailedLogging)
            {
                _logger.LogDebug(
                    "Sent SignalR notification: Battle={BattleId}, EventType={EventType}",
                    battleId,
                    eventType
                );
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to send SignalR notification for battle {BattleId}, eventType {EventType}",
                battleId,
                eventType
            );
        }
    }

    /// <summary>
    /// 发送详细事件数据（Phase 2 扩展）
    /// </summary>
    public async Task NotifyEventAsync(Guid battleId, object eventData)
    {
        if (!_options.EnableSignalR)
        {
            return;
        }

        try
        {
            var groupName = $"battle_{battleId}";
            await _hubContext.Clients
                .Group(groupName)
                .SendAsync("BattleEvent", eventData);

            if (_options.EnableDetailedLogging)
            {
                _logger.LogDebug(
                    "Sent detailed event notification: Battle={BattleId}, EventType={EventType}",
                    battleId,
                    eventData.GetType().Name
                );
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to send detailed event notification for battle {BattleId}",
                battleId
            );
        }
    }
}
