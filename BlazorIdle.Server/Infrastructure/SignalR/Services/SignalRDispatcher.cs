using System.Threading.Channels;
using Microsoft.AspNetCore.SignalR;
using BlazorIdle.Server.Infrastructure.SignalR.Hubs;
using BlazorIdle.Server.Infrastructure.SignalR.Models;

namespace BlazorIdle.Server.Infrastructure.SignalR.Services;

/// <summary>
/// SignalR消息分发器
/// 使用异步队列和批量发送优化性能，支持消息优先级调度
/// </summary>
public class SignalRDispatcher : ISignalRDispatcher, IDisposable
{
    private readonly IHubContext<GameHub> _hubContext;
    private readonly IConnectionManager _connectionManager;
    private readonly ILogger<SignalRDispatcher> _logger;
    private readonly Channel<PendingMessage> _messageChannel;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _processingTask;
    private readonly SignalROptions _options;

    // 监控指标
    private int _totalMessagesSent = 0;
    private int _failedMessages = 0;
    private readonly List<long> _latencyHistory = new();
    private readonly object _latencyLock = new();

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="hubContext">GameHub上下文，用于发送消息</param>
    /// <param name="connectionManager">连接管理器，用于获取用户连接ID</param>
    /// <param name="logger">日志记录器</param>
    /// <param name="options">SignalR配置选项</param>
    public SignalRDispatcher(
        IHubContext<GameHub> hubContext,
        IConnectionManager connectionManager,
        ILogger<SignalRDispatcher> logger,
        SignalROptions options)
    {
        _hubContext = hubContext;
        _connectionManager = connectionManager;
        _logger = logger;
        _options = options;

        // 创建有界通道（背压控制）
        // 当队列满时，写入操作会等待，避免内存溢出
        _messageChannel = Channel.CreateBounded<PendingMessage>(new BoundedChannelOptions(_options.QueueCapacity)
        {
            FullMode = BoundedChannelFullMode.Wait
        });

        // 启动后台消息处理任务
        _processingTask = Task.Run(() => ProcessMessagesAsync(_cts.Token));

        _logger.LogInformation(
            "SignalRDispatcher initialized with QueueCapacity={QueueCapacity}, BatchSize={BatchSize}, BatchIntervalMs={BatchIntervalMs}",
            _options.QueueCapacity, _options.BatchSize, _options.BatchIntervalMs);
    }

    /// <summary>
    /// 向指定用户发送消息
    /// 消息会被加入队列，由后台任务异步处理
    /// </summary>
    public async Task SendToUserAsync(
        string userId,
        string method,
        object message,
        MessagePriority priority = MessagePriority.Normal)
    {
        var pendingMessage = new PendingMessage
        {
            Type = MessageType.User,
            Target = userId,
            Method = method,
            Message = message,
            Priority = priority,
            EnqueuedAt = DateTime.UtcNow
        };

        await _messageChannel.Writer.WriteAsync(pendingMessage, _cts.Token);
    }

    /// <summary>
    /// 向指定组发送消息
    /// 消息会被加入队列，由后台任务异步处理
    /// </summary>
    public async Task SendToGroupAsync(
        string groupName,
        string method,
        object message,
        MessagePriority priority = MessagePriority.Normal)
    {
        var pendingMessage = new PendingMessage
        {
            Type = MessageType.Group,
            Target = groupName,
            Method = method,
            Message = message,
            Priority = priority,
            EnqueuedAt = DateTime.UtcNow
        };

        await _messageChannel.Writer.WriteAsync(pendingMessage, _cts.Token);
    }

    /// <summary>
    /// 向所有客户端发送消息
    /// 消息会被加入队列，由后台任务异步处理
    /// </summary>
    public async Task SendToAllAsync(
        string method,
        object message,
        MessagePriority priority = MessagePriority.Normal)
    {
        var pendingMessage = new PendingMessage
        {
            Type = MessageType.All,
            Target = "all",
            Method = method,
            Message = message,
            Priority = priority,
            EnqueuedAt = DateTime.UtcNow
        };

        await _messageChannel.Writer.WriteAsync(pendingMessage, _cts.Token);
    }

