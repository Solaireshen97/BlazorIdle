# 修复：登录后初始化任务列表时 BattleId 返回 NULL 的问题

## 问题描述

当用户登录后，前端调用 `GetCharacterPlansAsync` 初始化任务列表时，返回的计划中 `BattleId` 字段为 NULL，导致前端的 `RefreshPlansAsync` 方法无法启动轮询功能。

## 根本原因

1. 当玩家离线时，`OfflineDetectionService` 会调用 `PausePlanAsync` 暂停正在运行的计划
2. `PausePlanAsync` 在停止战斗后会将 `plan.BattleId` 设置为 `null`（ActivityPlanService.cs 第 236 行）
3. 当玩家重新登录时，`OfflineSettlementService.CheckAndSettleAsync` 会处理离线收益
4. 在处理过程中，计划状态从 `Paused` 改为 `Running`（Offline.cs 第 134 行）
5. **但是没有启动新的战斗，所以 BattleId 仍然是 NULL**
6. 前端获取到 `State=Running` 但 `BattleId=null` 的计划，无法启动轮询

## 解决方案

在 `OfflineSettlementService.CheckAndSettleAsync` 中，当计划被暂停且在离线结算后仍未完成时：

1. 将计划状态改回 `Paused`
2. 调用 `ActivityPlanService.StartPlanAsync` 恢复计划
3. `StartPlanAsync` 会创建新的战斗实例和 BattleId
4. 前端获取到的计划现在有了有效的 BattleId，可以正常启动轮询

## 技术实现

### 1. 修改 OfflineSettlementService 构造函数

添加 `startPlan` 委托参数，用于调用 `ActivityPlanService.StartPlanAsync`：

```csharp
public OfflineSettlementService(
    ICharacterRepository characters, 
    BattleSimulator simulator,
    IActivityPlanRepository plans,
    OfflineFastForwardEngine engine,
    GameDbContext db,
    Func<Guid, CancellationToken, Task<ActivityPlan?>>? tryStartNextPlan = null,
    Func<Guid, CancellationToken, Task<Guid>>? startPlan = null)  // 新增
```

### 2. 更新 CheckAndSettleAsync 逻辑

在处理离线结算后，如果计划未完成且之前是暂停状态，则恢复运行：

```csharp
else if (!result.PlanCompleted && wasPaused && _startPlan is not null)
{
    // 计划未完成且之前是暂停的，需要恢复运行
    // 将状态改回 Paused，然后通过 StartPlanAsync 恢复（会创建新的 BattleId）
    runningPlan.State = ActivityState.Paused;
    await _plans.UpdateAsync(runningPlan, ct);
    
    try
    {
        // 调用 StartPlanAsync 恢复计划（会创建新的 BattleId 和战斗实例）
        await _startPlan(runningPlan.Id, ct);
    }
    catch (Exception)
    {
        // 如果恢复失败，保持 Paused 状态，用户可以稍后手动恢复
    }
}
```

### 3. 更新依赖注入配置

在 `DependencyInjection.cs` 中传递 `StartPlanAsync` 委托：

```csharp
services.AddTransient<OfflineSettlementService>(sp =>
{
    var planService = sp.GetRequiredService<ActivityPlanService>();
    
    return new OfflineSettlementService(
        // ... 其他参数
        planService.TryStartNextPendingPlanAsync,
        planService.StartPlanAsync  // 新增
    );
});
```

## 执行流程

```
玩家离线
  ↓
OfflineDetectionService 检测到离线
  ↓
调用 PausePlanAsync 暂停计划
  ↓
计划状态: Running → Paused
BattleId: [some-guid] → NULL
  ↓
玩家重新登录
  ↓
前端调用心跳接口
  ↓
心跳接口触发 CheckAndSettleAsync
  ↓
处理离线收益（FastForward）
  ↓
检测到计划未完成且之前是暂停状态
  ↓
✅ 调用 StartPlanAsync 恢复计划
  ↓
创建新的战斗实例和 BattleId
  ↓
计划状态: Paused → Running
BattleId: NULL → [new-guid]
  ↓
前端调用 GetCharacterPlansAsync
  ↓
✅ 获取到有效的 BattleId
  ↓
前端 RefreshPlansAsync 检测到运行中的计划
  ↓
✅ 启动轮询功能
```

## 测试验证

1. 构建项目：`dotnet build` ✅
2. 运行单元测试：`dotnet test --filter FullyQualifiedName~ActivityPlan` ✅
3. 运行离线测试：`dotnet test --filter FullyQualifiedName~Offline` ✅

## 影响范围

- 修改文件：
  - `BlazorIdle.Server/Application/Battles/Offline/Offline.cs`
  - `BlazorIdle.Server/Infrastructure/DependencyInjection.cs`
  
- 影响功能：
  - 离线结算后的计划恢复
  - 前端任务列表初始化
  - 前端轮询功能启动

## 注意事项

1. 每次暂停/恢复都会创建新的 BattleId，这是设计行为
2. 战斗状态通过 `BattleStateJson` 保存和恢复，确保进度连续性
3. 如果恢复失败，计划保持在 Paused 状态，用户可以手动恢复
4. 这个修复确保了离线上线的无缝衔接体验
