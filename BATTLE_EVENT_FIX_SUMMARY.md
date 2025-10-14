# 战斗事件 JSON 反序列化修复总结

## 问题描述

前端收到的战斗事件 `HandleBattleEvent` 内容是 JSON，但是 `HandleBattleMessageEvent` 和 `HandleBattleEvent` 方法中的 switch 语句尝试匹配的是 DTO 类型，导致模式匹配失败，无法正确显示战斗信息。

## 问题根源

当 SignalR 客户端使用 `_connection.On<object>("BattleEvent", OnBattleEvent)` 注册事件处理器时：

1. 服务器端发送具体的 DTO 对象（如 `AttackStartedEventDto`, `DamageAppliedEventDto` 等）
2. SignalR 自动将这些对象序列化为 JSON 并通过网络传输
3. 客户端 SignalR 接收到 JSON 数据
4. 因为注册时使用的是 `object` 类型，SignalR 默认将 JSON 反序列化为 `JsonElement` 类型
5. `JsonElement` 无法匹配 switch 语句中的具体 DTO 类型（如 `case AttackStartedEventDto`）
6. 所有 case 分支都不匹配，战斗事件无法被正确处理

## 解决方案

在 `BattleSignalRService.cs` 的 `OnBattleEvent` 方法中添加类型转换逻辑：

### 实现步骤

1. **检测 JsonElement**: 判断接收到的 `eventData` 是否为 `JsonElement` 类型
2. **提取 EventType**: 从 JSON 中读取 `EventType` 属性
3. **类型映射**: 根据 `EventType` 值，将 `JsonElement` 反序列化为对应的具体 DTO 类型
4. **传递类型化对象**: 将正确类型的 DTO 传递给所有注册的事件处理器

### 核心代码

```csharp
private void OnBattleEvent(object eventData)
{
    object? typedEvent = null;
    
    if (eventData is JsonElement jsonElement)
    {
        if (jsonElement.TryGetProperty("EventType", out var eventTypeProperty))
        {
            var eventType = eventTypeProperty.GetString();
            
            typedEvent = eventType switch
            {
                "AttackTick" => jsonElement.Deserialize<AttackTickEventDto>(),
                "SkillCastComplete" => jsonElement.Deserialize<SkillCastCompleteEventDto>(),
                "DamageApplied" => jsonElement.Deserialize<DamageAppliedEventDto>(),
                "AttackStarted" => jsonElement.Deserialize<AttackStartedEventDto>(),
                "DamageReceived" => jsonElement.Deserialize<DamageReceivedEventDto>(),
                "EnemyAttackStarted" => jsonElement.Deserialize<AttackStartedEventDto>(),
                "PlayerDeath" => jsonElement.Deserialize<PlayerDeathEventDto>(),
                "EnemyKilled" => jsonElement.Deserialize<EnemyKilledEventDto>(),
                "TargetSwitched" => jsonElement.Deserialize<TargetSwitchedEventDto>(),
                _ => eventData
            };
        }
    }
    else
    {
        typedEvent = eventData;
    }
    
    // 传递类型化的事件给处理器
    if (typedEvent != null)
    {
        foreach (var handler in _battleEventHandlers)
        {
            handler(typedEvent);
        }
    }
}
```

## 修改的文件

1. **BlazorIdle/Services/BattleSignalRService.cs**
   - 添加 `using System.Text.Json;` 引用
   - 重写 `OnBattleEvent` 方法，添加类型转换逻辑

2. **tests/BlazorIdle.Tests/BattleEventDeserializationTests.cs** (新增)
   - 添加 5 个单元测试验证 JSON 反序列化功能
   - 测试各种 DTO 类型的反序列化
   - 验证 EventType 属性访问

## 验证结果

✅ 所有单元测试通过 (5/5)
✅ 编译成功，无错误
✅ 类型匹配现在可以正常工作

## 影响范围

### 直接影响
- `Characters.razor` 中的 `HandleBattleMessageEvent` 方法现在可以正确匹配战斗事件类型
- 战斗日志可以正确显示攻击、伤害等信息
- 增量更新功能（进度条同步）可以正确处理事件

### 不影响
- 服务器端代码保持不变
- 其他 SignalR 事件处理（如 StateChanged）保持不变
- DTO 定义保持不变

## 测试用例覆盖

- ✅ `AttackStartedEventDto` 反序列化
- ✅ `DamageAppliedEventDto` 反序列化
- ✅ `DamageReceivedEventDto` 反序列化
- ✅ `AttackTickEventDto` 反序列化
- ✅ EventType 属性访问

## 扩展性

如果将来添加新的战斗事件类型，只需要：

1. 在 `BlazorIdle.Shared/Models/BattleNotifications.cs` 中定义新的 DTO 类
2. 在 `OnBattleEvent` 方法的 switch 表达式中添加新的映射
3. 在 `HandleBattleEvent` 或 `HandleBattleMessageEvent` 中添加对应的处理逻辑

## 总结

这个修复解决了 SignalR 在使用 `object` 类型注册事件时，JSON 反序列化为 `JsonElement` 而非具体类型的问题。通过在接收端添加类型识别和转换逻辑，确保前端能够正确处理所有战斗事件，显示完整的战斗信息。