    /// <summary>
    /// 获取分发器性能指标
    /// </summary>
    public Task<DispatcherMetrics> GetMetricsAsync()
    {
        var metrics = new DispatcherMetrics
        {
            QueueDepth = _messageChannel.Reader.Count,
            TotalMessagesSent = _totalMessagesSent,
            FailedMessages = _failedMessages,
            AverageLatency = CalculateAverageLatency()
        };

        return Task.FromResult(metrics);
    }

    /// <summary>
    /// 后台消息处理循环
    /// 从队列中读取消息，按批次和时间窗口发送
    /// </summary>
    private async Task ProcessMessagesAsync(CancellationToken cancellationToken)
    {
        var batch = new List<PendingMessage>();
        var lastFlushTime = DateTime.UtcNow;

        _logger.LogInformation("Message processing loop started");

        try
        {
            // 使用Task.WhenAny来同时等待新消息和时间触发
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    // 计算下次刷新的等待时间
                    var timeSinceLastFlush = DateTime.UtcNow - lastFlushTime;
                    var timeUntilNextFlush = TimeSpan.FromMilliseconds(_options.BatchIntervalMs) - timeSinceLastFlush;
                    
                    if (timeUntilNextFlush <= TimeSpan.Zero && batch.Count > 0)
                    {
                        // 时间到了且有消息，立即刷新
                        await SendBatchAsync(batch);
                        batch.Clear();
                        lastFlushTime = DateTime.UtcNow;
                        continue;
                    }

                    // 尝试读取消息，使用超时避免无限等待
                    var readTimeout = timeUntilNextFlush > TimeSpan.Zero && batch.Count > 0 
                        ? timeUntilNextFlush 
                        : TimeSpan.FromMilliseconds(100); // 如果批次为空，使用100ms超时

                    using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    timeoutCts.CancelAfter(readTimeout);

                    try
                    {
                        // 尝试读取一条消息
                        if (_messageChannel.Reader.TryRead(out var message))
                        {
                            batch.Add(message);
                        }
                        else
                        {
                            // 通道为空，等待新消息或超时
                            await _messageChannel.Reader.WaitToReadAsync(timeoutCts.Token);
                            continue;
                        }
                    }
                    catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                    {
                        // 超时，检查是否需要刷新
                        if (batch.Count > 0)
                        {
                            await SendBatchAsync(batch);
                            batch.Clear();
                            lastFlushTime = DateTime.UtcNow;
                        }
                        continue;
                    }

                    // 检查是否应该发送批次
                    if (batch.Count >= _options.BatchSize)
                    {
                        await SendBatchAsync(batch);
                        batch.Clear();
                        lastFlushTime = DateTime.UtcNow;
                    }
                }
                catch (Exception ex) when (!(ex is OperationCanceledException))
                {
                    _logger.LogError(ex, "Error in message processing iteration");
                    await Task.Delay(100, cancellationToken); // 避免紧密循环
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Message processing loop cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal error in message processing loop");
        }
        finally
        {
            // 确保处理剩余消息
            if (batch.Count > 0)
            {
                try
                {
                    await SendBatchAsync(batch);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error sending final batch");
                }
            }
        }
    }

