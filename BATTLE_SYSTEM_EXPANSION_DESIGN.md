# Battle System Expansion - Phased Implementation Plan

**Version**: 1.0  
**Status**: Design Phase Complete ✅  
**Language**: English (Chinese versions available)

---

## Executive Summary

This comprehensive design plan expands the current battle system with four core features while maintaining **minimal code changes**, **backward compatibility**, and **RNG determinism**.

### Four Core Features

| Feature | Purpose | Priority | Timeline |
|---------|---------|----------|----------|
| **Player Revival System** | Auto-revive after death (10s delay), reduces AFK frustration | P0 | 5 days |
| **Weighted Target Selection** | Random target selection with threat weights, taunt interface reserved | P1 | 3 days |
| **Enhanced Dungeon Mode** | High-difficulty mode: disable revival + enhanced drops | P2 | 2 days |
| **Monster Attack & Skills** | Monsters attack back, lightweight skill system | P1 | 8 days |

**Total Implementation Time**: 23 days (13 dev + 10 testing)

---

## Documentation Package

### 1. Detailed Implementation Plan (43KB, Chinese)
`战斗系统扩展-分阶段实施方案.md`

**Contents**:
- Current system analysis
- Requirements analysis (12 functional + 5 non-functional)
- Architecture design principles
- 5 implementation phases with detailed code examples
- Testing strategy (6 test suites)
- Risk assessment & mitigation
- Performance analysis
- Backward compatibility guarantees
- 4 appendices (file list, config examples, glossary, references)

### 2. Architecture Diagrams (18KB, Chinese)
`战斗系统扩展-架构图解.md`

**Contents**:
- Current vs. expanded architecture comparison
- Event flow diagrams (revival, target selection, monster attacks)
- Class relationship diagrams
- Data flow diagrams (damage, RNG, configuration)
- Configuration enablement matrix

### 3. Quick Reference Guide (13KB, Chinese)
`战斗系统扩展-快速参考.md`

**Contents**:
- Single-page feature overview
- 3-week timeline with daily breakdown
- File checklist (9 new, 6 modified)
- Configuration flags quick reference
- Key class API summaries
- Testing priorities
- Acceptance criteria
- Quick start guide

---

## Design Principles

### 1. Minimal Intrusion
- Add 9 new files, modify only 6 existing files
- No refactoring of existing combat logic
- Extensions over modifications

### 2. Backward Compatibility
- All new features **default to OFF**
- Existing battles work without any changes
- Migration path: coexistence → gradual rollout → full adoption

### 3. RNG Determinism
- All randomness uses `context.Rng`
- Record `Rng.Index` before/after events
- Same seed = exact same battle replay

### 4. Modularity
- Player revival system: independent
- Monster attack system: independent
- Target selection system: independent

### 5. Lightweight Design
- No new resource types
- Simple cooldown + probability model for monster skills
- Avoid deep nested logic

---

## Implementation Phases

### Phase 1: Player Revival System (5 days)

**Goal**: Players auto-revive 10 seconds after death instead of ending the battle.

**Key Components**:
```csharp
// New class
public class PlayerState
{
    public int CurrentHp { get; }
    public int MaxHp { get; }
    public bool IsDead => CurrentHp <= 0;
    public double? DeathTime { get; }
    
    public void TakeDamage(int amount, double now)
    public void Revive(double now)
}

// New event
public record PlayerRevivalEvent(double ExecuteAt) : IGameEvent
```

**Modifications**:
- `BattleContext`: Add `PlayerState Player` field
- `AttackTickEvent`: Check if player is alive before attacking
- `DamageCalculator`: Add `ApplyDamageToPlayer()` method

**Tests**:
- `PlayerRevivalTests.cs`: Death, revival, state checks
- RNG consistency validation

### Phase 2: Target Selection Enhancement (3 days)

**Goal**: Random target selection in multi-enemy battles.

