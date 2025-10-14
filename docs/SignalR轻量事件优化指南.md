# SignalR 轻量事件优化指南

## 概述

本文档描述了 SignalR 轻量事件系统的实现，用于优化前端进度条的实时更新，降低战斗信息的延迟。

## 架构设计

### 1. 事件类型

系统支持三种轻量级事件：

#### 1.1 AttackTickEvent (攻击触发事件)
**用途**: 每次攻击触发时通知前端更新进度条

**数据结构**:
```csharp
public sealed class AttackTickEventDto : BattleEventDto
{
    public double NextTriggerAt { get; set; }  // 下次攻击触发时间
    public double Interval { get; set; }        // 攻击间隔
}
```

**触发时机**: `AttackTickEvent.Execute()` 完成后

**数据量**: ~50 bytes

#### 1.2 SkillCastCompleteEvent (技能施放完成事件)
**用途**: 技能施放完成时通知前端

**数据结构**:
```csharp
public sealed class SkillCastCompleteEventDto : BattleEventDto
{
    public string SkillId { get; set; }         // 技能 ID
    public double CastCompleteAt { get; set; }  // 施放完成时间
}
```

**触发时机**: `SkillCastCompleteEvent.Execute()` 完成后

**数据量**: ~60 bytes

#### 1.3 DamageAppliedEvent (伤害应用事件)
**用途**: 伤害应用时实时反馈（可选，默认禁用）

**数据结构**:
```csharp
public sealed class DamageAppliedEventDto : BattleEventDto
{
    public string Source { get; set; }          // 伤害来源
    public int Damage { get; set; }             // 伤害值
    public bool IsCrit { get; set; }            // 是否暴击
    public int TargetCurrentHp { get; set; }    // 目标当前血量
    public int TargetMaxHp { get; set; }        // 目标最大血量
}
```

**触发时机**: 伤害应用后（需要在 DamageCalculator 中添加）

**数据量**: ~80 bytes

**注意**: 此事件频率高，默认禁用以保护性能

## 2. 配置系统

### 2.1 后端配置

配置文件: `BlazorIdle.Server/Config/SignalR/signalr-config.json`

```json
{
  "Notification": {
    "EnableAttackTickNotification": true,
    "EnableSkillCastCompleteNotification": true,
    "EnableDamageAppliedNotification": false
  }
}
```

**配置说明**:
- `EnableAttackTickNotification`: 启用攻击触发事件通知（推荐启用）
- `EnableSkillCastCompleteNotification`: 启用技能施放完成通知（推荐启用）
- `EnableDamageAppliedNotification`: 启用伤害应用通知（高频，默认禁用）

### 2.2 前端配置

配置文件: `BlazorIdle/wwwroot/config/progress-bar-config.json`

```json
{
  "SignalRIncrementalUpdate": {
    "EnableIncrementalUpdate": true,
    "EnableAttackTickUpdate": true,
    "EnableSkillCastUpdate": true,
    "EnableDamageAppliedUpdate": false,
    "ClientPredictionEnabled": true,
    "MaxPredictionAheadMs": 500,
    "SyncThresholdMs": 100,
    "ResetProgressOnMismatch": true
  },
  "Debug": {
    "LogSignalREvents": false,
    "LogIncrementalUpdates": false
  }
}
```

**配置说明**:
- `EnableIncrementalUpdate`: 总开关，控制是否启用增量更新
- `EnableAttackTickUpdate`: 启用攻击触发事件增量更新
- `EnableSkillCastUpdate`: 启用技能施放事件增量更新
- `EnableDamageAppliedUpdate`: 启用伤害应用事件更新
- `ClientPredictionEnabled`: 启用客户端预测（在事件间隙补偿）
- `MaxPredictionAheadMs`: 最大预测提前时间（毫秒）
- `SyncThresholdMs`: 同步阈值，超过此值则重新同步
- `ResetProgressOnMismatch`: 服务器数据不匹配时是否重置进度
- `LogSignalREvents`: 记录 SignalR 事件（调试用）
- `LogIncrementalUpdates`: 记录增量更新过程（调试用）

## 3. 工作原理

### 3.1 传统轮询模式

```
前端 ----轮询(500ms)----> 后端
     <---完整状态-------
     
延迟: 平均 250ms，最高 500ms
带宽: 每次 ~2KB
```

### 3.2 增量更新模式

```
攻击触发 ----SignalR----> 前端
         <--轻量事件(~50B)--
         
延迟: 平均 <50ms
带宽: 每次 ~50 bytes
轮询: 降低频率为备选方案
```

### 3.3 客户端预测机制

```
Time: 0s    0.5s   1.0s   1.5s   2.0s
      |      |      |      |      |
Event:[Attack]              [Attack]
      ↓                     ↓
Pred: 0%→100%→loop→100%→0%→100%
                           ↑
                      SignalR纠正
```

当收到 SignalR 事件时：
1. 立即更新进度条状态（NextTriggerAt, Interval）
2. 重置客户端时间基准
3. 客户端在事件间隙使用插值预测

## 4. 性能优势

### 4.1 延迟对比

| 场景 | 轮询模式 | 增量更新模式 | 改善 |
|-----|---------|------------|-----|
| 攻击触发 | 250ms (avg) | <50ms | 5倍+ |
| 技能施放 | 250ms (avg) | <50ms | 5倍+ |
| 目标切换 | 250ms (avg) | <50ms | 5倍+ |

