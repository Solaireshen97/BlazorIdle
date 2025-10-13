# SignalR 扩展开发指南

**版本**: 1.0  
**日期**: 2025-10-13  
**状态**: Stage 3 完成

---

## 📋 概述

本文档介绍如何扩展 SignalR 通知系统，包括自定义过滤器、动态事件注册等。

---

## 🎯 扩展点概览

SignalR 系统提供以下扩展点：

1. **通知过滤器** (`INotificationFilter`): 控制通知发送的决策逻辑
2. **事件类型**: 添加新的事件通知类型
3. **配置选项**: 扩展配置参数

---

## 🔌 通知过滤器

### 概念

通知过滤器允许你在发送通知前执行自定义逻辑，决定是否应该发送通知。

### 接口定义

```csharp
public interface INotificationFilter
{
    /// <summary>
    /// 过滤器名称
    /// </summary>
    string Name { get; }

    /// <summary>
    /// 过滤器优先级（数字越小优先级越高）
    /// </summary>
    int Priority { get; }

    /// <summary>
    /// 判断是否应该发送通知
    /// </summary>
    bool ShouldNotify(NotificationFilterContext context);
}
```

### 内置过滤器

#### 1. EventTypeFilter (Priority 10)

根据配置检查事件类型是否启用。

**使用场景**: 配置驱动的事件类型控制

**实现**:
```csharp
public sealed class EventTypeFilter : INotificationFilter
{
    public int Priority => 10;
    
    public bool ShouldNotify(NotificationFilterContext context)
    {
        // 根据配置检查事件类型
        return IsEventTypeEnabled(context.EventType);
    }
}
```

#### 2. RateLimitFilter (Priority 20)

基于时间窗口的速率限制。

**使用场景**: 防止高频事件导致的通知风暴

**实现**:
```csharp
public sealed class RateLimitFilter : INotificationFilter
{
    private readonly NotificationThrottler? _throttler;
    
    public int Priority => 20;
    
    public bool ShouldNotify(NotificationFilterContext context)
    {
        if (_throttler == null) return true;
        
        var key = $"battle_{context.BattleId}_{context.EventType}";
        return _throttler.ShouldSend(key);
    }
}
```

### 创建自定义过滤器

#### 示例 1: 时间段过滤器

仅在特定时间段内发送通知：

```csharp
public sealed class TimeRangeFilter : INotificationFilter
{
    public string Name => "TimeRangeFilter";
    public int Priority => 30; // 较低优先级
    
    public bool ShouldNotify(NotificationFilterContext context)
    {
        var hour = DateTime.UtcNow.Hour;
        
        // 仅在 8:00-22:00 之间发送通知
        return hour >= 8 && hour < 22;
    }
}
```

#### 示例 2: 用户级别过滤器

根据用户级别决定是否发送通知：

```csharp
public sealed class UserLevelFilter : INotificationFilter
{
    private readonly IUserService _userService;
    
    public UserLevelFilter(IUserService userService)
    {
        _userService = userService;
    }
    
    public string Name => "UserLevelFilter";
    public int Priority => 15;
    
    public bool ShouldNotify(NotificationFilterContext context)
    {
        // 从元数据获取用户ID
        var userId = context.GetMetadata<Guid>("UserId");
        var user = _userService.GetUser(userId);
        
        // 仅向 VIP 用户发送特定事件
        if (context.EventType == "RareDropNotification")
        {
            return user.IsVip;
        }
        
        return true;
    }
}
```

#### 示例 3: 条件组合过滤器

组合多个条件：

```csharp
public sealed class CompositeFilter : INotificationFilter
{
    public string Name => "CompositeFilter";
    public int Priority => 25;
    
    public bool ShouldNotify(NotificationFilterContext context)
    {
        // 条件 1: 事件类型检查
        if (context.EventType == "MinorEvent")
        {
            // 条件 2: 检查战斗是否重要
            var isImportantBattle = context.GetMetadata<bool>("IsImportant");
            if (!isImportantBattle)
            {
                return false; // 非重要战斗的小事件不通知
            }
        }
        
        // 条件 3: 检查通知频率
        var lastNotifyTime = context.GetMetadata<DateTime>("LastNotifyTime");
        if ((DateTime.UtcNow - lastNotifyTime).TotalSeconds < 5)
        {
            return false; // 5 秒内不重复通知
        }
        
        return true;
    }
}
```

### 注册过滤器

在 `Program.cs` 或 `DependencyInjection.cs` 中注册：

