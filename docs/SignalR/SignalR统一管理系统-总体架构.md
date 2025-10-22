# SignalR 统一管理系统 - 总体架构

**文档版本**: 1.0  
**生成日期**: 2025年10月21日  
**状态**: 设计规划  
**目标**: 设计统一的SignalR管理架构，支持BlazorIdle所有功能模块的实时通信需求

---

## 📚 目录

1. [设计目标与原则](#设计目标与原则)
2. [整体架构设计](#整体架构设计)
3. [核心组件详解](#核心组件详解)
4. [消息流程](#消息流程)
5. [连接管理](#连接管理)
6. [扩展机制](#扩展机制)
7. [性能优化](#性能优化)
8. [监控与诊断](#监控与诊断)

---

## 设计目标与原则

### 核心目标

1. **统一管理** 🎯
   - 单一SignalR连接服务所有功能模块
   - 统一的连接生命周期管理
   - 统一的消息路由机制

2. **高性能** ⚡
   - 支持高频推送（战斗系统5-10Hz）
   - 异步非阻塞消息分发
   - 智能批量发送

3. **可扩展** 🔧
   - 易于添加新的消息类型
   - 支持多服务器横向扩展
   - 模块化设计，低耦合

4. **高可用** 💪
   - 自动重连机制
   - 消息补发与快照恢复
   - 优雅降级

5. **易维护** 📝
   - 清晰的架构分层
   - 完善的日志与监控
   - 统一的错误处理

### 设计原则

#### 1. 分层架构原则

```
展示层 (Presentation)
    ↓
应用层 (Application) 
    ↓
领域层 (Domain)
    ↓
基础设施层 (Infrastructure)
```

SignalR系统位于基础设施层，为应用层提供实时通信能力。

#### 2. 单一职责原则

- **Hub**: 仅负责连接管理和消息路由
- **Dispatcher**: 仅负责消息分发和队列管理
- **Broadcaster**: 仅负责特定类型消息的广播
- **Handler**: 仅负责特定消息的业务处理

#### 3. 依赖倒置原则

```csharp
// 领域层定义接口
public interface IRealtimeNotifier
{
    Task NotifyAsync(string userId, INotificationMessage message);
}

// 基础设施层实现
public class SignalRNotifier : IRealtimeNotifier
{
    // 实现细节
}
```

#### 4. 开闭原则

- 对扩展开放：易于添加新的消息类型和处理器
- 对修改关闭：核心框架稳定，不因新功能而修改

---

## 整体架构设计

### 架构全景图

```
┌───────────────────────────────────────────────────────────────────────┐
│                     Blazor WebAssembly Client                         │
│                                                                        │
│  ┌──────────────────────────────────────────────────────────────┐    │
│  │  SignalRConnectionManager (单一连接)                          │    │
│  │  - 自动重连                                                    │    │
│  │  - 心跳检测                                                    │    │
│  │  - 连接状态管理                                                │    │
│  └────────────────────────┬─────────────────────────────────────┘    │
│                           │                                           │
│  ┌────────────────────────▼─────────────────────────────────────┐    │
│  │  MessageRouter (消息路由器)                                  │    │
│  │  - 根据消息类型路由到不同处理器                               │    │
│  └─────┬─────────┬─────────┬─────────┬───────────┬──────────────┘    │
│        │         │         │         │           │                   │
│   ┌────▼───┐ ┌──▼────┐ ┌──▼────┐ ┌──▼──────┐ ┌─▼────────┐          │
│   │Combat  │ │Activity│ │Crafting│ │Party   │ │Economy   │          │
│   │Handler │ │Handler│ │Handler│ │Handler │ │Handler   │          │
│   └────────┘ └───────┘ └───────┘ └─────────┘ └──────────┘          │
│                                                                        │
└────────────────────────────┬───────────────────────────────────────────┘
                             │ SignalR WebSocket
┌────────────────────────────▼───────────────────────────────────────────┐
│                     ASP.NET Core Server                                │
│                                                                        │
│  ┌──────────────────────────────────────────────────────────────┐    │
│  │  GameHub (统一SignalR Hub)                                   │    │
│  │  - 连接管理 (OnConnected/OnDisconnected)                     │    │
│  │  - Group管理 (战斗、队伍等)                                   │    │
│  │  - 消息路由 (发送到特定用户/组)                               │    │
│  └────────────────────────┬─────────────────────────────────────┘    │
│                           │                                           │
│  ┌────────────────────────▼─────────────────────────────────────┐    │
│  │  SignalRDispatcher (消息分发中心)                            │    │
│  │  - 消息队列管理                                               │    │
│  │  - 批量发送                                                   │    │
│  │  - 优先级调度                                                 │    │
│  │  - 背压控制                                                   │    │
│  └─────┬─────────┬─────────┬─────────┬───────────┬──────────────┘    │
│        │         │         │         │           │                   │
│   ┌────▼───────┐ ┌────▼───────┐ ┌───▼──────┐ ┌─▼─────────┐         │
│   │Combat      │ │Activity    │ │Party     │ │General    │         │
│   │Broadcaster │ │Broadcaster │ │Broadcaster│ │Broadcaster│         │
│   └────┬───────┘ └────┬───────┘ └───┬──────┘ └─┬─────────┘         │
│        │              │              │           │                   │
│  ┌─────▼──────────────▼──────────────▼───────────▼─────────┐        │
│  │         Domain Event Bus (领域事件总线)                  │        │
│  │  - 事件发布/订阅                                          │        │
│  │  - 异步处理                                               │        │
│  │  - 事件过滤                                               │        │
│  └─────┬──────────┬──────────┬──────────┬──────────┬────────┘        │
│        │          │          │          │          │                 │
│   ┌────▼────┐ ┌──▼─────┐ ┌──▼─────┐ ┌──▼────┐ ┌──▼────┐            │
│   │Combat   │ │Activity│ │Crafting│ │Party  │ │Economy│            │
│   │System   │ │System  │ │System  │ │System │ │System │            │
│   └─────────┘ └────────┘ └────────┘ └───────┘ └───────┘            │
│                                                                        │
└────────────────────────────────────────────────────────────────────────┘
```

### 关键特点

1. **单一Hub设计** 🎯
   - 所有消息通过 `GameHub` 统一处理
   - 避免多个Hub带来的连接管理复杂度
   - 简化客户端连接逻辑

2. **消息类型路由** 🚦
   - 基于消息类型自动路由到相应处理器
   - 支持动态注册新的消息类型
   - 类型安全的消息处理

3. **模块化Broadcaster** 📡
   - 每个功能模块有独立的Broadcaster
   - Broadcaster专注于特定类型消息的生成和广播
   - 可独立开发、测试和优化

4. **事件驱动架构** ⚡
   - 通过领域事件总线解耦
   - Broadcaster订阅相关领域事件
   - 异步处理，不阻塞业务逻辑

---

## 核心组件详解

### 1. GameHub (统一Hub)

**职责**: SignalR连接管理和消息路由的统一入口

**接口定义**:

```csharp
// Infrastructure/SignalR/Hubs/GameHub.cs
public class GameHub : Hub
{
    private readonly IConnectionManager _connectionManager;
    private readonly IMessageRouter _messageRouter;
    private readonly ILogger<GameHub> _logger;

    // 连接建立
    public override async Task OnConnectedAsync()
    {
        var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            await Clients.Caller.SendAsync("Error", "Unauthorized");
            Context.Abort();
            return;
        }

        await _connectionManager.RegisterConnectionAsync(userId, Context.ConnectionId);
        await Clients.Caller.SendAsync("Connected", new { userId, connectionId = Context.ConnectionId });
        
        _logger.LogInformation("User {UserId} connected with {ConnectionId}", userId, Context.ConnectionId);
    }

    // 连接断开
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!string.IsNullOrEmpty(userId))
        {
            await _connectionManager.UnregisterConnectionAsync(userId, Context.ConnectionId);
            _logger.LogInformation("User {UserId} disconnected", userId);
        }

        await base.OnDisconnectedAsync(exception);
    }

    // 订阅战斗
    public async Task SubscribeToBattle(string battleId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"battle:{battleId}");
        await Clients.Caller.SendAsync("Subscribed", "battle", battleId);
    }

    // 取消订阅战斗
    public async Task UnsubscribeFromBattle(string battleId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"battle:{battleId}");
        await Clients.Caller.SendAsync("Unsubscribed", "battle", battleId);
    }

    // 订阅队伍
    public async Task SubscribeToParty(string partyId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"party:{partyId}");
        await Clients.Caller.SendAsync("Subscribed", "party", partyId);
    }

    // 请求战斗状态同步
    public async Task RequestBattleSync(string battleId, long lastVersion)
    {
        await _messageRouter.RouteRequestAsync("BattleSync", new { battleId, lastVersion }, Context.ConnectionId);
    }

    // 心跳
    public Task Heartbeat()
    {
        return Task.CompletedTask;
    }
}
```

**关键方法**:

| 方法 | 说明 |
|------|------|
| `OnConnectedAsync` | 用户连接时注册会话 |
| `OnDisconnectedAsync` | 用户断开时清理会话 |
| `SubscribeToBattle` | 加入战斗Group（接收战斗推送） |
| `SubscribeToParty` | 加入队伍Group（接收队伍推送） |
| `RequestBattleSync` | 请求补发战斗消息（断线重连） |
| `Heartbeat` | 保持连接活跃 |

---

### 2. IConnectionManager (连接管理器)

**职责**: 管理用户连接状态和订阅关系

**接口定义**:

```csharp
// Infrastructure/SignalR/IConnectionManager.cs
public interface IConnectionManager
{
    Task RegisterConnectionAsync(string userId, string connectionId);
    Task UnregisterConnectionAsync(string userId, string connectionId);
    Task<string?> GetConnectionIdAsync(string userId);
    Task<IEnumerable<string>> GetConnectionIdsAsync(string userId);
    Task<bool> IsConnectedAsync(string userId);
    Task<UserSession?> GetSessionAsync(string userId);
}

public class UserSession
{
    public string UserId { get; set; } = string.Empty;
    public List<string> ConnectionIds { get; set; } = new();
    public Dictionary<string, object> Subscriptions { get; set; } = new();
    public DateTime LastHeartbeat { get; set; }
    public DateTime ConnectedAt { get; set; }
}
```

**实现要点**:

```csharp
// Infrastructure/SignalR/ConnectionManager.cs
public class ConnectionManager : IConnectionManager
{
    private readonly ConcurrentDictionary<string, UserSession> _sessions = new();
    private readonly IMemoryCache _cache;

    public async Task RegisterConnectionAsync(string userId, string connectionId)
    {
        var session = _sessions.GetOrAdd(userId, _ => new UserSession
        {
            UserId = userId,
            ConnectedAt = DateTime.UtcNow
        });

        lock (session.ConnectionIds)
        {
            if (!session.ConnectionIds.Contains(connectionId))
            {
                session.ConnectionIds.Add(connectionId);
            }
        }

        session.LastHeartbeat = DateTime.UtcNow;
    }

    public async Task UnregisterConnectionAsync(string userId, string connectionId)
    {
        if (_sessions.TryGetValue(userId, out var session))
        {
            lock (session.ConnectionIds)
            {
                session.ConnectionIds.Remove(connectionId);
                
                // 如果没有活跃连接了，移除会话
                if (session.ConnectionIds.Count == 0)
                {
                    _sessions.TryRemove(userId, out _);
                }
            }
        }
    }

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

    public Task<bool> IsConnectedAsync(string userId)
    {
        return Task.FromResult(_sessions.ContainsKey(userId));
    }
}
```

---

### 3. SignalRDispatcher (消息分发中心)

**职责**: 管理消息队列，批量发送，优先级调度

**接口定义**:

```csharp
// Infrastructure/SignalR/ISignalRDispatcher.cs
public interface ISignalRDispatcher
{
    Task SendToUserAsync(string userId, string method, object message, MessagePriority priority = MessagePriority.Normal);
    Task SendToGroupAsync(string groupName, string method, object message, MessagePriority priority = MessagePriority.Normal);
    Task SendToAllAsync(string method, object message, MessagePriority priority = MessagePriority.Normal);
}

public enum MessagePriority
{
    Low = 0,
    Normal = 1,
    High = 2,
    Critical = 3
}
```

**实现要点**:

```csharp
// Infrastructure/SignalR/SignalRDispatcher.cs
public class SignalRDispatcher : ISignalRDispatcher
{
    private readonly IHubContext<GameHub> _hubContext;
    private readonly IConnectionManager _connectionManager;
    private readonly Channel<PendingMessage> _messageChannel;
    private readonly ILogger<SignalRDispatcher> _logger;
    private readonly CancellationTokenSource _cts = new();

    public SignalRDispatcher(
        IHubContext<GameHub> hubContext,
        IConnectionManager connectionManager,
        ILogger<SignalRDispatcher> logger)
    {
        _hubContext = hubContext;
        _connectionManager = connectionManager;
        _logger = logger;
        
        // 创建有界通道（背压控制）
        _messageChannel = Channel.CreateBounded<PendingMessage>(new BoundedChannelOptions(10000)
        {
            FullMode = BoundedChannelFullMode.Wait
        });

        // 启动后台消费者
        _ = Task.Run(() => ProcessMessagesAsync(_cts.Token));
    }

    public async Task SendToUserAsync(string userId, string method, object message, MessagePriority priority = MessagePriority.Normal)
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

    private async Task ProcessMessagesAsync(CancellationToken cancellationToken)
    {
        // 批量处理消息
        var batch = new List<PendingMessage>();
        
        await foreach (var message in _messageChannel.Reader.ReadAllAsync(cancellationToken))
        {
            batch.Add(message);

            // 达到批量阈值或时间窗口
            if (batch.Count >= 100 || (batch.Count > 0 && (DateTime.UtcNow - batch[0].EnqueuedAt).TotalMilliseconds > 50))
            {
                await SendBatchAsync(batch);
                batch.Clear();
            }
        }
    }

    private async Task SendBatchAsync(List<PendingMessage> messages)
    {
        // 按优先级排序
        messages.Sort((a, b) => b.Priority.CompareTo(a.Priority));

        foreach (var msg in messages)
        {
            try
            {
                switch (msg.Type)
                {
                    case MessageType.User:
                        var connectionId = await _connectionManager.GetConnectionIdAsync(msg.Target);
                        if (connectionId != null)
                        {
                            await _hubContext.Clients.Client(connectionId).SendAsync(msg.Method, msg.Message);
                        }
                        break;

                    case MessageType.Group:
                        await _hubContext.Clients.Group(msg.Target).SendAsync(msg.Method, msg.Message);
                        break;

                    case MessageType.All:
                        await _hubContext.Clients.All.SendAsync(msg.Method, msg.Message);
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send message: {Method} to {Target}", msg.Method, msg.Target);
            }
        }
    }
}

internal class PendingMessage
{
    public MessageType Type { get; set; }
    public string Target { get; set; } = string.Empty;
    public string Method { get; set; } = string.Empty;
    public object Message { get; set; } = null!;
    public MessagePriority Priority { get; set; }
    public DateTime EnqueuedAt { get; set; }
}

internal enum MessageType
{
    User,
    Group,
    All
}
```

**核心特性**:

- **异步队列**: 使用 Channel 实现高性能消息队列
- **批量发送**: 减少SignalR调用次数，提升性能
- **优先级调度**: 关键消息优先发送
- **背压控制**: 有界通道防止消息堆积
- **错误隔离**: 单条消息失败不影响其他消息

---

### 4. Broadcaster (专用广播器)

每个功能模块有自己的Broadcaster，负责生成和广播特定类型的消息。

#### 4.1 CombatBroadcaster (战斗广播器)

```csharp
// Infrastructure/SignalR/Broadcasters/CombatBroadcaster.cs
public class CombatBroadcaster : ICombatBroadcaster
{
    private readonly ISignalRDispatcher _dispatcher;
    private readonly ILogger<CombatBroadcaster> _logger;
    private readonly ConcurrentDictionary<string, BattleFrameBuffer> _frameBuffers = new();

    public async Task BroadcastFrameTickAsync(string battleId, FrameTick frame)
    {
        await _dispatcher.SendToGroupAsync(
            $"battle:{battleId}",
            "FrameTick",
            frame,
            MessagePriority.High
        );
    }

    public async Task BroadcastKeyEventAsync(string battleId, KeyEvent keyEvent)
    {
        await _dispatcher.SendToGroupAsync(
            $"battle:{battleId}",
            "KeyEvent",
            keyEvent,
            MessagePriority.Critical
        );
    }

    public async Task BroadcastSnapshotAsync(string battleId, BattleSnapshot snapshot)
    {
        await _dispatcher.SendToGroupAsync(
            $"battle:{battleId}",
            "BattleSnapshot",
            snapshot,
            MessagePriority.Normal
        );
    }
}
```

#### 4.2 ActivityBroadcaster (活动广播器)

```csharp
// Infrastructure/SignalR/Broadcasters/ActivityBroadcaster.cs
public class ActivityBroadcaster : IActivityBroadcaster
{
    private readonly ISignalRDispatcher _dispatcher;

    public async Task NotifyActivityCompletedAsync(string userId, ActivityCompletedMessage message)
    {
        await _dispatcher.SendToUserAsync(
            userId,
            "ActivityCompleted",
            message,
            MessagePriority.Normal
        );
    }

    public async Task NotifyActivityStartedAsync(string userId, ActivityStartedMessage message)
    {
        await _dispatcher.SendToUserAsync(
            userId,
            "ActivityStarted",
            message,
            MessagePriority.Normal
        );
    }
}
```

#### 4.3 PartyBroadcaster (队伍广播器)

```csharp
// Infrastructure/SignalR/Broadcasters/PartyBroadcaster.cs
public class PartyBroadcaster : IPartyBroadcaster
{
    private readonly ISignalRDispatcher _dispatcher;

    public async Task BroadcastPartyMemberJoinedAsync(string partyId, PartyMemberJoinedMessage message)
    {
        await _dispatcher.SendToGroupAsync(
            $"party:{partyId}",
            "PartyMemberJoined",
            message,
            MessagePriority.High
        );
    }

    public async Task BroadcastPartyFrameTickAsync(string partyId, PartyFrameTick frame)
    {
        await _dispatcher.SendToGroupAsync(
            $"party:{partyId}",
            "PartyFrameTick",
            frame,
            MessagePriority.High
        );
    }
}
```

---

### 5. DomainEventBus (领域事件总线)

**职责**: 解耦领域逻辑和SignalR推送

**接口定义**:

```csharp
// Domain/Events/IDomainEventBus.cs
public interface IDomainEventBus
{
    Task PublishAsync<TEvent>(TEvent @event) where TEvent : IDomainEvent;
    IDisposable Subscribe<TEvent>(Func<TEvent, Task> handler) where TEvent : IDomainEvent;
}

public interface IDomainEvent
{
    Guid EventId { get; }
    DateTime OccurredAt { get; }
    string EventType { get; }
}
```

**使用示例**:

```csharp
// Domain/Combat/BattleRunner.cs
public class BattleRunner
{
    private readonly IDomainEventBus _eventBus;

    public async Task ExecuteBattleTickAsync()
    {
        // ... 战斗逻辑 ...

        // 发布领域事件
        await _eventBus.PublishAsync(new BattleFrameGeneratedEvent
        {
            BattleId = _battleId,
            Frame = GenerateFrame()
        });
    }
}

// Infrastructure/SignalR/Broadcasters/CombatBroadcaster.cs
public class CombatBroadcaster : ICombatBroadcaster
{
    public CombatBroadcaster(IDomainEventBus eventBus, ISignalRDispatcher dispatcher)
    {
        // 订阅领域事件
        eventBus.Subscribe<BattleFrameGeneratedEvent>(OnBattleFrameGeneratedAsync);
    }

    private async Task OnBattleFrameGeneratedAsync(BattleFrameGeneratedEvent @event)
    {
        // 广播到SignalR
        await BroadcastFrameTickAsync(@event.BattleId, @event.Frame);
    }
}
```

**优势**:

- ✅ 领域层不依赖SignalR
- ✅ 易于测试
- ✅ 支持多个订阅者
- ✅ 异步处理不阻塞业务逻辑

---

## 消息流程

### 完整流程图

```
┌─────────────────────────────────────────────────────────────────────┐
│ 1. 业务逻辑执行                                                      │
│                                                                      │
│    BattleRunner.ExecuteTick()                                       │
│         │                                                            │
│         ├─ 更新战斗状态                                               │
│         ├─ 计算伤害                                                   │
│         └─ 生成 FrameTick                                            │
│                                                                      │
└──────────────────────┬──────────────────────────────────────────────┘
                       │
                       ▼
┌─────────────────────────────────────────────────────────────────────┐
│ 2. 发布领域事件                                                      │
│                                                                      │
│    eventBus.PublishAsync(new BattleFrameGeneratedEvent(...))       │
│                                                                      │
└──────────────────────┬──────────────────────────────────────────────┘
                       │
                       ▼
┌─────────────────────────────────────────────────────────────────────┐
│ 3. Broadcaster 订阅处理                                              │
│                                                                      │
│    CombatBroadcaster.OnBattleFrameGenerated()                       │
│         │                                                            │
│         ├─ 缓存帧到 FrameBuffer（用于补发）                           │
│         └─ 调用 Dispatcher                                           │
│                                                                      │
└──────────────────────┬──────────────────────────────────────────────┘
                       │
                       ▼
┌─────────────────────────────────────────────────────────────────────┐
│ 4. 消息分发                                                          │
│                                                                      │
│    dispatcher.SendToGroupAsync("battle:123", "FrameTick", frame)   │
│         │                                                            │
│         ├─ 加入消息队列                                               │
│         ├─ 优先级排序                                                 │
│         └─ 批量发送                                                   │
│                                                                      │
└──────────────────────┬──────────────────────────────────────────────┘
                       │
                       ▼
┌─────────────────────────────────────────────────────────────────────┐
│ 5. SignalR Hub 推送                                                  │
│                                                                      │
│    GameHub.Clients.Group("battle:123").SendAsync("FrameTick", ...) │
│                                                                      │
└──────────────────────┬──────────────────────────────────────────────┘
                       │
                       ▼ (WebSocket)
┌─────────────────────────────────────────────────────────────────────┐
│ 6. 客户端接收                                                        │
│                                                                      │
│    connection.On<FrameTick>("FrameTick", frame => {...})           │
│         │                                                            │
│         ├─ 版本检查                                                   │
│         ├─ 应用状态更新                                               │
│         └─ 触发UI渲染                                                 │
│                                                                      │
└─────────────────────────────────────────────────────────────────────┘
```

### 关键流程说明

#### 流程1: 正常消息推送

```
业务逻辑 → 领域事件 → Broadcaster → Dispatcher → Hub → 客户端
```

- **异步**: 每一步都是异步的，不阻塞业务逻辑
- **解耦**: 通过事件总线和接口解耦
- **可靠**: 有错误处理和重试机制

#### 流程2: 断线重连同步

```
客户端重连 → Hub.OnConnectedAsync → 客户端请求同步 → 
Hub.RequestBattleSync → Broadcaster查询FrameBuffer → 
补发缺失消息或发送快照 → 客户端恢复状态
```

#### 流程3: Group订阅

```
客户端加入战斗 → Hub.SubscribeToBattle → 
Groups.AddToGroupAsync → 开始接收该战斗的广播消息
```

---

## 连接管理

### 连接生命周期

```
┌──────────────┐
│ Disconnected │
└──────┬───────┘
       │ OnConnectedAsync
       ▼
┌──────────────┐
│  Connected   │ ◄─────┐
└──────┬───────┘       │ Heartbeat
       │ Subscribe     │
       ▼               │
┌──────────────┐       │
│  Subscribed  │───────┘
└──────┬───────┘
       │ OnDisconnectedAsync
       ▼
┌──────────────┐
│ Disconnected │
└──────────────┘
```

### 重连策略

```csharp
// Client Side
public class ReconnectionPolicy
{
    private static readonly int[] RetryDelays = { 0, 2, 5, 10, 20, 30 };

    public async Task<bool> TryReconnectAsync(HubConnection connection)
    {
        for (int i = 0; i < RetryDelays.Length; i++)
        {
            if (i > 0)
            {
                await Task.Delay(TimeSpan.FromSeconds(RetryDelays[i]));
            }

            try
            {
                await connection.StartAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Reconnection attempt {Attempt} failed", i + 1);
            }
        }

        return false;
    }
}
```

### 心跳检测

```csharp
// Client Side
public class HeartbeatService
{
    private readonly IHubConnection _connection;
    private readonly PeriodicTimer _timer = new(TimeSpan.FromSeconds(30));

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        while (await _timer.WaitForNextTickAsync(cancellationToken))
        {
            try
            {
                await _connection.InvokeAsync("Heartbeat", cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Heartbeat failed");
                // 触发重连
                await TryReconnectAsync();
            }
        }
    }
}
```

---

## 扩展机制

### 1. 添加新的消息类型

#### 步骤1: 定义消息类型

```csharp
// Shared/Messages/CraftingMessages.cs
public class CraftingCompletedMessage
{
    public string UserId { get; set; } = string.Empty;
    public string RecipeId { get; set; } = string.Empty;
    public List<ItemStack> ProducedItems { get; set; } = new();
    public long Timestamp { get; set; }
}
```

#### 步骤2: 创建Broadcaster

```csharp
// Infrastructure/SignalR/Broadcasters/CraftingBroadcaster.cs
public class CraftingBroadcaster : ICraftingBroadcaster
{
    private readonly ISignalRDispatcher _dispatcher;
    private readonly IDomainEventBus _eventBus;

    public CraftingBroadcaster(ISignalRDispatcher dispatcher, IDomainEventBus eventBus)
    {
        _dispatcher = dispatcher;
        _eventBus = eventBus;

        // 订阅领域事件
        _eventBus.Subscribe<CraftingCompletedEvent>(OnCraftingCompletedAsync);
    }

    private async Task OnCraftingCompletedAsync(CraftingCompletedEvent @event)
    {
        var message = new CraftingCompletedMessage
        {
            UserId = @event.UserId,
            RecipeId = @event.RecipeId,
            ProducedItems = @event.ProducedItems,
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };

        await _dispatcher.SendToUserAsync(@event.UserId, "CraftingCompleted", message);
    }
}
```

#### 步骤3: 注册服务

```csharp
// Program.cs
builder.Services.AddSingleton<ICraftingBroadcaster, CraftingBroadcaster>();
```

#### 步骤4: 客户端处理

```typescript
// Client
connection.on("CraftingCompleted", (message: CraftingCompletedMessage) => {
    console.log(`Crafting completed: ${message.recipeId}`);
    // 更新UI
});
```

### 2. 横向扩展（多服务器）

使用 Redis Backplane 支持多服务器部署：

```csharp
// Program.cs
builder.Services.AddSignalR()
    .AddStackExchangeRedis("localhost:6379", options =>
    {
        options.Configuration.ChannelPrefix = "BlazorIdle";
    });
```

**工作原理**:

```
Server 1                Server 2                Server 3
   │                       │                       │
   ├─ User A              ├─ User B              ├─ User C
   │                       │                       │
   └─────────┬─────────────┴─────────────┬─────────┘
             │                           │
             ▼                           ▼
        ┌────────────────────────────────────┐
        │       Redis Backplane              │
        │  (消息分发到所有服务器)              │
        └────────────────────────────────────┘
```

所有服务器的消息都会通过Redis同步，保证一致性。

---

## 性能优化

### 1. 批量发送

```csharp
// 批量处理减少SignalR调用
private async Task SendBatchAsync(List<PendingMessage> messages)
{
    var batches = messages.GroupBy(m => (m.Type, m.Target));
    
    foreach (var batch in batches)
    {
        var messagesToSend = batch.Select(m => new { m.Method, m.Message }).ToArray();
        
        if (batch.Key.Type == MessageType.Group)
        {
            await _hubContext.Clients.Group(batch.Key.Target)
                .SendAsync("BatchMessages", messagesToSend);
        }
    }
}
```

### 2. 消息压缩

```csharp
// 对大消息启用压缩
builder.Services.AddSignalR()
    .AddMessagePackProtocol(options =>
    {
        options.SerializerOptions = MessagePackSerializerOptions.Standard
            .WithCompression(MessagePackCompression.Lz4Block);
    });
```

### 3. 连接池管理

```csharp
// 空闲连接自动断开
public class IdleConnectionCleaner : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            
            var idleSessions = _connectionManager.GetIdleSessions(TimeSpan.FromMinutes(30));
            foreach (var session in idleSessions)
            {
                // 通知客户端即将断开
                await _hubContext.Clients.User(session.UserId)
                    .SendAsync("IdleWarning", "Connection will be closed due to inactivity");
            }
        }
    }
}
```

### 4. 自适应频率

```csharp
// 根据连接数动态调整推送频率
public class AdaptiveFrequencyController
{
    public int GetOptimalFrequency(int connectionCount)
    {
        return connectionCount switch
        {
            < 100 => 10,    // 10Hz
            < 500 => 8,     // 8Hz
            < 1000 => 5,    // 5Hz
            _ => 2          // 2Hz
        };
    }
}
```

---

## 监控与诊断

### 1. 关键指标

```csharp
// Infrastructure/SignalR/Metrics/SignalRMetrics.cs
public class SignalRMetrics
{
    public int ActiveConnections { get; set; }
    public int TotalMessagesSent { get; set; }
    public int MessageQueueDepth { get; set; }
    public double AverageLatency { get; set; }
    public int FailedMessages { get; set; }
    public Dictionary<string, int> MessageTypeDistribution { get; set; } = new();
}
```

### 2. 实时监控面板

```csharp
// 定期发送监控数据到管理后台
public class MetricsReporter : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var metrics = _metricsCollector.GetMetrics();
            
            await _hubContext.Clients.Group("admin")
                .SendAsync("Metrics", metrics);
            
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }
}
```

### 3. 日志记录

```csharp
// 结构化日志
_logger.LogInformation(
    "Message sent: {Method} to {Target} ({Type}) in {Duration}ms",
    method, target, type, duration);
```

---

## 总结

### 核心价值

1. **统一管理** 🎯
   - 单一Hub，单一连接
   - 统一的消息路由和分发
   - 降低系统复杂度

2. **高性能** ⚡
   - 异步非阻塞
   - 批量发送
   - 自适应频率

3. **可扩展** 🔧
   - 易于添加新功能
   - 支持横向扩展
   - 模块化设计

4. **可维护** 📝
   - 清晰的架构
   - 完善的监控
   - 丰富的日志

### 下一步

1. ✅ 阅读 [SignalR需求分析与边界定义.md](./SignalR需求分析与边界定义.md)
2. ✅ 阅读 [SignalR实施计划-分步指南.md](./SignalR实施计划-分步指南.md)
3. ✅ 阅读 [API与SignalR选择指南.md](./API与SignalR选择指南.md)

---

**文档状态**: ✅ 完成  
**最后更新**: 2025年10月21日  
**作者**: GitHub Copilot
