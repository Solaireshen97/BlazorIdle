# Attack Progress Bar Optimization Summary

## Overview

This optimization implements smooth, interpolated attack progress bars in the battle UI. Instead of showing binary states (0% or 100%), progress bars now smoothly increase based on attack speed and are corrected during each polling cycle.

## Key Changes

### Backend (Server)

**File**: `BlazorIdle.Server/Application/Battles/Step/StepBattleCoordinator.cs`

Added attack interval information to `StepBattleStatusDto`:
- `AttackInterval` - Normal attack interval in seconds
- `SpecialInterval` - Special attack interval in seconds

These intervals are extracted from the `TrackState` objects and sent to the client alongside the existing `NextAttackAt` and `NextSpecialAt` values.

### Client API Models

**File**: `BlazorIdle/Services/ApiModels.cs`

Added corresponding properties to `StepStatusResponse`:
- `AttackInterval`
- `SpecialInterval`

### Frontend (Blazor)

**File**: `BlazorIdle/Pages/Characters.razor`

#### 1. Client-Side State Tracking

Added tracking fields for both Step battles and Activity Plan battles:
```csharp
// Step battle tracking
private DateTime _lastStepUpdateTime;
private double _lastServerCurrentTime;
private double? _lastServerNextAttackAt;
private double? _lastServerAttackInterval;
// ... and special attack equivalents

// Activity plan battle tracking (similar set)
```

#### 2. UI Refresh Timer

Added a 100ms timer for smooth UI updates:
```csharp
private System.Timers.Timer? _uiRefreshTimer;
```

The timer starts when polling begins and stops when polling ends or component is disposed.

#### 3. Progress Calculation

Implemented four calculation methods:
- `CalculateStepAttackProgress()` - Normal attack for Step battles
- `CalculateStepSpecialProgress()` - Special attack for Step battles
- `CalculatePlanAttackProgress()` - Normal attack for Activity Plan battles
- `CalculatePlanSpecialProgress()` - Special attack for Activity Plan battles

**Algorithm**:
1. Calculate client elapsed time since last server update
2. Estimate current server time = last server time + client elapsed
3. Calculate attack start time = next attack time - attack interval
4. Calculate progress = (estimated time - start time) / interval
5. Clamp progress to [0.0, 1.0] range

#### 4. Server State Synchronization

During each poll, update tracking variables with latest server state:
```csharp
_lastStepUpdateTime = DateTime.UtcNow;
_lastServerCurrentTime = s.CurrentTime;
_lastServerNextAttackAt = s.NextAttackAt;
_lastServerAttackInterval = s.AttackInterval;
```

This ensures any server-side resets (monster death, respawn, target switch) are immediately reflected in the progress bar.

#### 5. UI Template Updates

Updated Razor templates to use the new calculation methods:
```razor
@if (stepStatus.NextAttackAt.HasValue)
{
    var (attackProgress, attackTime) = CalculateStepAttackProgress();
    <div style="width: @(attackProgress * 100)%;"></div>
}
```

Removed CSS transitions as progress now updates continuously.

## How It Handles Reset Scenarios

### Monster Death
- Server calls `ResetAttackProgress()` which sets `NextTriggerAt = CurrentTime + Interval`
- Client polls and receives new `NextAttackAt` value
- Progress calculation automatically starts from 0% with new cycle

### Monster Respawn
- Server resets attack progress during spawn wait
- Client synchronizes on next poll
- Progress bar starts fresh with new monster

### Target Switch
- Server calls `ResetAttackProgress()` when retargeting
- Client receives updated state
- Progress bar resets to beginning of new cycle

### Time Drift Correction
- Every poll cycle resets the client's time reference
- Prevents accumulated drift between client and server clocks
- Even if client estimates incorrectly, next poll will correct it

## Testing

### Unit Tests

Created `AttackProgressCalculationTests.cs` with 9 tests covering:
1. Progress starts at 0% at beginning of cycle
2. Progress reaches 50% at halfway point
3. Progress reaches 100% at end of cycle
4. Progress clamps at 100% (no overflow)
5. Progress resets correctly after server update
6. Fast attack speed (0.5s interval)
7. Slow attack speed (5s interval)
8. Very small time intervals (precision test)
9. Haste effect (dynamic interval changes)

**All tests pass ✅**

### Manual Testing Scenarios

Recommended manual tests:
1. Duration mode - observe smooth progression
2. Continuous mode - verify reset on monster death
3. Dungeon mode - check wave/run transitions
4. Activity plans - long-running battle stability
5. Special attacks - dual progress bar behavior

## Performance Impact

- **UI Refresh**: 100ms timer (10 FPS) - negligible CPU usage
- **Computation**: Simple arithmetic operations - no object allocation
- **Memory**: ~12 tracking fields + 1 Timer object < 1KB
- **Network**: No additional API calls (reuses existing polling)

## Benefits

1. **Visual Polish**: Smooth, professional-looking progress bars
2. **Accuracy**: Server state synchronization prevents drift
3. **Simplicity**: Client-side interpolation, no server changes to battle engine
4. **Robustness**: Automatic handling of all reset scenarios
5. **Performance**: Minimal resource usage

## Technical Highlights

- **Interpolation**: Client estimates progress between polls
- **Correction**: Every poll resets reference point
- **Separation**: Step and Plan battles tracked independently
- **Type Safety**: Nullable checks prevent runtime errors
- **Clean Code**: Follows existing code style and patterns

## Files Modified

1. `BlazorIdle.Server/Application/Battles/Step/StepBattleCoordinator.cs` - Added interval properties
2. `BlazorIdle/Services/ApiModels.cs` - Added interval properties to client DTO
3. `BlazorIdle/Pages/Characters.razor` - Core implementation (tracking, calculation, UI)

## Files Created

1. `tests/BlazorIdle.Tests/AttackProgressCalculationTests.cs` - Unit tests
2. `攻击进度条优化报告.md` - Detailed Chinese documentation
3. `ATTACK_PROGRESS_BAR_SUMMARY.md` - This summary

## Backward Compatibility

✅ Fully backward compatible:
- Only added new optional fields
- Existing fields unchanged
- If `AttackInterval` is null, progress returns (0.0, 0.0)
- No breaking changes to any APIs

## Build Status

- ✅ Build: Success (1 pre-existing warning)
- ✅ Tests: 9/9 passed
- ✅ Warnings: Fixed nullable value warnings with `!` operator

---

**Implementation Date**: 2025-01-17  
**Version**: 1.0  
**Developer**: GitHub Copilot