```csharp
// 注册内置过滤器
builder.Services.AddSingleton<INotificationFilter, EventTypeFilter>();
builder.Services.AddSingleton<INotificationFilter, RateLimitFilter>();

// 注册自定义过滤器
builder.Services.AddSingleton<INotificationFilter, TimeRangeFilter>();
builder.Services.AddSingleton<INotificationFilter, UserLevelFilter>();

// 注册过滤器管道
builder.Services.AddSingleton<NotificationFilterPipeline>();
```

### 过滤器管道

过滤器按优先级顺序执行，任何一个过滤器返回 `false` 都会阻止通知发送。

**执行流程**:
```
1. EventTypeFilter (Priority 10) ✅ 通过
2. UserLevelFilter (Priority 15) ✅ 通过
3. RateLimitFilter (Priority 20) ❌ 阻止
   → 通知被阻止，不再执行后续过滤器
```

**使用示例**:
```csharp
public async Task NotifyStateChangeAsync(Guid battleId, string eventType)
{
    var context = new NotificationFilterContext
    {
        BattleId = battleId,
        EventType = eventType
    };
    
    // 添加元数据
    context.AddMetadata("UserId", userId);
    context.AddMetadata("IsImportant", true);
    
    // 执行过滤器管道
    if (!_filterPipeline.Execute(context))
    {
        _logger.LogDebug("通知被过滤器阻止");
        return;
    }
    
    // 发送通知
    await _hubContext.Clients.Group($"battle_{battleId}")
        .SendAsync("StateChanged", ...);
}
```

---

## 🔔 添加新事件类型

### 步骤 1: 定义配置选项

在 `NotificationOptions` 中添加新事件的开关：

```csharp
public sealed class NotificationOptions
{
    // 现有配置...
    
    /// <summary>
    /// 启用宝箱发现通知（Phase 3）
    /// </summary>
    public bool EnableChestFoundNotification { get; set; } = false;
}
```

### 步骤 2: 更新 EventTypeFilter

在 `EventTypeFilter` 中添加新事件类型的检查：

```csharp
public bool ShouldNotify(NotificationFilterContext context)
{
    return context.EventType switch
    {
        // 现有事件...
        "ChestFound" => _options.Notification.EnableChestFoundNotification,
        _ => true
    };
}
```

### 步骤 3: 创建事件 DTO（可选）

为详细事件创建 DTO：

```csharp
public record ChestFoundEventDto(
    Guid BattleId,
    double EventTime,
    string ChestId,
    string ChestType,
    int[] PossibleRewards
) : BattleEventDto(BattleId, EventTime, "ChestFound");
```

### 步骤 4: 在业务逻辑中发送通知

在相应的事件处理代码中调用通知服务：

```csharp
// 在发现宝箱时
if (chestFound && _notificationService != null)
{
    await _notificationService.NotifyStateChangeAsync(battleId, "ChestFound");
    
    // 或发送详细数据
    await _notificationService.NotifyEventAsync(battleId, new ChestFoundEventDto(
        BattleId: battleId,
        EventTime: clock.CurrentTime,
        ChestId: chest.Id,
        ChestType: chest.Type,
        PossibleRewards: chest.GetPossibleRewards()
    ));
}
```

### 步骤 5: 前端订阅事件

在客户端订阅新事件：

```csharp
_signalR.OnChestFound(evt => 
{
    ShowNotification($"发现宝箱: {evt.ChestType}");
    await PlayChestAnimation();
});
```

### 步骤 6: 配置新事件

在 `appsettings.json` 中配置：

```json
{
  "SignalR": {
    "Notification": {
      "EnableChestFoundNotification": true
    }
  }
}
```

---

## 📝 元数据传递

### 什么是元数据

元数据允许过滤器之间传递信息，或向后续处理步骤提供上下文。

### 使用示例

```csharp
// 发送方添加元数据
var context = new NotificationFilterContext
{
    BattleId = battleId,
    EventType = "EnemyKilled"
};

context.AddMetadata("EnemyLevel", enemy.Level);
context.AddMetadata("IsElite", enemy.IsElite);
context.AddMetadata("DropCount", drops.Count);

// 过滤器读取元数据
public bool ShouldNotify(NotificationFilterContext context)
{
    var enemyLevel = context.GetMetadata<int>("EnemyLevel");
    var isElite = context.GetMetadata<bool>("IsElite");
    
    // 仅通知精英敌人或高等级敌人
    return isElite || enemyLevel >= 50;
}

// 后续过滤器可以添加更多元数据
context.AddMetadata("ShouldPlayAnimation", true);
```

---