**Key Components**:
```csharp
// New class
public static class TargetSelector
{
    public static Encounter? SelectRandomTarget(
        EncounterGroup group, RngContext rng)
    
    public static Encounter? SelectWeightedRandomTarget(
        EncounterGroup group, RngContext rng)
}

// Extension to Encounter
public class Encounter
{
    public double ThreatWeight { get; private set; } = 1.0;
    public void ModifyThreatWeight(double multiplier)
}
```

**Tests**:
- `TargetSelectionTests.cs`: Distribution validation, weight testing
- Verify equal probability (33.3% for 3 enemies)

### Phase 3: Enhanced Dungeon Configuration (2 days)

**Goal**: Reserve configuration for high-difficulty dungeon mode.

**Key Components**:
```csharp
public class BattleMeta
{
    public bool AllowAutoRevival { get; set; } = true;
    public bool EnhancedDungeonMode { get; set; } = false;
    public double EnhancedDropMultiplier { get; set; } = 1.0;
}

// New event
public record BattleFailureEvent(double ExecuteAt) : IGameEvent
```

**Tests**:
- `EnhancedDungeonTests.cs`: Battle failure, drop multiplier

### Phase 4: Monster Attack & Skills (8 days)

**Goal**: Monsters attack players and use simplified skills.

**Key Components**:
```csharp
// Extended EnemyDefinition
public class EnemyDefinition
{
    public int AttackPower { get; init; } = 0; // 0 = dummy mode
    public double AttackInterval { get; init; } = 2.0;
    public List<MonsterSkillDefinition> Skills { get; init; } = new();
}

// New skill model
public class MonsterSkillDefinition
{
    public string Id { get; init; }
    public double Cooldown { get; init; }
    public TriggerType Trigger { get; init; }
    public double TriggerProbability { get; init; }
    public SkillEffect Effect { get; init; }
}

public enum TriggerType
{
    OnCooldown,      // Use when ready
    OnHpThreshold,   // Use when HP < threshold
    OnPlayerAttack   // Chance to trigger on player attack
}

// New events
public record MonsterAttackTickEvent(...) : IGameEvent
public record MonsterSkillCheckEvent(...) : IGameEvent
```

**Tests**:
- `MonsterAttackTests.cs`: Regular attack intervals
- `MonsterSkillTests.cs`: Cooldown, probability, triggers

### Phase 5: Testing & Validation (3 days)

**Test Suites**:
1. Unit tests for each component
2. Integration tests with all features enabled
3. Performance tests (ensure < 10% overhead)
4. Regression tests (backward compatibility)
5. RNG consistency tests (replay validation)

---

## File Checklist

### New Files (9)

```
BlazorIdle.Server/Domain/Combat/
├─ PlayerState.cs
├─ PlayerRevivalEvent.cs
├─ BattleFailureEvent.cs
├─ TargetSelector.cs
├─ MonsterAttackTickEvent.cs
├─ MonsterSkillDefinition.cs
├─ MonsterSkillSystem.cs
├─ MonsterSkillCheckEvent.cs
└─ SkillEffect.cs
```

### Modified Files (6)

```
BlazorIdle.Server/Domain/Combat/
├─ BattleContext.cs              (+PlayerState field)
├─ BattleEngine.cs               (+monster initialization)
├─ Encounter.cs                  (+ThreatWeight, +SkillSystem)
├─ AttackTickEvent.cs            (+player death check, +random target)
├─ DamageCalculator.cs           (+ApplyDamageToPlayer)
└─ Enemies/EnemyDefinition.cs    (+attack attributes, +skills)

BlazorIdle.Shared/Models/
└─ BattleMeta.cs                 (+revival/enhanced config)
```

---

## Configuration Quick Reference

### Flag Defaults (Backward Compatible)

| Flag | Default | Description |
|------|---------|-------------|
| `EnablePlayerDeath` | `false` | Enable player death mechanism |
| `PlayerRevivalDelaySeconds` | `10.0` | Revival delay in seconds |
| `UseWeightedTargetSelection` | `false` | Enable weighted target selection |
| `AllowAutoRevival` | `true` | Allow auto-revival |
| `EnhancedDungeonMode` | `false` | Enhanced dungeon mode |
| `EnhancedDropMultiplier` | `1.0` | Enhanced drop multiplier |

