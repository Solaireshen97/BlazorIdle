# 离线恢复 BattleId 问题修复 - 实施总结

## 问题描述

用户反馈：登录后初始化任务列表 `GetCharacterPlansAsync` 时，返回的 `BattleId` 为 `NULL`，导致前端无法轮询战斗状态。

### 详细现象

1. 玩家离线后，后端触发离线暂停，将 `BattleId` 设置为 `null`
2. 玩家上线时，后端执行离线结算，但只进行快进计算，未重新启动实际战斗
3. 前端获取到的计划状态为 `Running`，但 `BattleId` 为 `null`
4. 前端无法开始轮询，用户看不到战斗进度
5. 用户感觉像是开了一场新战斗，而不是延续之前的战斗

### 用户期望

玩家登录后应该看到：
- 角色和怪物正在战斗
- 敌人血量等状态延续离线前的进度
- 战斗统计（金币、经验、击杀数）继续增长
- 整个过程无缝衔接，无需手动操作

## 根本原因分析

### 离线暂停流程（已有功能）

```
玩家离线 
  → OfflineDetectionService 检测到离线
  → 调用 ActivityPlanService.PausePlanAsync
  → 保存战斗状态到 BattleStateJson
  → 停止战斗引擎（释放内存）
  → 设置 BattleId = null
  → 设置 State = Paused
```

### 离线恢复流程（修复前）

```
玩家上线
  → 前端调用 UpdateHeartbeatAsync
  → 后端调用 CheckAndSettleAsync
  → 运行 OfflineFastForwardEngine 快进计算
  → 更新 ExecutedSeconds 和 BattleStateJson
  → 如果计划完成，标记为 Completed
  → 如果计划未完成，状态可能是 Running 或 Paused
  → 返回结算结果
  → 前端刷新计划列表
  ❌ BattleId 仍然是 null
  ❌ 前端无法开始轮询
```

### 问题所在

**CheckAndSettleAsync 只执行了离线快进计算，但没有重新启动实际战斗。**

虽然战斗状态被保存并更新了，但没有创建新的运行中战斗（RunningBattle），所以没有 BattleId。

## 解决方案

### 核心思路

在离线结算后，如果计划未完成，自动调用 `StartPlanAsync` 重新启动战斗：
1. `StartPlanAsync` 会从 `BattleStateJson` 加载战斗状态
2. 创建新的 `RunningBattle` 实例
3. 调用 `BattleEngine.RestoreBattleState` 恢复战斗状态
4. 设置新的 `BattleId`
5. 将计划状态设置为 `Running`

### 实施步骤

#### 1. 修改 OfflineSettlementService

**文件**: `BlazorIdle.Server/Application/Battles/Offline/Offline.cs`

**修改内容**:
- 添加 `_startPlan` 委托字段
- 在构造函数中接收 `StartPlanAsync` 委托
- 在 `CheckAndSettleAsync` 中，判断计划是否未完成
- 如果未完成，调用 `_startPlan` 重新启动战斗

**关键代码**:
```csharp
// 如果计划未完成，需要重新启动战斗以恢复 BattleId
else if (!result.PlanCompleted && _startPlan is not null)
{
    // 如果是 Running 状态但没有 BattleId，先改为 Paused
    if (runningPlan.State == ActivityState.Running && !runningPlan.BattleId.HasValue)
    {
        runningPlan.State = ActivityState.Paused;
        await _plans.UpdateAsync(runningPlan, ct);
    }
    
    try
    {
        await _startPlan(runningPlan.Id, ct);
        // 重新加载计划以获取更新后的 BattleId
        runningPlan = await _plans.GetAsync(runningPlan.Id, ct);
    }
    catch (Exception)
    {
        // 如果启动失败，用户可以手动点击恢复按钮
    }
}
```

#### 2. 更新依赖注入配置

**文件**: `BlazorIdle.Server/Infrastructure/DependencyInjection.cs`

