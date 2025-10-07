# OfflineFastForwardEngine Documentation

## 📌 Overview

`OfflineFastForwardEngine` is the core component of the offline battle system, responsible for simulating activity plan execution during the character's offline period. The engine reuses `BattleSimulator` for battle simulation and calculates the actual simulation duration based on the activity plan's limit type (Duration/Infinite).

## 🏗️ Architecture

### Core Responsibilities

1. **Offline Duration Cap**: Ensures offline simulation does not exceed the configured limit (default 12 hours)
2. **Plan Remaining Time Calculation**: Calculates the actual simulation duration based on plan type and executed time
3. **Battle Simulation**: Invokes `BattleSimulator` to execute fast-forward simulation
4. **Reward Calculation**: Aggregates gold, experience, and loot drops
5. **Status Determination**: Determines whether the plan is completed

### Dependencies

```
OfflineFastForwardEngine
├── BattleSimulator         (Battle simulator)
├── ActivityPlan            (Activity plan entity)
├── Character               (Character entity)
└── EconomyCalculator       (Economy reward calculator)
```

## 📊 Data Models

### OfflineFastForwardResult

Offline fast-forward result containing simulation duration, reward statistics, and plan status information.

```csharp
public sealed class OfflineFastForwardResult
{
    public Guid CharacterId { get; init; }           // Character ID
    public Guid PlanId { get; init; }                // Plan ID
    public double SimulatedSeconds { get; init; }    // Actual simulated duration (seconds)
    public bool PlanCompleted { get; init; }         // Whether plan is completed
    public long TotalDamage { get; init; }           // Total damage
    public int TotalKills { get; init; }             // Total kills
    public long Gold { get; init; }                  // Gold reward
    public long Exp { get; init; }                   // Experience reward
    public Dictionary<string, double> Loot { get; init; } // Loot drops (expected value)
    public double UpdatedExecutedSeconds { get; init; }   // Updated executed seconds
}
```

### OfflineCheckResult

Offline check result for frontend display of offline reward preview.

```csharp
public sealed class OfflineCheckResult
{
    public bool HasOfflineTime { get; init; }        // Whether there is offline time
    public double OfflineSeconds { get; init; }      // Total offline duration (seconds)
    public bool HasRunningPlan { get; init; }        // Whether there is a running plan
    public OfflineFastForwardResult? Settlement { get; init; } // Settlement result
    public bool PlanCompleted { get; init; }         // Whether plan is completed
    public bool NextPlanStarted { get; init; }       // Whether next plan was started
    public Guid? NextPlanId { get; init; }           // Next plan ID
}
```

## 🔧 Core Method

### FastForward

Fast-forward simulation of activity plan execution during offline period.

```csharp
public OfflineFastForwardResult FastForward(
    Character character,          // Character entity
    ActivityPlan plan,            // Activity plan
    double offlineSeconds,        // Offline duration (seconds)
    double maxCapSeconds = 43200.0) // Max offline cap (default 12 hours)
```

**Execution Flow**:

```
1. Validate parameters
   ├── Check character is not null
   ├── Check plan is not null
   └── Check offlineSeconds >= 0

2. Cap offline duration
   └── cappedOfflineSeconds = Min(offlineSeconds, maxCapSeconds)

3. Calculate plan remaining time
   ├── If Infinite type
   │   └── Return full offline duration
   └── If Duration type
       ├── remaining = LimitValue - ExecutedSeconds
       └── Return Min(remaining, cappedOfflineSeconds)

4. Fast-forward battle simulation
   ├── Parse activity config (PayloadJson)
   ├── Build character stats
   ├── Create battle config
   ├── Execute battle simulation
   └── Calculate rewards

5. Update plan status
   ├── updatedExecutedSeconds = ExecutedSeconds + SimulatedSeconds
   └── planCompleted = IsLimitReached()

6. Return result
   └── OfflineFastForwardResult
```

## 💡 Usage Examples

### Basic Usage

```csharp
// Create engine
var simulator = new BattleSimulator();
var engine = new OfflineFastForwardEngine(simulator);

// Get character and plan
var character = await _characterRepo.GetAsync(characterId);
var plan = await _planRepo.GetRunningPlanAsync(characterId);

// Calculate offline duration
var offlineSeconds = (DateTime.UtcNow - character.LastSeenAtUtc).TotalSeconds;

// Execute fast-forward simulation
var result = engine.FastForward(character, plan, offlineSeconds);

// Apply results
plan.ExecutedSeconds = result.UpdatedExecutedSeconds;
if (result.PlanCompleted)
{
    plan.State = ActivityState.Completed;
    plan.CompletedAt = DateTime.UtcNow;
}

character.Gold += result.Gold;
character.Experience += result.Exp;
```

