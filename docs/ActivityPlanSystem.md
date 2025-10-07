# Activity Plan System Documentation

## Overview

The Activity Plan System is a core feature of BlazorIdle that provides a unified planning and scheduling framework for game activities such as combat, gathering, and crafting. It allows players to create, start, and manage multiple activity plans, each with different limit conditions (e.g., duration limits, infinite duration).

## Design Principles

1. **Minimal Changes**: Add a planning layer on top of the existing combat system without breaking existing logic
2. **Consistent Code Style**: Follow the project's existing code organization and naming conventions
3. **Extensibility**: Reserve extension points for future activity types like gathering and crafting
4. **State Management**: Clear state machine design ensuring correct plan state transitions

## Core Concepts

### Activity Type

Currently supported activity types:
- **Combat (Continuous Combat)**: Continuous combat against specified enemies, automatically respawning after defeat
- **Dungeon**: Challenge dungeons, supporting single-run or loop modes

Future extensions (reserved):
- Gather (Gathering)
- Craft (Crafting)

### Limit Type

- **Duration**: Execute for a specified number of seconds, automatically complete when duration is reached
- **Infinite**: Execute indefinitely until manually stopped

### Activity State

State machine flow: `Pending → Running → Completed/Cancelled`

- **Pending**: Plan created but not started
- **Running**: Plan is executing
- **Completed**: Plan finished normally (limit condition met or manually stopped)
- **Cancelled**: Plan cancelled by user

## Data Models

### ActivityPlan Entity

```csharp
public class ActivityPlan
{
    public Guid Id { get; set; }                  // Unique plan identifier
    public Guid CharacterId { get; set; }         // Owner character ID
    public int SlotIndex { get; set; }            // Slot index (0-4)
    public ActivityType Type { get; set; }        // Activity type
    public LimitType LimitType { get; set; }      // Limit type
    public double? LimitValue { get; set; }       // Limit value (seconds)
    public ActivityState State { get; set; }      // Activity state
    public DateTime CreatedAt { get; set; }       // Creation time
    public DateTime? StartedAt { get; set; }      // Start time
    public DateTime? CompletedAt { get; set; }    // Completion time
    public string PayloadJson { get; set; }       // Activity config (JSON)
    public Guid? BattleId { get; set; }          // Associated battle ID
    public double ExecutedSeconds { get; set; }   // Executed duration
}
```

### CombatActivityPayload Configuration

Configuration parameters for combat activities:

```csharp
public class CombatActivityPayload
{
    public string? EnemyId { get; set; }          // Enemy ID
    public int EnemyCount { get; set; }           // Enemy count
    public double? RespawnDelay { get; set; }     // Respawn delay (seconds)
    public ulong? Seed { get; set; }              // Random seed
}
```

### DungeonActivityPayload Configuration

Configuration parameters for dungeon activities:

```csharp
public class DungeonActivityPayload
{
    public string DungeonId { get; set; }         // Dungeon ID
    public bool Loop { get; set; }                // Whether to loop
    public double? WaveDelay { get; set; }        // Wave delay (seconds)
    public double? RunDelay { get; set; }         // Run delay (seconds)
    public ulong? Seed { get; set; }              // Random seed
}
```

## API Endpoints

### 1. Create Combat Plan

**Endpoint**: `POST /api/activity-plans/combat`

**Parameters**:
- `characterId` (Guid, required): Character ID
- `slotIndex` (int, default 0): Slot index (0-4)
- `limitType` (string, default "duration"): Limit type ("duration" or "infinite")
- `limitValue` (double?, optional): Limit value (seconds, required for duration type)
- `enemyId` (string?, optional): Enemy ID
- `enemyCount` (int, default 1): Enemy count
- `respawnDelay` (double?, optional): Respawn delay (seconds)
- `seed` (ulong?, optional): Random seed

**Example**:
```bash
# Create a 1-hour combat plan
POST /api/activity-plans/combat?characterId=xxx&limitType=duration&limitValue=3600&enemyId=goblin

# Create an infinite duration combat plan
POST /api/activity-plans/combat?characterId=xxx&limitType=infinite&enemyId=orc
```

### 2. Create Dungeon Plan

**Endpoint**: `POST /api/activity-plans/dungeon`

**Parameters**:
- `characterId` (Guid, required): Character ID
- `slotIndex` (int, default 0): Slot index (0-4)
- `limitType` (string, default "duration"): Limit type
- `limitValue` (double?, optional): Limit value (seconds)
- `dungeonId` (string, default "intro_cave"): Dungeon ID
- `loop` (bool, default false): Whether to loop
- `waveDelay` (double?, optional): Wave delay
- `runDelay` (double?, optional): Run delay
- `seed` (ulong?, optional): Random seed

### 3. Start Plan

**Endpoint**: `POST /api/activity-plans/{id}/start`

### 4. Stop Plan

**Endpoint**: `POST /api/activity-plans/{id}/stop`

### 5. Cancel Plan

**Endpoint**: `POST /api/activity-plans/{id}/cancel`

### 6. Get All Plans for Character

