# UI Changes Visual Guide - Offline Recovery Feature

## Overview
This document provides a visual guide to the UI changes made to support the offline recovery feature for activity plans.

## State Display Changes

### Before Fix
The task list only displayed 4 states:

| State Value | Display Text | Text Color | Available Actions |
|-------------|--------------|------------|-------------------|
| 0 | 待执行 | Yellow | Cancel |
| 1 | 执行中 | Green | Stop, View Battle |
| 2 | 已完成 | Gray | Delete |
| 3 | 已取消 | Gray | Delete |
| 4 | ❌ Not Displayed | - | ❌ No Actions |

**Problem**: When a task was paused (State = 4) due to player going offline, the frontend couldn't properly display or handle it.

### After Fix
The task list now displays all 5 states:

| State Value | Display Text | Text Color | Available Actions |
|-------------|--------------|------------|-------------------|
| 0 | 待执行 | Yellow | Cancel |
| 1 | 执行中 | Green | Stop, View Battle |
| 2 | 已完成 | Gray | Delete |
| 3 | 已取消 | Gray | Delete |
| 4 | ✅ 已暂停 | **Blue** | **Resume, Cancel** |

**Solution**: Paused tasks are now clearly displayed with blue text and have "Resume" and "Cancel" actions available.

## UI Component Changes

### 1. Task Status Text
```razor
<!-- Before -->
var stateName = plan.State == 0 ? "待执行" : 
                plan.State == 1 ? "执行中" : 
                plan.State == 2 ? "已完成" : "已取消";

<!-- After -->
var stateName = plan.State == 0 ? "待执行" : 
                plan.State == 1 ? "执行中" : 
                plan.State == 2 ? "已完成" : 
                plan.State == 3 ? "已取消" : 
                plan.State == 4 ? "已暂停" : "未知";
```

### 2. Task Status Color
```razor
<!-- Before -->
var stateClass = plan.State == 0 ? "text-warning" : 
                 plan.State == 1 ? "text-success" : 
                 plan.State == 2 ? "text-secondary" : "text-muted";

<!-- After -->
var stateClass = plan.State == 0 ? "text-warning" : 
                 plan.State == 1 ? "text-success" : 
                 plan.State == 2 ? "text-secondary" : 
                 plan.State == 3 ? "text-muted" : 
                 plan.State == 4 ? "text-info" : "text-dark";
```

### 3. Action Buttons

#### Before Fix
```html
<!-- State 0: Pending -->
<button class="btn btn-sm btn-danger">取消</button>

<!-- State 1: Running -->
<button class="btn btn-sm btn-warning">停止</button>
<button class="btn btn-sm btn-info">查看战斗</button>

<!-- State 2/3: Completed/Cancelled -->
<button class="btn btn-sm btn-outline-secondary">删除</button>

<!-- State 4: Paused - NO BUTTONS SHOWN ❌ -->
```

#### After Fix
```html
<!-- State 0: Pending -->
<button class="btn btn-sm btn-danger">取消</button>

<!-- State 1: Running -->
<button class="btn btn-sm btn-warning">停止</button>
<button class="btn btn-sm btn-info">查看战斗</button>

<!-- State 2/3: Completed/Cancelled -->
<button class="btn btn-sm btn-outline-secondary">删除</button>

<!-- State 4: Paused - NEW ✅ -->
<button class="btn btn-sm btn-success">恢复</button>
<button class="btn btn-sm btn-danger">取消</button>
```

## Visual Representation

### Task List Table - Before
```
┌─────────────────────────────────────────────────────────────────────┐
│ ID       │ 类型  │ 状态    │ 槽位 │ 限制  │ 执行时长 │ 操作        │
├─────────────────────────────────────────────────────────────────────┤
│ 12345678 │ 战斗  │ 执行中  │ 0    │ 300秒 │ 45秒     │ [停止]     │
│ 87654321 │ 副本  │ 已完成  │ 1    │ 无限  │ 120秒    │ [删除]     │
│ ABCDEF12 │ 战斗  │ ???     │ 2    │ 300秒 │ 30秒     │ (no btns)  │
└─────────────────────────────────────────────────────────────────────┘
                                     ↑ Paused task not displayed properly
```

### Task List Table - After
```
┌─────────────────────────────────────────────────────────────────────┐
│ ID       │ 类型  │ 状态    │ 槽位 │ 限制  │ 执行时长 │ 操作        │
├─────────────────────────────────────────────────────────────────────┤
│ 12345678 │ 战斗  │ 执行中  │ 0    │ 300秒 │ 45秒     │ [停止]     │
│ 87654321 │ 副本  │ 已完成  │ 1    │ 无限  │ 120秒    │ [删除]     │
│ ABCDEF12 │ 战斗  │ 已暂停  │ 2    │ 300秒 │ 30秒     │ [恢复][取消]│
└─────────────────────────────────────────────────────────────────────┘
                         ↑ Blue text      ↑ New action buttons
```

## Offline Recovery Flow - UI Perspective

### User Journey

```
1. Player creates and starts a task
   ┌─────────────────────────┐
   │ Task List               │
   │ ─────────────────────── │
   │ Task 1: 执行中 (Green)  │
   │ [停止] [查看战斗]        │
   └─────────────────────────┘

2. Player goes offline (closes browser)
   ↓ 70 seconds pass
   ↓ Backend detects offline and pauses task

3. Player comes back online (opens browser)
   ┌─────────────────────────┐
   │ 🎉 欢迎回来！           │
   │                         │
   │ 离线时长: 70秒          │
   │ 金币: +500              │
   │ 经验: +1000             │
   │ 击杀: 15                │
   │                         │
   │ ▶️ 活动计划继续执行中   │
   │                         │
   │     [关闭]              │
   └─────────────────────────┘

4. Player clicks [关闭] - AUTO POLLING STARTS ✅
   ┌─────────────────────────┐
   │ Task List               │
   │ ─────────────────────── │
   │ Task 1: 执行中 (Green)  │ ← Automatically resumed
   │ [停止] [查看战斗]        │
   │                         │
   │ 当前计划战斗状态         │
   │ DPS: 1250              │ ← Polling active
   │ SimSec: 95.5           │ ← Updates every 2s
   └─────────────────────────┘
```