### Scenario 1: Duration Plan Not Completed

```csharp
// Initial state
// - Plan limit: 2 hours (7200 seconds)
// - Executed: 30 minutes (1800 seconds)
// - Offline: 1 hour (3600 seconds)

var plan = new ActivityPlan
{
    LimitType = LimitType.Duration,
    LimitValue = 7200.0,
    ExecutedSeconds = 1800.0,
    // ...
};

var result = engine.FastForward(character, plan, 3600.0);

// Result
// - SimulatedSeconds = 3600 (simulate 1 hour)
// - UpdatedExecutedSeconds = 5400 (1800 + 3600)
// - PlanCompleted = false (5400 < 7200)
```

### Scenario 2: Duration Plan Completed

```csharp
// Initial state
// - Plan limit: 1 hour (3600 seconds)
// - Executed: 45 minutes (2700 seconds)
// - Offline: 30 minutes (1800 seconds)

var plan = new ActivityPlan
{
    LimitType = LimitType.Duration,
    LimitValue = 3600.0,
    ExecutedSeconds = 2700.0,
    // ...
};

var result = engine.FastForward(character, plan, 1800.0);

// Result
// - SimulatedSeconds = 900 (only simulate remaining 15 minutes)
// - UpdatedExecutedSeconds = 3600 (2700 + 900)
// - PlanCompleted = true (3600 >= 3600)
```

### Scenario 3: Infinite Plan

```csharp
// Initial state
// - Plan limit: Infinite
// - Executed: 1.4 hours (5000 seconds)
// - Offline: 1 hour (3600 seconds)

var plan = new ActivityPlan
{
    LimitType = LimitType.Infinite,
    LimitValue = null,
    ExecutedSeconds = 5000.0,
    // ...
};

var result = engine.FastForward(character, plan, 3600.0);

// Result
// - SimulatedSeconds = 3600 (simulate full offline duration)
// - UpdatedExecutedSeconds = 8600 (5000 + 3600)
// - PlanCompleted = false (infinite plans never complete)
```

### Scenario 4: Exceeding 12-Hour Cap

```csharp
// Initial state
// - Plan limit: Infinite
// - Offline: 27.8 hours (100000 seconds)
// - Max cap: 12 hours (43200 seconds)

var plan = new ActivityPlan
{
    LimitType = LimitType.Infinite,
    // ...
};

var result = engine.FastForward(character, plan, 100000.0, 43200.0);

// Result
// - SimulatedSeconds = 43200 (capped at 12 hours)
// - UpdatedExecutedSeconds = 43200
// - PlanCompleted = false
```

## 🧪 Test Coverage

### Test Matrix

| Test Scenario | Plan Type | Offline | Executed | Expected Result |
|--------------|-----------|---------|----------|-----------------|
| Parameter validation | - | - | - | Throws exception |
| Offline cap | Infinite | 13.9h | 0 | Capped at 12h |
| Duration not completed | Duration(2h) | 1h | 0.5h | Simulate 1h, not completed |
| Duration completed | Duration(1h) | 0.5h | 0.75h | Simulate 0.25h, completed |
| Duration already done | Duration(1h) | 0.5h | 1h | Simulate 0, completed |
| Infinite full time | Infinite | 1h | 1.4h | Simulate 1h, not completed |
| Infinite over cap | Infinite | 27.8h | 0 | Capped at 12h |
| Short offline | Duration(1h) | 1min | 0 | Simulate 1min |
| Multiple invocations | Duration(1h) | 0.5h×2 | 0→0.5h | Accumulate 1h, completed |

### Unit Tests

All tests are located in `BlazorIdle.Tests/OfflineFastForwardEngineTests.cs`

```csharp
✓ FastForward_WithNullCharacter_ThrowsArgumentNullException
✓ FastForward_WithNullPlan_ThrowsArgumentNullException
✓ FastForward_WithNegativeOfflineSeconds_ThrowsArgumentOutOfRangeException
✓ FastForward_WithOfflineTimeExceeding12Hours_CapAt12Hours
✓ FastForward_DurationPlan_CalculatesRemainingTimeCorrectly
✓ FastForward_DurationPlan_CompletesWhenRemainingTimeIsLessThanOfflineTime
✓ FastForward_DurationPlan_AlreadyCompleted_SimulatesZeroSeconds
✓ FastForward_InfinitePlan_SimulatesFullOfflineTime
✓ FastForward_InfinitePlan_WithLongOfflineTime_CapsAt12Hours
✓ FastForward_GeneratesValidRewards
✓ FastForward_WithShortOfflineTime_WorksCorrectly
✓ FastForward_MultipleInvocations_AccumulateExecutedSeconds
```

