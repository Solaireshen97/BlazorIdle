# 轮询和心跳功能修复总结 / Polling and Heartbeat Fix Summary

## 问题描述 / Problem Description

**中文**:
当前软件前端界面打开后，虽然计划任务已经能够正常识别暂停恢复后执行中的状态了，但是轮询和发送心跳的功能似乎没能正常执行，软件前端的战斗处于一个暂停的状态。

**English**:
After the frontend interface is opened, although the scheduled task can correctly recognize the resumed running state after pausing, the polling and heartbeat sending functions do not execute properly, causing the frontend combat to appear in a paused state.

## 根本原因 / Root Cause

在 `CheckOfflineRewardsAsync()` 方法中，只有当存在离线时间（`HasOfflineTime == true`）时，才会调用 `RefreshPlansAsync()`。

如果用户在短时间内重新打开页面（没有离线时间），或者计划是通过其他方式恢复的，轮询将永远不会启动。

**In English**:
In the `CheckOfflineRewardsAsync()` method, `RefreshPlansAsync()` is only called when there is offline time (`HasOfflineTime == true`). 

If a user reopens the page within a short time (no offline time), or if a plan was resumed via another method, polling will never start.

## 解决方案 / Solution

### 修改文件 / Modified File
- `BlazorIdle/Pages/Characters.razor`

### 代码变更 / Code Changes

在 `CheckOfflineRewardsAsync()` 方法中添加 `else` 分支，确保即使没有离线时间也会调用 `RefreshPlansAsync()`：

```csharp
if (heartbeatResponse?.OfflineSettlement != null && heartbeatResponse.OfflineSettlement.HasOfflineTime)
{
    // 处理离线结算
    // ...
    await RefreshPlansAsync();
    // ...
}
else
{
    // 即使没有离线时间，也要刷新计划列表并启动轮询（如果有运行中的计划）
    await RefreshPlansAsync();
}
```

### 工作原理 / How It Works

1. 页面加载时：`OnInitializedAsync()` → `LoadUserDataAsync()` → `CheckOfflineRewardsAsync()`
2. `CheckOfflineRewardsAsync()` 更新心跳
3. 如果有离线时间，处理离线结算并调用 `RefreshPlansAsync()`
4. **新增**：如果没有离线时间，仍然调用 `RefreshPlansAsync()`（之前缺失）
5. `RefreshPlansAsync()` 检查是否有运行中的计划（State == 1）并启动轮询
6. 轮询启动后，每2秒通过 `UpdateHeartbeatIfNeededAsync()` 更新心跳

## 影响 / Impact

### 修复的场景 / Fixed Scenarios

1. **快速重新打开页面** / Quick page reload
   - 用户关闭浏览器后短时间内重新打开
   - User closes browser and reopens within short time
   
2. **通过其他方式恢复计划** / Plan resumed via other means
   - 计划通过API或其他设备恢复
   - Plan resumed via API or another device

3. **服务器重启后** / After server restart
   - 服务器重启并恢复运行中的计划
   - Server restarts and resumes running plans

### 保持不变的行为 / Unchanged Behavior

- 离线结算功能正常工作
- Offline settlement works normally
- 心跳更新机制保持不变
- Heartbeat update mechanism unchanged
- 轮询逻辑保持不变
- Polling logic unchanged

## 测试建议 / Testing Recommendations

1. 创建并启动一个计划任务
2. 关闭浏览器标签页
3. 在10秒内（无离线时间）重新打开页面
4. 验证：
   - 计划状态显示为"执行中"
   - 战斗轮询正常运行（数据每2秒更新）
   - 心跳正常发送（LastHeartbeat时间持续更新）

**English**:
1. Create and start a scheduled task
2. Close the browser tab
3. Reopen the page within 10 seconds (no offline time)
4. Verify:
   - Plan state shows as "Running"
   - Battle polling works normally (data updates every 2 seconds)
   - Heartbeat sends normally (LastHeartbeat time continuously updates)

## 相关代码 / Related Code

### RefreshPlansAsync 方法
```csharp
async Task RefreshPlansAsync()
{
    if (lastCreated is null) return;
    try
    {
        characterPlans = await Api.GetCharacterPlansAsync(lastCreated.Id);
        
        // 查找正在运行的计划并获取其战斗状态
        // State: 0=Pending, 1=Running, 2=Completed, 3=Cancelled, 4=Paused
        var runningPlan = characterPlans?.FirstOrDefault(p => p.State == 1);
        if (runningPlan?.BattleId.HasValue == true)
        {
            if (!planIsPolling)
            {
                _ = StartPlanPollingAsync(runningPlan.BattleId.Value);
            }
        }
        else
        {
            StopPlanPolling();
            currentPlanBattle = null;
        }
    }
    catch (Exception ex)
    {
        planError = $"刷新计划列表失败: {ex.Message}";
    }
}
```

### StartPlanPollingAsync 方法
```csharp
async Task StartPlanPollingAsync(Guid battleId)
{
    _planPollCts?.Cancel();
    _planPollCts = new CancellationTokenSource();
    planIsPolling = true;

    try
    {
        while (!_planPollCts.IsCancellationRequested)
        {
            try
            {
                currentPlanBattle = await Api.GetStepBattleStatusAsync(battleId, "sampled", _planPollCts.Token);
                
                // 同时刷新计划列表以获取最新的ExecutedSeconds
                if (lastCreated is not null)
                {
                    characterPlans = await Api.GetCharacterPlansAsync(lastCreated.Id);
                }
                
                // 更新心跳时间（每次刷新计划状态时）
                await UpdateHeartbeatIfNeededAsync();
                
                await InvokeAsync(StateHasChanged);
            }
            catch (Exception) { }

            await Task.Delay(2000, _planPollCts.Token);
        }
    }
    catch (TaskCanceledException) { }
    finally
    {
        planIsPolling = false;
    }
}
```

## 总结 / Summary

这是一个最小化的外科手术式修复，只添加了5行代码（包括注释），确保了轮询和心跳功能在所有场景下都能正常工作。

**English**:
This is a minimal surgical fix that adds only 5 lines of code (including comments), ensuring that polling and heartbeat functions work properly in all scenarios.