### Alternative Flow - Manual Resume

```
3b. Player sees dialog but doesn't close it immediately
    Instead, clicks [刷新列表] outside the dialog
    
   ┌─────────────────────────┐
   │ Task List               │
   │ ─────────────────────── │
   │ Task 1: 已暂停 (Blue)   │ ← Shows paused state
   │ [恢复] [取消]            │ ← New buttons available
   └─────────────────────────┘

4b. Player clicks [恢复] - MANUAL RESUME + AUTO POLLING ✅
   ┌─────────────────────────┐
   │ Task List               │
   │ ─────────────────────── │
   │ Task 1: 执行中 (Green)  │ ← Changed to Running
   │ [停止] [查看战斗]        │
   │                         │
   │ 当前计划战斗状态         │
   │ DPS: 1250              │ ← Polling active
   │ SimSec: 95.5           │ ← Updates every 2s
   └─────────────────────────┘
```

## Color Coding System

| State | Color | Bootstrap Class | Visual Purpose |
|-------|-------|-----------------|----------------|
| Pending (0) | 🟡 Yellow | `text-warning` | Task waiting to start |
| Running (1) | 🟢 Green | `text-success` | Task actively executing |
| Completed (2) | ⚫ Gray | `text-secondary` | Task finished normally |
| Cancelled (3) | ⚫ Gray | `text-muted` | Task stopped by user |
| Paused (4) | 🔵 Blue | `text-info` | Task paused (offline) |

## Button Styles

| Action | Color | Bootstrap Class | When Visible |
|--------|-------|-----------------|--------------|
| 创建计划 | Blue | `btn-primary` | Always (if not at max) |
| 停止 | Yellow | `btn-warning` | State = Running |
| 取消 | Red | `btn-danger` | State = Pending or Paused |
| 恢复 | Green | `btn-success` | State = Paused |
| 删除 | Gray | `btn-outline-secondary` | State = Completed or Cancelled |
| 查看战斗 | Blue | `btn-info` | State = Running (with BattleId) |

## Implementation Details

### Frontend Changes Summary

#### Files Modified
1. **BlazorIdle/Services/ApiClient.cs**
   - Added `ResumePlanAsync()` method
   - Calls `/api/activity-plans/{id}/resume` endpoint

2. **BlazorIdle/Pages/Characters.razor**
   - Updated state name mapping (line ~298)
   - Updated state color mapping (line ~299)
   - Added Paused state action buttons (line ~327-332)
   - Added `ResumePlanAsync()` method (line ~1393-1415)
   - Enhanced `CloseOfflineSettlement()` method (line ~862-875)
   - Added state comments in `RefreshPlansAsync()` (line ~1293)

### Key Logic Changes

#### Auto-Polling Trigger Points

1. **After Offline Recovery (Automatic)**
```csharp
// In CloseOfflineSettlement()
if (offlineCheckResult?.PlanCompleted == false)
{
    await RefreshPlansAsync(); // This triggers polling if State = 1
}
```

2. **After Manual Resume (Explicit)**
```csharp
// In ResumePlanAsync()
var response = await Api.ResumePlanAsync(planId);
if (response?.BattleId != Guid.Empty)
{
    await RefreshPlansAsync();
    _ = StartPlanPollingAsync(response.BattleId); // Explicit start
}
```

3. **During Plan List Refresh (Automatic)**
```csharp
// In RefreshPlansAsync()
var runningPlan = characterPlans?.FirstOrDefault(p => p.State == 1);
if (runningPlan?.BattleId.HasValue == true)
{
    if (!planIsPolling)
    {
        _ = StartPlanPollingAsync(runningPlan.BattleId.Value);
    }
}
```

## Testing Checklist

Use this checklist when manually testing the UI changes:

### Visual Verification
- [ ] Paused state displays as "已暂停" in blue text
- [ ] Resume button appears for paused tasks
- [ ] Resume button is green (success style)
- [ ] Cancel button appears alongside Resume button
- [ ] All 5 states have distinct colors
- [ ] State transitions update UI immediately

### Functional Verification
- [ ] Clicking Resume button resumes the task
- [ ] Clicking Resume button starts polling automatically
- [ ] Closing offline dialog refreshes plan list
- [ ] Closing offline dialog starts polling if task continues
- [ ] Battle status panel appears and updates every 2s
- [ ] ExecutedSeconds increases continuously
- [ ] All task types work (combat continuous/infinite, dungeon continuous/loop)

### Edge Cases
- [ ] Multiple paused tasks display correctly
- [ ] Resume works after server restart
- [ ] Polling stops when task completes
- [ ] Polling doesn't start for completed tasks
- [ ] UI handles API errors gracefully
- [ ] Refresh button works while polling

## Conclusion

The UI changes provide clear visual feedback for all task states, including the previously missing Paused state. The automatic polling mechanism ensures a smooth user experience when tasks are recovered from offline, eliminating the need for manual refreshing.

Key improvements:
- ✅ Complete state coverage (all 5 states)
- ✅ Clear visual distinction (5 different colors)
- ✅ Intuitive action buttons (context-appropriate)
- ✅ Automatic polling (seamless UX)
- ✅ Manual control (Resume button)

These changes fully integrate the frontend with the backend's offline pause/resume functionality, providing a complete offline recovery experience for users.