**修改内容**:
- 在注册 `OfflineSettlementService` 时，传入 `ActivityPlanService.StartPlanAsync` 委托

**修改代码**:
```csharp
services.AddTransient<OfflineSettlementService>(sp =>
{
    var planService = sp.GetRequiredService<ActivityPlanService>();
    
    return new OfflineSettlementService(
        characters,
        simulator,
        plans,
        engine,
        db,
        planService.TryStartNextPendingPlanAsync,
        planService.StartPlanAsync  // 新增
    );
});
```

### 离线恢复流程（修复后）

```
玩家上线
  → 前端调用 UpdateHeartbeatAsync
  → 后端调用 CheckAndSettleAsync
  → 运行 OfflineFastForwardEngine 快进计算
  → 更新 ExecutedSeconds 和 BattleStateJson
  → 如果计划未完成：
      → 调用 StartPlanAsync(planId)
      → 从 BattleStateJson 加载战斗状态
      → 创建新的 RunningBattle
      → 调用 RestoreBattleState 恢复敌人血量等
      → 设置新的 BattleId
      → 设置 State = Running
  → 返回结算结果
  → 前端刷新计划列表
  ✅ BattleId 有值
  ✅ 前端自动开始轮询
  ✅ 战斗无缝继续
```

## 技术细节

### 战斗状态恢复机制

**BattleStateJson 包含的信息**:
```json
{
  "Enemies": [
    {
      "EnemyId": "dummy",
      "CurrentHp": 850,
      "MaxHp": 1000,
      "IsDead": false,
      "KillTime": null,
      "Overkill": 0
    }
  ],
  "WaveIndex": 2,
  "RunCount": 1,
  "SnapshotAtSeconds": 150.5
}
```

**恢复流程**:
1. `StartPlanAsync` 从 `plan.BattleStateJson` 反序列化 `BattleState`
2. 创建 `RunningBattle` 时传入 `battleState` 参数
3. `StepBattleCoordinator.Start` 调用 `engine.RestoreBattleState(battleState)`
4. `BattleEngine.RestoreBattleState` 恢复敌人血量、波次索引等
5. 战斗从保存的状态继续

### 特殊情况处理

**情况1: 计划状态是 Running 但没有 BattleId**

这是修复前可能出现的异常状态。`StartPlanAsync` 不允许启动 `Running` 状态的计划（会抛出异常），所以需要先将状态改为 `Paused`。

```csharp
if (runningPlan.State == ActivityState.Running && !runningPlan.BattleId.HasValue)
{
    runningPlan.State = ActivityState.Paused;
    await _plans.UpdateAsync(runningPlan, ct);
}
```

**情况2: StartPlanAsync 失败**

如果重启失败（例如有其他计划正在运行），catch 块会捕获异常，计划保持当前状态。用户可以手动点击"恢复"按钮来重试。

**情况3: 计划在离线期间完成**

如果离线时长超过计划剩余时间，`FastForward` 会将计划标记为 `Completed`。此时不需要重启战斗，而是尝试启动下一个待执行的计划（已有逻辑）。

## 前端兼容性

前端代码无需修改，已有逻辑完全兼容：

### CheckOfflineRewardsAsync

```csharp
// 刷新计划列表
await RefreshPlansAsync();

// 检查运行中的计划
var runningPlan = characterPlans?.FirstOrDefault(p => p.State == 1);
if (runningPlan?.BattleId.HasValue == true)
{
    _ = StartPlanPollingAsync(runningPlan.BattleId.Value);  // 自动开始轮询
}
```

### RefreshPlansAsync

```csharp
var runningPlan = characterPlans?.FirstOrDefault(p => p.State == 1);
if (runningPlan?.BattleId.HasValue == true)
{
    if (!planIsPolling)
    {
        _ = StartPlanPollingAsync(runningPlan.BattleId.Value);  // 自动开始轮询
    }
}
```

