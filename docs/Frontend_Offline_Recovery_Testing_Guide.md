# Frontend Offline Recovery Feature Testing Guide

## Overview

This document describes how to test the frontend activity plan status offline recovery feature to ensure all task types (single enemy continuous/infinite, dungeon continuous/infinite) properly support offline recovery functionality.

## Feature Description

### Core Features
1. **Offline Pause**: When a player goes offline beyond the threshold (default 60 seconds), the background service automatically pauses running tasks
2. **Offline Fast-forward**: When the player logs back in, the system fast-forwards and simulates battles during offline period and grants rewards
3. **Auto Resume**: If the task is incomplete, the system automatically resumes the task and continues execution
4. **Frontend Polling**: The frontend automatically detects resumed tasks and starts status polling

### Task State Descriptions
- **State = 0 (Pending)**: Pending - Yellow text
- **State = 1 (Running)**: Running - Green text
- **State = 2 (Completed)**: Completed - Gray text
- **State = 3 (Cancelled)**: Cancelled - Gray text
- **State = 4 (Paused)**: Paused - Blue text

## Test Environment Setup

### 1. Start Server
```bash
cd /home/runner/work/BlazorIdle/BlazorIdle/BlazorIdle.Server
dotnet run
```

### 2. Start Frontend
```bash
cd /home/runner/work/BlazorIdle/BlazorIdle/BlazorIdle
dotnet watch run
```

### 3. Configure Offline Detection Threshold (Optional)
Edit `appsettings.json`:
```json
{
  "Offline": {
    "OfflineDetectionSeconds": 60,
    "MaxOfflineSeconds": 43200,
    "EnableAutoSettlement": true
  }
}
```

## Test Scenarios

### Scenario 1: Single Enemy Continuous Mode Offline Recovery

#### Steps
1. **Create Character**
   - Open browser and visit `http://localhost:5000`
   - Login to account
   - Create a new character (if not already created)

2. **Create Activity Plan**
   - Activity Type: Combat
   - Limit Type: Duration
   - Duration: 300 seconds (5 minutes)
   - Enemy: Select any enemy (e.g., Training Dummy)
   - Enemy Count: 1
   - Click "Create Plan"

3. **Wait for Task to Start**
   - Task starts automatically (State = 1)
   - Observe "Current Plan Battle Status" panel, confirm DPS, kills, etc. are updating
   - Wait about 10-20 seconds to ensure task has executed for a while

