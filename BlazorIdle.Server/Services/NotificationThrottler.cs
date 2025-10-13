namespace BlazorIdle.Server.Services;

/// <summary>
/// 通知节流器
/// 防止高频事件导致过多的 SignalR 通信
/// </summary>
public sealed class NotificationThrottler
{
    private readonly Dictionary<string, ThrottleState> _throttleStates = new();
    private readonly object _lock = new();
    private readonly TimeSpan _defaultWindow;

    /// <summary>
    /// 节流状态
    /// </summary>
    private sealed class ThrottleState
    {
        public DateTime LastSentTime { get; set; }
        public int SuppressedCount { get; set; }
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="windowMs">节流窗口大小（毫秒）</param>
    public NotificationThrottler(int windowMs = 1000)
    {
        _defaultWindow = TimeSpan.FromMilliseconds(windowMs);
    }

    /// <summary>
    /// 检查是否应该发送通知
    /// </summary>
    /// <param name="eventKey">事件唯一键（如 "battle_{battleId}_{eventType}"）</param>
    /// <returns>如果应该发送返回 true，否则返回 false</returns>
    public bool ShouldSend(string eventKey)
    {
        return ShouldSend(eventKey, _defaultWindow);
    }

    /// <summary>
    /// 检查是否应该发送通知（自定义窗口）
    /// </summary>
    /// <param name="eventKey">事件唯一键</param>
    /// <param name="window">节流窗口</param>
    /// <returns>如果应该发送返回 true，否则返回 false</returns>
    public bool ShouldSend(string eventKey, TimeSpan window)
    {
        lock (_lock)
        {
            var now = DateTime.UtcNow;

            if (!_throttleStates.TryGetValue(eventKey, out var state))
            {
                // 首次发送
                _throttleStates[eventKey] = new ThrottleState
                {
                    LastSentTime = now,
                    SuppressedCount = 0
                };
                return true;
            }

            var elapsed = now - state.LastSentTime;
            if (elapsed < window)
            {
                // 在节流窗口内，抑制通知
                state.SuppressedCount++;
                return false;
            }

            // 超过窗口，允许发送
            state.LastSentTime = now;
            state.SuppressedCount = 0;
            return true;
        }
    }

    /// <summary>
    /// 获取被抑制的通知数量
    /// </summary>
    /// <param name="eventKey">事件唯一键</param>
    /// <returns>被抑制的通知数量</returns>
    public int GetSuppressedCount(string eventKey)
    {
        lock (_lock)
        {
            return _throttleStates.TryGetValue(eventKey, out var state) ? state.SuppressedCount : 0;
        }
    }

    /// <summary>
    /// 清理过期的节流状态（可定期调用）
    /// </summary>
    /// <param name="expirationMinutes">超过多少分钟未使用则清理，默认 30 分钟</param>
    public void CleanupExpiredStates(int expirationMinutes = 30)
    {
        lock (_lock)
        {
            var cutoff = DateTime.UtcNow.AddMinutes(-expirationMinutes);
            var keysToRemove = _throttleStates
                .Where(kvp => kvp.Value.LastSentTime < cutoff)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in keysToRemove)
            {
                _throttleStates.Remove(key);
            }
        }
    }

    /// <summary>
    /// 获取当前节流状态数量
    /// </summary>
    public int GetStateCount()
    {
        lock (_lock)
        {
            return _throttleStates.Count;
        }
    }

    /// <summary>
    /// 清空所有节流状态
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _throttleStates.Clear();
        }
    }
}