## ⚙️ Configuration Options

### Offline Duration Cap

Default: 12 hours (43200 seconds)

```csharp
// Use default
var result = engine.FastForward(character, plan, offlineSeconds);

// Custom cap (e.g., 6 hours)
var result = engine.FastForward(character, plan, offlineSeconds, maxCapSeconds: 21600.0);
```

### Activity Configuration (PayloadJson)

The `PayloadJson` field of the activity plan contains battle configuration:

```json
{
    "EnemyId": "dummy",
    "EnemyCount": 1,
    "Mode": "continuous",
    "DungeonId": null
}
```

**Field descriptions**:
- `EnemyId`: Enemy ID (resolved from EnemyRegistry)
- `EnemyCount`: Number of enemies
- `Mode`: Battle mode ("continuous", "dungeon", "dungeonloop")
- `DungeonId`: Dungeon ID (used only in dungeon mode)

## 🔍 Implementation Details

### Remaining Time Calculation

```csharp
private double CalculateRemainingSeconds(ActivityPlan plan, double cappedOfflineSeconds)
{
    if (plan.LimitType == LimitType.Infinite)
    {
        // Infinite plan: use full offline duration
        return cappedOfflineSeconds;
    }

    if (plan.LimitType == LimitType.Duration && plan.LimitValue.HasValue)
    {
        // Duration plan: calculate remaining time
        var remaining = plan.LimitValue.Value - plan.ExecutedSeconds;
        
        // Ensure not exceeding actual offline duration
        return Math.Min(Math.Max(0, remaining), cappedOfflineSeconds);
    }

    // Default to 0 for other cases
    return 0;
}
```

### Completion Status Check

```csharp
private bool CheckPlanCompleted(ActivityPlan plan, double updatedExecutedSeconds)
{
    if (plan.LimitType == LimitType.Infinite)
        return false; // Infinite plans never complete

    if (plan.LimitType == LimitType.Duration && plan.LimitValue.HasValue)
        return updatedExecutedSeconds >= plan.LimitValue.Value;

    return false;
}
```

### Random Seed Generation

```csharp
private static ulong DeriveSeed(Guid characterId, Guid planId)
{
    var charRng = RngContext.FromGuid(characterId);
    var planRng = RngContext.FromGuid(planId);
    charRng.Skip(2);
    planRng.Skip(3);
    ulong salt = (ulong)DateTime.UtcNow.Ticks;
    return RngContext.Hash64(charRng.NextUInt64() ^ planRng.NextUInt64() ^ salt);
}
```

## 📝 Design Principles

1. **Minimal Changes**: Reuse existing `BattleSimulator` and `EconomyCalculator`, avoid reimplementing battle logic
2. **Clear Separation of Concerns**: Engine only handles fast-forward simulation, not database updates or plan chaining
3. **Testability**: All core logic has unit test coverage
4. **Configurability**: Offline duration cap can be configured via parameters
5. **Consistent Code Style**: Follows existing project code style and naming conventions

## 🚀 Future Extensions

### Step 2: Extend OfflineSettlementService

Next step is to add `CheckAndSettleAsync` method in `OfflineSettlementService`:

```csharp
public async Task<OfflineCheckResult> CheckAndSettleAsync(
    Guid characterId, 
    CancellationToken ct)
{
    // 1. Get character, calculate offline duration
    // 2. Find running plan
    // 3. Call OfflineFastForwardEngine.FastForward()
    // 4. Update plan status
    // 5. If plan completed, try to start next plan
    // 6. Return OfflineCheckResult
}
```

### Step 3: Add API Endpoints

- `GET /api/offline/check?characterId={id}` - Check offline rewards
- `POST /api/offline/apply` - Apply offline settlement

### Step 4: Frontend Integration

- Create `OfflineSettlementDialog.razor` component
- Extend `ApiClient.cs` with offline API methods
- Modify `Characters.razor` to integrate offline check

## 📚 Related Documentation

- [离线战斗系统实施总结](./离线战斗系统实施总结.md)
- [离线战斗快速开始](./离线战斗快速开始.md)
- [OfflineBattleImplementationPlan](./OfflineBattleImplementationPlan.md)

---

**Document Version**: 1.0  
**Created**: 2025-01-08  
**Last Updated**: 2025-01-08
