using BlazorIdle.Server.Application.Abstractions;
using BlazorIdle.Server.Config;
using Microsoft.Extensions.Options;

namespace BlazorIdle.Server.Services.Filters;

/// <summary>
/// 速率限制过滤器
/// 基于节流器实现的过滤器
/// </summary>
public sealed class RateLimitFilter : INotificationFilter
{
    private readonly SignalROptions _options;
    private readonly NotificationThrottler? _throttler;

    public RateLimitFilter(IOptions<SignalROptions> options)
    {
        _options = options.Value;
        
        if (_options.Performance.EnableThrottling)
        {
            _throttler = new NotificationThrottler(_options.Performance.ThrottleWindowMs);
        }
    }

    public string Name => "RateLimitFilter";

    public int Priority => 20; // 中等优先级

    public bool ShouldNotify(NotificationFilterContext context)
    {
        if (_throttler == null)
        {
            return true; // 未启用节流，允许所有通知
        }

        var throttleKey = $"battle_{context.BattleId}_{context.EventType}";
        var shouldSend = _throttler.ShouldSend(throttleKey);

        if (!shouldSend)
        {
            // 将抑制计数添加到元数据
            context.AddMetadata("SuppressedCount", _throttler.GetSuppressedCount(throttleKey));
        }

        return shouldSend;
    }

    /// <summary>
    /// 清理过期状态
    /// </summary>
    public void CleanupExpiredStates(int expirationMinutes = 30)
    {
        _throttler?.CleanupExpiredStates(expirationMinutes);
    }

    /// <summary>
    /// 获取状态数量
    /// </summary>
    public int GetStateCount()
    {
        return _throttler?.GetStateCount() ?? 0;
    }
}
