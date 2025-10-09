# 离线恢复战斗ID修复

## 问题描述

登录后初始化任务列表 `GetCharacterPlansAsync` 时，返回的 `BattleId` 为 `NULL`。

### 问题原因

1. 当玩家离线时，后端触发 `PausePlanAsync`，保存战斗状态到 `BattleStateJson`，但将 `BattleId` 设置为 `null`
2. 当玩家上线时，`CheckAndSettleAsync` 执行离线结算，但只运行快进模拟，并未重新启动实际战斗
3. 计划状态被更新（可能是 `Running` 或 `Paused`），但 `BattleId` 仍然是 `null`
4. 前端获取计划列表时，发现 `BattleId` 为空，无法启动轮询，用户看不到战斗进度

### 用户期望

玩家登录后应该看到角色和怪物正在战斗的状态（保持离线时的战斗进度），而不是像开了一场新战斗的感觉。

## 解决方案

### 后端修改

#### 1. OfflineSettlementService 增强

**文件**: `BlazorIdle.Server/Application/Battles/Offline/Offline.cs`

**修改内容**:
- 添加 `StartPlanAsync` 委托注入
- 在 `CheckAndSettleAsync` 中，如果计划未完成，自动调用 `StartPlanAsync` 重新启动战斗
- 战斗启动时会自动从 `BattleStateJson` 加载并恢复战斗状态（敌人血量、波次索引等）

**关键逻辑**:
```csharp
else if (!result.PlanCompleted && _startPlan is not null)
{
    // 计划未完成，需要重新启动战斗以恢复 BattleId
    // 如果计划状态是 Running 但没有 BattleId，先改为 Paused
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

#### 2. 依赖注入更新

**文件**: `BlazorIdle.Server/Infrastructure/DependencyInjection.cs`

**修改内容**:
- 在注册 `OfflineSettlementService` 时，同时传入 `ActivityPlanService.StartPlanAsync` 委托
- 这样离线结算服务就能够在需要时重新启动战斗

### 战斗状态恢复

**文件**: `BlazorIdle.Server/Application/Activities/ActivityPlanService.cs`

`StartPlanAsync` 方法已经支持从 `BattleStateJson` 恢复战斗状态：
1. 读取计划的 `BattleStateJson`（如果存在）
2. 反序列化为 `BattleState` 对象
3. 创建新的战斗时传入 `battleState` 参数
4. 战斗引擎调用 `RestoreBattleState` 恢复敌人血量、波次等状态

**恢复的状态包括**:
- 敌人当前血量（`CurrentHp`）
- 敌人最大血量（`MaxHp`）
- 敌人死亡状态（`IsDead`）
- 击杀时间（`KillTime`）
- 过量伤害（`Overkill`）
- 当前波次索引（`WaveIndex`，地下城模式）
- 已完成轮数（`RunCount`，地下城循环模式）

### 前端行为

前端代码无需修改，已有逻辑会自动处理：

1. **登录时检查离线收益** (`CheckOfflineRewardsAsync`)
   - 调用 `UpdateHeartbeatAsync`，后端执行离线结算并重启战斗
   - 刷新计划列表 (`RefreshPlansAsync`)
   - 发现运行中的计划有 `BattleId`，自动开始轮询

2. **定期刷新计划** (`RefreshPlansAsync`)
   - 查找 `State == 1` (Running) 的计划
   - 如果有 `BattleId`，自动启动轮询

3. **手动恢复** (`ResumePlanAsync`)
   - 用户可以手动点击"恢复"按钮
   - 调用后端 `/api/activity-plans/{id}/resume`
   - 成功后自动启动轮询

## 测试场景

### 场景1：离线后计划未完成

1. 玩家创建一个300秒的战斗计划
2. 战斗进行100秒后玩家离线（触发暂停）
3. 离线200秒后玩家上线
4. **期望**: 
   - 离线期间继续计算200秒战斗（总计300秒）
   - 计划状态变为 `Completed`
   - 金币和经验正确发放
   - 启动下一个待执行的计划（如果有）

### 场景2：离线后计划部分完成

1. 玩家创建一个300秒的战斗计划
2. 战斗进行100秒后玩家离线（触发暂停）
3. 离线100秒后玩家上线
4. **期望**:
   - 离线期间继续计算100秒战斗（总计200秒）
   - 计划状态保持 `Running`，但 `ExecutedSeconds = 200`
   - 战斗自动重启，`BattleId` 不为空
   - 前端自动开始轮询战斗状态
   - 战斗状态（敌人血量等）恢复为离线结算后的状态

### 场景3：离线时间超过计划剩余时间

1. 玩家创建一个300秒的战斗计划
2. 战斗进行100秒后玩家离线（触发暂停）
3. 离线500秒后玩家上线
4. **期望**:
   - 离线期间只计算200秒战斗（计划剩余时间）
   - 计划状态变为 `Completed`
   - 金币和经验正确发放
   - 启动下一个待执行的计划（如果有）

## 技术细节

### 为什么不直接保留 BattleId？

暂停时将 `BattleId` 设为 `null` 是设计决策，原因：
1. **释放内存**: 停止战斗引擎可以释放内存中的战斗状态
2. **状态持久化**: 战斗状态保存到 `BattleStateJson`，可以在数据库中持久化
3. **灵活恢复**: 可以在任何时候重新启动战斗并恢复状态

### 离线快进 vs 在线战斗

**离线快进**:
- 使用 `OfflineFastForwardEngine`
- 一次性计算整个离线时长
- 保存战斗状态到 `BattleStateJson`
- 不创建实际的运行中战斗（没有 `BattleId`）

**在线战斗**:
- 使用 `StepBattleCoordinator` 管理
- 实时推进战斗时间，支持轮询查询
- 有独立的 `BattleId`
- 可以从 `BattleStateJson` 恢复状态

### StartPlanAsync 的状态处理

`StartPlanAsync` 支持启动以下状态的计划：
- `Pending`: 新创建的计划
- `Paused`: 暂停的计划

不支持启动 `Running` 状态的计划（会抛出异常），因为运行中的计划理论上应该已经有 `BattleId`。

**特殊情况处理**: 如果计划状态是 `Running` 但 `BattleId` 为空（离线结算后可能出现），需要先改为 `Paused` 再调用 `StartPlanAsync`。

## 相关代码文件

- `BlazorIdle.Server/Application/Battles/Offline/Offline.cs` - 离线结算服务
- `BlazorIdle.Server/Application/Battles/Offline/OfflineFastForwardEngine.cs` - 离线快进引擎
- `BlazorIdle.Server/Application/Battles/Offline/BattleState.cs` - 战斗状态快照
- `BlazorIdle.Server/Application/Activities/ActivityPlanService.cs` - 活动计划服务
- `BlazorIdle.Server/Domain/Combat/Engine/BattleEngine.cs` - 战斗引擎（恢复状态）
- `BlazorIdle.Server/Infrastructure/DependencyInjection.cs` - 依赖注入配置
- `BlazorIdle/Pages/Characters.razor` - 前端计划管理（无需修改）

## 总结

通过自动重启战斗并恢复战斗状态，用户在离线后重新登录时能够看到角色和怪物正在战斗的状态，提供了无缝的游戏体验。整个过程对用户透明，无需手动操作。
