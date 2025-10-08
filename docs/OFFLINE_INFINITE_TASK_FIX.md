# 离线收益结算功能修复文档

## 问题描述

在更新后的离线功能中发现两个主要问题：

1. **无限时长任务被错误标记为完成**
   - 当玩家离线超过60秒后，系统会自动停止正在运行的活动计划
   - 停止操作将计划状态设置为 `Completed`（已完成）
   - 对于无限时长任务（`LimitType.Infinite`），这是错误的行为，因为无限任务理论上永远不会"完成"

2. **离线结算无法正常处理已停止的无限任务**
   - 当玩家重新上线时，离线结算服务会查找 `Running` 状态的计划
   - 但由于无限任务已被标记为 `Completed`，离线结算无法找到并处理它们
   - 结果是无限任务无法继续执行，前端显示任务已结束

## 问题根源

### 原有流程

```
玩家启动无限任务 
  → 计划状态: Running
  → 玩家离线 > 60秒
  → OfflineDetectionService 调用 StopPlanAsync
  → 计划状态: Completed ❌ (错误!)
  → 玩家上线
  → 离线结算查找 Running 计划
  → 找不到计划 ❌
  → 无限任务无法恢复
```

### 核心问题

`StopPlanAsync` 方法被设计用于正常停止计划，它会：
- 停止战斗协调器
- 将计划状态设置为 `Completed`
- 清空战斗状态快照
- 触发自动启动下一个待执行任务

这个行为对于正常完成的有限时长任务是正确的，但对于需要暂停以等待离线结算的任务是错误的。

## 解决方案

### 1. 添加专用的暂停方法

在 `ActivityPlanService` 中新增 `PausePlanForOfflineAsync` 方法：

```csharp
/// <summary>
/// 暂停活动计划（保存战斗状态但不标记为完成，用于离线玩家）
/// </summary>
public async Task<bool> PausePlanForOfflineAsync(Guid planId, CancellationToken ct = default)
{
    var plan = await _plans.GetAsync(planId, ct);
    if (plan is null || plan.State != ActivityState.Running)
        return false;

    // 保存战斗状态快照
    if (plan.BattleId.HasValue)
    {
        if (_coordinator.TryGet(plan.BattleId.Value, out var rb) && rb != null)
        {
            var battleState = rb.Engine.CaptureBattleState();
            plan.BattleStateJson = JsonSerializer.Serialize(battleState);
            plan.ExecutedSeconds = rb.Clock.CurrentTime;
        }
    }

    // 停止战斗（释放内存资源）
    if (plan.BattleId.HasValue)
    {
        await _coordinator.StopAndFinalizeAsync(plan.BattleId.Value, ct);
    }

    // 关键：保持 Running 状态，不标记为 Completed
    await _plans.UpdateAsync(plan, ct);
    return true;
}
```

**关键特性：**
- ✅ 保存战斗状态快照（`BattleStateJson`）
- ✅ 保存已执行时长（`ExecutedSeconds`）
- ✅ 停止战斗协调器（释放内存资源）
- ✅ **保持计划状态为 `Running`**（不设置为 `Completed`）
- ✅ 不清空战斗状态快照
- ✅ 不触发自动启动下一个任务

### 2. 修改离线检测服务

修改 `OfflineDetectionService.CheckAndPauseOfflinePlayers` 方法：

```csharp
// 原代码
await planService.StopPlanAsync(plan.Id, ct);

// 修改为
await planService.PausePlanForOfflineAsync(plan.Id, ct);
```

### 3. 增强离线结算服务

修改 `OfflineSettlementService.CheckAndSettleAsync` 方法，在离线结算后重新启动未完成的计划：

```csharp
// 离线结算后，检查计划是否完成
if (!result.PlanCompleted && _tryStartNextPlan is not null)
{
    // 对于未完成的计划（无限任务或部分完成的有限任务）
    // 重新启动战斗，从战斗状态快照继续
    try
    {
        var originalStartedAt = runningPlan.StartedAt;
        runningPlan.State = ActivityState.Pending;
        await _plans.UpdateAsync(runningPlan, ct);
        
        await _tryStartNextPlan(characterId, ct);
        
        // 恢复原始开始时间
        var reloadedPlan = await _plans.GetAsync(runningPlan.Id, ct);
        if (reloadedPlan != null && originalStartedAt.HasValue)
        {
            reloadedPlan.StartedAt = originalStartedAt;
            await _plans.UpdateAsync(reloadedPlan, ct);
        }
    }
    catch (Exception)
    {
        // 如果重启失败，恢复 Running 状态
        runningPlan.State = ActivityState.Running;
        await _plans.UpdateAsync(runningPlan, ct);
    }
}
```

## 修复后的流程

