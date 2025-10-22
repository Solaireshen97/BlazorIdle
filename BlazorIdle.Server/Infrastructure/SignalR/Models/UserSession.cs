namespace BlazorIdle.Server.Infrastructure.SignalR.Models;

/// <summary>
/// 用户会话模型
/// 用于追踪用户的SignalR连接状态、订阅信息和元数据
/// </summary>
public class UserSession
{
    /// <summary>
    /// 用户ID
    /// </summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// 用户的所有活跃连接ID列表
    /// 支持同一用户多个设备/标签页同时连接
    /// </summary>
    public List<string> ConnectionIds { get; set; } = new();

    /// <summary>
    /// 用户订阅的资源
    /// Key: 订阅类型（如 "battle", "party", "activity"）
    /// Value: 订阅的资源ID集合
    /// </summary>
    public Dictionary<string, HashSet<string>> Subscriptions { get; set; } = new();

    /// <summary>
    /// 首次连接时间
    /// </summary>
    public DateTime ConnectedAt { get; set; }

    /// <summary>
    /// 最后一次心跳时间
    /// 用于检测空闲连接和异常断线
    /// </summary>
    public DateTime LastHeartbeat { get; set; }

    /// <summary>
    /// 会话元数据
    /// 可存储额外的会话相关信息，如用户偏好设置、临时状态等
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();
}
