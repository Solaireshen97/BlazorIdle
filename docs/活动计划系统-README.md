# 活动计划系统 (Activity Plan System)

## 简介

活动计划系统是 BlazorIdle 的核心功能之一，允许玩家创建和管理多个活动计划（如战斗、采集、制作），支持队列执行、自动衔接和多种限制条件。

## 快速开始

### 1. 创建活动计划

```bash
curl -X POST http://localhost:5000/api/activities/plans \
  -H "Content-Type: application/json" \
  -d '{
    "characterId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "slotIndex": 0,
    "type": "combat",
    "limitType": "duration",
    "limitValue": 3600,
    "payloadJson": "{\"enemyId\":\"dummy\",\"mode\":\"duration\"}"
  }'
```

### 2. 查询槽位状态

```bash
curl http://localhost:5000/api/activities/characters/3fa85f64-5717-4562-b3fc-2c963f66afa6/slots
```

### 3. 取消活动

```bash
curl -X POST http://localhost:5000/api/activities/plans/{planId}/cancel
```

## 核心概念

### 活动类型 (ActivityType)

- **Combat (战斗)**: 当前支持，封装现有战斗系统
- **Gather (采集)**: 未来扩展
- **Craft (制作)**: 未来扩展

### 状态机 (ActivityState)

```
Pending (待定) → Running (运行中) → Completed (已完成)
                              ↓
                         Cancelled (已取消)
```

### 限制条件 (LimitSpec)

1. **DurationLimit (时长限制)**: 按模拟时间（秒）限制
2. **CountLimit (计数限制)**: 按击杀数、采集次数等限制
3. **InfiniteLimit (无限制)**: 需要手动停止

### 槽位系统 (ActivitySlot)

- 每个角色有 3-5 个活动槽位（可配置）
- 每个槽位可以排队多个活动计划
- 当前活动完成后，自动执行队列中的下一个

## 典型使用场景

### 场景1：先打1小时A怪，再打1小时B怪

```csharp
// 创建第一个计划（立即执行）
var plan1 = coordinator.CreatePlan(
    characterId: characterId,
    slotIndex: 0,
    type: ActivityType.Combat,
    limit: new DurationLimit(3600),
    payloadJson: JsonSerializer.Serialize(new { enemyId = "dummy" })
);

// 创建第二个计划（进入队列，自动衔接）
var plan2 = coordinator.CreatePlan(
    characterId: characterId,
    slotIndex: 0,
    type: ActivityType.Combat,
    limit: new DurationLimit(3600),
    payloadJson: JsonSerializer.Serialize(new { enemyId = "tank" })
);
```

### 场景2：击杀100个敌人

```csharp
var plan = coordinator.CreatePlan(
    characterId: characterId,
    slotIndex: 0,
    type: ActivityType.Combat,
    limit: new CountLimit(100),
    payloadJson: JsonSerializer.Serialize(new { 
        enemyId = "dummy",
        mode = "continuous" // 持续模式，敌人死亡后自动刷新
    })
);
```

### 场景3：多槽位并行

```csharp
// 槽位0：战斗
coordinator.CreatePlan(characterId, 0, ActivityType.Combat, ...);

// 槽位1：采集（未来功能）
coordinator.CreatePlan(characterId, 1, ActivityType.Gather, ...);

// 槽位2：制作（未来功能）
coordinator.CreatePlan(characterId, 2, ActivityType.Craft, ...);
```

## 架构

```
┌─────────────────────────────────────────────────────────┐
│                   ActivitiesController                   │ API层
│          (RESTful API for activity management)           │
└─────────────────────────────────────────────────────────┘
                            │
┌─────────────────────────────────────────────────────────┐
│               ActivityCoordinator                        │
│  (Manages all activities, slots, and state transitions)  │ 应用层
└─────────────────────────────────────────────────────────┘
                            │
        ┌───────────────────┼───────────────────┐
        │                   │                   │
┌───────────────┐  ┌────────────────┐  ┌───────────────┐
│CombatActivity │  │GatherActivity  │  │CraftActivity  │
│   Executor    │  │   Executor     │  │   Executor    │
│   (现有)      │  │   (未来)       │  │   (未来)      │
└───────────────┘  └────────────────┘  └───────────────┘
        │
┌───────────────────────────────────────────────────────┐
│              StepBattleCoordinator                     │ 现有系统
│         (Existing battle system, no changes)           │ (零修改)
└───────────────────────────────────────────────────────┘
```

## 文档

- **[实现总结](./活动计划系统-实现总结.md)**: 详细的架构设计和实现说明
- **[API使用指南](./活动计划系统-API使用指南.md)**: 完整的API端点和使用示例
- **[扩展指南](./活动计划系统-扩展指南.md)**: 如何添加新功能和活动类型

## 测试

```bash
# 运行所有活动计划测试
dotnet test --filter "FullyQualifiedName~ActivityPlanTests"
```

测试覆盖：
- ✅ 状态机转换（11个测试用例）
- ✅ 限制条件判断
- ✅ 槽位队列管理
- ✅ 边界条件和异常情况

## 配置

在 `appsettings.json` 中配置：

```json
{
  "Activity": {
    "AdvanceIntervalSeconds": 1.0,    // 活动推进间隔
    "PruneIntervalMinutes": 10.0,     // 清理间隔
    "SlotsPerCharacter": 3            // 每个角色的槽位数量
  }
}
```

## 特性

✅ **零侵入集成**: 现有战斗系统无需修改  
✅ **状态机管理**: 严格的状态转换和异常处理  
✅ **多种限制**: 时长、计数、无限制，易于扩展  
✅ **自动衔接**: 队列自动执行，无需手动干预  
✅ **高扩展性**: 清晰的接口设计，支持新活动类型  
✅ **线程安全**: ConcurrentDictionary，支持并发  
✅ **完整测试**: 11个测试用例全部通过  

## 未来规划

- [ ] 持久化层（数据库存储）
- [ ] 离线快进集成
- [ ] 前端UI界面
- [ ] 采集活动实现
- [ ] 制作活动实现
- [ ] 条件触发
- [ ] 循环队列
- [ ] 活动模板

## 贡献者

- 系统设计：基于《整合设计总结.txt》第8章
- 实现：GitHub Copilot Workspace
- 测试和文档：完整覆盖

## 许可证

与主项目相同

---

**需要帮助？** 查看[API使用指南](./活动计划系统-API使用指南.md)或[扩展指南](./活动计划系统-扩展指南.md)
