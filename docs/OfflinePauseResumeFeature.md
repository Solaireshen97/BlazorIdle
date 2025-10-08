# Offline Pause and Resume Feature

## Overview

This document explains the implementation of offline player detection and task pause/resume functionality. This feature ensures that when players go offline, their running tasks are properly paused with saved state (rather than completed), allowing them to resume when players come back online.

## Core Features

### 1. Offline Detection and Task Pausing

**Location**: `BlazorIdle.Server/Services/OfflineDetectionService.cs`

- **Check Interval**: Every 30 seconds
- **Offline Threshold**: Configurable, default 60 seconds (via `Offline:OfflineDetectionSeconds` config)
- **Pause Mechanism**: 
  - When a player is offline beyond threshold, calls `PausePlanAsync` to pause the task
  - Saves current battle state snapshot (`BattleStateJson`) and executed duration (`ExecutedSeconds`)
  - Stops the battle engine to free memory resources
  - Sets task state to `Paused` (not `Completed`)

### 2. Task State Machine

**New State**: `ActivityState.Paused`

State transition flow:
```
Pending → Running → Paused (offline) → Running (resume online)
                 ↓
           Completed/Cancelled
```

### 3. Task Pause Method

**Location**: `ActivityPlanService.PausePlanAsync`

**Features**:
- Saves current battle state snapshot
- Updates executed duration
- Stops battle engine (frees memory)
- Sets task state to `Paused`
- Retains `BattleStateJson` for later resumption

**Differences from StopPlanAsync**:
| Feature | PausePlanAsync | StopPlanAsync |
|---------|----------------|---------------|
| Task State | Paused | Completed |
| Battle State Snapshot | Retained | Cleared |
| Resumable | Yes | No |
| Use Case | Player offline pause | Normal task completion |

### 4. Task Resume Mechanisms

#### 4.1 Resume on Player Login

**Location**: `OfflineSettlementService.CheckAndSettleAsync`

When player comes online:
1. Checks for `Running` or `Paused` state plans
2. If `Paused`, temporarily changes to `Running` for fast-forward engine processing
3. Uses `OfflineFastForwardEngine` to fast-forward simulate offline battles
4. Restores battle progress from saved `BattleStateJson` (enemy HP, waves, etc.)
5. Calculates and returns offline rewards
6. If task not completed, maintains current state awaiting resume

#### 4.2 Resume After Server Restart

**Location**: `StepBattleHostedService.RecoverPausedPlansAsync`

On server startup:
1. Finds all `Paused` state plans
2. Checks if player is online (heartbeat within 60 seconds)
3. If player is online, automatically calls `StartPlanAsync` to resume task
4. If player is offline, maintains `Paused` state, awaits player login for offline settlement

### 5. Enhanced Start Method

**Location**: `ActivityPlanService.StartPlanAsync`

**Enhancements**:
- Supports starting new `Pending` tasks
- Supports resuming `Paused` tasks
- Restores battle state snapshot from `BattleStateJson`
- Inherits previous `ExecutedSeconds` and battle progress

### 6. Enhanced Offline Fast-Forward Engine

**Location**: `OfflineFastForwardEngine.FastForward`

**Enhancements**:
- Supports processing `Running` and `Paused` state plans
- Restores battle state from `BattleStateJson`
- Ensures seamless progress inheritance (enemy HP, kill count, waves, etc.)
- Updates battle state snapshot for next use

## Configuration

In `appsettings.json`:

```json
{
  "Offline": {
    "OfflineDetectionSeconds": 60,     // Offline detection threshold (seconds)
    "MaxOfflineSeconds": 43200,        // Max offline duration (12 hours)
    "EnableAutoSettlement": true       // Enable auto settlement
  }
}
```

## Use Cases

### Scenario 1: Short Offline Period

1. Player is executing a 2-hour combat task
2. Player goes offline for 10 minutes
3. `OfflineDetectionService` detects offline > 60 seconds, calls `PausePlanAsync`
4. Task state changes to `Paused`, current progress saved
5. Player comes back online
6. `CheckAndSettleAsync` fast-forwards 10 minutes of combat, calculates rewards
7. Returns offline rewards for player to claim
8. Task continues execution (if not completed)