### 4.2 带宽对比

| 场景 | 轮询模式 | 增量更新模式 | 改善 |
|-----|---------|------------|-----|
| 攻击触发 | 2KB | 50B | 40倍 |
| 技能施放 | 2KB | 60B | 33倍 |

### 4.3 服务器负载

- 轮询模式: 每秒处理 N 个完整状态请求
- 增量模式: 仅推送变化事件，按需触发

## 5. 使用指南

### 5.1 开发模式调试

启用调试日志以查看事件流:

```json
{
  "Debug": {
    "LogSignalREvents": true,
    "LogIncrementalUpdates": true
  }
}
```

控制台输出示例:
```
[SignalR] AttackTick: NextAt=12.34, Interval=2.00
[SignalR] 增量更新: 进度条已更新
```

### 5.2 性能调优

**低网络延迟环境**:
```json
{
  "MaxPredictionAheadMs": 300,
  "SyncThresholdMs": 50
}
```

**高网络延迟环境**:
```json
{
  "MaxPredictionAheadMs": 800,
  "SyncThresholdMs": 200
}
```

**移动设备**:
```json
{
  "EnableDamageAppliedUpdate": false,
  "ClientPredictionEnabled": true
}
```

### 5.3 故障降级

如果 SignalR 连接失败，系统自动降级到轮询模式：

```
SignalR 不可用
    ↓
自动切换到轮询
    ↓
功能正常（延迟略高）
```

## 6. 扩展指南

### 6.1 添加新事件类型

1. 定义 DTO (`BlazorIdle.Shared/Models/BattleNotifications.cs`):
```csharp
public sealed class NewEventDto : BattleEventDto
{
    public string CustomData { get; set; }
}
```

2. 在事件触发点发送通知:
```csharp
if (context.NotificationService?.IsAvailable == true)
{
    var eventDto = new NewEventDto
    {
        BattleId = context.Battle.Id,
        EventTime = ExecuteAt,
        EventType = "NewEvent",
        CustomData = "value"
    };
    _ = context.NotificationService.NotifyEventAsync(context.Battle.Id, eventDto);
}
```

3. 添加配置选项 (`SignalROptions.cs`):
```csharp
public bool EnableNewEventNotification { get; set; } = false;
```

4. 前端添加处理器 (`Characters.razor`):
```csharp
case NewEventDto newEvent:
    if (config.EnableNewEventUpdate)
    {
        await HandleNewEvent(newEvent);
    }
    break;
```

### 6.2 自定义处理逻辑

在 `Characters.razor` 中实现自定义处理:

```csharp
private async Task HandleCustomEvent(CustomEventDto evt)
{
    // 1. 验证事件属于当前战斗
    if (!IsCurrentBattle(evt.BattleId))
        return;
    
    // 2. 更新本地状态
    UpdateLocalState(evt);
    
    // 3. 刷新 UI
    await InvokeAsync(StateHasChanged);
}
```

## 7. 故障排查

### 7.1 事件未收到

检查清单:
- [ ] SignalR 连接是否成功（查看浏览器控制台）
- [ ] 后端配置是否启用对应事件
- [ ] 前端配置是否启用对应事件
- [ ] 是否正确订阅了战斗 ID

### 7.2 进度条不准确

检查清单:
- [ ] 客户端时间是否同步
- [ ] MaxPredictionAheadMs 是否过大
- [ ] SyncThresholdMs 是否合理
- [ ] 轮询是否正常作为备选

### 7.3 性能问题

检查清单:
- [ ] 是否错误启用了 DamageAppliedNotification
- [ ] 日志是否关闭（生产环境）
- [ ] 客户端预测是否过于频繁

## 8. 最佳实践

### 8.1 事件选择

| 场景 | 推荐事件 | 原因 |
|-----|---------|-----|
| 进度条更新 | AttackTick | 频率适中，数据量小 |
| 技能冷却 | SkillCastComplete | 低频，有意义 |
| 实时伤害 | DamageApplied | 高频，仅调试用 |

### 8.2 配置建议

**生产环境**:
```json
{
  "EnableAttackTickNotification": true,
  "EnableSkillCastCompleteNotification": true,
  "EnableDamageAppliedNotification": false,
  "Debug": {
    "LogSignalREvents": false,
    "LogIncrementalUpdates": false
  }
}
```

**开发环境**:
```json
{
  "EnableAttackTickNotification": true,
  "EnableSkillCastCompleteNotification": true,
  "EnableDamageAppliedNotification": true,
  "Debug": {
    "LogSignalREvents": true,
    "LogIncrementalUpdates": true
  }
}
```

## 9. 总结

SignalR 轻量事件系统通过推送小粒度事件，显著降低了前端战斗信息的延迟，同时保持了良好的可扩展性和配置灵活性。系统的主要优势：

1. **低延迟**: <50ms vs 250ms (5倍改善)
2. **低带宽**: 50 bytes vs 2KB (40倍改善)
3. **可配置**: 所有参数外部化到配置文件
4. **可扩展**: 易于添加新事件类型
5. **可靠性**: 自动降级到轮询模式
6. **兼容性**: 与现有系统无缝集成
