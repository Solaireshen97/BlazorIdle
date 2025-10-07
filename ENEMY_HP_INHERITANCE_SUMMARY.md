# Enemy HP Inheritance for Offline Battles - Implementation Summary

## Problem Statement

The user requested that the offline battle system provide a "seamless" experience where:
- Enemy HP should inherit from online battle progress
- If you go offline mid-battle (e.g., boss at 50% HP), offline calculation should continue from that point
- When you come back online, the online system should inherit the offline calculation results

## Solution Overview

Implemented a **battle progress snapshot system** that automatically saves and restores enemy state across online/offline transitions.

### Key Components

1. **BattleProgressSnapshot** - New data structure to store battle state:
   ```csharp
   public class BattleProgressSnapshot
   {
       public int? PrimaryEnemyHp { get; set; }        // Current enemy HP
       public int? WaveIndex { get; set; }             // For dungeons
       public int? RunCount { get; set; }              // For dungeons
       public double SimulatedSeconds { get; set; }    // Timestamp
   }
   ```

2. **ActivityPlan Extension** - Added `BattleProgressJson` field to persist snapshots

3. **Encounter Enhancement** - New constructor supporting custom initial HP:
   ```csharp
   public Encounter(EnemyDefinition enemy, int initialHp)
   ```

4. **Provider Updates** - `ContinuousEncounterProvider` now accepts `initialEnemyHp` parameter

5. **Automatic Save/Restore** - `OfflineFastForwardEngine` handles snapshot lifecycle:
   - Restores snapshot when starting offline calculation
   - Saves new snapshot after simulation
   - Clears snapshot when plan completes

## Data Flow

```
[Online Battle]
    ↓ Player goes offline
    Save snapshot: { PrimaryEnemyHp: 50, ... }
    ↓
[Offline Calculation]
    ↓ Restore snapshot
    ↓ Create enemy with HP=50
    ↓ Continue simulation
    ↓ Save new snapshot: { PrimaryEnemyHp: 20, ... }
    ↓ Player comes online
[Online Battle]
    ↓ Restore snapshot
    ↓ Continue from HP=20
```

## Test Coverage

Added 4 new unit tests:

1. **FastForward_WithEnemyHpInPayload_ShouldInheritEnemyHp**
   - Verifies HP restoration from snapshot

2. **FastForward_CompletedPlan_ShouldClearBattleProgress**
   - Ensures snapshots are cleaned up on completion

3. **FastForward_MultipleOfflineSessions_ShouldChainProgress**
   - Tests multiple online/offline cycles

4. **FastForward_InheritProgress_FromMidBattle_ShouldContinueSeamlessly**
   - End-to-end test of seamless continuation

All tests passing ✅

## Files Modified

| File | Change |
|------|--------|
| `BattleProgressSnapshot.cs` | New class |
| `ActivityPlan.cs` | Added `BattleProgressJson` field |
| `CombatActivityPayload.cs` | Added `CurrentEnemyHp` (reserved) |
| `Encounter.cs` | New constructor with custom HP |
| `EncounterGroup.cs` | New constructor accepting Encounters |
| `ContinuousEncounterProvider.cs` | Support for initial HP |
| `BattleSimulator.cs` | Added `InitialEnemyHp` to config |
| `OfflineFastForwardEngine.cs` | Save/restore logic |
| `OfflineFastForwardEngineTests.cs` | New tests |
| `20251007161850_AddBattleProgressJson.cs` | Database migration |

## Database Migration

```csharp
migrationBuilder.AddColumn<string>(
    name: "BattleProgressJson",
    table: "ActivityPlans",
    type: "TEXT",
    nullable: true);
```

## Design Principles

1. **Minimal Changes** - Only modified necessary components
2. **Backward Compatible** - New fields are nullable
3. **Automatic** - No manual intervention required
4. **Fail-Safe** - Gracefully handles missing/invalid snapshots
5. **Code Style** - Follows existing Chinese comment conventions

## Supported Scenarios

✅ Continuous battle mode with HP inheritance  
✅ Duration-limited plans  
✅ Infinite plans  
✅ Multiple online/offline transitions  
✅ Plan completion with automatic cleanup  

## Future Extensions (if needed)

- Dungeon mode wave/run progress (fields already reserved)
- Multi-enemy HP tracking (currently only primary target)
- Buff/debuff state persistence

## Verification

Build: ✅ Success (0 errors, 3 pre-existing warnings)  
Tests: ✅ All passing (including 4 new tests)  
Documentation: ✅ Comprehensive Chinese docs included  

## Conclusion

The implementation fully satisfies the user's requirement for "无感的离线战斗效果" (seamless offline battle experience) where enemy HP seamlessly inherits across online/offline boundaries.