```
玩家启动无限任务 
  → 计划状态: Running
  → 玩家离线 > 60秒
  → OfflineDetectionService 调用 PausePlanForOfflineAsync
  → 保存战斗状态快照和执行时长
  → 停止战斗协调器（释放内存）
  → 计划状态: Running ✅ (保持不变)
  → 玩家上线
  → 离线结算查找 Running 计划
  → 找到计划 ✅
  → 进行离线模拟结算
  → 计划状态: Running ✅ (无限任务不会完成)
  → 重新启动战斗（从快照继续）
  → 无限任务成功恢复 ✅
```

## 测试覆盖

新增了5个测试用例来验证修复：

1. **OfflineSettlement_InfiniteTask_ShouldNotMarkAsCompleted**
   - 验证无限任务离线结算后不会被标记为完成

2. **OfflineSettlement_InfiniteTask_MultipleOfflinePeriods_ShouldAccumulateTime**
   - 验证多次离线期间执行时长正确累加

3. **OfflineSettlement_InfiniteTask_LongOffline_ShouldRespect12HourCap**
   - 验证长时间离线（24小时）会被限制在12小时上限

4. **OfflineSettlement_InfiniteTask_ShouldPreserveBattleState**
   - 验证战斗状态在离线结算后被正确保存和恢复

5. **OfflineSettlement_InfiniteTask_VsDurationTask_DifferentBehavior**
   - 验证无限任务和有限任务的离线结算行为差异

所有测试均通过 ✅

## 影响范围

### 修改的文件

1. **BlazorIdle.Server/Application/Activities/ActivityPlanService.cs**
   - 新增 `PausePlanForOfflineAsync` 方法

2. **BlazorIdle.Server/Services/OfflineDetectionService.cs**
   - 修改 `CheckAndPauseOfflinePlayers` 方法
   - 使用新的暂停方法替代停止方法

3. **BlazorIdle.Server/Application/Battles/Offline/Offline.cs**
   - 修改 `CheckAndSettleAsync` 方法
   - 增加未完成计划的重新启动逻辑

### 新增的文件

1. **tests/BlazorIdle.Tests/OfflineInfiniteTaskTests.cs**
   - 新增5个测试用例

2. **docs/OFFLINE_INFINITE_TASK_FIX.md**
   - 本文档

## 向后兼容性

✅ **完全向后兼容**

- 没有修改数据库架构
- 没有修改API接口
- 没有修改前端代码
- 现有的有限时长任务不受影响
- 所有现有测试继续通过

## 使用场景示例

### 场景1：无限战斗任务

```
1. 玩家创建一个无限时长的战斗任务
2. 任务开始执行，状态为 Running
3. 玩家关闭游戏，离线5分钟
4. 系统检测到离线，暂停任务但保持 Running 状态
5. 玩家重新上线
6. 系统进行离线结算：
   - 模拟5分钟的战斗
   - 计算金币、经验和掉落
   - 自动发放收益
   - 任务继续运行（状态仍为 Running）
7. 前端显示任务继续执行中 ✅
```

### 场景2：无限地下城循环

```
1. 玩家创建一个无限循环的地下城任务
2. 任务开始执行，完成了3次地下城
3. 玩家离线12小时
4. 系统暂停任务（保持 Running 状态）
5. 玩家上线
6. 系统进行离线结算（受12小时上限限制）：
   - 模拟12小时的地下城循环
   - 计算完成的次数和收益
   - 自动发放收益
   - 任务继续运行
7. 玩家可以继续无限循环 ✅
```

## 注意事项

1. **离线时长上限**
   - 离线结算最多处理12小时（43200秒）
   - 超过12小时的离线时间会被截断
   - 这是为了防止服务器过载和保持游戏平衡

2. **战斗状态快照**
   - 战斗状态快照会在暂停时保存
   - 重新启动时会从快照恢复
   - 确保了离线前后的无缝衔接

3. **资源管理**
   - 离线玩家的战斗会被停止以释放内存
   - 但计划状态保持在数据库中
   - 玩家上线时会重新创建战斗实例

4. **有限任务不受影响**
   - 有限时长任务的行为没有改变
   - 达到时长限制后仍会正常完成
   - 会触发下一个待执行任务的自动启动

## 总结

本次修复解决了无限时长任务在离线后无法正常恢复的问题。通过引入专用的暂停机制，区分了"暂停等待离线结算"和"正常完成"两种状态，确保了无限任务可以在玩家离线和上线之间无缝衔接。

**核心改进：**
- ✅ 无限任务离线后不会被错误地标记为完成
- ✅ 离线结算可以正确处理无限任务
- ✅ 离线前后的进度无缝衔接
- ✅ 战斗状态得到正确保存和恢复
- ✅ 有限任务的行为保持不变
- ✅ 完全向后兼容

修复后，玩家可以放心地创建无限时长任务，即使离线一段时间后回来，任务仍会继续执行，并获得离线期间的收益。
