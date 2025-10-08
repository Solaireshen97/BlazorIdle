# 离线任务暂停与恢复功能修复总结

## 问题描述

原问题：`OfflineDetectionService` 的 `CheckAndPauseOfflinePlayers` 方法在检测到玩家离线超过阈值后，错误地调用了 `StopPlanAsync`，导致任务被标记为 `Completed` 状态，任务状态被清空，无法在玩家重新上线时恢复执行。

**期望行为**：
- 玩家离线时，任务应该被**暂停**（保存状态）
- 保留玩家最后一次更新心跳时间的任务状态
- 清空内存中的运行状态
- 等待玩家下次更新心跳后恢复任务
- 服务器重启后也能正常恢复

## 修复方案

### 1. 添加 `Paused` 状态

**文件**: `BlazorIdle.Server/Domain/Activities/ActivityState.cs`

```csharp
public enum ActivityState
{
    Pending = 0,
    Running = 1,
    Completed = 2,
    Cancelled = 3,
    Paused = 4  // 新增：暂停状态
}
```

**状态机转换**:
```
Pending → Running → Paused → Running → Completed/Cancelled
                      ↑          ↓
                      └──────────┘
```

### 2. 新增暂停方法

**文件**: `BlazorIdle.Server/Application/Activities/ActivityPlanService.cs`

#### `PausePlanAsync` - 暂停任务
- 保存战斗状态快照（BattleStateJson）
- 停止战斗但不结算
- 设置状态为 `Paused`
- 保存已执行时长（ExecutedSeconds）
- **不清空** BattleStateJson
- **不触发** 下一个任务自动启动

#### `ResumePlanAsync` - 恢复任务
- 将 `Paused` 状态改为 `Pending`
- 调用 `StartPlanAsync` 重新启动
- 自动加载 BattleStateJson 恢复战斗状态

### 3. 更新离线检测服务

**文件**: `BlazorIdle.Server/Services/OfflineDetectionService.cs`

**关键修改**:
```csharp
// 之前（错误）
await planService.StopPlanAsync(plan.Id, ct);

// 现在（正确）
await planService.PausePlanAsync(plan.Id, ct);
```

### 4. 新增服务器重启恢复服务

**文件**: `BlazorIdle.Server/Services/PausedPlanRecoveryService.cs`

实现 `IHostedService` 接口，在服务器启动时：
1. 查询所有 `State == Paused` 的计划
2. 验证角色存在
3. 检查是否有其他运行中的任务
4. 调用 `ResumePlanAsync` 恢复任务

**注册**: 在 `Program.cs` 中添加：
```csharp
builder.Services.AddHostedService<PausedPlanRecoveryService>();
```

### 5. 更新离线结算服务

**文件**: `BlazorIdle.Server/Application/Battles/Offline/Offline.cs`

在 `CheckAndSettleAsync` 方法中，查找运行中或暂停的计划：
```csharp
var runningPlan = await _plans.GetRunningPlanAsync(characterId, ct);

if (runningPlan is null)
{
    var pausedPlan = await _plans.GetByCharacterIdAndStateAsync(
        characterId, ActivityState.Paused, ct);
    if (pausedPlan is not null)
    {
        runningPlan = pausedPlan;
    }
}
```

### 6. 扩展仓储接口

**文件**: `BlazorIdle.Server/Application/Abstractions/IActivityPlanRepository.cs`

新增方法：
```csharp
Task<ActivityPlan?> GetByCharacterIdAndStateAsync(
    Guid characterId, ActivityState state, CancellationToken ct = default);
    
Task<List<ActivityPlan>> GetAllPausedPlansAsync(
    CancellationToken ct = default);
```

### 7. 添加 API 端点

**文件**: `BlazorIdle.Server/Api/ActivityPlansController.cs`

新增端点：
```
POST /api/activity-plans/{id}/resume
```

### 8. 更新前端客户端

**文件**: `BlazorIdle/Services/ApiClient.cs`

- 更新 `ActivityPlanDto.State` 注释，包含 Paused 状态
- 添加 `ResumePlanAsync` 方法

## 修复效果对比

### 之前（错误实现）

**离线检测时**:
```
State: Running → Completed
BattleStateJson: 保存 → 清空
CompletedAt: null → DateTime.UtcNow
ExecutedSeconds: 1800 → 1800
```
**问题**: 任务被标记为完成，无法恢复

### 现在（正确实现）

**离线检测时**:
```
State: Running → Paused
BattleStateJson: 保存 → 保留
CompletedAt: null → null
ExecutedSeconds: 1800 → 1800
```
**优点**: 任务可以恢复执行

## 完整流程示例

### 场景：玩家离线后重新上线

