# Phase 3: 扩展性设计（上篇）

**阶段目标**: 设计通用事件推送框架，为未来非战斗事件集成做准备  
**实施时间**: 1-2周  
**前置条件**: Phase 1 和 Phase 2 完成  
**后续阶段**: 实际业务功能实现

---

## 📋 目录

1. [扩展性目标](#扩展性目标)
2. [通用事件推送框架](#通用事件推送框架)
3. [活动计划事件](#活动计划事件)
4. [装备与物品事件](#装备与物品事件)
5. [经济系统事件](#经济系统事件)
6. [社交系统预留](#社交系统预留)
7. [监控与诊断](#监控与诊断)
8. [实施指南](#实施指南)

---

## 扩展性目标

### 设计原则

```
✅ 统一事件模型：所有事件继承 IDomainEvent
✅ 松耦合架构：通过事件总线解耦
✅ 自动推送：INotificationEvent 自动转发到 SignalR
✅ 易于扩展：新增事件类型无需修改基础设施
✅ 性能优先：支持事件过滤和批量推送
```

---

### 扩展点清单

| 扩展点 | 说明 | 实现方式 |
|--------|------|----------|
| 新事件类型 | 定义新的事件类 | 实现 `IDomainEvent` 或 `INotificationEvent` |
| 事件处理器 | 订阅并处理事件 | 通过 `IDomainEventBus.Subscribe<T>()` |
| SignalR 方法 | 自定义客户端方法名 | 在 `ToClientMessage()` 中指定 |
| 事件过滤 | 基于条件过滤事件 | 使用 `Subscribe` 的 filter 参数 |
| 事件转换 | 自定义事件到消息的转换 | 重写 `ToClientMessage()` |
| 批量推送 | 聚合多个事件 | 实现自定义 `BatchingDispatcher` |

---

## 通用事件推送框架

### 事件分类体系

```
IDomainEvent (基础接口)
│
├─ INotificationEvent (可推送事件)
│   │
│   ├─ Combat Events (战斗事件 - Phase 2)
│   │   ├─ BattleStartedEvent
│   │   ├─ CombatSegmentFlushedEvent
│   │   └─ BattleEndedEvent
│   │
│   ├─ Activity Events (活动事件 - Phase 3)
│   │   ├─ ActivityStartedEvent
│   │   ├─ ActivityProgressEvent
│   │   └─ ActivityCompletedEvent
│   │
│   ├─ Equipment Events (装备事件 - Phase 3)
│   │   ├─ EquipmentAcquiredEvent
│   │   ├─ EquipmentUpgradedEvent
│   │   └─ EquipmentDestroyedEvent
│   │
│   ├─ Economy Events (经济事件 - Phase 3)
│   │   ├─ GoldChangedEvent
│   │   ├─ ExperienceGainedEvent
│   │   └─ LevelUpEvent
│   │
│   └─ Social Events (社交事件 - 未来)
│       ├─ FriendRequestEvent
│       ├─ PartyInviteEvent
│       └─ ChatMessageEvent
│
└─ Internal Events (内部事件，不推送)
    ├─ DataPersistenceEvent
    ├─ CacheInvalidationEvent
    └─ ConfigReloadEvent
```

---

### 事件路由策略

**1. 基于用户路由**

```csharp
// 单用户事件（最常见）
public class ActivityCompletedEvent : INotificationEvent
{
    public Guid? CharacterId { get; init; } // 指定目标用户
    
    // 自动路由到该用户的所有连接
}
```

**2. 基于组路由**

```csharp
// 组播事件（如组队、公会）
public class PartyEvent : INotificationEvent
{
    public Guid? CharacterId => null; // 不指定单个用户
    public Guid PartyId { get; init; }   // 指定组
    
    // 需要自定义路由逻辑
}
```

**3. 广播事件**

```csharp
// 全局事件（如服务器公告）
public class ServerAnnouncementEvent : INotificationEvent
{
    public Guid? CharacterId => null; // null = 广播
    
    // 推送给所有在线用户
}
```

---

### 事件优先级策略

```csharp
// Infrastructure/Messaging/PriorityBasedSubscriber.cs
public class PriorityBasedSubscriber : IHostedService
{
    private readonly IDomainEventBus _eventBus;
    private readonly ISignalRDispatcher _dispatcher;
    private readonly Channel<PrioritizedEvent> _eventQueue;

    public PriorityBasedSubscriber(
        IDomainEventBus eventBus,
        ISignalRDispatcher dispatcher)
    {
        _eventBus = eventBus;
        _dispatcher = dispatcher;
        
        // 按优先级排序的队列
        _eventQueue = Channel.CreateUnbounded<PrioritizedEvent>(
            new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false
            });
    }

    public Task StartAsync(CancellationToken ct)
    {
        // 订阅所有 INotificationEvent
        _eventBus.Subscribe<INotificationEvent>(async @event =>
        {
            await _eventQueue.Writer.WriteAsync(new PrioritizedEvent
            {
                Event = @event,
                Priority = @event.Priority,
                EnqueuedAt = DateTime.UtcNow
            }, ct);
        });

        // 启动处理任务
        _ = ProcessEventsAsync(ct);

        return Task.CompletedTask;
    }

    private async Task ProcessEventsAsync(CancellationToken ct)
    {
        // 使用优先级队列（简化版，实际可用 PriorityQueue<T>）
        var pending = new List<PrioritizedEvent>();

        while (!ct.IsCancellationRequested)
        {
            // 收集一批事件
            var timer = Task.Delay(50, ct); // 50ms 批处理窗口
            while (!timer.IsCompleted && 
                   _eventQueue.Reader.TryRead(out var item))
            {
                pending.Add(item);
            }

            if (pending.Count > 0)
            {
                // 按优先级排序
                pending.Sort((a, b) => 
                    b.Priority.CompareTo(a.Priority));

                // 发送
                foreach (var item in pending)
                {
                    await SendEventAsync(item.Event);
                }

                pending.Clear();
            }

            await timer;
        }
    }

    private async Task SendEventAsync(INotificationEvent @event)
    {
        var method = GetMethodName(@event.EventType);
        var message = @event.ToClientMessage();

        if (@event.CharacterId.HasValue)
        {
            await _dispatcher.SendToUserAsync(
                @event.CharacterId.Value,
                method,
                message);
        }
        else
        {
            // 广播或自定义路由
            await _dispatcher.BroadcastAsync(method, message);
        }
    }

    private static string GetMethodName(string eventType)
    {
        return $"On{eventType}";
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;

    private record PrioritizedEvent
    {
        public required INotificationEvent Event { get; init; }
        public required NotificationPriority Priority { get; init; }
        public required DateTime EnqueuedAt { get; init; }
    }
}
```

---

## 活动计划事件

### 事件定义

**1. ActivityStartedEvent（活动开始）**

```csharp
// Domain/Events/Activity/ActivityStartedEvent.cs
namespace BlazorIdle.Server.Domain.Events.Activity;

using BlazorIdle.Server.Domain.Events;

public sealed record ActivityStartedEvent : INotificationEvent
{
    public required Guid EventId { get; init; }
    public required DateTime OccurredAtUtc { get; init; }
    public string EventType => "ActivityStarted";
    public required Guid? CharacterId { get; init; }

    // 活动信息
    public required Guid ActivityId { get; init; }
    public required string ActivityType { get; init; } // "Combat" / "Gather" / "Craft"
    public required int SlotIndex { get; init; }
    public required ActivityLimit Limit { get; init; }
    public required object? Payload { get; init; } // 活动特定数据

    public NotificationPriority Priority => NotificationPriority.Normal;

    public object ToClientMessage()
    {
        return new
        {
            eventId = EventId,
            characterId = CharacterId,
            activity = new
            {
                id = ActivityId,
                type = ActivityType,
                slotIndex = SlotIndex,
                limit = new
                {
                    type = Limit.Type,
                    target = Limit.TargetValue,
                    remaining = Limit.Remaining
                },
                payload = Payload
            },
            startedAt = OccurredAtUtc
        };
    }
}

public sealed record ActivityLimit
{
    public required string Type { get; init; } // "Count" / "Duration" / "Infinite"
    public required double TargetValue { get; init; }
    public required double Remaining { get; init; }
}
```

**2. ActivityProgressEvent（活动进度更新）**

```csharp
// Domain/Events/Activity/ActivityProgressEvent.cs
public sealed record ActivityProgressEvent : INotificationEvent
{
    public required Guid EventId { get; init; }
    public required DateTime OccurredAtUtc { get; init; }
    public string EventType => "ActivityProgress";
    public required Guid? CharacterId { get; init; }

    public required Guid ActivityId { get; init; }
    public required double Progress { get; init; } // 0.0 - 1.0
    public required double Remaining { get; init; }
    public required object? IntermediateResults { get; init; }

    public NotificationPriority Priority => NotificationPriority.Low;

    public object ToClientMessage()
    {
        return new
        {
            eventId = EventId,
            activityId = ActivityId,
            progress = Progress,
            remaining = Remaining,
            results = IntermediateResults
        };
    }
}
```

**3. ActivityCompletedEvent（活动完成）**

```csharp
// Domain/Events/Activity/ActivityCompletedEvent.cs
public sealed record ActivityCompletedEvent : INotificationEvent
{
    public required Guid EventId { get; init; }
    public required DateTime OccurredAtUtc { get; init; }
    public string EventType => "ActivityCompleted";
    public required Guid? CharacterId { get; init; }

    public required Guid ActivityId { get; init; }
    public required string ActivityType { get; init; }
    public required ActivityResult Result { get; init; }
    public required double ActualDuration { get; init; }

    public NotificationPriority Priority => NotificationPriority.High;

    public object ToClientMessage()
    {
        return new
        {
            eventId = EventId,
            characterId = CharacterId,
            activity = new
            {
                id = ActivityId,
                type = ActivityType
            },
            result = new
            {
                success = Result.Success,
                rewards = Result.Rewards,
                message = Result.Message
            },
            duration = ActualDuration,
            completedAt = OccurredAtUtc
        };
    }
}

public sealed record ActivityResult
{
    public required bool Success { get; init; }
    public required object? Rewards { get; init; }
    public required string? Message { get; init; }
}
```

---

### 集成示例

```csharp
// Application/Activities/ActivityCoordinator.cs (假设)
public class ActivityCoordinator
{
    private readonly IDomainEventBus _eventBus;

    public Guid StartActivity(
        Guid characterId,
        string activityType,
        int slotIndex,
        object payload)
    {
        var activityId = Guid.NewGuid();
        
        // ... 启动活动逻辑 ...

        // 发布事件
        _eventBus.Publish(new ActivityStartedEvent
        {
            EventId = Guid.NewGuid(),
            OccurredAtUtc = DateTime.UtcNow,
            CharacterId = characterId,
            ActivityId = activityId,
            ActivityType = activityType,
            SlotIndex = slotIndex,
            Limit = new ActivityLimit
            {
                Type = "Duration",
                TargetValue = 60,
                Remaining = 60
            },
            Payload = payload
        });

        return activityId;
    }

    public void CompleteActivity(Guid activityId, ActivityResult result)
    {
        // ... 完成活动逻辑 ...

        _eventBus.Publish(new ActivityCompletedEvent
        {
            EventId = Guid.NewGuid(),
            OccurredAtUtc = DateTime.UtcNow,
            CharacterId = /* ... */,
            ActivityId = activityId,
            ActivityType = /* ... */,
            Result = result,
            ActualDuration = /* ... */
        });
    }
}
```

---

## 装备与物品事件

### 事件定义

**1. EquipmentAcquiredEvent（装备获得）**

```csharp
// Domain/Events/Equipment/EquipmentAcquiredEvent.cs
namespace BlazorIdle.Server.Domain.Events.Equipment;

using BlazorIdle.Server.Domain.Events;

public sealed record EquipmentAcquiredEvent : INotificationEvent
{
    public required Guid EventId { get; init; }
    public required DateTime OccurredAtUtc { get; init; }
    public string EventType => "EquipmentAcquired";
    public required Guid? CharacterId { get; init; }

    // 装备信息
    public required Guid EquipmentId { get; init; }
    public required string EquipmentDefinitionId { get; init; }
    public required string Name { get; init; }
    public required string Tier { get; init; } // "Common" / "Rare" / "Epic" / "Legendary"
    public required List<EquipmentAffix> Affixes { get; init; }
    public required string Source { get; init; } // "Drop" / "Craft" / "Quest"

    public NotificationPriority Priority => NotificationPriority.High;

    public object ToClientMessage()
    {
        return new
        {
            eventId = EventId,
            characterId = CharacterId,
            equipment = new
            {
                id = EquipmentId,
                definitionId = EquipmentDefinitionId,
                name = Name,
                tier = Tier,
                affixes = Affixes.Select(a => new
                {
                    id = a.AffixId,
                    value = a.Value
                }),
                source = Source
            },
            acquiredAt = OccurredAtUtc
        };
    }
}

public sealed record EquipmentAffix
{
    public required string AffixId { get; init; }
    public required double Value { get; init; }
}
```

**2. EquipmentUpgradedEvent（装备升级）**

```csharp
// Domain/Events/Equipment/EquipmentUpgradedEvent.cs
public sealed record EquipmentUpgradedEvent : INotificationEvent
{
    public required Guid EventId { get; init; }
    public required DateTime OccurredAtUtc { get; init; }
    public string EventType => "EquipmentUpgraded";
    public required Guid? CharacterId { get; init; }

    public required Guid EquipmentId { get; init; }
    public required string UpgradeType { get; init; } // "Tier" / "Affix" / "Enchant"
    public required object OldState { get; init; }
    public required object NewState { get; init; }
    public required UpgradeCost Cost { get; init; }

    public NotificationPriority Priority => NotificationPriority.Normal;

    public object ToClientMessage()
    {
        return new
        {
            eventId = EventId,
            equipmentId = EquipmentId,
            upgradeType = UpgradeType,
            oldState = OldState,
            newState = NewState,
            cost = new
            {
                gold = Cost.Gold,
                materials = Cost.Materials
            }
        };
    }
}

public sealed record UpgradeCost
{
    public required int Gold { get; init; }
    public required Dictionary<string, int> Materials { get; init; }
}
```

**3. ItemReceivedEvent（物品获得）**

```csharp
// Domain/Events/Items/ItemReceivedEvent.cs
namespace BlazorIdle.Server.Domain.Events.Items;

using BlazorIdle.Server.Domain.Events;

public sealed record ItemReceivedEvent : INotificationEvent
{
    public required Guid EventId { get; init; }
    public required DateTime OccurredAtUtc { get; init; }
    public string EventType => "ItemReceived";
    public required Guid? CharacterId { get; init; }

    public required string ItemId { get; init; }
    public required string Name { get; init; }
    public required int Quantity { get; init; }
    public required string Source { get; init; }

    public NotificationPriority Priority => NotificationPriority.Normal;

    public object ToClientMessage()
    {
        return new
        {
            eventId = EventId,
            characterId = CharacterId,
            item = new
            {
                id = ItemId,
                name = Name,
                quantity = Quantity,
                source = Source
            },
            receivedAt = OccurredAtUtc
        };
    }
}
```

---

## 经济系统事件

### 事件定义

**1. GoldChangedEvent（金币变化）**

```csharp
// Domain/Events/Economy/GoldChangedEvent.cs
namespace BlazorIdle.Server.Domain.Events.Economy;

using BlazorIdle.Server.Domain.Events;

public sealed record GoldChangedEvent : INotificationEvent
{
    public required Guid EventId { get; init; }
    public required DateTime OccurredAtUtc { get; init; }
    public string EventType => "GoldChanged";
    public required Guid? CharacterId { get; init; }

    public required int OldAmount { get; init; }
    public required int NewAmount { get; init; }
    public required int Delta { get; init; }
    public required string Reason { get; init; } // "Battle" / "Quest" / "Trade" / "Purchase"

    // 金币变化频繁，设为低优先级
    public NotificationPriority Priority => NotificationPriority.Low;

    public object ToClientMessage()
    {
        return new
        {
            eventId = EventId,
            gold = new
            {
                old = OldAmount,
                @new = NewAmount,
                delta = Delta,
                reason = Reason
            }
        };
    }
}
```

**2. ExperienceGainedEvent（经验获得）**

```csharp
// Domain/Events/Economy/ExperienceGainedEvent.cs
public sealed record ExperienceGainedEvent : INotificationEvent
{
    public required Guid EventId { get; init; }
    public required DateTime OccurredAtUtc { get; init; }
    public string EventType => "ExperienceGained";
    public required Guid? CharacterId { get; init; }

    public required int Amount { get; init; }
    public required int CurrentExp { get; init; }
    public required int ExpToNextLevel { get; init; }
    public required double Progress { get; init; } // 0.0 - 1.0
    public required string Source { get; init; }

    public NotificationPriority Priority => NotificationPriority.Low;

    public object ToClientMessage()
    {
        return new
        {
            eventId = EventId,
            experience = new
            {
                gained = Amount,
                current = CurrentExp,
                toNextLevel = ExpToNextLevel,
                progress = Progress,
                source = Source
            }
        };
    }
}
```

**3. LevelUpEvent（升级）**

```csharp
// Domain/Events/Economy/LevelUpEvent.cs
public sealed record LevelUpEvent : INotificationEvent
{
    public required Guid EventId { get; init; }
    public required DateTime OccurredAtUtc { get; init; }
    public string EventType => "LevelUp";
    public required Guid? CharacterId { get; init; }

    public required int OldLevel { get; init; }
    public required int NewLevel { get; init; }
    public required Dictionary<string, int> StatIncreases { get; init; }
    public required List<string> NewAbilities { get; init; }

    // 升级是重要事件，设为高优先级
    public NotificationPriority Priority => NotificationPriority.Critical;

    public object ToClientMessage()
    {
        return new
        {
            eventId = EventId,
            characterId = CharacterId,
            levelUp = new
            {
                oldLevel = OldLevel,
                newLevel = NewLevel,
                statIncreases = StatIncreases,
                newAbilities = NewAbilities
            },
            leveledUpAt = OccurredAtUtc
        };
    }
}
```

---

## 社交系统预留

### 设计预留接口

虽然社交功能暂未实现，但我们可以预留接口和事件定义，确保架构的前瞻性。

**1. 组播支持**

```csharp
// Infrastructure/Messaging/IGroupManager.cs (预留)
public interface IGroupManager
{
    /// <summary>
    /// 将用户加入组
    /// </summary>
    Task AddToGroupAsync(Guid userId, string groupName);

    /// <summary>
    /// 将用户移出组
    /// </summary>
    Task RemoveFromGroupAsync(Guid userId, string groupName);

    /// <summary>
    /// 获取组内所有用户
    /// </summary>
    Task<List<Guid>> GetGroupMembersAsync(string groupName);

    /// <summary>
    /// 发送消息给组
    /// </summary>
    Task SendToGroupAsync(string groupName, string method, object data);
}
```

**2. 社交事件示例**

```csharp
// Domain/Events/Social/PartyInviteEvent.cs (预留)
namespace BlazorIdle.Server.Domain.Events.Social;

using BlazorIdle.Server.Domain.Events;

public sealed record PartyInviteEvent : INotificationEvent
{
    public required Guid EventId { get; init; }
    public required DateTime OccurredAtUtc { get; init; }
    public string EventType => "PartyInvite";
    
    // 目标用户（接收邀请的人）
    public required Guid? CharacterId { get; init; }

    // 邀请信息
    public required Guid PartyId { get; init; }
    public required Guid InviterId { get; init; }
    public required string InviterName { get; init; }

    public NotificationPriority Priority => NotificationPriority.High;

    public object ToClientMessage()
    {
        return new
        {
            eventId = EventId,
            partyId = PartyId,
            inviter = new
            {
                id = InviterId,
                name = InviterName
            },
            invitedAt = OccurredAtUtc
        };
    }
}
```

---

## 监控与诊断

### 1. 事件统计面板

```csharp
// Infrastructure/Messaging/EventStatisticsService.cs
public class EventStatisticsService : IHostedService
{
    private readonly IDomainEventBus _eventBus;
    private readonly ConcurrentDictionary<string, EventTypeStats> _stats = new();
    private IDisposable? _subscription;

    public EventStatisticsService(IDomainEventBus eventBus)
    {
        _eventBus = eventBus;
    }

    public Task StartAsync(CancellationToken ct)
    {
        // 订阅所有事件进行统计
        _subscription = _eventBus.Subscribe<IDomainEvent>(@event =>
        {
            var eventType = @event.EventType;
            _stats.AddOrUpdate(
                eventType,
                _ => new EventTypeStats
                {
                    EventType = eventType,
                    Count = 1,
                    LastOccurred = @event.OccurredAtUtc
                },
                (_, stats) =>
                {
                    stats.Count++;
                    stats.LastOccurred = @event.OccurredAtUtc;
                    return stats;
                });

            return Task.CompletedTask;
        });

        return Task.CompletedTask;
    }

    public Dictionary<string, EventTypeStats> GetStatistics()
    {
        return new Dictionary<string, EventTypeStats>(_stats);
    }

    public Task StopAsync(CancellationToken ct)
    {
        _subscription?.Dispose();
        return Task.CompletedTask;
    }
}

public class EventTypeStats
{
    public required string EventType { get; init; }
    public int Count { get; set; }
    public DateTime LastOccurred { get; set; }
}
```

---

### 2. 监控 API

```csharp
// Api/MonitoringController.cs
[ApiController]
[Route("api/monitoring")]
public class MonitoringController : ControllerBase
{
    private readonly IDomainEventBus _eventBus;
    private readonly ISignalRDispatcher _dispatcher;
    private readonly ConnectionManager _connectionManager;
    private readonly EventStatisticsService _statistics;

    public MonitoringController(
        IDomainEventBus eventBus,
        ISignalRDispatcher dispatcher,
        ConnectionManager connectionManager,
        EventStatisticsService statistics)
    {
        _eventBus = eventBus;
        _dispatcher = dispatcher;
        _connectionManager = connectionManager;
        _statistics = statistics;
    }

    /// <summary>
    /// 获取事件总线统计信息
    /// </summary>
    [HttpGet("eventbus")]
    public ActionResult<EventBusStatistics> GetEventBusStatistics()
    {
        return Ok(_eventBus.GetStatistics());
    }

    /// <summary>
    /// 获取 SignalR 分发器统计信息
    /// </summary>
    [HttpGet("dispatcher")]
    public ActionResult<DispatcherStatistics> GetDispatcherStatistics()
    {
        return Ok(_dispatcher.GetStatistics());
    }

    /// <summary>
    /// 获取连接管理器统计信息
    /// </summary>
    [HttpGet("connections")]
    public ActionResult<ConnectionStatistics> GetConnectionStatistics()
    {
        return Ok(_connectionManager.GetStatistics());
    }

    /// <summary>
    /// 获取事件类型统计
    /// </summary>
    [HttpGet("events")]
    public ActionResult<Dictionary<string, EventTypeStats>> GetEventStatistics()
    {
        return Ok(_statistics.GetStatistics());
    }

    /// <summary>
    /// 获取综合健康状态
    /// </summary>
    [HttpGet("health")]
    public ActionResult<HealthStatus> GetHealthStatus()
    {
        var eventBusStats = _eventBus.GetStatistics();
        var dispatcherStats = _dispatcher.GetStatistics();
        var connectionStats = _connectionManager.GetStatistics();

        var health = new HealthStatus
        {
            IsHealthy = true,
            EventBus = new
            {
                totalPublished = eventBusStats.TotalPublished,
                totalSubscriptions = eventBusStats.TotalSubscriptions
            },
            SignalR = new
            {
                totalSent = dispatcherStats.TotalSent,
                queuedMessages = dispatcherStats.QueuedMessages,
                failedDeliveries = dispatcherStats.FailedDeliveries
            },
            Connections = new
            {
                total = connectionStats.TotalConnections,
                uniqueUsers = connectionStats.UniqueUsers,
                avgDuration = connectionStats.AverageConnectionDuration
            }
        };

        // 健康检查逻辑
        if (dispatcherStats.QueuedMessages > 1000)
        {
            health.IsHealthy = false;
            health.Warnings.Add("Message queue is too large");
        }

        if (dispatcherStats.FailedDeliveries > eventBusStats.TotalPublished * 0.1)
        {
            health.IsHealthy = false;
            health.Warnings.Add("High failure rate in message delivery");
        }

        return Ok(health);
    }
}

public record HealthStatus
{
    public bool IsHealthy { get; set; }
    public object EventBus { get; init; } = null!;
    public object SignalR { get; init; } = null!;
    public object Connections { get; init; } = null!;
    public List<string> Warnings { get; init; } = new();
}
```

---

### 3. 实时监控仪表盘（Blazor 组件）

```razor
@* Pages/Admin/Monitoring.razor *@
@page "/admin/monitoring"
@inject HttpClient Http
@inject BattleNotificationService NotificationService
@implements IDisposable

<h3>SignalR Monitoring Dashboard</h3>

<div class="dashboard">
    <div class="card">
        <h4>Event Bus</h4>
        <p>Published: @_eventBusStats?.TotalPublished</p>
        <p>Subscriptions: @_eventBusStats?.TotalSubscriptions</p>
    </div>

    <div class="card">
        <h4>SignalR Dispatcher</h4>
        <p>Sent: @_dispatcherStats?.TotalSent</p>
        <p>Queued: @_dispatcherStats?.QueuedMessages</p>
        <p>Failed: @_dispatcherStats?.FailedDeliveries</p>
    </div>

    <div class="card">
        <h4>Connections</h4>
        <p>Total: @_connectionStats?.TotalConnections</p>
        <p>Users: @_connectionStats?.UniqueUsers</p>
        <p>Avg Duration: @_connectionStats?.AverageConnectionDuration.ToString("F2")s</p>
    </div>
</div>

<div class="event-types">
    <h4>Event Types</h4>
    <table>
        <thead>
            <tr>
                <th>Event Type</th>
                <th>Count</th>
                <th>Last Occurred</th>
            </tr>
        </thead>
        <tbody>
            @if (_eventStats != null)
            {
                @foreach (var (eventType, stats) in _eventStats.OrderByDescending(x => x.Value.Count))
                {
                    <tr>
                        <td>@eventType</td>
                        <td>@stats.Count</td>
                        <td>@stats.LastOccurred.ToString("HH:mm:ss")</td>
                    </tr>
                }
            }
        </tbody>
    </table>
</div>

@code {
    private EventBusStatistics? _eventBusStats;
    private DispatcherStatistics? _dispatcherStats;
    private ConnectionStatistics? _connectionStats;
    private Dictionary<string, EventTypeStats>? _eventStats;
    private Timer? _refreshTimer;

    protected override void OnInitialized()
    {
        _refreshTimer = new Timer(async _ => await RefreshStats(), null, TimeSpan.Zero, TimeSpan.FromSeconds(2));
    }

    private async Task RefreshStats()
    {
        try
        {
            _eventBusStats = await Http.GetFromJsonAsync<EventBusStatistics>("api/monitoring/eventbus");
            _dispatcherStats = await Http.GetFromJsonAsync<DispatcherStatistics>("api/monitoring/dispatcher");
            _connectionStats = await Http.GetFromJsonAsync<ConnectionStatistics>("api/monitoring/connections");
            _eventStats = await Http.GetFromJsonAsync<Dictionary<string, EventTypeStats>>("api/monitoring/events");
            
            StateHasChanged();
        }
        catch (Exception ex)
        {
            // Log error
        }
    }

    public void Dispose()
    {
        _refreshTimer?.Dispose();
    }
}
```

---

## 实施指南

### Week 1: 通用框架完善

#### Day 1-2: 事件优先级支持

**任务清单**:
- [ ] 实现 `PriorityBasedSubscriber`
- [ ] 更新 `NotificationEventSubscriber` 支持优先级
- [ ] 测试不同优先级事件的处理顺序
- [ ] 压力测试

---

#### Day 3-4: 监控系统

**任务清单**:
- [ ] 实现 `EventStatisticsService`
- [ ] 创建 `MonitoringController`
- [ ] 实现健康检查端点
- [ ] 创建监控仪表盘 UI

---

#### Day 5: 文档和示例

**任务清单**:
- [ ] 编写事件定义指南
- [ ] 创建新增事件类型的示例代码
- [ ] 更新 API 文档
- [ ] 整理最佳实践

---

### Week 2: 业务事件集成（可选）

根据项目实际进度，选择性实施以下事件：

#### Option 1: 活动计划事件

**任务清单**:
- [ ] 定义活动事件类型
- [ ] 在 ActivityCoordinator 中发布事件
- [ ] 客户端集成
- [ ] 测试验证

---

#### Option 2: 装备和物品事件

**任务清单**:
- [ ] 定义装备/物品事件
- [ ] 在装备系统中发布事件
- [ ] 客户端通知UI
- [ ] 测试验证

---

#### Option 3: 经济系统事件

**任务清单**:
- [ ] 定义经济事件
- [ ] 在经济服务中发布事件
- [ ] 客户端资源显示更新
- [ ] 测试验证

---

## 扩展最佳实践

### 1. 新增事件类型步骤

**步骤 1**: 定义事件类

```csharp
// Domain/Events/YourModule/YourEvent.cs
public sealed record YourEvent : INotificationEvent
{
    // 实现必需属性
    public required Guid EventId { get; init; }
    public required DateTime OccurredAtUtc { get; init; }
    public string EventType => "YourEventName";
    public required Guid? CharacterId { get; init; }
    
    // 自定义属性
    public required string CustomData { get; init; }
    
    // 设置优先级
    public NotificationPriority Priority => NotificationPriority.Normal;
    
    // 定义客户端消息格式
    public object ToClientMessage()
    {
        return new
        {
            eventId = EventId,
            customData = CustomData
        };
    }
}
```

**步骤 2**: 在业务逻辑中发布事件

```csharp
// Application/YourModule/YourService.cs
public class YourService
{
    private readonly IDomainEventBus _eventBus;

    public void DoSomething()
    {
        // 业务逻辑...

        // 发布事件
        _eventBus.Publish(new YourEvent
        {
            EventId = Guid.NewGuid(),
            OccurredAtUtc = DateTime.UtcNow,
            CharacterId = /* ... */,
            CustomData = "data"
        });
    }
}
```

**步骤 3**: 客户端订阅（如果需要）

```csharp
// Blazor Client
public class YourNotificationService
{
    public event Action<YourEventNotification>? OnYourEvent;

    public void Initialize()
    {
        _connection.On<JsonElement>("OnYourEventName", data =>
        {
            var notification = JsonSerializer.Deserialize<YourEventNotification>(
                data.GetRawText());
            OnYourEvent?.Invoke(notification);
        });
    }
}
```

**就这么简单！** 事件会自动通过事件总线和 SignalR 推送到客户端。

---

### 2. 事件过滤示例

```csharp
// 只订阅特定角色的事件
_eventBus.Subscribe<BattleEndedEvent>(
    filter: e => e.CharacterId == myCharacterId,
    handler: async e => { /* 处理逻辑 */ }
);

// 只订阅高优先级事件
_eventBus.Subscribe<INotificationEvent>(
    filter: e => e.Priority >= NotificationPriority.High,
    handler: async e => { /* 处理逻辑 */ }
);
```

---

### 3. 批量推送示例

```csharp
// 聚合多个小事件为一个批量通知
public class ResourceChangeBatcher : IHostedService
{
    private readonly Dictionary<Guid, ResourceChanges> _pending = new();

    public Task StartAsync(CancellationToken ct)
    {
        _eventBus.Subscribe<GoldChangedEvent>(HandleGoldChanged);
        _eventBus.Subscribe<ExperienceGainedEvent>(HandleExpGained);
        
        _ = FlushPeriodically(ct);
        return Task.CompletedTask;
    }

    private async Task FlushPeriodically(CancellationToken ct)
    {
        var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));
        while (await timer.WaitForNextTickAsync(ct))
        {
            foreach (var (characterId, changes) in _pending)
            {
                await _dispatcher.SendToUserAsync(
                    characterId,
                    "OnResourcesChanged",
                    changes
                );
            }
            _pending.Clear();
        }
    }
}
```

---

## 总结

### Phase 3 完成标志

- [ ] 事件优先级系统实施
- [ ] 监控和诊断系统完整
- [ ] 至少实现一个非战斗事件类型（活动/装备/经济）
- [ ] 扩展文档和示例完整
- [ ] 性能满足要求
- [ ] 所有测试通过

---

### 后续演进方向

1. **社交功能集成**
   - 实现 `IGroupManager`
   - 添加好友、组队、公会事件
   - 实时聊天功能

2. **高级事件功能**
   - 事件溯源和重放
   - 事件持久化
   - 事件版本管理

3. **横向扩展**
   - Redis Backplane for SignalR
   - 分布式事件总线
   - 多服务器支持

4. **性能优化**
   - 消息压缩
   - 智能采样
   - 连接池优化

---

**恭喜！** 完成 Phase 3 后，你已经拥有了一个完整、可扩展、高性能的 SignalR 推送系统！🎉
