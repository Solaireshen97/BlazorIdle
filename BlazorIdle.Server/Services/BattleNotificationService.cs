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

        // 检查特定事件类型是否启用
        if (!IsEventTypeEnabled(eventType))
        {
            if (_options.EnableDetailedLogging)
            {
                _logger.LogDebug(
                    "Event type {EventType} is disabled in configuration, skipping notification for battle {BattleId}",
                    eventType,
                    battleId
                );
            }
            return;
        }

        try
        {
            var startTime = DateTime.UtcNow;
            var groupName = $"{_options.BattleGroupPrefix}{battleId}";
            var notification = new StateChangedEvent
            {
                BattleId = battleId,
                EventType = eventType,
                Timestamp = startTime
            };

            await _hubContext.Clients
                .Group(groupName)
                .SendAsync("StateChanged", notification);

            // 性能监控：测量通知延迟
            if (_options.Monitoring.EnableLatencyMeasurement)
            {
                var latencyMs = (DateTime.UtcNow - startTime).TotalMilliseconds;
                if (latencyMs > _options.Monitoring.SlowNotificationThresholdMs)
                {
                    _logger.LogWarning(
                        "Slow SignalR notification detected: Battle={BattleId}, EventType={EventType}, Latency={Latency}ms",
                        battleId,
                        eventType,
                        latencyMs
                    );
                }
                else if (_options.EnableDetailedLogging)
                {
                    _logger.LogDebug(
                        "Sent SignalR notification: Battle={BattleId}, EventType={EventType}, Latency={Latency}ms",
                        battleId,
                        eventType,
                        latencyMs
                    );
                }
            }
            else if (_options.EnableDetailedLogging)
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
    /// 检查事件类型是否启用
    /// </summary>
    private bool IsEventTypeEnabled(string eventType)
    {
        return eventType switch
        {
            "PlayerDeath" => _options.Notification.EnablePlayerDeathNotification,
            "PlayerRevive" => _options.Notification.EnablePlayerReviveNotification,
            "EnemyKilled" => _options.Notification.EnableEnemyKilledNotification,
            "TargetSwitched" => _options.Notification.EnableTargetSwitchedNotification,
            "WaveSpawn" => _options.Notification.EnableWaveSpawnNotification,
            "SkillCast" => _options.Notification.EnableSkillCastNotification,
            "BuffChange" => _options.Notification.EnableBuffChangeNotification,
            _ => true // 默认启用未知类型
        };
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
            var startTime = DateTime.UtcNow;
            var groupName = $"{_options.BattleGroupPrefix}{battleId}";
            await _hubContext.Clients
                .Group(groupName)
                .SendAsync("BattleEvent", eventData);

            // 性能监控：测量通知延迟
            if (_options.Monitoring.EnableLatencyMeasurement)
            {
                var latencyMs = (DateTime.UtcNow - startTime).TotalMilliseconds;
                if (latencyMs > _options.Monitoring.SlowNotificationThresholdMs)
                {
                    _logger.LogWarning(
                        "Slow detailed event notification: Battle={BattleId}, EventType={EventType}, Latency={Latency}ms",
                        battleId,
                        eventData.GetType().Name,
                        latencyMs
                    );
                }
                else if (_options.EnableDetailedLogging)
                {
                    _logger.LogDebug(
                        "Sent detailed event notification: Battle={BattleId}, EventType={EventType}, Latency={Latency}ms",
                        battleId,
                        eventData.GetType().Name,
                        latencyMs
                    );
                }
            }
            else if (_options.EnableDetailedLogging)
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
