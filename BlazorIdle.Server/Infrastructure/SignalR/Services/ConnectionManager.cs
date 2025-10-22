using System.Collections.Concurrent;
using BlazorIdle.Server.Infrastructure.SignalR.Models;

namespace BlazorIdle.Server.Infrastructure.SignalR.Services;

/// <summary>
/// 连接管理器实现
/// 使用内存字典管理所有活跃的SignalR连接和用户会话
/// 线程安全的实现，支持高并发场景
/// </summary>
public class ConnectionManager : IConnectionManager
{
    /// <summary>
    /// 用户会话字典，Key为用户ID
    /// 使用ConcurrentDictionary确保线程安全
    /// </summary>
    private readonly ConcurrentDictionary<string, UserSession> _sessions = new();
    
    private readonly ILogger<ConnectionManager> _logger;

    public ConnectionManager(ILogger<ConnectionManager> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 注册新的SignalR连接
    /// 如果用户会话不存在则创建新会话，否则添加到现有会话
    /// </summary>
    public Task RegisterConnectionAsync(string userId, string connectionId)
    {
        // 获取或创建用户会话
        var session = _sessions.GetOrAdd(userId, _ => new UserSession
        {
            UserId = userId,
            ConnectedAt = DateTime.UtcNow,
            LastHeartbeat = DateTime.UtcNow
        });

        // 线程安全地添加连接ID
        lock (session.ConnectionIds)
        {
            if (!session.ConnectionIds.Contains(connectionId))
            {
                session.ConnectionIds.Add(connectionId);
                _logger.LogDebug(
                    "为用户 {UserId} 添加了连接 {ConnectionId}，总连接数：{Count}",
                    userId, connectionId, session.ConnectionIds.Count);
            }
        }

        // 更新心跳时间
        session.LastHeartbeat = DateTime.UtcNow;
        return Task.CompletedTask;
    }

    /// <summary>
    /// 注销SignalR连接
    /// 移除指定的连接ID，如果用户没有其他活跃连接，则删除整个会话
    /// </summary>
    public Task UnregisterConnectionAsync(string userId, string connectionId)
    {
        if (_sessions.TryGetValue(userId, out var session))
        {
            lock (session.ConnectionIds)
            {
                session.ConnectionIds.Remove(connectionId);
                
                _logger.LogDebug(
                    "移除用户 {UserId} 的连接 {ConnectionId}，剩余连接数：{Count}",
                    userId, connectionId, session.ConnectionIds.Count);

                // 如果没有活跃连接了，移除整个会话
                if (session.ConnectionIds.Count == 0)
                {
                    _sessions.TryRemove(userId, out _);
                    _logger.LogInformation("用户 {UserId} 的会话已被移除", userId);
                }
            }
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// 获取用户的第一个活跃连接ID
    /// </summary>
    public Task<string?> GetConnectionIdAsync(string userId)
    {
        if (_sessions.TryGetValue(userId, out var session))
        {
            lock (session.ConnectionIds)
            {
                return Task.FromResult(session.ConnectionIds.FirstOrDefault());
            }
        }

        return Task.FromResult<string?>(null);
    }

    /// <summary>
    /// 获取用户的所有活跃连接ID
    /// 返回副本以避免并发修改问题
    /// </summary>
    public Task<IEnumerable<string>> GetConnectionIdsAsync(string userId)
    {
        if (_sessions.TryGetValue(userId, out var session))
        {
            lock (session.ConnectionIds)
            {
                return Task.FromResult<IEnumerable<string>>(session.ConnectionIds.ToList());
            }
        }

        return Task.FromResult(Enumerable.Empty<string>());
    }

    /// <summary>
    /// 检查用户是否在线
    /// </summary>
    public Task<bool> IsConnectedAsync(string userId)
    {
        return Task.FromResult(_sessions.ContainsKey(userId));
    }

    /// <summary>
    /// 获取用户会话信息
    /// </summary>
    public Task<UserSession?> GetSessionAsync(string userId)
    {
        _sessions.TryGetValue(userId, out var session);
        return Task.FromResult(session);
    }

    /// <summary>
    /// 添加用户订阅
    /// 记录用户订阅的资源，用于消息推送和清理
    /// </summary>
    public Task AddSubscriptionAsync(string userId, string type, string id)
    {
        if (_sessions.TryGetValue(userId, out var session))
        {
            // 确保订阅类型的集合存在
            if (!session.Subscriptions.ContainsKey(type))
            {
                session.Subscriptions[type] = new HashSet<string>();
            }

            session.Subscriptions[type].Add(id);
            _logger.LogDebug(
                "用户 {UserId} 订阅了 {Type}:{Id}",
                userId, type, id);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// 移除用户订阅
    /// </summary>
    public Task RemoveSubscriptionAsync(string userId, string type, string id)
    {
        if (_sessions.TryGetValue(userId, out var session))
        {
            if (session.Subscriptions.TryGetValue(type, out var subscriptions))
            {
                subscriptions.Remove(id);
                _logger.LogDebug(
                    "用户 {UserId} 取消订阅 {Type}:{Id}",
                    userId, type, id);
            }
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// 获取空闲的会话列表
    /// 用于清理长时间无心跳的连接
    /// </summary>
    public IEnumerable<UserSession> GetIdleSessions(TimeSpan idleThreshold)
    {
        var cutoffTime = DateTime.UtcNow - idleThreshold;
        return _sessions.Values.Where(s => s.LastHeartbeat < cutoffTime).ToList();
    }
}
