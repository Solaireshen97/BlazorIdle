# 前端计划任务离线恢复功能修复总结

## 修复日期
2025-01-08

## 问题描述

前端的计划任务状态管理存在以下问题，无法完全适配最新的游戏离线恢复功能：

1. **缺少暂停状态显示**: 前端只处理 0-3 状态（待执行、执行中、已完成、已取消），没有处理状态 4（已暂停）
2. **缺少恢复按钮**: 暂停的计划无法在前端手动恢复
3. **离线恢复后轮询未重启**: 玩家上线后，即使计划已恢复运行，前端战斗状态轮询没有自动重启
4. **计划自动衔接后轮询未启动**: 当离线期间计划完成并自动启动下一个计划时，前端没有检测并启动新计划的轮询

## 修复方案

### 1. 添加暂停状态显示支持

**文件**: `BlazorIdle/Pages/Characters.razor`

**修改内容**:
```csharp
// 状态名称映射
var stateName = plan.State == 0 ? "待执行" 
    : plan.State == 1 ? "执行中" 
    : plan.State == 2 ? "已完成" 
    : plan.State == 3 ? "已取消" 
    : plan.State == 4 ? "已暂停"  // 新增
    : "未知";

// 状态样式映射
var stateClass = plan.State == 0 ? "text-warning" 
    : plan.State == 1 ? "text-success" 
    : plan.State == 2 ? "text-secondary" 
    : plan.State == 3 ? "text-muted" 
    : plan.State == 4 ? "text-info"    // 新增：蓝色
    : "text-muted";
```

### 2. 添加恢复按钮和功能

**文件**: `BlazorIdle/Pages/Characters.razor`

**UI 修改**:
```html
@if (plan.State == 4) // Paused
{
    <button class="btn btn-sm btn-success" @onclick="() => ResumePlanAsync(plan.Id)" disabled="@isBusy">恢复</button>
    <button class="btn btn-sm btn-danger" @onclick="() => CancelPlanAsync(plan.Id)" disabled="@isBusy">取消</button>
}
```

**新增方法**:
```csharp
async Task ResumePlanAsync(Guid planId)
{
    try
    {
        isBusy = true;
        var response = await Api.ResumePlanAsync(planId);
        if (response != null)
        {
            await RefreshPlansAsync();
            
            // 如果计划恢复成功并且有战斗ID，开始轮询
            if (response.Resumed && response.BattleId.HasValue)
            {
                _ = StartPlanPollingAsync(response.BattleId.Value);
            }
        }
        else
        {
            planError = "恢复计划失败";
        }
    }
    catch (Exception ex)
    {
        planError = $"恢复计划异常: {ex.Message}";
    }
    finally
    {
        isBusy = false;
    }
}
```

### 3. 添加 API 客户端方法

**文件**: `BlazorIdle/Services/ApiClient.cs`

**新增方法**:
```csharp
public async Task<ResumePlanResponse?> ResumePlanAsync(Guid planId, CancellationToken ct = default)
{
    SetAuthHeader();
    using var content = new StringContent("{}", Encoding.UTF8, "application/json");
    var resp = await _http.PostAsync($"/api/activity-plans/{planId}/resume", content, ct);
    if (!resp.IsSuccessStatusCode) return null;
    return await resp.Content.ReadFromJsonAsync<ResumePlanResponse>(cancellationToken: ct);
}
```

**新增 DTO**:
```csharp
public sealed class ResumePlanResponse
{
    public Guid PlanId { get; set; }
    public Guid? BattleId { get; set; }
    public bool Resumed { get; set; }
}
```

**更新注释**:
```csharp
public int State { get; set; }  // 0=Pending, 1=Running, 2=Completed, 3=Cancelled, 4=Paused
```

### 4. 离线恢复后自动重启轮询

**文件**: `BlazorIdle/Pages/Characters.razor`

**修改 `CheckOfflineRewardsAsync` 方法**:
```csharp
// 如果计划未完成且下一个计划已启动，重新开始轮询
if (heartbeatResponse.OfflineSettlement.NextPlanStarted && heartbeatResponse.OfflineSettlement.NextPlanId.HasValue)
{
    var nextPlan = characterPlans?.FirstOrDefault(p => p.Id == heartbeatResponse.OfflineSettlement.NextPlanId.Value);
    if (nextPlan?.BattleId.HasValue == true)
    {
        _ = StartPlanPollingAsync(nextPlan.BattleId.Value);
    }
}
// 如果计划未完成但没有启动下一个计划，检查当前运行中的计划
else if (!heartbeatResponse.OfflineSettlement.PlanCompleted && heartbeatResponse.OfflineSettlement.HasRunningPlan)
{
    var runningPlan = characterPlans?.FirstOrDefault(p => p.State == 1); // State=1 is Running
    if (runningPlan?.BattleId.HasValue == true)
    {
        _ = StartPlanPollingAsync(runningPlan.BattleId.Value);
    }
}
```

**修改 `UpdateHeartbeatIfNeededAsync` 方法**: 添加相同的轮询重启逻辑

### 5. 代码注释改进