### Typical Configurations

```csharp
// 1. Default (backward compatible)
var meta = new BattleMeta(); // All new features OFF

// 2. Standard battle
var meta = new BattleMeta
{
    EnablePlayerDeath = true,
    UseWeightedTargetSelection = false
};

// 3. Enhanced Dungeon
var meta = new BattleMeta
{
    EnablePlayerDeath = true,
    AllowAutoRevival = false,
    EnhancedDungeonMode = true,
    EnhancedDropMultiplier = 2.5
};
```

---

## Risk Management

| Risk | Probability | Impact | Mitigation |
|------|-------------|--------|------------|
| RNG inconsistency breaks replay | Medium | High | Strict unit tests + RNG review process |
| Performance degradation > 10% | Low | Medium | Performance benchmarks + optimization |
| Backward compatibility break | Low | High | Complete regression test suite |
| Revival mechanism deadlock | Medium | Medium | Boundary condition tests |
| Monster skill config errors | High | Low | Config validator + unit tests |

---

## Acceptance Criteria

### Phase 1: Player Revival
- ✅ Player auto-revives 10s after death
- ✅ Attack progress pauses when dead
- ✅ Attack resumes after revival
- ✅ RNG consistency test passes

### Phase 2: Target Selection
- ✅ Random target selection in multi-enemy battles
- ✅ Probability distribution matches expectations
- ✅ ThreatWeight interface usable

### Phase 3: Enhanced Dungeon
- ✅ Enhanced mode disables auto-revival
- ✅ Death triggers BattleFailure
- ✅ Drop multiplier correctly applied

### Phase 4: Monster System
- ✅ Monsters attack players at intervals
- ✅ Monster skills trigger on cooldown
- ✅ Skill probability matches config
- ✅ Backward compatible: AttackPower=0 no attacks

### Phase 5: Testing
- ✅ All unit tests pass
- ✅ Integration tests pass
- ✅ Performance tests meet targets
- ✅ Regression tests pass

---

## Timeline

```
Week 1
├─ Day 1-5:  Phase 1 - Player Revival
└─ Day 6-8:  Phase 2 - Target Selection

Week 2
├─ Day 9-10:  Phase 3 - Enhanced Dungeon
└─ Day 11-18: Phase 4 - Monster Attack & Skills

Week 3
├─ Day 19-21: Phase 5 - Integration Testing
└─ Day 22-23: Documentation & Review
```

**Milestones**:
- M1 (Day 5): Player revival complete
- M2 (Day 8): Target selection complete
- M3 (Day 10): Enhanced Dungeon complete
- M4 (Day 18): Monster system complete
- M5 (Day 21): Integration testing complete
- M6 (Day 23): Documentation review complete

---

## Next Steps

**Current Status**: Design phase complete ✅

**Options**:

**A. Start Implementation**
- Begin with Phase 1 (Player Revival)
- 5-day sprint to first milestone
- Validate design feasibility

**B. Design Review**
- Technical team reviews documents
- Provide feedback and adjustments
- Revise plan before starting

**C. Pilot Validation**
- Implement simplified version (revival only)
- Validate technical feasibility
- Adjust plan based on findings

---

## Related Documents

- `战斗系统扩展-分阶段实施方案.md` - Detailed plan (Chinese)
- `战斗系统扩展-架构图解.md` - Architecture diagrams (Chinese)
- `战斗系统扩展-快速参考.md` - Quick reference (Chinese)

---

## Key Guarantees

✅ **Minimal Changes**: 9 new files + 6 modifications, no refactoring  
✅ **Backward Compatible**: All new features OFF by default  
✅ **RNG Deterministic**: Same seed = exact same battle  
✅ **High Test Coverage**: Target > 80%  
✅ **Performance Controlled**: Expected overhead < 10%  

---

**Document Version**: 1.0  
**Created**: 2025-01-09  
**Status**: Design Complete, Awaiting Approval ✅