**Endpoint**: `GET /api/activity-plans/character/{characterId}`

### 7. Get Plans for Specific Slot

**Endpoint**: `GET /api/activity-plans/character/{characterId}/slot/{slotIndex}`

### 8. Get Single Plan

**Endpoint**: `GET /api/activity-plans/{id}`

### 9. Delete Plan

**Endpoint**: `DELETE /api/activity-plans/{id}`

**Note**: Can only delete plans that are not running. Running plans must be stopped first.

## Usage Flow

### Typical Scenario: Create and Execute a Combat Plan

1. **Create Plan**:
```bash
POST /api/activity-plans/combat?characterId=xxx&limitType=duration&limitValue=3600&enemyId=dummy
# Returns { "id": "plan-uuid", ... }
```

2. **Start Plan**:
```bash
POST /api/activity-plans/plan-uuid/start
# Returns { "planId": "plan-uuid", "battleId": "battle-uuid" }
```

3. **Query Battle Status** (using existing battle API):
```bash
GET /api/battles/step/battle-uuid/status
```

4. **Manually Stop Plan** (optional):
```bash
POST /api/activity-plans/plan-uuid/stop
```

5. **View Plan Results**:
```bash
GET /api/activity-plans/plan-uuid
# Check state, executedSeconds, completedAt fields
```

## Architecture

### Domain Layer

- **Domain/Activities/**: Activity plan domain models
  - `ActivityPlan.cs`: Activity plan entity
  - `ActivityType.cs`: Activity type enum
  - `ActivityState.cs`: Activity state enum
  - `LimitType.cs`: Limit type enum
  - `CombatActivityPayload.cs`: Combat configuration
  - `DungeonActivityPayload.cs`: Dungeon configuration

### Application Layer

- **Application/Activities/**: Activity plan services
  - `ActivityPlanService.cs`: Core business logic
    - Create plan
    - Start plan (integrates with StepBattleCoordinator)
    - Stop plan
    - Cancel plan
    - Update progress

- **Application/Abstractions/**: Repository interfaces
  - `IActivityPlanRepository.cs`: Activity plan repository interface

### Infrastructure Layer

- **Infrastructure/Persistence/Repositories/**: Data access
  - `ActivityPlanRepository.cs`: Activity plan repository implementation

- **Infrastructure/Persistence/Migrations/**: Database migrations
  - `20251007033423_AddActivityPlanTable.cs`: Create ActivityPlans table

### API Layer

- **Api/ActivityPlansController.cs**: RESTful API controller
  - Provides endpoints for create, query, start, stop, cancel

## Integration with Existing Systems

### Combat System Integration

The Activity Plan System integrates with the existing combat system through `StepBattleCoordinator`:

1. When creating a plan, activity configuration is stored in the `PayloadJson` field
2. When starting a plan, parse the configuration and call `StepBattleCoordinator.Start()` to start combat
3. When stopping a plan, call `StepBattleCoordinator.StopAndFinalizeAsync()` to stop combat
4. The battle ID is stored in `ActivityPlan.BattleId` for easy association queries

## Future Extensions

### Multi-Slot Support

The current design includes the `SlotIndex` field (0-4), reserved for future parallel execution across multiple slots.

### Gathering Activities

```csharp
public enum ActivityType
{
    Combat = 1,
    Dungeon = 2,
    Gather = 3,  // Future implementation
}
```

### Crafting Activities

```csharp
public enum ActivityType
{
    Combat = 1,
    Dungeon = 2,
    Gather = 3,
    Craft = 4,  // Future implementation
}
```

### Count Limit

```csharp
public enum LimitType
{
    Duration = 1,
    Infinite = 2,
    Count = 3,  // Future: Limit by kill count or gather count
}
```

## Testing

Test file location: `tests/BlazorIdle.Tests/ActivityPlanTests.cs`

Test coverage:
- ✅ Infinite duration plan limit check
- ✅ Duration limit reached determination
- ✅ State transition validity
- ✅ Activity type support

Run tests:
```bash
dotnet test --filter "FullyQualifiedName~ActivityPlanTests"
```

## FAQ

### Q: Can I run multiple plans simultaneously?
A: Currently, each character can only run one plan at a time (enforced by `GetRunningPlanAsync`). Future versions will support multi-slot parallelism.

### Q: When will an infinite duration plan stop?
A: Infinite duration plans need to be manually stopped via the `/stop` endpoint, or automatically stopped by background services when the character goes offline.

### Q: Will I automatically receive rewards when a plan completes?
A: Yes, combat rewards are handled by the existing periodic reward system. Query rewards via the battle API after plan completion.

### Q: How do I check plan execution progress?
A: View the `ExecutedSeconds` field via `GET /api/activity-plans/{id}`, or check battle details via the battle API.

## Summary

The Activity Plan System provides BlazorIdle with a flexible activity management framework, supporting both duration-limited and infinite duration modes, seamlessly integrating with the existing combat system, and reserving extension points for future gathering and crafting features. The system design follows Domain-Driven Design (DDD) principles with clear code structure, easy to maintain and extend.
