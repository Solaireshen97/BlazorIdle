# Phase 2: 战斗事件集成设计（中篇）

**阶段目标**: 集成战斗系统事件推送，实现实时战斗状态同步  
**实施时间**: 2-3周  
**前置条件**: Phase 1 完成  
**后续阶段**: Phase 3 扩展性设计

---

## 📋 目录

1. [集成策略](#集成策略)
2. [战斗事件定义](#战斗事件定义)
3. [事件发布点](#事件发布点)
4. [客户端接口设计](#客户端接口设计)
5. [性能优化](#性能优化)
6. [实施步骤](#实施步骤)

---

## 集成策略

### 核心原则

```
✅ 最小化侵入：不修改现有战斗核心逻辑
✅ 事件驱动：通过事件总线解耦
✅ 渐进集成：先基础事件，后详细事件
✅ 性能优先：聚合推送，减少频率
```

---

### 集成架构

```
┌─────────────────────────────────────────────────────────────┐
│  Domain.Combat (现有代码 - 最小修改)                         │
│                                                              │
│  ┌────────────────────────────────────────────────────┐     │
│  │  BattleRunner                                     │     │
│  │  - 战斗循环执行                                    │     │
│  │  - 调用 SegmentCollector                          │     │
│  └──────────────────┬─────────────────────────────────┘     │
│                     │                                       │
│  ┌──────────────────▼─────────────────────────────────┐     │
│  │  SegmentCollector                                 │     │
│  │  - Flush() 时发布 CombatSegmentFlushedEvent      │     │  <-- 新增
│  └──────────────────┬─────────────────────────────────┘     │
│                     │                                       │
└─────────────────────┼───────────────────────────────────────┘
                      │ 发布事件
                      ▼
┌─────────────────────────────────────────────────────────────┐
│  Domain.Events (新增)                                       │
│                                                              │
│  ┌────────────────────────────────────────────────────┐     │
│  │  CombatSegmentFlushedEvent : INotificationEvent   │     │
│  │  - SegmentData                                    │     │
│  │  - BattleId                                       │     │
│  └──────────────────┬─────────────────────────────────┘     │
│                     │                                       │
│  ┌──────────────────▼─────────────────────────────────┐     │
│  │  BattleEndedEvent : INotificationEvent            │     │
│  │  - Result (Victory/Defeat)                        │     │
│  │  - Rewards                                        │     │
│  └──────────────────┬─────────────────────────────────┘     │
│                     │                                       │
└─────────────────────┼───────────────────────────────────────┘
                      │ 事件总线
                      ▼
┌─────────────────────────────────────────────────────────────┐
│  Infrastructure.Messaging                                   │
│                                                              │
│  ┌────────────────────────────────────────────────────┐     │
│  │  NotificationEventSubscriber                      │     │
│  │  - 订阅 INotificationEvent                        │     │
│  │  - 转换为 SignalR 消息                            │     │
│  │  - 推送到客户端                                   │     │
│  └──────────────────────────────────────────────────────┘     │
│                                                              │
└─────────────────────────────────────────────────────────────┘
```

---

## 战斗事件定义

### 1. CombatSegmentFlushedEvent（战斗段刷新事件）

**触发时机**: `SegmentCollector.Flush()` 被调用时

**设计要点**:
- 包含聚合的战斗统计数据
- 高频事件（每 5 秒或 200 事件）
- 需要优化传输大小

**事件定义**:

```csharp
// Domain/Events/Combat/CombatSegmentFlushedEvent.cs
namespace BlazorIdle.Server.Domain.Events.Combat;

using BlazorIdle.Server.Domain.Combat;
using BlazorIdle.Server.Domain.Events;

/// <summary>
/// 战斗段刷新事件
/// 当 SegmentCollector 聚合一批事件并 Flush 时触发
/// </summary>
public sealed record CombatSegmentFlushedEvent : INotificationEvent
{
    public required Guid EventId { get; init; }
    public required DateTime OccurredAtUtc { get; init; }
    public string EventType => "CombatSegmentFlushed";
    public required Guid? CharacterId { get; init; }

    // 战斗上下文
    public required Guid BattleId { get; init; }
    public required double BattleTime { get; init; }

    // Segment 数据
    public required CombatSegmentData SegmentData { get; init; }

    // 推送配置
    public NotificationPriority Priority => NotificationPriority.Normal;

    public object ToClientMessage()
    {
        return new
        {
            eventId = EventId,
            battleId = BattleId,
            battleTime = BattleTime,
            segment = new
            {
                startTime = SegmentData.StartTime,
                endTime = SegmentData.EndTime,
                totalDamage = SegmentData.TotalDamage,
                damageBySource = SegmentData.DamageBySource,
                damageByType = SegmentData.DamageByType,
                resourceFlow = SegmentData.ResourceFlow,
                tagCounters = SegmentData.TagCounters,
                eventCount = SegmentData.EventCount
            }
        };
    }
}

/// <summary>
/// 战斗段数据（从 CombatSegment 提取）
/// </summary>
public sealed record CombatSegmentData
{
    public required double StartTime { get; init; }
    public required double EndTime { get; init; }
    public required int TotalDamage { get; init; }
    public required IReadOnlyDictionary<string, int> DamageBySource { get; init; }
    public required IReadOnlyDictionary<string, int> DamageByType { get; init; }
    public required IReadOnlyDictionary<string, int> ResourceFlow { get; init; }
    public required IReadOnlyDictionary<string, int> TagCounters { get; init; }
    public required int EventCount { get; init; }
}
```

---

### 2. BattleStartedEvent（战斗开始事件）

**触发时机**: 战斗初始化完成，第一次 step 执行前

**事件定义**:

```csharp
// Domain/Events/Combat/BattleStartedEvent.cs
namespace BlazorIdle.Server.Domain.Events.Combat;

using BlazorIdle.Server.Domain.Events;
using BlazorIdle.Server.Domain.Combat.Professions;

public sealed record BattleStartedEvent : INotificationEvent
{
    public required Guid EventId { get; init; }
    public required DateTime OccurredAtUtc { get; init; }
    public string EventType => "BattleStarted";
    public required Guid? CharacterId { get; init; }

    // 战斗信息
    public required Guid BattleId { get; init; }
    public required Profession Profession { get; init; }
    public required string EnemyId { get; init; }
    public required int EnemyCount { get; init; }
    public required string Mode { get; init; } // "duration" / "continuous" / "dungeon"
    public required string? DungeonId { get; init; }

    public NotificationPriority Priority => NotificationPriority.High;

    public object ToClientMessage()
    {
        return new
        {
            eventId = EventId,
            battleId = BattleId,
            characterId = CharacterId,
            profession = Profession.ToString(),
            enemy = new
            {
                id = EnemyId,
                count = EnemyCount
            },
            mode = Mode,
            dungeonId = DungeonId,
            startedAt = OccurredAtUtc
        };
    }
}
```

---

### 3. BattleEndedEvent（战斗结束事件）

**触发时机**: 战斗结束（无论胜利、失败或超时）

**事件定义**:

```csharp
// Domain/Events/Combat/BattleEndedEvent.cs
namespace BlazorIdle.Server.Domain.Events.Combat;

using BlazorIdle.Server.Domain.Events;

public sealed record BattleEndedEvent : INotificationEvent
{
    public required Guid EventId { get; init; }
    public required DateTime OccurredAtUtc { get; init; }
    public string EventType => "BattleEnded";
    public required Guid? CharacterId { get; init; }

    // 战斗信息
    public required Guid BattleId { get; init; }
    public required BattleResult Result { get; init; }
    public required double Duration { get; init; }

    // 战斗统计
    public required BattleStatistics Statistics { get; init; }

    // 奖励
    public required BattleRewards Rewards { get; init; }

    public NotificationPriority Priority => NotificationPriority.High;

    public object ToClientMessage()
    {
        return new
        {
            eventId = EventId,
            battleId = BattleId,
            characterId = CharacterId,
            result = Result.ToString().ToLowerInvariant(),
            duration = Duration,
            statistics = new
            {
                totalDamage = Statistics.TotalDamage,
                dps = Statistics.Dps,
                killCount = Statistics.KillCount,
                deathCount = Statistics.DeathCount
            },
            rewards = new
            {
                gold = Rewards.Gold,
                experience = Rewards.Experience,
                items = Rewards.Items
            },
            endedAt = OccurredAtUtc
        };
    }
}

public enum BattleResult
{
    Victory,
    Defeat,
    Timeout,
    Interrupted
}

public sealed record BattleStatistics
{
    public required int TotalDamage { get; init; }
    public required double Dps { get; init; }
    public required int KillCount { get; init; }
    public required int DeathCount { get; init; }
}

public sealed record BattleRewards
{
    public required int Gold { get; init; }
    public required int Experience { get; init; }
    public required List<ItemDrop> Items { get; init; }
}

public sealed record ItemDrop
{
    public required string ItemId { get; init; }
    public required int Quantity { get; init; }
    public required string Rarity { get; init; }
}
```

---

### 4. BattleStateSnapshotEvent（战斗状态快照事件）

**触发时机**: 定时快照（可选，用于离线战斗回放）

**设计说明**:
这是一个低优先级的可选事件，用于支持离线战斗的状态回放。
可以在 Phase 2 后期或 Phase 3 实现。

```csharp
// Domain/Events/Combat/BattleStateSnapshotEvent.cs
namespace BlazorIdle.Server.Domain.Events.Combat;

using BlazorIdle.Server.Domain.Events;

/// <summary>
/// 战斗状态快照事件（用于离线回放）
/// </summary>
public sealed record BattleStateSnapshotEvent : INotificationEvent
{
    public required Guid EventId { get; init; }
    public required DateTime OccurredAtUtc { get; init; }
    public string EventType => "BattleStateSnapshot";
    public required Guid? CharacterId { get; init; }

    public required Guid BattleId { get; init; }
    public required double BattleTime { get; init; }
    public required CombatantSnapshot Character { get; init; }
    public required CombatantSnapshot Enemy { get; init; }

    public NotificationPriority Priority => NotificationPriority.Low;

    public object ToClientMessage()
    {
        return new
        {
            eventId = EventId,
            battleId = BattleId,
            battleTime = BattleTime,
            character = new
            {
                hp = Character.CurrentHp,
                maxHp = Character.MaxHp,
                resources = Character.Resources,
                buffs = Character.ActiveBuffs
            },
            enemy = new
            {
                hp = Enemy.CurrentHp,
                maxHp = Enemy.MaxHp
            }
        };
    }
}

public sealed record CombatantSnapshot
{
    public required int CurrentHp { get; init; }
    public required int MaxHp { get; init; }
    public required Dictionary<string, int> Resources { get; init; }
    public required List<string> ActiveBuffs { get; init; }
}
```

---

## 事件发布点

### 修改点 1: SegmentCollector.Flush()

**位置**: `Domain/Combat/SegmentCollector.cs`

**修改策略**: 在 `Flush()` 方法末尾添加事件发布逻辑

**原始代码**（简化）:

```csharp
public CombatSegment Flush(double currentTime)
{
    var total = 0;
    var bySource = new Dictionary<string, int>();
    var byType = new Dictionary<string, int>();

    foreach (var (src, dmg, type) in _damageEvents)
    {
        total += dmg;
        // ... 聚合逻辑
    }

    var seg = new CombatSegment
    {
        StartTime = SegmentStart,
        EndTime = currentTime,
        TotalDamage = total,
        DamageBySource = bySource,
        DamageByType = byType,
        TagCounters = new Dictionary<string, int>(_tagCounters),
        ResourceFlow = new Dictionary<string, int>(_resourceFlow),
        EventCount = EventCount,
        RngStartInclusive = _rngStartInclusive ?? 0,
        RngEndInclusive = _rngEndInclusive
    };

    Reset(currentTime);
    return seg;
}
```

**修改后**:

```csharp
public CombatSegment Flush(double currentTime)
{
    var total = 0;
    var bySource = new Dictionary<string, int>();
    var byType = new Dictionary<string, int>();

    foreach (var (src, dmg, type) in _damageEvents)
    {
        total += dmg;
        // ... 聚合逻辑
    }

    var seg = new CombatSegment
    {
        StartTime = SegmentStart,
        EndTime = currentTime,
        TotalDamage = total,
        DamageBySource = bySource,
        DamageByType = byType,
        TagCounters = new Dictionary<string, int>(_tagCounters),
        ResourceFlow = new Dictionary<string, int>(_resourceFlow),
        EventCount = EventCount,
        RngStartInclusive = _rngStartInclusive ?? 0,
        RngEndInclusive = _rngEndInclusive
    };

    // 新增：发布 Segment 刷新事件
    OnSegmentFlushed?.Invoke(seg);

    Reset(currentTime);
    return seg;
}

// 新增：事件回调
public Action<CombatSegment>? OnSegmentFlushed { get; set; }
```

**说明**: 
- 使用委托回调，避免在 Domain 层直接依赖事件总线
- 由上层（Application 层）注册回调并发布事件

---

### 修改点 2: StepBattleCoordinator.Start()

**位置**: `Application/Battles/Step/StepBattleCoordinator.cs`

**修改策略**: 在战斗启动时发布 `BattleStartedEvent`

**修改代码**:

```csharp
public Guid Start(
    Guid characterId,
    Profession profession,
    CharacterStats stats,
    double seconds,
    ulong seed,
    string? enemyId,
    int enemyCount,
    StepBattleMode mode = StepBattleMode.Duration,
    string? dungeonId = null,
    double? continuousRespawnDelaySeconds = null,
    double? dungeonWaveDelaySeconds = null,
    double? dungeonRunDelaySeconds = null)
{
    var eid = EnemyRegistry.Resolve(enemyId).Id;
    var enemy = EnemyRegistry.Resolve(eid);
    var id = Guid.NewGuid();

    var rb = new RunningBattle(
        id: id,
        characterId: characterId,
        profession: profession,
        seed: seed,
        targetSeconds: seconds,
        enemyDef: enemy,
        enemyCount: enemyCount,
        stats: stats,
        mode: mode,
        dungeonId: dungeonId,
        continuousRespawnDelaySeconds: continuousRespawnDelaySeconds,
        dungeonWaveDelaySeconds: dungeonWaveDelaySeconds,
        dungeonRunDelaySeconds: dungeonRunDelaySeconds
    );

    // 注册 Segment 刷新回调
    rb.Collector.OnSegmentFlushed = segment =>
    {
        PublishSegmentFlushedEvent(id, characterId, rb.Clock.CurrentTime, segment);
    };

    if (!_running.TryAdd(id, rb))
        throw new InvalidOperationException("Failed to register running battle.");

    // 新增：发布战斗开始事件
    PublishBattleStartedEvent(id, characterId, profession, eid, enemyCount, mode, dungeonId);

    return id;
}

private void PublishBattleStartedEvent(
    Guid battleId,
    Guid characterId,
    Profession profession,
    string enemyId,
    int enemyCount,
    StepBattleMode mode,
    string? dungeonId)
{
    var @event = new BattleStartedEvent
    {
        EventId = Guid.NewGuid(),
        OccurredAtUtc = DateTime.UtcNow,
        CharacterId = characterId,
        BattleId = battleId,
        Profession = profession,
        EnemyId = enemyId,
        EnemyCount = enemyCount,
        Mode = mode.ToString().ToLowerInvariant(),
        DungeonId = dungeonId
    };

    // 通过注入的事件总线发布
    _eventBus.Publish(@event);
}

private void PublishSegmentFlushedEvent(
    Guid battleId,
    Guid characterId,
    double battleTime,
    CombatSegment segment)
{
    var @event = new CombatSegmentFlushedEvent
    {
        EventId = Guid.NewGuid(),
        OccurredAtUtc = DateTime.UtcNow,
        CharacterId = characterId,
        BattleId = battleId,
        BattleTime = battleTime,
        SegmentData = new CombatSegmentData
        {
            StartTime = segment.StartTime,
            EndTime = segment.EndTime,
            TotalDamage = segment.TotalDamage,
            DamageBySource = segment.DamageBySource,
            DamageByType = segment.DamageByType,
            ResourceFlow = segment.ResourceFlow,
            TagCounters = segment.TagCounters,
            EventCount = segment.EventCount
        }
    };

    _eventBus.Publish(@event);
}
```

**依赖注入更新**:

```csharp
// StepBattleCoordinator 构造函数
private readonly IDomainEventBus _eventBus;

public StepBattleCoordinator(
    IServiceScopeFactory scopeFactory,
    IDomainEventBus eventBus) // 新增
{
    _scopeFactory = scopeFactory;
    _eventBus = eventBus;
}
```

---

### 修改点 3: StepBattleCoordinator.Stop()

**位置**: `Application/Battles/Step/StepBattleCoordinator.cs`

**修改策略**: 在战斗停止时发布 `BattleEndedEvent`

**修改代码**:

```csharp
public async Task<(bool found, StepBattleStatusDto? status)> Stop(Guid id)
{
    if (!_running.TryRemove(id, out var rb))
    {
        // 尝试从数据库加载已完成的战斗
        // ... 现有逻辑 ...
        return (false, null);
    }

    rb.MarkCompleted();
    var finalStatus = GetStatus(id);

    // 新增：发布战斗结束事件
    if (finalStatus.found)
    {
        PublishBattleEndedEvent(rb, finalStatus.status);
    }

    // 持久化逻辑
    // ...

    return finalStatus;
}

private void PublishBattleEndedEvent(RunningBattle rb, StepBattleStatusDto status)
{
    var result = DetermineBattleResult(status);
    var statistics = ExtractStatistics(status);
    var rewards = ExtractRewards(status);

    var @event = new BattleEndedEvent
    {
        EventId = Guid.NewGuid(),
        OccurredAtUtc = DateTime.UtcNow,
        CharacterId = rb.CharacterId,
        BattleId = rb.Id,
        Result = result,
        Duration = status.EffectiveDuration,
        Statistics = statistics,
        Rewards = rewards
    };

    _eventBus.Publish(@event);
}

private static BattleResult DetermineBattleResult(StepBattleStatusDto status)
{
    // 根据状态判断结果
    if (status.KillCount > 0)
        return BattleResult.Victory;
    if (status.DeathCount > 0)
        return BattleResult.Defeat;
    return BattleResult.Timeout;
}

private static BattleStatistics ExtractStatistics(StepBattleStatusDto status)
{
    return new BattleStatistics
    {
        TotalDamage = (int)status.TotalDamage,
        Dps = status.Dps,
        KillCount = status.KillCount.Values.Sum(),
        DeathCount = status.DeathCount
    };
}

private static BattleRewards ExtractRewards(StepBattleStatusDto status)
{
    return new BattleRewards
    {
        Gold = status.TotalGold,
        Experience = status.TotalExp,
        Items = status.Drops?.Select(d => new ItemDrop
        {
            ItemId = d.ItemId,
            Quantity = d.Quantity,
            Rarity = d.Rarity
        }).ToList() ?? new List<ItemDrop>()
    };
}
```

---

## 客户端接口设计

### Blazor WebAssembly 客户端集成

**1. SignalR 客户端服务**

```csharp
// BlazorIdle/Services/BattleNotificationService.cs
using Microsoft.AspNetCore.SignalR.Client;
using System.Text.Json;

namespace BlazorIdle.Services;

public class BattleNotificationService : IAsyncDisposable
{
    private HubConnection? _connection;
    private readonly ILogger<BattleNotificationService> _logger;
    private readonly string _hubUrl;
    private Guid? _userId;

    // 事件
    public event Action<BattleStartedNotification>? OnBattleStarted;
    public event Action<CombatSegmentNotification>? OnCombatSegmentUpdate;
    public event Action<BattleEndedNotification>? OnBattleEnded;

    public BattleNotificationService(
        IConfiguration configuration,
        ILogger<BattleNotificationService> logger)
    {
        _logger = logger;
        _hubUrl = configuration["ApiBaseUrl"] + "/hubs/notifications";
    }

    public async Task ConnectAsync(Guid userId)
    {
        _userId = userId;

        _connection = new HubConnectionBuilder()
            .WithUrl($"{_hubUrl}?userId={userId}")
            .WithAutomaticReconnect(new[] {
                TimeSpan.Zero,
                TimeSpan.FromSeconds(2),
                TimeSpan.FromSeconds(5),
                TimeSpan.FromSeconds(10)
            })
            .Build();

        // 注册服务端方法
        _connection.On<JsonElement>("OnBattleStarted", data =>
        {
            var notification = JsonSerializer.Deserialize<BattleStartedNotification>(
                data.GetRawText());
            if (notification != null)
            {
                OnBattleStarted?.Invoke(notification);
            }
        });

        _connection.On<JsonElement>("OnCombatSegmentFlushed", data =>
        {
            var notification = JsonSerializer.Deserialize<CombatSegmentNotification>(
                data.GetRawText());
            if (notification != null)
            {
                OnCombatSegmentUpdate?.Invoke(notification);
            }
        });

        _connection.On<JsonElement>("OnBattleEnded", data =>
        {
            var notification = JsonSerializer.Deserialize<BattleEndedNotification>(
                data.GetRawText());
            if (notification != null)
            {
                OnBattleEnded?.Invoke(notification);
            }
        });

        _connection.Reconnecting += error =>
        {
            _logger.LogWarning("Connection lost, reconnecting...");
            return Task.CompletedTask;
        };

        _connection.Reconnected += connectionId =>
        {
            _logger.LogInformation("Reconnected with connection {ConnectionId}", connectionId);
            return Task.CompletedTask;
        };

        _connection.Closed += error =>
        {
            _logger.LogError(error, "Connection closed");
            return Task.CompletedTask;
        };

        await _connection.StartAsync();
        _logger.LogInformation("Connected to battle notifications hub");

        // 启动心跳
        _ = SendHeartbeatAsync();
    }

    private async Task SendHeartbeatAsync()
    {
        var timer = new PeriodicTimer(TimeSpan.FromSeconds(10));
        while (_connection?.State == HubConnectionState.Connected &&
               await timer.WaitForNextTickAsync())
        {
            try
            {
                await _connection.InvokeAsync("Heartbeat");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send heartbeat");
            }
        }
    }

    public async Task SubscribeToBattleAsync(Guid battleId)
    {
        if (_connection?.State == HubConnectionState.Connected)
        {
            await _connection.InvokeAsync("SubscribeToBattle", battleId);
            _logger.LogDebug("Subscribed to battle {BattleId}", battleId);
        }
    }

    public async Task UnsubscribeFromBattleAsync(Guid battleId)
    {
        if (_connection?.State == HubConnectionState.Connected)
        {
            await _connection.InvokeAsync("UnsubscribeFromBattle", battleId);
            _logger.LogDebug("Unsubscribed from battle {BattleId}", battleId);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection != null)
        {
            await _connection.DisposeAsync();
        }
    }
}

// 通知模型
public record BattleStartedNotification
{
    public Guid EventId { get; init; }
    public Guid BattleId { get; init; }
    public Guid CharacterId { get; init; }
    public string Profession { get; init; } = string.Empty;
    public EnemyInfo Enemy { get; init; } = null!;
    public string Mode { get; init; } = string.Empty;
    public DateTime StartedAt { get; init; }
}

public record EnemyInfo
{
    public string Id { get; init; } = string.Empty;
    public int Count { get; init; }
}

public record CombatSegmentNotification
{
    public Guid EventId { get; init; }
    public Guid BattleId { get; init; }
    public double BattleTime { get; init; }
    public SegmentInfo Segment { get; init; } = null!;
}

public record SegmentInfo
{
    public double StartTime { get; init; }
    public double EndTime { get; init; }
    public int TotalDamage { get; init; }
    public Dictionary<string, int> DamageBySource { get; init; } = new();
    public Dictionary<string, int> ResourceFlow { get; init; } = new();
    public Dictionary<string, int> TagCounters { get; init; } = new();
    public int EventCount { get; init; }
}

public record BattleEndedNotification
{
    public Guid EventId { get; init; }
    public Guid BattleId { get; init; }
    public Guid CharacterId { get; init; }
    public string Result { get; init; } = string.Empty;
    public double Duration { get; init; }
    public StatisticsInfo Statistics { get; init; } = null!;
    public RewardsInfo Rewards { get; init; } = null!;
    public DateTime EndedAt { get; init; }
}

public record StatisticsInfo
{
    public int TotalDamage { get; init; }
    public double Dps { get; init; }
    public int KillCount { get; init; }
    public int DeathCount { get; init; }
}

public record RewardsInfo
{
    public int Gold { get; init; }
    public int Experience { get; init; }
    public List<ItemInfo> Items { get; init; } = new();
}

public record ItemInfo
{
    public string ItemId { get; init; } = string.Empty;
    public int Quantity { get; init; }
    public string Rarity { get; init; } = string.Empty;
}
```

---

**2. Blazor 组件使用示例**

```razor
@* Pages/Battle.razor *@
@page "/battle"
@inject BattleNotificationService NotificationService
@inject ILogger<Battle> Logger
@implements IAsyncDisposable

<h3>Battle</h3>

@if (_currentBattle != null)
{
    <div class="battle-info">
        <h4>Battle ID: @_currentBattle.BattleId</h4>
        <p>Mode: @_currentBattle.Mode</p>
        <p>Enemy: @_currentBattle.Enemy.Id (x@_currentBattle.Enemy.Count)</p>
        <p>Time: @_battleTime.ToString("F2")s</p>
    </div>

    <div class="battle-stats">
        <h5>Combat Stats</h5>
        <p>Total Damage: @_totalDamage</p>
        <p>DPS: @(_battleTime > 0 ? (_totalDamage / _battleTime).ToString("F2") : "0")</p>
        <p>Segments Received: @_segmentCount</p>
    </div>

    @if (_latestSegment != null)
    {
        <div class="latest-segment">
            <h5>Latest Segment</h5>
            <p>Duration: @(_latestSegment.EndTime - _latestSegment.StartTime)s</p>
            <p>Damage: @_latestSegment.TotalDamage</p>
            <p>Events: @_latestSegment.EventCount</p>
            
            <h6>Damage by Source:</h6>
            <ul>
                @foreach (var (source, damage) in _latestSegment.DamageBySource)
                {
                    <li>@source: @damage</li>
                }
            </ul>
        </div>
    }
}
else if (_battleResult != null)
{
    <div class="battle-result">
        <h4>Battle Ended: @_battleResult.Result</h4>
        <p>Duration: @_battleResult.Duration.ToString("F2")s</p>
        
        <h5>Statistics</h5>
        <p>Total Damage: @_battleResult.Statistics.TotalDamage</p>
        <p>DPS: @_battleResult.Statistics.Dps.ToString("F2")</p>
        <p>Kills: @_battleResult.Statistics.KillCount</p>
        
        <h5>Rewards</h5>
        <p>Gold: @_battleResult.Rewards.Gold</p>
        <p>Experience: @_battleResult.Rewards.Experience</p>
        @if (_battleResult.Rewards.Items.Any())
        {
            <h6>Items:</h6>
            <ul>
                @foreach (var item in _battleResult.Rewards.Items)
                {
                    <li>@item.ItemId x@item.Quantity (@item.Rarity)</li>
                }
            </ul>
        }
    </div>
}
else
{
    <p>No active battle</p>
}

@code {
    private BattleStartedNotification? _currentBattle;
    private BattleEndedNotification? _battleResult;
    private SegmentInfo? _latestSegment;
    private int _totalDamage;
    private double _battleTime;
    private int _segmentCount;

    protected override async Task OnInitializedAsync()
    {
        // 订阅通知事件
        NotificationService.OnBattleStarted += HandleBattleStarted;
        NotificationService.OnCombatSegmentUpdate += HandleCombatSegmentUpdate;
        NotificationService.OnBattleEnded += HandleBattleEnded;

        // 连接到通知服务（使用当前用户ID）
        var userId = Guid.Parse("your-user-id"); // TODO: 从认证获取
        await NotificationService.ConnectAsync(userId);
    }

    private void HandleBattleStarted(BattleStartedNotification notification)
    {
        Logger.LogInformation("Battle started: {BattleId}", notification.BattleId);
        
        _currentBattle = notification;
        _battleResult = null;
        _totalDamage = 0;
        _battleTime = 0;
        _segmentCount = 0;
        _latestSegment = null;

        // 订阅这个战斗的更新
        _ = NotificationService.SubscribeToBattleAsync(notification.BattleId);

        StateHasChanged();
    }

    private void HandleCombatSegmentUpdate(CombatSegmentNotification notification)
    {
        Logger.LogDebug("Combat segment update: {BattleId} @ {Time}",
            notification.BattleId, notification.BattleTime);

        _latestSegment = notification.Segment;
        _totalDamage += notification.Segment.TotalDamage;
        _battleTime = notification.BattleTime;
        _segmentCount++;

        StateHasChanged();
    }

    private void HandleBattleEnded(BattleEndedNotification notification)
    {
        Logger.LogInformation("Battle ended: {BattleId} - {Result}",
            notification.BattleId, notification.Result);

        _battleResult = notification;
        _currentBattle = null;

        // 取消订阅
        _ = NotificationService.UnsubscribeFromBattleAsync(notification.BattleId);

        StateHasChanged();
    }

    public async ValueTask DisposeAsync()
    {
        NotificationService.OnBattleStarted -= HandleBattleStarted;
        NotificationService.OnCombatSegmentUpdate -= HandleCombatSegmentUpdate;
        NotificationService.OnBattleEnded -= HandleBattleEnded;
    }
}
```

---

## 性能优化

### 1. Segment 聚合优化

**问题**: CombatSegment 每 5 秒或 200 事件推送一次，高频战斗可能产生大量消息

**优化方案**:

```csharp
// Infrastructure/Messaging/SegmentBatchingDispatcher.cs
public class SegmentBatchingDispatcher : IHostedService
{
    private readonly IDomainEventBus _eventBus;
    private readonly ISignalRDispatcher _dispatcher;
    private readonly ConcurrentDictionary<Guid, SegmentBatch> _batches = new();
    private readonly PeriodicTimer _flushTimer;

    public SegmentBatchingDispatcher(
        IDomainEventBus eventBus,
        ISignalRDispatcher dispatcher)
    {
        _eventBus = eventBus;
        _dispatcher = dispatcher;
        _flushTimer = new PeriodicTimer(TimeSpan.FromSeconds(1)); // 1秒批量推送
    }

    public Task StartAsync(CancellationToken ct)
    {
        // 订阅 CombatSegmentFlushedEvent
        _eventBus.Subscribe<CombatSegmentFlushedEvent>(HandleSegmentEvent);

        // 启动定时刷新
        _ = FlushBatchesAsync(ct);

        return Task.CompletedTask;
    }

    private Task HandleSegmentEvent(CombatSegmentFlushedEvent @event)
    {
        var batch = _batches.GetOrAdd(@event.BattleId, _ => new SegmentBatch
        {
            BattleId = @event.BattleId,
            CharacterId = @event.CharacterId
        });

        batch.AddSegment(@event.SegmentData);
        return Task.CompletedTask;
    }

    private async Task FlushBatchesAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && await _flushTimer.WaitForNextTickAsync(ct))
        {
            var batches = _batches.ToArray();
            foreach (var (battleId, batch) in batches)
            {
                if (batch.ShouldFlush())
                {
                    await SendBatchAsync(batch);
                    _batches.TryRemove(battleId, out _);
                }
            }
        }
    }

    private async Task SendBatchAsync(SegmentBatch batch)
    {
        if (!batch.CharacterId.HasValue) return;

        var message = new
        {
            battleId = batch.BattleId,
            segments = batch.GetSegments(),
            aggregated = new
            {
                totalDamage = batch.TotalDamage,
                totalEvents = batch.TotalEvents
            }
        };

        await _dispatcher.SendToUserAsync(
            batch.CharacterId.Value,
            "OnCombatSegmentBatch",
            message);
    }

    public Task StopAsync(CancellationToken ct)
    {
        return Task.CompletedTask;
    }

    private class SegmentBatch
    {
        private readonly List<CombatSegmentData> _segments = new();
        public Guid BattleId { get; init; }
        public Guid? CharacterId { get; init; }
        public int TotalDamage { get; private set; }
        public int TotalEvents { get; private set; }

        public void AddSegment(CombatSegmentData segment)
        {
            lock (_segments)
            {
                _segments.Add(segment);
                TotalDamage += segment.TotalDamage;
                TotalEvents += segment.EventCount;
            }
        }

        public bool ShouldFlush()
        {
            lock (_segments)
            {
                return _segments.Count > 0;
            }
        }

        public List<CombatSegmentData> GetSegments()
        {
            lock (_segments)
            {
                return _segments.ToList();
            }
        }
    }
}
```

---

### 2. 客户端节流

**问题**: 前端更新频率过高可能导致卡顿

**优化方案**: 使用 RxJS 或 System.Reactive 节流更新

```csharp
// BlazorIdle/Services/BattleNotificationService.cs
using System.Reactive.Linq;
using System.Reactive.Subjects;

public class BattleNotificationService
{
    private readonly Subject<CombatSegmentNotification> _segmentSubject = new();
    
    public IObservable<CombatSegmentNotification> SegmentUpdates => 
        _segmentSubject
            .Sample(TimeSpan.FromMilliseconds(200)) // 每200ms最多更新一次
            .ObserveOn(SynchronizationContext.Current!);

    private void OnSegmentReceived(CombatSegmentNotification notification)
    {
        _segmentSubject.OnNext(notification);
    }
}
```

---

### 3. 消息压缩

**优化**: 对大型消息启用压缩

```csharp
// Program.cs
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = true;
})
.AddMessagePackProtocol(); // 使用 MessagePack 替代 JSON（可选）

// 或使用 Gzip 压缩
builder.Services.AddResponseCompression(options =>
{
    options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(
        new[] { "application/octet-stream" });
});
```

---

## 实施步骤

### Week 1: 事件定义和发布

#### Day 1-2: 创建事件定义

**任务清单**:
- [ ] 创建 `Domain/Events/Combat/` 目录
- [ ] 实现 `CombatSegmentFlushedEvent`
- [ ] 实现 `BattleStartedEvent`
- [ ] 实现 `BattleEndedEvent`
- [ ] 编写事件单元测试

---

#### Day 3-4: 集成事件发布

**任务清单**:
- [ ] 修改 `SegmentCollector` 添加回调
- [ ] 修改 `StepBattleCoordinator` 注入事件总线
- [ ] 在 `Start()` 发布 `BattleStartedEvent`
- [ ] 在 Segment Flush 时发布事件
- [ ] 在 `Stop()` 发布 `BattleEndedEvent`
- [ ] 验证事件正确发布

---

#### Day 5: 订阅和转发

**任务清单**:
- [ ] 创建 `NotificationEventSubscriber`
- [ ] 订阅战斗事件
- [ ] 转发到 SignalR
- [ ] 添加日志和监控
- [ ] 集成测试

---

### Week 2: 客户端集成

#### Day 6-7: Blazor 客户端服务

**任务清单**:
- [ ] 创建 `BattleNotificationService`
- [ ] 实现 SignalR 连接管理
- [ ] 实现事件订阅
- [ ] 实现自动重连
- [ ] 实现心跳机制
- [ ] 单元测试

---

#### Day 8-9: UI 集成

**任务清单**:
- [ ] 创建战斗页面组件
- [ ] 订阅通知服务
- [ ] 实现实时状态更新
- [ ] 添加战斗动画/效果
- [ ] UI 测试

---

#### Day 10: 性能优化

**任务清单**:
- [ ] 实现 Segment 批量推送
- [ ] 实现客户端节流
- [ ] 压力测试
- [ ] 性能调优
- [ ] 文档更新

---

### Week 3 (可选): 高级功能

#### 离线战斗回放

**任务清单**:
- [ ] 实现 `BattleStateSnapshotEvent`
- [ ] 定时快照机制
- [ ] 客户端回放UI
- [ ] 测试验证

---

## 验收标准

### Phase 2 完成标志

- [ ] 战斗开始时客户端收到通知
- [ ] 战斗过程中实时接收 Segment 更新
- [ ] 战斗结束时收到完整结果
- [ ] 所有事件包含正确数据
- [ ] 消息延迟 < 500ms
- [ ] 客户端UI流畅无卡顿
- [ ] 所有测试通过
- [ ] 文档完整

---

## 下一步

Phase 2 完成后，继续阅读 [Phase3-扩展性设计.md](./Phase3-扩展性设计.md)