    /// <summary>
    /// 批量发送消息
    /// 按优先级排序后依次发送
    /// </summary>
    private async Task SendBatchAsync(List<PendingMessage> messages)
    {
        if (messages.Count == 0) return;

        // 按优先级排序（高优先级在前）
        messages.Sort((a, b) => b.Priority.CompareTo(a.Priority));

        _logger.LogDebug("Sending batch of {Count} messages", messages.Count);

        foreach (var msg in messages)
        {
            var startTime = DateTime.UtcNow;

            try
            {
                // 根据消息类型选择发送方式
                switch (msg.Type)
                {
                    case MessageType.User:
                        // 获取用户的所有连接ID并逐一发送
                        var connectionIds = await _connectionManager.GetConnectionIdsAsync(msg.Target);
                        foreach (var connectionId in connectionIds)
                        {
                            await _hubContext.Clients.Client(connectionId).SendCoreAsync(msg.Method, new object?[] { msg.Message }, default);
                        }
                        break;

                    case MessageType.Group:
                        // 向组内所有客户端发送
                        await _hubContext.Clients.Group(msg.Target).SendCoreAsync(msg.Method, new object?[] { msg.Message }, default);
                        break;

                    case MessageType.All:
                        // 向所有连接的客户端发送
                        await _hubContext.Clients.All.SendCoreAsync(msg.Method, new object?[] { msg.Message }, default);
                        break;
                }

                // 更新成功计数器
                Interlocked.Increment(ref _totalMessagesSent);

                // 记录消息延迟（从入队到发送的时间）
                var latency = (DateTime.UtcNow - msg.EnqueuedAt).TotalMilliseconds;
                RecordLatency((long)latency);

                _logger.LogTrace(
                    "Message sent: {Method} to {Target} (Type: {Type}, Priority: {Priority}, Latency: {Latency}ms)",
                    msg.Method, msg.Target, msg.Type, msg.Priority, latency);
            }
            catch (Exception ex)
            {
                // 记录失败但不中断批处理
                Interlocked.Increment(ref _failedMessages);
                _logger.LogError(ex, 
                    "Failed to send message: {Method} to {Target} (Type: {Type}, Priority: {Priority})",
                    msg.Method, msg.Target, msg.Type, msg.Priority);
            }
        }
    }

    /// <summary>
    /// 记录消息延迟
    /// 保持最近1000个延迟数据用于计算平均值
    /// </summary>
    private void RecordLatency(long latency)
    {
        lock (_latencyLock)
        {
            _latencyHistory.Add(latency);
            
            // 限制历史记录大小，避免内存无限增长
            if (_latencyHistory.Count > 1000)
            {
                _latencyHistory.RemoveAt(0);
            }
        }
    }

    /// <summary>
    /// 计算平均延迟
    /// </summary>
    private double CalculateAverageLatency()
    {
        lock (_latencyLock)
        {
            return _latencyHistory.Count > 0 ? _latencyHistory.Average() : 0;
        }
    }

    /// <summary>
    /// 释放资源
    /// 停止后台处理任务并清理队列
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _logger.LogInformation("Disposing SignalRDispatcher");

        try
        {
            // 取消后台处理任务
            _cts.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // CTS已被释放，忽略
        }
        
        // 标记通道为完成，不再接受新消息
        _messageChannel.Writer.Complete();
        
        // 等待处理任务完成（最多5秒）
        try
        {
            _processingTask.Wait(TimeSpan.FromSeconds(5));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Processing task did not complete within timeout");
        }
        
        _cts.Dispose();
        _disposed = true;
    }

    private bool _disposed = false;
}

/// <summary>
/// 待发送消息的内部表示
/// </summary>
internal class PendingMessage
{
    /// <summary>
    /// 消息目标类型
    /// </summary>
    public MessageType Type { get; set; }

    /// <summary>
    /// 目标标识（用户ID、组名或"all"）
    /// </summary>
    public string Target { get; set; } = string.Empty;

    /// <summary>
    /// SignalR方法名
    /// </summary>
    public string Method { get; set; } = string.Empty;

    /// <summary>
    /// 消息内容
    /// </summary>
    public object Message { get; set; } = null!;

    /// <summary>
    /// 消息优先级
    /// </summary>
    public MessagePriority Priority { get; set; }

    /// <summary>
    /// 消息入队时间
    /// 用于计算延迟
    /// </summary>
    public DateTime EnqueuedAt { get; set; }
}

/// <summary>
/// 消息目标类型枚举
/// </summary>
internal enum MessageType
{
    /// <summary>
    /// 发送给指定用户
    /// </summary>
    User,

    /// <summary>
    /// 发送给指定组
    /// </summary>
    Group,

    /// <summary>
    /// 发送给所有客户端
    /// </summary>
    All
}