4. **Simulate Offline**
   - Close browser tab (don't click logout)
   - Wait at least 70 seconds (exceeds 60-second offline detection threshold)

5. **Verify Pause** (Optional, requires database access)
   - Query database on server side, confirm task state changed to Paused (State = 4)
   - Confirm `BattleStateJson` field has saved battle state snapshot

6. **Return Online**
   - Reopen browser and visit application
   - Login with same account

7. **Verify Offline Settlement Dialog**
   - Should see "Welcome Back!" dialog
   - Dialog displays:
     - Offline duration (about 70 seconds)
     - Offline rewards (gold, experience, kills)
     - Task status: "▶️ Activity plan continues executing"
     - Executed duration (should include offline period duration)

8. **Close Dialog and Verify Polling**
   - Click "Close" button
   - Confirm task status in task list is "Running" (State = 1)
   - Confirm "Current Plan Battle Status" panel appears and displays real-time data
   - Observe ExecutedSeconds continues increasing
   - Wait for task completion or manually stop

#### Expected Results
- ✅ Task paused during offline period
- ✅ Offline reward dialog displayed upon login
- ✅ Task automatically resumes execution
- ✅ Frontend automatically starts polling task status
- ✅ Task progress seamlessly continues, ExecutedSeconds correctly accumulates

---

### Scenario 2: Single Enemy Infinite Mode Offline Recovery

#### Steps
1. **Create Activity Plan**
   - Activity Type: Combat
   - Limit Type: **Infinite**
   - Enemy: Select any enemy
   - Enemy Count: 1
   - Click "Create Plan"

2. **Wait for Task to Start**
   - Task starts automatically
   - Wait about 10-20 seconds

3. **Simulate Offline**
   - Close browser tab
   - Wait at least 70 seconds

4. **Return Online**
   - Reopen browser and login

5. **Verify Offline Settlement Dialog**
   - Should see offline reward dialog
   - Task status should display: "▶️ Activity plan continues executing" (because it's infinite mode)

6. **Close Dialog and Verify Polling**
   - Click "Close" button
   - Confirm task status is "Running"
   - Confirm polling works normally
   - **Important**: Because it's infinite mode, task will continue executing indefinitely

7. **Manually Stop Task**
   - Click "Stop" button
   - Confirm task status changes to "Completed"

#### Expected Results
- ✅ Infinite mode task can be paused offline
- ✅ Task continues after coming back online
- ✅ Task doesn't complete due to offline
- ✅ Can manually stop task

---

### Scenario 3: Dungeon Continuous Mode Offline Recovery

#### Steps
1. **Create Activity Plan**
   - Activity Type: Dungeon
   - Limit Type: Duration
   - Duration: 300 seconds
   - Dungeon ID: intro_cave
   - Loop Mode: **Unchecked** (single run mode)
   - Click "Create Plan"

2. **Wait for Task to Start**
   - Task starts automatically
   - Wait about 10-20 seconds

3. **Simulate Offline**
   - Close browser tab
   - Wait at least 70 seconds

4. **Return Online**
   - Reopen browser and login

5. **Verify Offline Settlement Dialog**
   - Should see offline reward dialog
   - Check if displays "Activity plan continues executing" or "Activity plan completed"
   - Note: If dungeon completed during offline period, will display "✅ Activity plan completed"

6. **Close Dialog and Verify**
   - Click "Close" button
   - If task incomplete:
     - Confirm task status is "Running"
     - Confirm polling works normally
   - If task completed:
     - Confirm task status is "Completed"
     - Can choose to delete the task

#### Expected Results
- ✅ Dungeon task can be paused offline
- ✅ Waves and kills during offline period correctly accumulated
- ✅ Task resumes current wave after recovery
- ✅ Task status correctly updates when dungeon completes

---

### Scenario 4: Dungeon Infinite Mode (Loop) Offline Recovery

#### Steps
1. **Create Activity Plan**
   - Activity Type: Dungeon
   - Limit Type: **Infinite**
   - Dungeon ID: intro_cave
   - Loop Mode: **Checked** (loop mode)
   - Click "Create Plan"

2. **Wait for Task to Start**
   - Task starts automatically
   - Wait for dungeon to complete at least one loop (observe kills and waves)
   - Wait about 20-30 seconds

3. **Simulate Offline**
   - Close browser tab
   - Wait at least 70 seconds

4. **Return Online**
   - Reopen browser and login

5. **Verify Offline Settlement Dialog**
   - Should see offline reward dialog
   - Task status should display: "▶️ Activity plan continues executing"
   - Should see accumulated rewards from multiple dungeon completions

6. **Close Dialog and Verify Polling**
   - Click "Close" button
   - Confirm task status is "Running"
   - Confirm polling works normally
   - Observe dungeon continues looping

7. **Manually Stop Task**
   - Click "Stop" button
   - Confirm task status changes to "Completed"

#### Expected Results
- ✅ Loop dungeon task can be paused offline
- ✅ Multiple dungeon completions during offline correctly accumulated
- ✅ Task continues looping after recovery
- ✅ Can manually stop loop task

---

### Scenario 5: Manual Resume from Paused State

#### Steps
1. **Create Any Activity Plan and Start**
   - Select any task type (combat or dungeon)
   - Limit type can be duration or infinite

2. **Simulate Offline to Cause Pause**
   - Wait for task to execute 10-20 seconds
   - Close browser
   - Wait at least 70 seconds

3. **Verify Paused State** (requires database access)
   - Query database on server side
   - Confirm task state is Paused (State = 4)

4. **Return Online but Don't Close Dialog**
   - Reopen browser and login
   - See offline reward dialog
   - **Don't click close button**

5. **Manually Refresh Plan List**
   - Find "Refresh List" button outside dialog and click
   - Observe task list

6. **Verify Paused State Display**
   - Task status should display as "Paused" (blue text)
   - Should see "Resume" and "Cancel" buttons

7. **Click Resume Button**
   - Click "Resume" button
   - Confirm task status changes to "Running"
   - Confirm polling starts automatically
   - Confirm "Current Plan Battle Status" panel displays and updates

#### Expected Results
- ✅ Paused state correctly displays as "Paused" (blue)
- ✅ Displays "Resume" and "Cancel" buttons
- ✅ Task resumes execution after clicking "Resume"
- ✅ Frontend automatically starts polling
- ✅ Progress seamlessly continues

---

## Troubleshooting

### Issue 1: Task Not Paused After Offline
**Possible Causes**:
- Offline duration less than 60 seconds
- OfflineDetectionService not started
- Offline detection threshold configured too large

**Solutions**:
- Ensure offline for at least 70 seconds
- Check server logs, confirm OfflineDetectionService is running
- Check `OfflineDetectionSeconds` configuration in appsettings.json

### Issue 2: No Offline Reward Dialog After Login
**Possible Causes**:
- Heartbeat update failed
- Offline settlement service not properly configured
- Frontend not correctly calling heartbeat API

**Solutions**:
- Check browser console network requests
- Confirm `/api/users/heartbeat` endpoint responds normally
- Check offline settlement related logs in server logs

### Issue 3: Task Polling Not Auto-Starting After Closing Dialog
**Possible Causes**:
- RefreshPlansAsync method not correctly called
- Task state not Running (State = 1)
- BattleId is empty

**Solutions**:
- Check browser console for error messages
- Confirm task state has resumed to Running
- Confirm BattleId field is not empty
- Manually click "Refresh List" button

### Issue 4: Task Progress Discontinuous
**Possible Causes**:
- BattleStateJson not correctly saved
- FastForward engine didn't correctly restore battle state

**Solutions**:
- Check BattleStateJson field in database
- Review FastForward related logs in server logs
- Confirm ExecutedSeconds field correctly updated

## Performance Verification

### Verification Points
1. **Memory Usage**: Paused tasks should not occupy battle engine memory
2. **Response Speed**: Resume task should complete within 2 seconds
3. **Data Accuracy**: Offline period reward calculation should be accurate
4. **Polling Frequency**: Task status polling should execute every 2 seconds

### Verification Methods
- Monitor network requests using browser developer tools
- Check performance metrics in server logs
- Compare gold, experience, kills, etc. before and after offline

## Test Report Template

```markdown
## Test Report

### Test Environment
- Date: YYYY-MM-DD
- Tester: [Name]
- Server Version: [Version]
- Frontend Version: [Version]

### Test Results

#### Scenario 1: Single Enemy Continuous Mode
- [ ] Pass / [ ] Fail
- Issue Description: 

#### Scenario 2: Single Enemy Infinite Mode
- [ ] Pass / [ ] Fail
- Issue Description: 

#### Scenario 3: Dungeon Continuous Mode
- [ ] Pass / [ ] Fail
- Issue Description: 

#### Scenario 4: Dungeon Infinite Mode
- [ ] Pass / [ ] Fail
- Issue Description: 

#### Scenario 5: Manual Resume Test
- [ ] Pass / [ ] Fail
- Issue Description: 

### Discovered Issues
1. 
2. 
3. 

### Recommendations
1. 
2. 
3. 
```

## Summary

Through the above test scenarios, the frontend activity plan status offline recovery feature can be comprehensively verified. Focus on:
1. Task pause and resume state transitions
2. Correct calculation and display of offline rewards
3. Automatic start of frontend polling
4. Compatibility of various task types
5. Smooth user experience

After ensuring all scenarios work properly, the frontend offline recovery feature can be deployed to production.
