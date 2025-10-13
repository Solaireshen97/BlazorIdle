using BlazorIdle.Server.Application.Abstractions;
using BlazorIdle.Server.Config;
using Microsoft.Extensions.Options;

namespace BlazorIdle.Server.Services.Filters;

/// <summary>
/// 事件类型过滤器
/// 根据配置检查事件类型是否启用
/// </summary>
public sealed class EventTypeFilter : INotificationFilter
{
    private readonly SignalROptions _options;

    public EventTypeFilter(IOptions<SignalROptions> options)
    {
        _options = options.Value;
    }

    public string Name => "EventTypeFilter";

    public int Priority => 10; // 较高优先级

    public bool ShouldNotify(NotificationFilterContext context)
    {
        return context.EventType switch
        {
            "PlayerDeath" => _options.Notification.EnablePlayerDeathNotification,
            "PlayerRevive" => _options.Notification.EnablePlayerReviveNotification,
            "EnemyKilled" => _options.Notification.EnableEnemyKilledNotification,
            "TargetSwitched" => _options.Notification.EnableTargetSwitchedNotification,
            "WaveSpawn" => _options.Notification.EnableWaveSpawnNotification,
            "SkillCast" => _options.Notification.EnableSkillCastNotification,
            "BuffChange" => _options.Notification.EnableBuffChangeNotification,
            _ => true // 未知类型默认允许
        };
    }
}
