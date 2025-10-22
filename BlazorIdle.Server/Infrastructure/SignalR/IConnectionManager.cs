using BlazorIdle.Server.Infrastructure.SignalR.Models;

namespace BlazorIdle.Server.Infrastructure.SignalR;

/// <summary>
/// 连接管理器接口
/// 负责管理用户的SignalR连接、会话和订阅信息
/// </summary>
public interface IConnectionManager
{
    /// <summary>
    /// 注册新的SignalR连接
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <param name="connectionId">SignalR连接ID</param>
    Task RegisterConnectionAsync(string userId, string connectionId);

    /// <summary>
    /// 注销SignalR连接
    /// 如果用户没有其他活跃连接，则清理整个会话
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <param name="connectionId">SignalR连接ID</param>
    Task UnregisterConnectionAsync(string userId, string connectionId);

    /// <summary>
    /// 获取用户的第一个活跃连接ID
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <returns>连接ID，如果用户未连接则返回null</returns>
    Task<string?> GetConnectionIdAsync(string userId);

    /// <summary>
    /// 获取用户的所有活跃连接ID
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <returns>连接ID列表</returns>
    Task<IEnumerable<string>> GetConnectionIdsAsync(string userId);

    /// <summary>
    /// 检查用户是否在线
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <returns>如果用户有活跃连接则返回true</returns>
    Task<bool> IsConnectedAsync(string userId);

    /// <summary>
    /// 获取用户会话信息
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <returns>用户会话，如果不存在则返回null</returns>
    Task<UserSession?> GetSessionAsync(string userId);

    /// <summary>
    /// 添加用户订阅
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <param name="type">订阅类型（如 "battle", "party"）</param>
    /// <param name="id">订阅的资源ID</param>
    Task AddSubscriptionAsync(string userId, string type, string id);

    /// <summary>
    /// 移除用户订阅
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <param name="type">订阅类型</param>
    /// <param name="id">订阅的资源ID</param>
    Task RemoveSubscriptionAsync(string userId, string type, string id);

    /// <summary>
    /// 获取空闲的会话列表
    /// 用于清理长时间无心跳的连接
    /// </summary>
    /// <param name="idleThreshold">空闲时间阈值</param>
    /// <returns>空闲会话列表</returns>
    IEnumerable<UserSession> GetIdleSessions(TimeSpan idleThreshold);
}
