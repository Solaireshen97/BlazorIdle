using BlazorIdle.Server.Infrastructure.SignalR.Models;

namespace BlazorIdle.Server.Infrastructure.SignalR;

/// <summary>
/// SignalR消息分发器接口
/// 负责管理消息队列、批量发送和优先级调度
/// </summary>
public interface ISignalRDispatcher
{
    /// <summary>
    /// 向指定用户发送消息
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <param name="method">SignalR方法名</param>
    /// <param name="message">消息内容</param>
    /// <param name="priority">消息优先级，默认为普通优先级</param>
    Task SendToUserAsync(
        string userId,
        string method,
        object message,
        MessagePriority priority = MessagePriority.Normal);

    /// <summary>
    /// 向指定组发送消息
    /// </summary>
    /// <param name="groupName">组名（如 "battle:123"）</param>
    /// <param name="method">SignalR方法名</param>
    /// <param name="message">消息内容</param>
    /// <param name="priority">消息优先级，默认为普通优先级</param>
    Task SendToGroupAsync(
        string groupName,
        string method,
        object message,
        MessagePriority priority = MessagePriority.Normal);

    /// <summary>
    /// 向所有连接的客户端发送消息
    /// </summary>
    /// <param name="method">SignalR方法名</param>
    /// <param name="message">消息内容</param>
    /// <param name="priority">消息优先级，默认为普通优先级</param>
    Task SendToAllAsync(
        string method,
        object message,
        MessagePriority priority = MessagePriority.Normal);

    /// <summary>
    /// 获取分发器性能指标
    /// 用于监控和调试
    /// </summary>
    /// <returns>性能指标数据</returns>
    Task<DispatcherMetrics> GetMetricsAsync();
}

/// <summary>
/// 分发器性能指标
/// 提供消息队列状态和性能数据
/// </summary>
public class DispatcherMetrics
{
    /// <summary>
    /// 当前消息队列深度
    /// 表示待发送的消息数量
    /// </summary>
    public int QueueDepth { get; set; }

    /// <summary>
    /// 累计已发送的消息总数
    /// </summary>
    public int TotalMessagesSent { get; set; }

    /// <summary>
    /// 累计发送失败的消息数
    /// </summary>
    public int FailedMessages { get; set; }

    /// <summary>
    /// 平均消息发送延迟（毫秒）
    /// 从消息入队到实际发送的平均时间
    /// </summary>
    public double AverageLatency { get; set; }
}
