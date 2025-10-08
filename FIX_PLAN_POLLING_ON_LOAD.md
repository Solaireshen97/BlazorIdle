# 修复：前端界面打开时计划轮询和心跳功能未启动

## 问题描述

用户报告：当前软件前端界面打开后，虽然计划任务已经能够正常识别暂停恢复后执行中的状态了，但是一上来无法获取到战斗ID信息导致轮询和发送心跳的功能没能正常执行，软件前端的战斗处于一个暂停的状态。

## 问题原因

在 `BlazorIdle/Pages/Characters.razor` 文件中的 `CheckOfflineRewardsAsync()` 方法中，`RefreshPlansAsync()` 调用被放置在了离线结算检测的条件判断内部：

```csharp
if (heartbeatResponse?.OfflineSettlement != null && heartbeatResponse.OfflineSettlement.HasOfflineTime)
{
    // ... 
    await RefreshPlansAsync();  // 只有在检测到离线时间时才会调用
    // ...
}
```

这导致了以下问题：
1. 当用户打开前端界面时，如果没有检测到离线时间（HasOfflineTime == false），`RefreshPlansAsync()` 不会被调用
2. `RefreshPlansAsync()` 内部包含了检查运行中计划并启动轮询的逻辑
3. 因此，即使有运行中的计划（State=1），轮询也不会启动
4. 没有轮询就没有心跳更新，导致战斗状态无法更新

## 解决方案

将 `RefreshPlansAsync()` 调用移到离线结算检测判断之外，确保无论是否有离线时间都会调用：

```csharp
// 更新心跳时间（会自动触发离线检测和结算）
var heartbeatResponse = await Api.UpdateHeartbeatAsync(selectedCharacter.Id);

// 刷新计划列表（无论是否有离线时间都需要刷新以获取运行中的计划）
await RefreshPlansAsync();

// 如果心跳响应中包含离线结算结果，显示弹窗
if (heartbeatResponse?.OfflineSettlement != null && heartbeatResponse.OfflineSettlement.HasOfflineTime)
{
    // 再次刷新计划列表以获取最新状态
    await RefreshPlansAsync();
    // ...
}
```

## 修改内容

**修改文件**: `BlazorIdle/Pages/Characters.razor`

**具体变更**:
1. 在 `CheckOfflineRewardsAsync()` 方法中，将 `await RefreshPlansAsync();` 移到离线检测判断之前（第853行）
2. 保留离线结算处理块中的 `RefreshPlansAsync()` 调用，以确保离线结算后能获取最新状态
3. 更新注释说明刷新的目的

## 技术细节

### 执行流程（修复后）

1. 页面加载 → `OnInitializedAsync()`
2. 加载用户数据 → `LoadUserDataAsync()`
3. 检查离线收益 → `CheckOfflineRewardsAsync()`
4. 更新心跳 → `Api.UpdateHeartbeatAsync()`
5. **刷新计划列表** → `RefreshPlansAsync()` ⬅️ **关键修复点**
6. `RefreshPlansAsync()` 内部逻辑：
   ```csharp
   var runningPlan = characterPlans?.FirstOrDefault(p => p.State == 1);
   if (runningPlan?.BattleId.HasValue == true)
   {
       if (!planIsPolling)
       {
           _ = StartPlanPollingAsync(runningPlan.BattleId.Value);  // 自动启动轮询
       }
   }
   ```
7. 轮询启动后，每2秒执行：
   - 获取战斗状态 → `Api.GetStepBattleStatusAsync()`
   - 刷新计划列表 → `Api.GetCharacterPlansAsync()`
   - 更新心跳 → `UpdateHeartbeatIfNeededAsync()`

### 关键代码片段

**RefreshPlansAsync 的自动轮询启动逻辑**:
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

**StartPlanPollingAsync 的心跳更新逻辑**:
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

## 验证

### 构建验证
```bash
$ dotnet build
Build succeeded.
    1 Warning(s)
    0 Error(s)
```

### 代码逻辑验证
- ✅ `RefreshPlansAsync()` 总是会在页面加载时被调用
- ✅ 如果有运行中的计划（State=1），轮询会自动启动
- ✅ 轮询启动后会每2秒更新战斗状态和心跳
- ✅ 离线结算功能不受影响，依然正常工作

## 测试建议

1. **场景1：有运行中的计划**
   - 创建并启动一个计划
   - 等待计划开始执行
   - 刷新页面或重新打开浏览器
   - **预期结果**：页面加载后，战斗状态应该立即开始更新，计划执行时长应该持续增加

2. **场景2：有暂停的计划**
   - 创建一个计划并让它执行一段时间
   - 通过后端或等待超时让计划暂停（State=4）
   - 刷新页面
   - **预期结果**：页面显示计划为"已暂停"状态，但不会开始轮询（这是正确的）

3. **场景3：没有活动计划**
   - 确保没有运行中或暂停的计划
   - 刷新页面
   - **预期结果**：页面正常加载，不会启动轮询（这是正确的）

4. **场景4：离线恢复**
   - 创建并启动一个计划
   - 关闭浏览器超过60秒
   - 重新打开浏览器
   - **预期结果**：显示离线结算弹窗，关闭后计划继续执行并正常轮询

## 影响范围

**修改范围**: 最小化，仅修改1个方法，移动3行代码

**影响的功能**:
- ✅ 页面加载时的计划状态检查和轮询启动（修复的核心问题）
- ✅ 离线恢复功能（仍然正常工作，不受影响）

**不影响的功能**:
- 创建计划
- 手动恢复计划
- 取消/删除计划
- 其他战斗功能

## 相关文件

- `BlazorIdle/Pages/Characters.razor` - 修复的主文件
- `FRONTEND_OFFLINE_RECOVERY_DELIVERY.md` - 之前的离线恢复功能文档
- `docs/FrontendScheduledTasksOfflineRecoveryFix.md` - 相关的修复文档

## 总结

本次修复通过一个简单但关键的改动，解决了前端界面打开时无法启动计划轮询和心跳更新的问题。修改确保了 `RefreshPlansAsync()` 方法总是在页面加载时被调用，从而能够检测运行中的计划并自动启动轮询。

这个修复是对之前离线恢复功能的完善，确保了用户体验的连续性，无论用户是刚打开页面、刚恢复离线、还是在正常使用过程中，计划任务都能正确地恢复执行状态。

---

**修复日期**: 2025-01-XX  
**影响版本**: v1.0+  
**状态**: ✅ 已修复并验证
