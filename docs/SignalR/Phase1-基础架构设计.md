# Phase 1: SignalR 基础架构设计（下篇）

**阶段目标**: 建立 SignalR 基础设施，实现连接管理和消息分发机制  
**实施时间**: 2周  
**前置条件**: 无  
**后续阶段**: Phase 2 战斗事件集成

---

## 📋 目录

1. [架构设计](#架构设计)
2. [组件详细设计](#组件详细设计)
3. [实施步骤](#实施步骤)
4. [代码示例](#代码示例)
5. [测试方案](#测试方案)
6. [部署配置](#部署配置)

---

## 架构设计

### 整体架构

```
┌───────────────────────────────────────────────────────────┐
│  Infrastructure.Messaging (新增)                          │
│                                                            │
│  ┌──────────────────────────────────────────────────┐     │
│  │  GameNotificationHub : Hub                       │     │
│  │  职责：SignalR 连接管理和消息路由                 │     │
│  └──────────────────────┬───────────────────────────┘     │
│                         │                                 │
│  ┌──────────────────────▼───────────────────────────┐     │
│  │  ISignalRDispatcher                             │     │
│  │  └─ SignalRDispatcher (实现)                    │     │
│  │  职责：消息队列和批量发送                        │     │
│  └──────────────────────┬───────────────────────────┘     │
│                         │                                 │
│  ┌──────────────────────▼───────────────────────────┐     │
│  │  IDomainEventBus                                │     │
│  │  └─ InMemoryEventBus (实现)                     │     │
│  │  职责：领域事件发布和订阅                        │     │
│  └──────────────────────────────────────────────────┘     │
│                                                            │
└───────────────────────────────────────────────────────────┘

┌───────────────────────────────────────────────────────────┐
│  Domain Events (新增接口定义)                              │
│                                                            │
│  IDomainEvent (基础接口)                                   │
│  ├─ INotificationEvent (可推送事件标记接口)                │
│  │   ├─ CombatSegmentFlushedEvent                        │
│  │   ├─ BattleEndedEvent                                 │
│  │   └─ ActivityCompletedEvent (未来)                    │
│  └─ ... (其他领域事件)                                     │
│                                                            │
└───────────────────────────────────────────────────────────┘
```

---

### 模块划分

| 模块 | 职责 | 位置 |
|------|------|------|
| **GameNotificationHub** | SignalR Hub，管理连接 | `Infrastructure/Messaging/Hubs/` |
| **SignalRDispatcher** | 消息分发器，队列管理 | `Infrastructure/Messaging/` |
| **InMemoryEventBus** | 内存事件总线 | `Infrastructure/Messaging/` |
| **IDomainEvent** | 领域事件基础接口 | `Domain/Events/` |
| **ConnectionManager** | 连接状态管理 | `Infrastructure/Messaging/` |

---

## 组件详细设计

### 1. IDomainEvent（领域事件基础接口）

**位置**: `BlazorIdle.Server/Domain/Events/IDomainEvent.cs`

**设计目标**:
- 所有领域事件的统一接口
- 支持事件溯源
- 轻量级，易于序列化

**接口定义**:

```csharp
namespace BlazorIdle.Server.Domain.Events;

/// <summary>
/// 领域事件基础接口
/// </summary>
public interface IDomainEvent
{
    /// <summary>
    /// 事件唯一标识
    /// </summary>
    Guid EventId { get; }

    /// <summary>
    /// 事件发生时间（UTC）
    /// </summary>
    DateTime OccurredAtUtc { get; }

    /// <summary>
    /// 事件类型标识（用于序列化和路由）
    /// </summary>
    string EventType { get; }

    /// <summary>
    /// 关联的角色ID（可选，用于消息路由）
    /// </summary>
    Guid? CharacterId { get; }
}
```

---

### 2. INotificationEvent（可推送事件标记接口）

**位置**: `BlazorIdle.Server/Domain/Events/INotificationEvent.cs`

**设计目标**:
- 标记需要推送到客户端的事件
- 区分内部事件和外部通知
- 支持事件优先级

**接口定义**:

```csharp
namespace BlazorIdle.Server.Domain.Events;

/// <summary>
/// 可推送到客户端的事件标记接口
/// 实现此接口的事件会被 SignalRDispatcher 自动推送
/// </summary>
public interface INotificationEvent : IDomainEvent
{
    /// <summary>
    /// 通知优先级（用于流控和过滤）
    /// </summary>
    NotificationPriority Priority { get; }

    /// <summary>
    /// 获取客户端通知消息（用于序列化到客户端）
    /// </summary>
    object ToClientMessage();
}

/// <summary>
/// 通知优先级
/// </summary>
public enum NotificationPriority
{
    /// <summary>低优先级（如技能释放）</summary>
    Low = 0,
    
    /// <summary>普通优先级（如伤害事件）</summary>
    Normal = 1,
    
    /// <summary>高优先级（如战斗结束）</summary>
    High = 2,
    
    /// <summary>紧急（如角色死亡）</summary>
    Critical = 3
}
```

---

### 3. IDomainEventBus（事件总线接口）

**位置**: `BlazorIdle.Server/Infrastructure/Messaging/IDomainEventBus.cs`

**设计目标**:
- 解耦事件发布者和订阅者
- 支持异步处理
- 支持多订阅者
- 线程安全

**接口定义**:

```csharp
namespace BlazorIdle.Server.Infrastructure.Messaging;

using BlazorIdle.Server.Domain.Events;

/// <summary>
/// 领域事件总线接口
/// </summary>
public interface IDomainEventBus
{
    /// <summary>
    /// 发布事件（异步）
    /// </summary>
    Task PublishAsync<TEvent>(TEvent @event, CancellationToken ct = default)
        where TEvent : IDomainEvent;

    /// <summary>
    /// 发布事件（同步，立即返回，异步执行）
    /// </summary>
    void Publish<TEvent>(TEvent @event)
        where TEvent : IDomainEvent;

    /// <summary>
    /// 订阅事件
    /// </summary>
    IDisposable Subscribe<TEvent>(Func<TEvent, Task> handler)
        where TEvent : IDomainEvent;

    /// <summary>
    /// 订阅事件（带过滤）
    /// </summary>
    IDisposable Subscribe<TEvent>(
        Func<TEvent, bool> filter,
        Func<TEvent, Task> handler)
        where TEvent : IDomainEvent;

    /// <summary>
    /// 获取统计信息
    /// </summary>
    EventBusStatistics GetStatistics();
}

/// <summary>
/// 事件总线统计信息
/// </summary>
public record EventBusStatistics(
    int TotalPublished,
    int TotalSubscriptions,
    Dictionary<string, int> EventTypeCounts
);
```

---

### 4. InMemoryEventBus（内存事件总线实现）

**位置**: `BlazorIdle.Server/Infrastructure/Messaging/InMemoryEventBus.cs`

**设计要点**:
- 使用 Channel 实现异步队列
- 后台任务处理事件分发
- 线程安全的订阅管理
- 错误隔离（单个处理器失败不影响其他）

**核心实现**:

```csharp
using System.Threading.Channels;
using System.Collections.Concurrent;
using BlazorIdle.Server.Domain.Events;

namespace BlazorIdle.Server.Infrastructure.Messaging;

public sealed class InMemoryEventBus : IDomainEventBus, IDisposable
{
    private readonly Channel<IDomainEvent> _eventChannel;
    private readonly ConcurrentDictionary<Type, List<Subscription>> _subscriptions;
    private readonly ILogger<InMemoryEventBus> _logger;
    private readonly Task _processingTask;
    private readonly CancellationTokenSource _cts;

    // 统计
    private int _totalPublished;
    private readonly ConcurrentDictionary<string, int> _eventTypeCounts;

    public InMemoryEventBus(ILogger<InMemoryEventBus> logger)
    {
        _logger = logger;
        _eventChannel = Channel.CreateUnbounded<IDomainEvent>(
            new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false
            });

        _subscriptions = new ConcurrentDictionary<Type, List<Subscription>>();
        _eventTypeCounts = new ConcurrentDictionary<string, int>();
        _cts = new CancellationTokenSource();

        // 启动后台处理任务
        _processingTask = Task.Run(ProcessEventsAsync);
    }

    public async Task PublishAsync<TEvent>(TEvent @event, CancellationToken ct = default)
        where TEvent : IDomainEvent
    {
        ArgumentNullException.ThrowIfNull(@event);
        
        await _eventChannel.Writer.WriteAsync(@event, ct);
        
        Interlocked.Increment(ref _totalPublished);
        _eventTypeCounts.AddOrUpdate(@event.EventType, 1, (_, count) => count + 1);
    }

    public void Publish<TEvent>(TEvent @event) where TEvent : IDomainEvent
    {
        ArgumentNullException.ThrowIfNull(@event);
        
        _eventChannel.Writer.TryWrite(@event);
        
        Interlocked.Increment(ref _totalPublished);
        _eventTypeCounts.AddOrUpdate(@event.EventType, 1, (_, count) => count + 1);
    }

    public IDisposable Subscribe<TEvent>(Func<TEvent, Task> handler)
        where TEvent : IDomainEvent
    {
        return Subscribe<TEvent>(_ => true, handler);
    }

    public IDisposable Subscribe<TEvent>(
        Func<TEvent, bool> filter,
        Func<TEvent, Task> handler)
        where TEvent : IDomainEvent
    {
        var eventType = typeof(TEvent);
        var subscription = new Subscription(
            async (@event) =>
            {
                if (@event is TEvent typedEvent && filter(typedEvent))
                {
                    await handler(typedEvent);
                }
            });

        _subscriptions.AddOrUpdate(
            eventType,
            _ => new List<Subscription> { subscription },
            (_, list) =>
            {
                lock (list)
                {
                    list.Add(subscription);
                }
                return list;
            });

        return subscription;
    }

    public EventBusStatistics GetStatistics()
    {
        return new EventBusStatistics(
            _totalPublished,
            _subscriptions.Values.Sum(list => list.Count),
            new Dictionary<string, int>(_eventTypeCounts)
        );
    }

    private async Task ProcessEventsAsync()
    {
        await foreach (var @event in _eventChannel.Reader.ReadAllAsync(_cts.Token))
        {
            try
            {
                await DispatchEventAsync(@event);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing event {EventType} {EventId}",
                    @event.EventType, @event.EventId);
            }
        }
    }

    private async Task DispatchEventAsync(IDomainEvent @event)
    {
        var eventType = @event.GetType();
        
        // 获取该类型及其基类/接口的所有订阅
        var types = GetEventTypeHierarchy(eventType);
        
        foreach (var type in types)
        {
            if (_subscriptions.TryGetValue(type, out var subscriptions))
            {
                List<Subscription> handlers;
                lock (subscriptions)
                {
                    handlers = subscriptions.ToList();
                }

                // 并行执行所有处理器
                var tasks = handlers
                    .Where(s => !s.IsDisposed)
                    .Select(s => ExecuteHandlerAsync(s, @event));

                await Task.WhenAll(tasks);
            }
        }
    }

    private async Task ExecuteHandlerAsync(Subscription subscription, IDomainEvent @event)
    {
        try
        {
            await subscription.Handler(@event);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "Error in event handler for {EventType} {EventId}",
                @event.EventType, @event.EventId);
        }
    }

    private static IEnumerable<Type> GetEventTypeHierarchy(Type eventType)
    {
        yield return eventType;

        // 基类
        var baseType = eventType.BaseType;
        while (baseType != null && baseType != typeof(object))
        {
            yield return baseType;
            baseType = baseType.BaseType;
        }

        // 接口
        foreach (var iface in eventType.GetInterfaces())
        {
            yield return iface;
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        _eventChannel.Writer.Complete();
        
        try
        {
            _processingTask.Wait(TimeSpan.FromSeconds(5));
        }
        catch
        {
            // Ignore
        }
        
        _cts.Dispose();
    }

    private class Subscription : IDisposable
    {
        public Func<IDomainEvent, Task> Handler { get; }
        public bool IsDisposed { get; private set; }

        public Subscription(Func<IDomainEvent, Task> handler)
        {
            Handler = handler;
        }

        public void Dispose()
        {
            IsDisposed = true;
        }
    }
}
```

---

### 5. ISignalRDispatcher（SignalR 消息分发器接口）

**位置**: `BlazorIdle.Server/Infrastructure/Messaging/ISignalRDispatcher.cs`

**设计目标**:
- 封装 SignalR 推送细节
- 支持多种推送模式（单用户、多用户、广播）
- 异步队列缓冲
- 批量发送优化

**接口定义**:

```csharp
namespace BlazorIdle.Server.Infrastructure.Messaging;

/// <summary>
/// SignalR 消息分发器接口
/// </summary>
public interface ISignalRDispatcher
{
    /// <summary>
    /// 发送消息给指定用户
    /// </summary>
    Task SendToUserAsync(Guid userId, string method, object data, CancellationToken ct = default);

    /// <summary>
    /// 发送消息给多个用户
    /// </summary>
    Task SendToUsersAsync(IEnumerable<Guid> userIds, string method, object data, CancellationToken ct = default);

    /// <summary>
    /// 广播消息给所有连接的客户端
    /// </summary>
    Task BroadcastAsync(string method, object data, CancellationToken ct = default);

    /// <summary>
    /// 发送消息给指定组
    /// </summary>
    Task SendToGroupAsync(string groupName, string method, object data, CancellationToken ct = default);

    /// <summary>
    /// 获取统计信息
    /// </summary>
    DispatcherStatistics GetStatistics();
}

/// <summary>
/// 分发器统计信息
/// </summary>
public record DispatcherStatistics(
    int TotalSent,
    int QueuedMessages,
    int FailedDeliveries,
    Dictionary<string, int> MethodCounts
);
```

---

### 6. SignalRDispatcher（实现）

**位置**: `BlazorIdle.Server/Infrastructure/Messaging/SignalRDispatcher.cs`

**核心实现**:

```csharp
using Microsoft.AspNetCore.SignalR;
using System.Threading.Channels;

namespace BlazorIdle.Server.Infrastructure.Messaging;

public sealed class SignalRDispatcher : ISignalRDispatcher, IDisposable
{
    private readonly IHubContext<GameNotificationHub> _hubContext;
    private readonly ILogger<SignalRDispatcher> _logger;
    private readonly Channel<SignalRMessage> _messageChannel;
    private readonly Task _processingTask;
    private readonly CancellationTokenSource _cts;

    // 统计
    private int _totalSent;
    private int _failedDeliveries;
    private readonly ConcurrentDictionary<string, int> _methodCounts;

    public SignalRDispatcher(
        IHubContext<GameNotificationHub> hubContext,
        ILogger<SignalRDispatcher> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
        _methodCounts = new ConcurrentDictionary<string, int>();
        _cts = new CancellationTokenSource();

        // 创建消息通道（背压控制）
        _messageChannel = Channel.CreateBounded<SignalRMessage>(
            new BoundedChannelOptions(1000)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = true,
                SingleWriter = false
            });

        // 启动后台发送任务
        _processingTask = Task.Run(ProcessMessagesAsync);
    }

    public async Task SendToUserAsync(
        Guid userId,
        string method,
        object data,
        CancellationToken ct = default)
    {
        var message = new SignalRMessage
        {
            Type = MessageType.ToUser,
            UserId = userId,
            Method = method,
            Data = data
        };

        await _messageChannel.Writer.WriteAsync(message, ct);
    }

    public async Task SendToUsersAsync(
        IEnumerable<Guid> userIds,
        string method,
        object data,
        CancellationToken ct = default)
    {
        var message = new SignalRMessage
        {
            Type = MessageType.ToUsers,
            UserIds = userIds.ToList(),
            Method = method,
            Data = data
        };

        await _messageChannel.Writer.WriteAsync(message, ct);
    }

    public async Task BroadcastAsync(
        string method,
        object data,
        CancellationToken ct = default)
    {
        var message = new SignalRMessage
        {
            Type = MessageType.Broadcast,
            Method = method,
            Data = data
        };

        await _messageChannel.Writer.WriteAsync(message, ct);
    }

    public async Task SendToGroupAsync(
        string groupName,
        string method,
        object data,
        CancellationToken ct = default)
    {
        var message = new SignalRMessage
        {
            Type = MessageType.ToGroup,
            GroupName = groupName,
            Method = method,
            Data = data
        };

        await _messageChannel.Writer.WriteAsync(message, ct);
    }

    public DispatcherStatistics GetStatistics()
    {
        return new DispatcherStatistics(
            _totalSent,
            _messageChannel.Reader.Count,
            _failedDeliveries,
            new Dictionary<string, int>(_methodCounts)
        );
    }

    private async Task ProcessMessagesAsync()
    {
        // 批量发送配置
        const int batchSize = 10;
        const int batchDelayMs = 50;

        var batch = new List<SignalRMessage>();
        var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(batchDelayMs));

        try
        {
            while (!_cts.Token.IsCancellationRequested)
            {
                // 收集批量消息
                while (batch.Count < batchSize &&
                       _messageChannel.Reader.TryRead(out var message))
                {
                    batch.Add(message);
                }

                // 如果有消息或超时，发送批量
                if (batch.Count > 0)
                {
                    await SendBatchAsync(batch);
                    batch.Clear();
                }

                // 等待下一批或超时
                await timer.WaitForNextTickAsync(_cts.Token);
            }
        }
        catch (OperationCanceledException)
        {
            // 正常关闭
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in message processing loop");
        }
    }

    private async Task SendBatchAsync(List<SignalRMessage> messages)
    {
        foreach (var message in messages)
        {
            try
            {
                await SendMessageAsync(message);
                Interlocked.Increment(ref _totalSent);
                _methodCounts.AddOrUpdate(message.Method, 1, (_, count) => count + 1);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, 
                    "Failed to send SignalR message: {Method}", message.Method);
                Interlocked.Increment(ref _failedDeliveries);
            }
        }
    }

    private Task SendMessageAsync(SignalRMessage message)
    {
        return message.Type switch
        {
            MessageType.ToUser => _hubContext.Clients
                .User(message.UserId.ToString())
                .SendAsync(message.Method, message.Data),

            MessageType.ToUsers => _hubContext.Clients
                .Users(message.UserIds!.Select(id => id.ToString()))
                .SendAsync(message.Method, message.Data),

            MessageType.Broadcast => _hubContext.Clients.All
                .SendAsync(message.Method, message.Data),

            MessageType.ToGroup => _hubContext.Clients
                .Group(message.GroupName!)
                .SendAsync(message.Method, message.Data),

            _ => Task.CompletedTask
        };
    }

    public void Dispose()
    {
        _cts.Cancel();
        _messageChannel.Writer.Complete();
        
        try
        {
            _processingTask.Wait(TimeSpan.FromSeconds(5));
        }
        catch
        {
            // Ignore
        }

        _cts.Dispose();
    }

    private class SignalRMessage
    {
        public MessageType Type { get; init; }
        public Guid UserId { get; init; }
        public List<Guid>? UserIds { get; init; }
        public string? GroupName { get; init; }
        public required string Method { get; init; }
        public required object Data { get; init; }
    }

    private enum MessageType
    {
        ToUser,
        ToUsers,
        Broadcast,
        ToGroup
    }
}
```

---

### 7. GameNotificationHub（SignalR Hub）

**位置**: `BlazorIdle.Server/Infrastructure/Messaging/Hubs/GameNotificationHub.cs`

**设计要点**:
- 管理客户端连接生命周期
- 支持用户认证（未来）
- 连接状态跟踪
- 心跳检测

**核心实现**:

```csharp
using Microsoft.AspNetCore.SignalR;

namespace BlazorIdle.Server.Infrastructure.Messaging.Hubs;

/// <summary>
/// 游戏通知 Hub
/// 客户端方法约定：
/// - OnBattleUpdate: 战斗更新
/// - OnBattleEnded: 战斗结束
/// - OnNotification: 通用通知
/// </summary>
public class GameNotificationHub : Hub
{
    private readonly ILogger<GameNotificationHub> _logger;
    private readonly ConnectionManager _connectionManager;

    public GameNotificationHub(
        ILogger<GameNotificationHub> logger,
        ConnectionManager connectionManager)
    {
        _logger = logger;
        _connectionManager = connectionManager;
    }

    public override async Task OnConnectedAsync()
    {
        var connectionId = Context.ConnectionId;
        
        // TODO: 从认证上下文获取 userId
        // 暂时从查询字符串获取（仅用于开发）
        var userIdStr = Context.GetHttpContext()?.Request.Query["userId"].ToString();
        if (Guid.TryParse(userIdStr, out var userId))
        {
            _connectionManager.AddConnection(userId, connectionId);
            _logger.LogInformation(
                "User {UserId} connected with connection {ConnectionId}",
                userId, connectionId);
        }
        else
        {
            _logger.LogWarning(
                "Connection {ConnectionId} without valid userId",
                connectionId);
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var connectionId = Context.ConnectionId;
        _connectionManager.RemoveConnection(connectionId);

        if (exception != null)
        {
            _logger.LogWarning(exception,
                "Connection {ConnectionId} disconnected with error",
                connectionId);
        }
        else
        {
            _logger.LogInformation(
                "Connection {ConnectionId} disconnected",
                connectionId);
        }

        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// 客户端心跳（保活）
    /// </summary>
    public Task Heartbeat()
    {
        var connectionId = Context.ConnectionId;
        _connectionManager.UpdateHeartbeat(connectionId);
        return Task.CompletedTask;
    }

    /// <summary>
    /// 客户端订阅特定战斗
    /// </summary>
    public async Task SubscribeToBattle(Guid battleId)
    {
        var groupName = $"battle_{battleId}";
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        
        _logger.LogDebug(
            "Connection {ConnectionId} subscribed to battle {BattleId}",
            Context.ConnectionId, battleId);
    }

    /// <summary>
    /// 客户端取消订阅战斗
    /// </summary>
    public async Task UnsubscribeFromBattle(Guid battleId)
    {
        var groupName = $"battle_{battleId}";
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
        
        _logger.LogDebug(
            "Connection {ConnectionId} unsubscribed from battle {BattleId}",
            Context.ConnectionId, battleId);
    }
}
```

---

### 8. ConnectionManager（连接管理器）

**位置**: `BlazorIdle.Server/Infrastructure/Messaging/ConnectionManager.cs`

**职责**:
- 跟踪用户和连接的映射关系
- 心跳检测
- 自动清理断开连接

**实现**:

```csharp
using System.Collections.Concurrent;

namespace BlazorIdle.Server.Infrastructure.Messaging;

public sealed class ConnectionManager
{
    private readonly ConcurrentDictionary<string, ConnectionInfo> _connections = new();
    private readonly ConcurrentDictionary<Guid, HashSet<string>> _userConnections = new();
    private readonly ILogger<ConnectionManager> _logger;

    public ConnectionManager(ILogger<ConnectionManager> logger)
    {
        _logger = logger;
        
        // 启动心跳检查任务
        Task.Run(MonitorHeartbeatsAsync);
    }

    public void AddConnection(Guid userId, string connectionId)
    {
        var info = new ConnectionInfo
        {
            UserId = userId,
            ConnectionId = connectionId,
            ConnectedAt = DateTime.UtcNow,
            LastHeartbeat = DateTime.UtcNow
        };

        _connections[connectionId] = info;

        _userConnections.AddOrUpdate(
            userId,
            _ => new HashSet<string> { connectionId },
            (_, set) =>
            {
                lock (set)
                {
                    set.Add(connectionId);
                }
                return set;
            });
    }

    public void RemoveConnection(string connectionId)
    {
        if (_connections.TryRemove(connectionId, out var info))
        {
            if (_userConnections.TryGetValue(info.UserId, out var set))
            {
                lock (set)
                {
                    set.Remove(connectionId);
                    if (set.Count == 0)
                    {
                        _userConnections.TryRemove(info.UserId, out _);
                    }
                }
            }
        }
    }

    public void UpdateHeartbeat(string connectionId)
    {
        if (_connections.TryGetValue(connectionId, out var info))
        {
            info.LastHeartbeat = DateTime.UtcNow;
        }
    }

    public IEnumerable<string> GetUserConnections(Guid userId)
    {
        if (_userConnections.TryGetValue(userId, out var set))
        {
            lock (set)
            {
                return set.ToList();
            }
        }
        return Enumerable.Empty<string>();
    }

    public bool IsUserOnline(Guid userId)
    {
        return _userConnections.ContainsKey(userId);
    }

    public ConnectionStatistics GetStatistics()
    {
        return new ConnectionStatistics(
            _connections.Count,
            _userConnections.Count,
            _connections.Values.Average(c => 
                (DateTime.UtcNow - c.ConnectedAt).TotalSeconds)
        );
    }

    private async Task MonitorHeartbeatsAsync()
    {
        var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        const int timeoutSeconds = 120;

        while (await timer.WaitForNextTickAsync())
        {
            try
            {
                var now = DateTime.UtcNow;
                var stale = _connections
                    .Where(kvp => (now - kvp.Value.LastHeartbeat).TotalSeconds > timeoutSeconds)
                    .Select(kvp => kvp.Key)
                    .ToList();

                foreach (var connectionId in stale)
                {
                    _logger.LogWarning(
                        "Removing stale connection {ConnectionId}", connectionId);
                    RemoveConnection(connectionId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in heartbeat monitor");
            }
        }
    }

    private class ConnectionInfo
    {
        public required Guid UserId { get; init; }
        public required string ConnectionId { get; init; }
        public required DateTime ConnectedAt { get; init; }
        public DateTime LastHeartbeat { get; set; }
    }
}

public record ConnectionStatistics(
    int TotalConnections,
    int UniqueUsers,
    double AverageConnectionDuration
);
```

---

## 实施步骤

### Week 1: 核心基础设施

#### Day 1-2: 接口定义和事件总线

**任务清单**:
- [ ] 创建 `Domain/Events/` 目录
- [ ] 实现 `IDomainEvent` 接口
- [ ] 实现 `INotificationEvent` 接口
- [ ] 创建 `Infrastructure/Messaging/` 目录
- [ ] 实现 `IDomainEventBus` 接口
- [ ] 实现 `InMemoryEventBus` 类
- [ ] 编写事件总线单元测试

**验收标准**:
```
✅ 可以发布和订阅事件
✅ 支持事件过滤
✅ 多订阅者正确接收事件
✅ 异常隔离（一个处理器失败不影响其他）
✅ 单元测试通过
```

---

#### Day 3-4: SignalR 基础

**任务清单**:
- [ ] 安装 SignalR NuGet 包
- [ ] 创建 `GameNotificationHub` 类
- [ ] 实现 `ConnectionManager` 类
- [ ] 在 `Program.cs` 中配置 SignalR
- [ ] 实现基础的连接/断开处理
- [ ] 实现心跳机制

**验收标准**:
```
✅ 客户端可以连接到 Hub
✅ 连接状态正确跟踪
✅ 心跳机制工作正常
✅ 断线后自动清理
✅ 可以通过 Postman/Swagger 测试连接
```

---

#### Day 5: SignalR 分发器

**任务清单**:
- [ ] 实现 `ISignalRDispatcher` 接口
- [ ] 实现 `SignalRDispatcher` 类
- [ ] 实现消息队列和批量发送
- [ ] 添加统计信息收集
- [ ] 编写集成测试

**验收标准**:
```
✅ 可以向指定用户发送消息
✅ 批量发送工作正常
✅ 消息不丢失
✅ 统计信息准确
✅ 集成测试通过
```

---

### Week 2: 集成和测试

#### Day 6-7: 事件总线集成 SignalR

**任务清单**:
- [ ] 创建事件订阅服务
- [ ] 订阅 `INotificationEvent`
- [ ] 将事件转换为 SignalR 消息
- [ ] 实现事件优先级处理
- [ ] 添加监控日志

**示例代码**:

```csharp
// Infrastructure/Messaging/NotificationEventSubscriber.cs
public class NotificationEventSubscriber : IHostedService
{
    private readonly IDomainEventBus _eventBus;
    private readonly ISignalRDispatcher _dispatcher;
    private readonly ILogger<NotificationEventSubscriber> _logger;
    private IDisposable? _subscription;

    public NotificationEventSubscriber(
        IDomainEventBus eventBus,
        ISignalRDispatcher dispatcher,
        ILogger<NotificationEventSubscriber> logger)
    {
        _eventBus = eventBus;
        _dispatcher = dispatcher;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken ct)
    {
        // 订阅所有 INotificationEvent
        _subscription = _eventBus.Subscribe<INotificationEvent>(
            async (@event) =>
            {
                try
                {
                    // 根据优先级决定是否发送
                    if (ShouldSend(@event))
                    {
                        await SendNotificationAsync(@event);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, 
                        "Error sending notification for event {EventId}",
                        @event.EventId);
                }
            });

        _logger.LogInformation("NotificationEventSubscriber started");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken ct)
    {
        _subscription?.Dispose();
        _logger.LogInformation("NotificationEventSubscriber stopped");
        return Task.CompletedTask;
    }

    private bool ShouldSend(INotificationEvent @event)
    {
        // 可以在这里实现流控逻辑
        // 例如：低优先级事件可能会被采样
        return true;
    }

    private async Task SendNotificationAsync(INotificationEvent @event)
    {
        var message = @event.ToClientMessage();
        var method = GetMethodName(@event.EventType);

        if (@event.CharacterId.HasValue)
        {
            // 发送给特定角色的拥有者
            await _dispatcher.SendToUserAsync(
                @event.CharacterId.Value,
                method,
                message);
        }
        else
        {
            // 广播
            await _dispatcher.BroadcastAsync(method, message);
        }

        _logger.LogDebug(
            "Sent notification: {EventType} to {CharacterId}",
            @event.EventType, @event.CharacterId);
    }

    private static string GetMethodName(string eventType)
    {
        // 将事件类型转换为客户端方法名
        // 例如: "BattleEnded" -> "OnBattleEnded"
        return $"On{eventType}";
    }
}
```

**验收标准**:
```
✅ 事件正确路由到 SignalR
✅ 优先级过滤工作正常
✅ 日志记录完整
✅ 无内存泄漏
```

---

#### Day 8-9: 依赖注入配置

**任务清单**:
- [ ] 创建扩展方法注册服务
- [ ] 配置 SignalR 选项
- [ ] 添加 CORS 配置
- [ ] 更新 `Program.cs`

**示例代码**:

```csharp
// Infrastructure/DependencyInjection/MessagingExtensions.cs
public static class MessagingExtensions
{
    public static IServiceCollection AddMessaging(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // 事件总线
        services.AddSingleton<IDomainEventBus, InMemoryEventBus>();

        // SignalR
        services.AddSignalR(options =>
        {
            options.EnableDetailedErrors = true;
            options.MaximumReceiveMessageSize = 128 * 1024; // 128 KB
            options.StreamBufferCapacity = 10;
            options.KeepAliveInterval = TimeSpan.FromSeconds(15);
            options.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
        });

        // SignalR 分发器
        services.AddSingleton<ISignalRDispatcher, SignalRDispatcher>();

        // 连接管理器
        services.AddSingleton<ConnectionManager>();

        // 事件订阅服务
        services.AddHostedService<NotificationEventSubscriber>();

        return services;
    }
}

// Program.cs 更新
var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddInfrastructure(builder.Configuration)
    .AddApplication()
    .AddMessaging(builder.Configuration); // 新增

// CORS 更新（支持 SignalR）
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowBlazorClient", policy =>
    {
        policy
            .WithOrigins("https://localhost:5001", "http://localhost:5000")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials(); // SignalR 需要
    });
});

var app = builder.Build();

// ... 中间件配置 ...

// 映射 SignalR Hub
app.MapHub<GameNotificationHub>("/hubs/notifications");

app.Run();
```

**验收标准**:
```
✅ 所有服务正确注册
✅ SignalR Hub 可访问
✅ CORS 配置正确
✅ 应用正常启动
```

---

#### Day 10: 端到端测试

**任务清单**:
- [ ] 创建测试客户端
- [ ] 测试基础连接
- [ ] 测试心跳机制
- [ ] 测试消息接收
- [ ] 压力测试（多连接）
- [ ] 编写文档

**测试客户端示例** (C# Console App):

```csharp
using Microsoft.AspNetCore.SignalR.Client;

var userId = Guid.NewGuid();
var connection = new HubConnectionBuilder()
    .WithUrl($"https://localhost:7001/hubs/notifications?userId={userId}")
    .WithAutomaticReconnect()
    .Build();

// 订阅消息
connection.On<object>("OnBattleUpdate", (data) =>
{
    Console.WriteLine($"Battle Update: {JsonSerializer.Serialize(data)}");
});

connection.On<object>("OnNotification", (data) =>
{
    Console.WriteLine($"Notification: {JsonSerializer.Serialize(data)}");
});

// 连接
await connection.StartAsync();
Console.WriteLine("Connected!");

// 发送心跳
var timer = new PeriodicTimer(TimeSpan.FromSeconds(10));
while (await timer.WaitForNextTickAsync())
{
    await connection.InvokeAsync("Heartbeat");
    Console.WriteLine("Heartbeat sent");
}
```

**验收标准**:
```
✅ 客户端成功连接
✅ 接收到推送消息
✅ 心跳机制工作
✅ 自动重连正常
✅ 多客户端并发无问题
```

---

## 测试方案

### 单元测试

```csharp
// Tests/Infrastructure/Messaging/InMemoryEventBusTests.cs
public class InMemoryEventBusTests
{
    [Fact]
    public async Task PublishAsync_Should_Deliver_Event_To_Subscribers()
    {
        // Arrange
        var bus = CreateEventBus();
        var received = new List<TestEvent>();
        bus.Subscribe<TestEvent>(e =>
        {
            received.Add(e);
            return Task.CompletedTask;
        });

        // Act
        var testEvent = new TestEvent { Data = "test" };
        await bus.PublishAsync(testEvent);
        await Task.Delay(100); // 等待异步处理

        // Assert
        Assert.Single(received);
        Assert.Equal("test", received[0].Data);
    }

    [Fact]
    public async Task Subscribe_With_Filter_Should_Only_Receive_Matching_Events()
    {
        // Arrange
        var bus = CreateEventBus();
        var received = new List<TestEvent>();
        bus.Subscribe<TestEvent>(
            filter: e => e.Data == "match",
            handler: e =>
            {
                received.Add(e);
                return Task.CompletedTask;
            });

        // Act
        await bus.PublishAsync(new TestEvent { Data = "match" });
        await bus.PublishAsync(new TestEvent { Data = "no-match" });
        await Task.Delay(100);

        // Assert
        Assert.Single(received);
        Assert.Equal("match", received[0].Data);
    }

    private static InMemoryEventBus CreateEventBus()
    {
        var logger = new NullLogger<InMemoryEventBus>();
        return new InMemoryEventBus(logger);
    }

    private record TestEvent : IDomainEvent
    {
        public Guid EventId { get; } = Guid.NewGuid();
        public DateTime OccurredAtUtc { get; } = DateTime.UtcNow;
        public string EventType => "Test";
        public Guid? CharacterId => null;
        public string Data { get; init; } = string.Empty;
    }
}
```

---

### 集成测试

```csharp
// Tests/Infrastructure/Messaging/SignalRIntegrationTests.cs
public class SignalRIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public SignalRIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Client_Should_Receive_Pushed_Message()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var receivedMessage = new TaskCompletionSource<object>();

        var connection = new HubConnectionBuilder()
            .WithUrl($"{_factory.Server.BaseAddress}hubs/notifications?userId={userId}",
                options => options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler())
            .Build();

        connection.On<object>("OnTestMessage", data =>
        {
            receivedMessage.SetResult(data);
        });

        await connection.StartAsync();

        // Act
        var dispatcher = _factory.Services.GetRequiredService<ISignalRDispatcher>();
        await dispatcher.SendToUserAsync(userId, "OnTestMessage", new { text = "hello" });

        // Assert
        var result = await receivedMessage.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.NotNull(result);
    }
}
```

---

## 部署配置

### appsettings.json

```json
{
  "Messaging": {
    "SignalR": {
      "KeepAliveIntervalSeconds": 15,
      "ClientTimeoutSeconds": 30,
      "MaxReceiveMessageSizeKB": 128,
      "EnableDetailedErrors": false
    },
    "EventBus": {
      "QueueCapacity": 10000,
      "BatchSize": 10,
      "BatchDelayMs": 50
    },
    "ConnectionManager": {
      "HeartbeatIntervalSeconds": 30,
      "HeartbeatTimeoutSeconds": 120
    }
  }
}
```

---

## 验收标准

### Phase 1 完成标志

- [ ] 所有单元测试通过
- [ ] 所有集成测试通过
- [ ] 客户端可以连接到 Hub
- [ ] 事件总线正常工作
- [ ] SignalR 分发器正常工作
- [ ] 连接管理器正常工作
- [ ] 心跳机制正常工作
- [ ] 文档完整

### 性能指标

- 消息延迟 < 100ms（99th percentile）
- 支持 1000+ 并发连接
- 内存占用 < 500MB（1000 连接）
- CPU 占用 < 20%（空闲）

---

## 下一步

Phase 1 完成后，继续阅读 [Phase2-战斗事件集成.md](./Phase2-战斗事件集成.md)
