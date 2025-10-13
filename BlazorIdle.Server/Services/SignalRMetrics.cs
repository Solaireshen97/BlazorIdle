using System.Diagnostics;

namespace BlazorIdle.Server.Services;

/// <summary>
/// SignalR 性能指标收集器
/// 用于监控和分析 SignalR 系统的运行状态
/// </summary>
public sealed class SignalRMetrics
{
    private long _totalNotificationsSent;
    private long _totalNotificationsFailed;
    private long _totalNotificationsSkipped;
    private readonly object _lock = new();
    private readonly List<long> _recentLatencies = new(100);

    /// <summary>
    /// 总发送通知数
    /// </summary>
    public long TotalNotificationsSent => Interlocked.Read(ref _totalNotificationsSent);

    /// <summary>
    /// 总失败通知数
    /// </summary>
    public long TotalNotificationsFailed => Interlocked.Read(ref _totalNotificationsFailed);

    /// <summary>
    /// 总跳过通知数（因配置禁用）
    /// </summary>
    public long TotalNotificationsSkipped => Interlocked.Read(ref _totalNotificationsSkipped);

    /// <summary>
    /// 平均通知延迟（毫秒）
    /// </summary>
    public double AverageLatencyMs
    {
        get
        {
            lock (_lock)
            {
                return _recentLatencies.Count > 0 
                    ? _recentLatencies.Average() 
                    : 0;
            }
        }
    }

    /// <summary>
    /// P95 通知延迟（毫秒）
    /// </summary>
    public double P95LatencyMs
    {
        get
        {
            lock (_lock)
            {
                if (_recentLatencies.Count == 0) return 0;
                
                var sorted = _recentLatencies.OrderBy(x => x).ToList();
                var index = (int)Math.Ceiling(sorted.Count * 0.95) - 1;
                return sorted[Math.Max(0, Math.Min(index, sorted.Count - 1))];
            }
        }
    }

    /// <summary>
    /// P99 通知延迟（毫秒）
    /// </summary>
    public double P99LatencyMs
    {
        get
        {
            lock (_lock)
            {
                if (_recentLatencies.Count == 0) return 0;
                
                var sorted = _recentLatencies.OrderBy(x => x).ToList();
                var index = (int)Math.Ceiling(sorted.Count * 0.99) - 1;
                return sorted[Math.Max(0, Math.Min(index, sorted.Count - 1))];
            }
        }
    }

    /// <summary>
    /// 记录成功发送的通知
    /// </summary>
    /// <param name="latencyMs">通知延迟（毫秒）</param>
    public void RecordNotificationSent(long latencyMs)
    {
        Interlocked.Increment(ref _totalNotificationsSent);
        
        lock (_lock)
        {
            _recentLatencies.Add(latencyMs);
            
            // 只保留最近 100 个延迟记录
            if (_recentLatencies.Count > 100)
            {
                _recentLatencies.RemoveAt(0);
            }
        }
    }

    /// <summary>
    /// 记录失败的通知
    /// </summary>
    public void RecordNotificationFailed()
    {
        Interlocked.Increment(ref _totalNotificationsFailed);
    }

    /// <summary>
    /// 记录跳过的通知
    /// </summary>
    public void RecordNotificationSkipped()
    {
        Interlocked.Increment(ref _totalNotificationsSkipped);
    }

    /// <summary>
    /// 获取成功率百分比
    /// </summary>
    public double SuccessRate
    {
        get
        {
            var total = TotalNotificationsSent + TotalNotificationsFailed;
            return total > 0 ? (double)TotalNotificationsSent / total * 100 : 100;
        }
    }

    /// <summary>
    /// 重置所有指标
    /// </summary>
    public void Reset()
    {
        Interlocked.Exchange(ref _totalNotificationsSent, 0);
        Interlocked.Exchange(ref _totalNotificationsFailed, 0);
        Interlocked.Exchange(ref _totalNotificationsSkipped, 0);
        
        lock (_lock)
        {
            _recentLatencies.Clear();
        }
    }

    /// <summary>
    /// 获取指标摘要
    /// </summary>
    public SignalRMetricsSummary GetSummary()
    {
        return new SignalRMetricsSummary
        {
            TotalSent = TotalNotificationsSent,
            TotalFailed = TotalNotificationsFailed,
            TotalSkipped = TotalNotificationsSkipped,
            SuccessRate = SuccessRate,
            AverageLatencyMs = AverageLatencyMs,
            P95LatencyMs = P95LatencyMs,
            P99LatencyMs = P99LatencyMs
        };
    }
}

/// <summary>
/// SignalR 指标摘要
/// </summary>
public sealed class SignalRMetricsSummary
{
    public long TotalSent { get; init; }
    public long TotalFailed { get; init; }
    public long TotalSkipped { get; init; }
    public double SuccessRate { get; init; }
    public double AverageLatencyMs { get; init; }
    public double P95LatencyMs { get; init; }
    public double P99LatencyMs { get; init; }
}