## 测试验证

### 构建测试

- ✅ 代码编译成功，无错误
- ✅ 解决方案构建通过
- ⚠️  有2个预存在的单元测试失败（与本次修改无关）

### 需要的手动测试

详见 `docs/ManualTestingGuide_OfflineRecovery.md`

关键测试点：
1. 创建战斗计划，等待30秒后离线
2. 等待30秒后重新登录
3. 验证：
   - ✓ 显示离线结算弹窗
   - ✓ BattleId 不为空
   - ✓ 战斗状态正确恢复
   - ✓ 前端自动开始轮询
   - ✓ 敌人血量延续之前的状态
   - ✓ ExecutedSeconds 正确累加

## 影响范围分析

### 修改的文件

1. `BlazorIdle.Server/Application/Battles/Offline/Offline.cs` (45 行修改)
   - 添加委托字段和参数
   - 添加自动重启战斗逻辑
   
2. `BlazorIdle.Server/Infrastructure/DependencyInjection.cs` (7 行修改)
   - 传入 StartPlanAsync 委托

3. 新增文档 (418 行)
   - `docs/OfflineRecoveryBattleIdFix.md` - 技术文档
   - `docs/ManualTestingGuide_OfflineRecovery.md` - 测试指南

### 不需要修改的部分

- ✅ 前端代码（已有逻辑兼容）
- ✅ ActivityPlanService.StartPlanAsync（已支持状态恢复）
- ✅ BattleEngine.RestoreBattleState（已实现）
- ✅ OfflineFastForwardEngine（已正确保存状态）
- ✅ 数据库结构（无需修改）

### 向后兼容性

- ✅ 不影响新创建的计划
- ✅ 不影响在线战斗
- ✅ 不影响手动启动/停止/取消功能
- ✅ 不影响计划自动衔接
- ✅ 如果自动重启失败，用户仍可手动恢复

## 预期效果

### 用户体验改善

**修复前**:
1. 玩家离线后回来，看到计划是"运行中"但没有进度
2. 需要手动点击"恢复"按钮
3. 战斗看起来像是重新开始的

**修复后**:
1. 玩家离线后回来，看到离线结算弹窗
2. 关闭弹窗后，战斗自动继续
3. 敌人血量、战斗时间等状态延续之前的进度
4. 完全无缝衔接，无需任何手动操作

### 技术收益

1. **自动化**: 无需用户手动操作即可恢复战斗
2. **状态一致性**: 战斗状态完整保存和恢复
3. **可靠性**: 有异常处理，失败时可手动重试
4. **可维护性**: 代码改动最小化，复用现有逻辑

## 后续改进建议

### 短期改进（可选）

1. **增加日志记录**
   - 在 StartPlanAsync 失败时记录详细错误
   - 便于排查问题

2. **增加重试机制**
   - 如果首次重启失败，可以尝试重试
   - 避免用户需要手动操作

3. **前端提示优化**
   - 如果战斗自动恢复失败，显示友好的提示
   - 引导用户点击"恢复"按钮

### 长期改进（讨论）

1. **是否需要保留 BattleId？**
   - 考虑在暂停时不清空 BattleId
   - 权衡内存占用和状态管理复杂度

2. **离线战斗状态持久化**
   - 考虑将战斗状态序列化到数据库
   - 支持更长时间的离线恢复
   - 可以查询历史战斗状态

3. **离线结算记录审计**
   - 保存每次离线结算的详细记录
   - 便于追溯和分析

## 总结

本次修复通过在离线结算后自动重启战斗，解决了 BattleId 为空的问题。实现简洁、影响范围小、向后兼容性好，用户体验得到显著改善。

**核心改动**: 2个文件，52行代码
**文档支持**: 2个文档，418行说明
**测试状态**: 编译通过，待手动验证
**预期收益**: 用户体验无缝，无需手动操作
