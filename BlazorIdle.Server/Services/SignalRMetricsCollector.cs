using System.Collections.Concurrent;

namespace BlazorIdle.Server.Services;

/// <summary>
/// SignalR 指标收集器
/// 收集和跟踪 SignalR 相关的性能指标
/// </summary>
public sealed class SignalRMetricsCollector
{
    private readonly ConcurrentDictionary<string, EventMetrics> _eventMetrics = new();
    private readonly ConcurrentDictionary<string, long> _counters = new();
    private readonly ILogger<SignalRMetricsCollector> _logger;
    private DateTime _startTime;

    public SignalRMetricsCollector(ILogger<SignalRMetricsCollector> logger)
    {
        _logger = logger;
        _startTime = DateTime.UtcNow;
    }

    /// <summary>
    /// 记录通知发送
    /// </summary>
    public void RecordNotificationSent(string eventType, bool throttled = false)
    {
        var metrics = _eventMetrics.GetOrAdd(eventType, _ => new EventMetrics());
        
        if (throttled)
        {
            Interlocked.Increment(ref metrics.ThrottledCount);
        }
        else
        {
            Interlocked.Increment(ref metrics.SentCount);
        }
        
        Interlocked.Increment(ref metrics.TotalAttempts);
        metrics.LastEventTime = DateTime.UtcNow;
    }

    /// <summary>
    /// 记录通知失败
    /// </summary>
    public void RecordNotificationFailed(string eventType, Exception? exception = null)
    {
        var metrics = _eventMetrics.GetOrAdd(eventType, _ => new EventMetrics());
        Interlocked.Increment(ref metrics.FailedCount);
        metrics.LastErrorTime = DateTime.UtcNow;
        metrics.LastError = exception?.Message;
    }

    /// <summary>
    /// 增加计数器
    /// </summary>
    public void IncrementCounter(string counterName, long delta = 1)
    {
        _counters.AddOrUpdate(counterName, delta, (_, current) => current + delta);
    }

    /// <summary>
    /// 获取计数器值
    /// </summary>
    public long GetCounter(string counterName)
    {
        return _counters.TryGetValue(counterName, out var value) ? value : 0;
    }

    /// <summary>
    /// 获取事件指标
    /// </summary>
    public EventMetrics? GetEventMetrics(string eventType)
    {
        return _eventMetrics.TryGetValue(eventType, out var metrics) ? metrics : null;
    }

    /// <summary>
    /// 获取所有事件的指标摘要
    /// </summary>
    public MetricsSummary GetSummary()
    {
        var summary = new MetricsSummary
        {
            CollectionStartTime = _startTime,
            SnapshotTime = DateTime.UtcNow,
            EventTypeMetrics = new Dictionary<string, EventMetrics>()
        };

        foreach (var kvp in _eventMetrics)
        {
            summary.EventTypeMetrics[kvp.Key] = kvp.Value.Clone();
        }

        // 计算总计
        summary.TotalNotificationsSent = summary.EventTypeMetrics.Values.Sum(m => m.SentCount);
        summary.TotalNotificationsThrottled = summary.EventTypeMetrics.Values.Sum(m => m.ThrottledCount);
        summary.TotalNotificationsFailed = summary.EventTypeMetrics.Values.Sum(m => m.FailedCount);
        summary.TotalAttempts = summary.EventTypeMetrics.Values.Sum(m => m.TotalAttempts);

        return summary;
    }

    /// <summary>
    /// 重置所有指标
    /// </summary>
    public void Reset()
    {
        _eventMetrics.Clear();
        _counters.Clear();
        _startTime = DateTime.UtcNow;
        _logger.LogInformation("SignalR 指标已重置");
    }

    /// <summary>
    /// 记录指标日志
    /// </summary>
    public void LogMetrics()
    {
        var summary = GetSummary();
        var duration = DateTime.UtcNow - _startTime;

        _logger.LogInformation(
            "SignalR 指标摘要 (运行时长: {Duration:hh\\:mm\\:ss}): " +
            "总发送={Sent}, 总节流={Throttled}, 总失败={Failed}, " +
            "节流率={ThrottleRate:P2}, 失败率={FailureRate:P2}",
            duration,
            summary.TotalNotificationsSent,
            summary.TotalNotificationsThrottled,
            summary.TotalNotificationsFailed,
            summary.ThrottleRate,
            summary.FailureRate
        );

        foreach (var (eventType, metrics) in summary.EventTypeMetrics)
        {
            _logger.LogDebug(
                "事件类型 {EventType}: 发送={Sent}, 节流={Throttled}, 失败={Failed}",
                eventType,
                metrics.SentCount,
                metrics.ThrottledCount,
                metrics.FailedCount
            );
        }
    }

    /// <summary>
    /// 事件指标
    /// </summary>
    public sealed class EventMetrics
    {
        public long SentCount;
        public long ThrottledCount;
        public long FailedCount;
        public long TotalAttempts;
        public DateTime? LastEventTime;
        public DateTime? LastErrorTime;
        public string? LastError;

        public EventMetrics Clone()
        {
            return new EventMetrics
            {
                SentCount = SentCount,
                ThrottledCount = ThrottledCount,
                FailedCount = FailedCount,
                TotalAttempts = TotalAttempts,
                LastEventTime = LastEventTime,
                LastErrorTime = LastErrorTime,
                LastError = LastError
            };
        }
    }

    /// <summary>
    /// 指标摘要
    /// </summary>
    public sealed class MetricsSummary
    {
        public DateTime CollectionStartTime { get; set; }
        public DateTime SnapshotTime { get; set; }
        public long TotalNotificationsSent { get; set; }
        public long TotalNotificationsThrottled { get; set; }
        public long TotalNotificationsFailed { get; set; }
        public long TotalAttempts { get; set; }
        public Dictionary<string, EventMetrics> EventTypeMetrics { get; set; } = new();

        public double ThrottleRate => TotalAttempts > 0
            ? (double)TotalNotificationsThrottled / TotalAttempts
            : 0;

        public double FailureRate => TotalAttempts > 0
            ? (double)TotalNotificationsFailed / TotalAttempts
            : 0;

        public TimeSpan CollectionDuration => SnapshotTime - CollectionStartTime;
    }
}
