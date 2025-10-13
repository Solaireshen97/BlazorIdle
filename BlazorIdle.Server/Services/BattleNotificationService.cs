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

    public BattleNotificationService(
        IHubContext<BattleNotificationHub> hubContext,
        ILogger<BattleNotificationService> logger,
        IOptions<SignalROptions> options)
    {
        _hubContext = hubContext;
        _logger = logger;
        _options = options.Value;
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

        var startTime = _options.Monitoring.EnableMetrics ? DateTime.UtcNow : default;

        try
        {
            var groupName = GetBattleGroupName(battleId);
            var notification = new StateChangedEvent
            {
                BattleId = battleId,
                EventType = eventType,
                Timestamp = DateTime.UtcNow
            };

            await _hubContext.Clients
                .Group(groupName)
                .SendAsync(_options.Methods.StateChanged, notification);

            if (_options.EnableDetailedLogging || _options.Monitoring.LogNotificationDetails)
            {
                _logger.LogDebug(
                    "Sent SignalR notification: Battle={BattleId}, EventType={EventType}, Method={MethodName}",
                    battleId,
                    eventType,
                    _options.Methods.StateChanged
                );
            }

            // 监控慢通知
            if (_options.Monitoring.EnableMetrics)
            {
                var elapsedMs = (DateTime.UtcNow - startTime).TotalMilliseconds;
                if (elapsedMs > _options.Monitoring.SlowNotificationThresholdMs)
                {
                    _logger.LogWarning(
                        "Slow SignalR notification detected: Battle={BattleId}, EventType={EventType}, ElapsedMs={ElapsedMs}",
                        battleId,
                        eventType,
                        elapsedMs
                    );
                }
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

        var startTime = _options.Monitoring.EnableMetrics ? DateTime.UtcNow : default;

        try
        {
            var groupName = GetBattleGroupName(battleId);
            await _hubContext.Clients
                .Group(groupName)
                .SendAsync(_options.Methods.BattleEvent, eventData);

            if (_options.EnableDetailedLogging || _options.Monitoring.LogNotificationDetails)
            {
                _logger.LogDebug(
                    "Sent detailed event notification: Battle={BattleId}, EventType={EventType}, Method={MethodName}",
                    battleId,
                    eventData.GetType().Name,
                    _options.Methods.BattleEvent
                );
            }

            // 监控慢通知
            if (_options.Monitoring.EnableMetrics)
            {
                var elapsedMs = (DateTime.UtcNow - startTime).TotalMilliseconds;
                if (elapsedMs > _options.Monitoring.SlowNotificationThresholdMs)
                {
                    _logger.LogWarning(
                        "Slow SignalR event notification detected: Battle={BattleId}, EventType={EventType}, ElapsedMs={ElapsedMs}",
                        battleId,
                        eventData.GetType().Name,
                        elapsedMs
                    );
                }
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

    /// <summary>
    /// 获取战斗组名（使用配置的前缀）
    /// </summary>
    private string GetBattleGroupName(Guid battleId)
    {
        return $"{_options.GroupNamePrefix}{battleId}";
    }
}