**文件**: `BlazorIdle/Pages/Characters.razor`

在 `RefreshPlansAsync` 方法中添加状态码说明注释：
```csharp
// State: 0=Pending, 1=Running, 2=Completed, 3=Cancelled, 4=Paused
```

## 技术细节

### 状态转换流程
```
Pending (0) → Running (1) → Paused (4) → Running (1) → Completed (2)
                          ↓
                      Cancelled (3)
```

### 离线恢复触发流程

1. 玩家上线，前端调用 `UpdateHeartbeatAsync`
2. 后端检测到离线时间超过阈值，触发 `CheckAndSettleAsync`
3. 后端返回 `OfflineCheckResult`，包含：
   - `HasOfflineTime`: 是否有离线时间
   - `OfflineSeconds`: 离线时长
   - `PlanCompleted`: 计划是否完成
   - `NextPlanStarted`: 是否启动了下一个计划
   - `NextPlanId`: 下一个计划的 ID
   - `Settlement`: 离线结算详情
4. 前端根据结果：
   - 显示离线结算弹窗
   - 刷新计划列表
   - 根据计划状态决定是否重启轮询

### 轮询重启逻辑

前端在以下时机重启战斗状态轮询：
1. 离线结算后检测到下一个计划已启动
2. 离线结算后检测到当前计划未完成且正在运行
3. 手动点击"恢复"按钮后
4. 刷新计划列表时检测到有运行中的计划（已有逻辑）

### API 端点

使用的后端端点：
- `POST /api/activity-plans/{id}/resume`: 恢复暂停的计划
- `GET /api/activity-plans/character/{characterId}`: 获取角色的计划列表
- `POST /api/characters/{id}/heartbeat`: 更新心跳并触发离线检测
- `GET /api/step-battles/{id}/status`: 获取战斗状态（轮询使用）

## 测试建议

### 关键测试场景

1. **持续战斗 + 离线恢复**
   - 创建持续战斗计划
   - 运行一段时间后离线
   - 重新上线验证轮询恢复

2. **无限战斗 + 离线恢复**
   - 创建无限战斗计划
   - 离线后上线验证继续执行

3. **地下城循环 + 离线恢复**
   - 创建循环地下城计划
   - 验证离线期间完成的轮数
   - 验证上线后继续循环

4. **计划自动衔接 + 离线恢复**
   - 创建两个连续计划
   - 离线期间第一个完成，第二个启动
   - 验证上线后第二个计划的轮询启动

5. **手动恢复暂停计划**
   - 创建计划并让其被暂停
   - 点击恢复按钮
   - 验证计划恢复执行且轮询启动

详细测试步骤见：[FrontendScheduledTasksOfflineRecoveryTest.md](./FrontendScheduledTasksOfflineRecoveryTest.md)

## 影响范围

### 修改的文件
1. `BlazorIdle/Pages/Characters.razor` - 前端主页面
2. `BlazorIdle/Services/ApiClient.cs` - API 客户端

### 新增的文件
1. `docs/FrontendScheduledTasksOfflineRecoveryTest.md` - 测试文档
2. `docs/FrontendScheduledTasksOfflineRecoveryFix.md` - 修复总结（本文档）

### 不影响的部分
- 后端逻辑（已完整实现）
- 数据库结构（已支持 Paused 状态）
- 离线检测服务（已正常工作）
- 离线快进引擎（已正确处理）

## 验证要点

- [ ] 暂停状态在 UI 中正确显示
- [ ] 恢复按钮可见且功能正常
- [ ] 离线恢复后战斗状态自动轮询
- [ ] 计划自动衔接后新计划自动轮询
- [ ] ExecutedSeconds 正确累积
- [ ] 金币和经验正确发放
- [ ] 离线结算弹窗信息准确
- [ ] 各种模式（持续、无限、循环）都正常工作

## 后续改进建议

1. **添加手动暂停功能**: 目前只能通过离线自动暂停，可以添加手动暂停按钮
2. **轮询状态指示器**: 添加一个视觉指示器显示轮询是否正在运行
3. **离线模拟预览**: 在离线结算前显示预计收益
4. **计划队列管理**: 更好的多计划管理界面
5. **错误重试机制**: 当轮询失败时自动重试

## 相关文档

- [离线暂停恢复功能说明](./离线暂停恢复功能说明.md)
- [离线战斗前端集成实施说明](../离线战斗前端集成实施说明.md)
- [活动计划自动完成功能说明](../活动计划自动完成功能说明.md)
- [API 端点实现总结](../API_ENDPOINTS_IMPLEMENTATION.md)

## 总结

本次修复成功解决了前端计划任务状态管理与离线恢复功能的适配问题。主要成果包括：

1. ✅ 完整支持所有5种计划状态（包括暂停状态）
2. ✅ 实现手动恢复暂停计划的功能
3. ✅ 修复离线恢复后战斗状态轮询自动重启
4. ✅ 修复计划自动衔接后轮询启动
5. ✅ 创建完整的测试文档

这些修复确保了玩家在各种场景下都能获得流畅的游戏体验，特别是在离线后重新上线时，计划任务能够无缝继续执行。