### Scenario 2: Server Restart

1. Player is executing a task, server needs maintenance restart
2. Before shutdown, task snapshot saved to database
3. Server restarts
4. `RecoverPausedPlansAsync` recovers all paused tasks
5. If player still online, task automatically resumes
6. If player offline, maintains paused state awaiting player login

### Scenario 3: Long Offline Period

1. Player is executing an infinite duration combat task
2. Player goes offline for 24 hours
3. Task is paused and state saved
4. Player comes back online
5. Offline settlement calculates maximum 12 hours of rewards (limited by `MaxOfflineSeconds`)
6. Task continues execution (from pre-offline progress)

## Technical Details

### Battle State Snapshot

`BattleStateJson` contains:
- Enemy health state (`EnemyHealthState`)
- Current wave index (`WaveIndex`)
- Completed battle runs (`RunCount`)
- Snapshot timestamp (`SnapshotAtSeconds`)

### Seamless Progress Inheritance

Progress inheritance is ensured through:
1. Saving complete battle state snapshot on pause
2. Restoring enemy HP, waves, etc. from snapshot on resume
3. Using `BattleEngine.RestoreBattleState` method to restore engine state
4. Inheriting `ExecutedSeconds` for accurate time calculations

### Memory Management

- Stops battle engine on pause, frees memory resources
- Battle state snapshot serialized to JSON and stored in database
- On resume, recreates battle engine and restores state from snapshot

## Testing Recommendations

### Unit Tests

1. Test `PausePlanAsync` correctly saves state
2. Test `StartPlanAsync` can resume paused tasks
3. Test `OfflineFastForwardEngine` supports paused state plans
4. Test battle state snapshot serialization/deserialization

### Integration Tests

1. Test complete offline-online cycle
2. Test resuming paused tasks after server restart
3. Test concurrent offline/online for multiple players
4. Test offline duration exceeding 12 hours

### Scenario Tests

1. Create a combat task
2. Wait for task to run for some time
3. Simulate player offline (stop heartbeat updates)
4. Wait 60 seconds, observe if task is paused
5. Simulate player online (resume heartbeat)
6. Verify offline rewards calculated correctly
7. Verify task continues execution

## Troubleshooting

### Issue: Task marked as Completed instead of Paused

**Cause**: Still using `StopPlanAsync` instead of `PausePlanAsync`

**Solution**: Ensure `OfflineDetectionService.CheckAndPauseOfflinePlayers` calls `PausePlanAsync`

### Issue: Paused task cannot resume

**Cause**: `StartPlanAsync` doesn't support `Paused` state

**Solution**: Ensure `StartPlanAsync` allows starting `Paused` state plans

### Issue: Progress lost after resume

**Cause**: Battle state snapshot not properly saved or restored

**Solution**: 
1. Check if `BattleStateJson` is correctly saved
2. Check if `BattleEngine.RestoreBattleState` is correctly called
3. Check if `ExecutedSeconds` is correctly updated

### Issue: Paused tasks not recovered after server restart

**Cause**: `RecoverPausedPlansAsync` not executed or errored

**Solution**: 
1. Check `StepBattleHostedService` startup logs
2. Check if exceptions are caught and logged
3. Verify database has `Paused` state plans

## Future Improvements

1. **Heartbeat Optimization**: Implement more precise heartbeat detection to reduce false positives
2. **Pause Notifications**: Push task pause notifications to clients
3. **Manual Pause**: Support player-initiated pause/resume
4. **Pause History**: Record task pause/resume history
5. **Multi-task Queue**: Support multiple tasks queued for execution
6. **Offline Compensation**: Provide additional compensation based on offline duration

## Related Documentation

- [Offline Battle Implementation Plan](./OfflineBattleImplementationPlan.md)
- [Activity Plan System Summary](./ACTIVITY_PLAN_AUTO_COMPLETION_SUMMARY.md)
- [Implementation Summary](../实施完成报告.md)