1. **玩家在线执行任务**
   - 状态：`Running`
   - ExecutedSeconds: 1800（30分钟）
   - BattleStateJson: 保存当前战斗状态

2. **玩家离线**
   - LastSeenAtUtc 停止更新
   - 60秒后，OfflineDetectionService 检测到离线
   - 调用 `PausePlanAsync`
   - 状态变为：`Paused`
   - BattleStateJson 保留
   - ExecutedSeconds 保持为 1800

3. **玩家离线期间**
   - 状态保持：`Paused`
   - 任务不执行
   - 状态保存在数据库中

4. **玩家重新上线**
   - 调用 `/api/offline/check` 端点
   - `CheckAndSettleAsync` 检测到 `Paused` 计划
   - 计算离线时长：例如 1200 秒（20分钟）
   - 使用 `OfflineFastForwardEngine` 快进模拟
   - 从 ExecutedSeconds=1800 开始模拟
   - 结算离线收益
   - 如果未完成，状态恢复为 `Running` 继续执行

### 场景：服务器重启

1. **服务器重启前**
   - 多个任务处于 `Paused` 状态
   - 状态保存在数据库中

2. **服务器启动**
   - `PausedPlanRecoveryService.StartAsync` 被调用
   - 查询所有 `Paused` 计划
   - 对每个计划调用 `ResumePlanAsync`
   - 任务恢复为 `Running` 状态

## 配置说明

**appsettings.json**:
```json
{
  "Offline": {
    "OfflineDetectionSeconds": 60,    // 离线检测阈值（秒）
    "MaxOfflineSeconds": 43200,       // 最大离线结算时长（秒）
    "AutoApplyRewards": true          // 自动应用奖励
  }
}
```

## 测试验证

新增测试文件：`tests/BlazorIdle.Tests/ActivityPlanPauseResumeTests.cs`

包含以下测试用例：
1. ✅ `ActivityState_PausedEnum_ShouldExist` - 验证 Paused 枚举存在
2. ✅ `ActivityPlan_WithPausedState_ShouldMaintainState` - 验证暂停状态的数据保留
3. ✅ `ActivityPlan_PausedState_ShouldNotReachLimit` - 验证暂停状态不会触发完成
4. ✅ `ActivityPlan_CompletedVsPaused_BehaviorDifference` - 验证完成与暂停的区别
5. ✅ `OfflineDetection_Scenario_PauseNotComplete` - 验证离线检测场景

所有测试通过 ✅

## 相关文档

新增文档：`docs/离线任务暂停与恢复系统文档.md`

包含：
- 系统概述
- 核心特性
- 组件详解
- 完整流程示例
- 配置说明
- 日志监控
- 测试建议

## 检查清单

- [x] 添加 Paused 状态到 ActivityState 枚举
- [x] 实现 PausePlanAsync 方法
- [x] 实现 ResumePlanAsync 方法
- [x] 更新 OfflineDetectionService 使用 PausePlanAsync
- [x] 实现 PausedPlanRecoveryService 用于服务器重启恢复
- [x] 更新 OfflineSettlementService 处理暂停的计划
- [x] 扩展 Repository 接口和实现
- [x] 添加 Resume API 端点
- [x] 更新前端 ApiClient
- [x] 编写单元测试
- [x] 编写完整文档
- [x] 验证编译无错误
- [x] 验证测试全部通过

## 剩余考虑事项

### 已处理的问题

1. ✅ **角色有多个暂停的任务**: `GetByCharacterIdAndStateAsync` 只返回第一个，其他任务会在第一个完成后自动启动
2. ✅ **任务限制时间到期**: `OfflineFastForwardEngine` 会检测并正确处理
3. ✅ **战斗状态JSON损坏**: `StartPlanAsync` 中有 try-catch 处理，失败时从头开始

### 已确认无其他离线相关问题

通过代码审查确认：
- 离线检测逻辑正确
- 离线结算逻辑正确
- 服务器重启恢复逻辑完整
- 状态转换符合预期
- 数据持久化正确

## 总结

本次修复完全解决了离线玩家任务错误完成的问题，确保了：

1. ✅ 离线玩家的任务会被**暂停**而不是**完成**
2. ✅ 任务状态（BattleStateJson、ExecutedSeconds）被正确保存
3. ✅ 玩家上线时任务可以从离线时的状态**继续执行**
4. ✅ 服务器重启后任务可以**自动恢复**
5. ✅ 离线结算功能正常工作

系统现在完全符合原始设计要求：**"暂停当前任务并以玩家最后一次更新心跳时间的状态来保存当前任务状态然后清空内存等待下次玩家更新心跳后恢复任务"**。
