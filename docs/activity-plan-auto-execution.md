# 活动计划自动执行功能

## 功能概述

活动计划系统现在支持自动任务执行和队列管理。当为角色添加活动计划时，系统会自动判断是否立即执行，无需手动启动。

## 核心特性

### 1. 自动启动首个任务

当创建一个新的活动计划时：
- 如果角色当前**没有正在运行的任务**，新计划会**自动启动**
- 如果角色已有正在运行的任务，新计划会保持 `Pending` 状态进入队列

### 2. 自动任务衔接

当一个任务完成或停止时：
- 系统会自动查找该角色的下一个待执行任务
- 如果存在待执行任务，会**自动启动**该任务
- 实现无缝的任务队列执行

### 3. 单任务执行限制

- 每个角色**同时只能执行一个任务**
- 这确保了战斗系统的一致性和资源管理

### 4. 队列优先级

待执行任务按以下顺序排列：
1. **槽位索引（SlotIndex）**：较小的槽位优先（0-4）
2. **创建时间（CreatedAt）**：同一槽位内，先创建的优先

## 使用方式

### 前端使用

前端只需要创建活动计划，无需手动启动：

```http
POST /api/activity-plans/combat?characterId={guid}&slotIndex=0&limitType=duration&limitValue=300&enemyId=goblin
```

系统会自动处理任务的执行和队列管理。

### API 流程示例

#### 场景 1：角色没有运行中的任务

```
1. POST /api/activity-plans/combat (创建任务A)
   → 任务A 自动启动为 Running 状态
   
2. POST /api/activity-plans/combat (创建任务B)
   → 任务B 保持 Pending 状态（因为A正在运行）
   
3. POST /api/activity-plans/{taskA-id}/stop (停止任务A)
   → 任务A 标记为 Completed
   → 任务B 自动启动为 Running 状态
```

#### 场景 2：添加多个任务

```
1. POST /api/activity-plans/combat (slotIndex=0) → 任务A 自动启动
2. POST /api/activity-plans/combat (slotIndex=1) → 任务B Pending
3. POST /api/activity-plans/combat (slotIndex=0) → 任务C Pending

当任务A完成后：
- 任务C 会先启动（slot 0 优先于 slot 1）
当任务C完成后：
- 任务B 会启动
```

## 技术实现

### 关键方法

1. **ActivityPlanService.CreatePlanAsync**
   - 创建计划后检查是否有运行中的任务
   - 如无运行任务，调用 `StartPlanAsync` 自动启动

2. **ActivityPlanService.StopPlanAsync**
   - 停止当前任务后调用 `TryStartNextPendingPlanAsync`
   - 自动查找并启动下一个待执行任务

3. **ActivityPlanRepository.GetNextPendingPlanAsync**
   - 返回角色的下一个待执行任务
   - 按 `SlotIndex` 升序，然后 `CreatedAt` 升序排序

### 状态机

```
Pending → Running → Completed/Cancelled
   ↑         |
   |         | (auto-start next)
   +---------+
```

## 错误处理

- 如果自动启动失败（例如角色数据异常），任务会保持 `Pending` 状态
- 不会影响任务的创建和存储
- 可以手动调用 `/api/activity-plans/{id}/start` 重试启动

## 注意事项

1. **任务限制条件**：确保设置合理的 `LimitType` 和 `LimitValue`，避免任务无限运行
2. **槽位管理**：合理使用 5 个槽位（0-4）来组织不同优先级的任务
3. **战斗资源**：系统会自动管理战斗实例的创建和销毁

## 测试

所有自动执行逻辑都包含在 `ActivityPlanAutoExecutionTests` 测试套件中：
- 队列排序逻辑
- 状态转换
- 单任务执行限制
- 自动执行流程

运行测试：
```bash
dotnet test --filter "FullyQualifiedName~ActivityPlanAutoExecutionTests"
```