## 🧪 测试自定义过滤器

### 单元测试示例

```csharp
[Fact]
public void TimeRangeFilter_DuringWorkingHours_ReturnsTrue()
{
    // Arrange
    var filter = new TimeRangeFilter();
    var context = new NotificationFilterContext
    {
        BattleId = Guid.NewGuid(),
        EventType = "EnemyKilled"
    };
    
    // Act - 假设当前时间在工作时间内
    var result = filter.ShouldNotify(context);
    
    // Assert
    // 注意: 这个测试依赖于当前时间，实际测试中应该注入时钟
    if (DateTime.UtcNow.Hour >= 8 && DateTime.UtcNow.Hour < 22)
    {
        Assert.True(result);
    }
    else
    {
        Assert.False(result);
    }
}
```

### 集成测试示例

```csharp
[Fact]
public void FilterPipeline_WithCustomFilter_BlocksNotification()
{
    // Arrange
    var filters = new List<INotificationFilter>
    {
        new EventTypeFilter(options),
        new TimeRangeFilter()
    };
    
    var pipeline = new NotificationFilterPipeline(filters, logger);
    var context = new NotificationFilterContext
    {
        BattleId = Guid.NewGuid(),
        EventType = "PlayerDeath"
    };
    
    // Act
    var result = pipeline.Execute(context);
    
    // Assert
    // 结果取决于当前时间
}
```

---

## 🔧 配置扩展

### 添加新配置节

如果需要为自定义过滤器添加配置：

```csharp
public sealed class CustomFilterOptions
{
    public const string SectionName = "CustomFilter";
    
    public bool EnableTimeRangeFilter { get; set; } = false;
    public int StartHour { get; set; } = 8;
    public int EndHour { get; set; } = 22;
}
```

在 `appsettings.json` 中：

```json
{
  "CustomFilter": {
    "EnableTimeRangeFilter": true,
    "StartHour": 9,
    "EndHour": 23
  }
}
```

注册配置：

```csharp
builder.Services.Configure<CustomFilterOptions>(
    builder.Configuration.GetSection(CustomFilterOptions.SectionName)
);
```

---

## 📊 性能考虑

### 过滤器性能建议

1. **尽早返回**: 优先级高的过滤器应该尽早阻止不需要的通知
2. **避免重复计算**: 缓存昂贵的计算结果
3. **异步操作**: 过滤器应该是同步的，避免异步调用
4. **异常处理**: 过滤器异常会被捕获，默认允许通知

### 性能监控

```csharp
public bool ShouldNotify(NotificationFilterContext context)
{
    var sw = Stopwatch.StartNew();
    
    try
    {
        // 过滤逻辑...
        return true;
    }
    finally
    {
        sw.Stop();
        if (sw.ElapsedMilliseconds > 10)
        {
            _logger.LogWarning(
                "过滤器 {FilterName} 执行时间过长: {Ms}ms",
                Name,
                sw.ElapsedMilliseconds
            );
        }
    }
}
```

---

## 📚 最佳实践

### 1. 过滤器命名

- 使用描述性的名称
- 后缀 `Filter`
- 示例: `EventTypeFilter`, `RateLimitFilter`, `UserLevelFilter`

### 2. 优先级设置

- 0-10: 关键过滤器（如配置检查）
- 11-20: 业务逻辑过滤器
- 21-30: 性能优化过滤器（如节流）
- 31+: 低优先级过滤器

### 3. 元数据键命名

- 使用 PascalCase
- 描述性的键名
- 示例: `UserId`, `IsImportant`, `LastNotifyTime`

### 4. 过滤器独立性

- 每个过滤器应该独立工作
- 不依赖其他过滤器的执行结果
- 通过元数据传递信息，而非共享状态

### 5. 测试覆盖

- 每个自定义过滤器至少 3 个测试：
  1. 允许通知的情况
  2. 阻止通知的情况
  3. 边界条件

---

## 🔗 相关文档

- [SignalR集成优化方案.md](./SignalR集成优化方案.md) - 完整技术设计
- [SignalR性能优化指南.md](./SignalR性能优化指南.md) - 性能优化
- [SignalR配置优化指南.md](./SignalR配置优化指南.md) - 配置详解

---

## 📞 技术支持

如有问题或建议：
1. 查看相关文档
2. 运行测试验证: `dotnet test --filter "FullyQualifiedName~NotificationFilter"`
3. 提交 Issue 或 PR

---

**编写人**: GitHub Copilot Agent  
**审核人**: -  
**版本**: 1.0  
**更新日期**: 2025-10-13
