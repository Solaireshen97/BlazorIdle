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
    private readonly NotificationThrottler? _throttler;
    private readonly SignalRMetricsCollector? _metricsCollector;

    public BattleNotificationService(
        IHubContext<BattleNotificationHub> hubContext,
        ILogger<BattleNotificationService> logger,
        IOptions<SignalROptions> options,
        SignalRMetricsCollector? metricsCollector = null)
    {
        _hubContext = hubContext;
        _logger = logger;
        _options = options.Value;
        _metricsCollector = metricsCollector;
        
        // 如果启用节流，创建节流器
        if (_options.Performance.EnableThrottling)
        {
            _throttler = new NotificationThrottler(_options.Performance.ThrottleWindowMs);
        }
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

        // 检查节流
        if (_throttler != null)
        {
            var throttleKey = $"battle_{battleId}_{eventType}";
            if (!_throttler.ShouldSend(throttleKey))
            {
                _metricsCollector?.RecordNotificationSent(eventType, throttled: true);
                
                if (_options.EnableDetailedLogging)
                {
                    _logger.LogDebug(
                        "Notification throttled: Battle={BattleId}, EventType={EventType}, SuppressedCount={Count}",
                        battleId,
                        eventType,
                        _throttler.GetSuppressedCount(throttleKey)
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

            _metricsCollector?.RecordNotificationSent(eventType, throttled: false);

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
            _metricsCollector?.RecordNotificationFailed(eventType, ex);
            
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
            "AttackTick" => _options.Notification.EnableAttackTickNotification,
            "DamageApplied" => _options.Notification.EnableDamageAppliedNotification,
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
