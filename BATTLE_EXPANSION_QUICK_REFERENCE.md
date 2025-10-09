# Battle System Expansion - Quick Reference Guide

## Document Purpose
This is a quick reference companion to the detailed implementation plan (战斗系统拓展详细方案.md). It provides a high-level overview and fast lookup for key decisions.

---

## 📋 Implementation Phases Overview

| Phase | Duration | Focus | Status |
|-------|----------|-------|--------|
| Phase 1 | Week 1-2 | Combatant abstraction layer | 📝 Planned |
| Phase 2 | Week 3-4 | Target selection system | 📝 Planned |
| Phase 3 | Week 5-7 | Player death & revival | 📝 Planned |
| Phase 4 | Week 8-10 | Enemy attack capabilities | 📝 Planned |
| Phase 5 | Week 11-13 | Enemy skill system | 📝 Planned |
| Phase 6 | Week 14-15 | Enhanced dungeon features | 📝 Planned |
| Phase 7 | Week 16-17 | RNG consistency & replay | 📝 Planned |
| Phase 8 | Week 18-20 | Integration & optimization | 📝 Planned |

**Total Duration**: 4-5 months (20 weeks)

---

## 🎯 Key Requirements Summary

### R1: Player Death & Revival System
- Players take damage and can die (HP → 0)
- Death pauses attack progress, tracks, and skills
- Auto-revive after 10 seconds (configurable)
- Combat continues during revival - no immediate failure
- Monsters pause attacks when no living players available

### R2: Multi-Target Selection & Threat System
- Random target selection for attacks/skills
- Equal probability by default (33% each for 3 targets)
- Threat weight system (reserve interface for taunt mechanics)
- Affects selection probability, not absolute targeting

### R3: Enhanced Dungeon Features (Reserved)
- Option to disable auto-revival in dungeons
- Player death triggers full run reset
- Enhanced drop multipliers
- Backward compatible

### R4: Monster Attack & Skill System
- Monsters have base attacks (similar to player attacks)
- Separate monster skill pool (not player skills)
- Lightweight model: cooldown + probability + trigger conditions
- No complex resource system (mana, rage, etc.)
- Easy to configure

### R5: Combat Replay & RNG Consistency
- All random events use RngContext
- Record RNG Index before/after events
- Ensure reproducible battles (same seed = same result)
- Offline fast-forward uses same logic as online

### R6: Minimal Increment Principle
- Don't rewrite existing player systems (AutoCast/Buff/Proc)
- Gradual evolution, backward compatible
- Reserve interfaces for future Actor abstraction
- Full unit test coverage

---

## 🏗️ Core Architecture Components

### ICombatant Interface
```csharp
public interface ICombatant
{
    string Id { get; }
    int CurrentHp { get; }
    int MaxHp { get; }
    bool IsDead { get; }
    CombatantState State { get; }
    double ThreatWeight { get; set; }
    
    int ReceiveDamage(int amount, DamageType type, double now);
    bool CanBeTargeted();
    bool CanAct();
}
```

### CombatantState Enum
- `Alive` - Can act and be targeted
- `Dead` - Cannot act, waiting for revival
- `Reviving` - In revival countdown

### TargetSelector
- Weighted random selection algorithm
- Uses RngContext for reproducibility
- Default weight: 1.0, Taunt: 5.0+

### EnemySkillDefinition
- **Trigger types**: OnCooldownReady, OnHpBelow, OnCombatTimeElapsed
- **Effects**: Damage, ApplyBuff, Heal, Summon
- **No resource costs** - lightweight design

---

## 🔧 Key Technical Designs

### Player Death State Machine
```
[Alive] → (HP ≤ 0) → [Dead] → (AutoRevive?) → [Reviving] → [Alive]
                            ↘ (No AutoRevive) → [BattleFailed]
```

### Target Selection Algorithm
```
1. Filter candidates: CanBeTargeted()
2. Calculate total weight: Σ(ThreatWeight)
3. Random roll ∈ [0, totalWeight)
4. Select target where cumulative weight ≥ roll
```

### Track Pause/Resume
```csharp
Pause(now):
  _pausedRemaining = NextTriggerAt - now
  NextTriggerAt = double.MaxValue

Resume(now):
  NextTriggerAt = now + _pausedRemaining
```

---

## 📊 Configuration Parameters

| Parameter | Default | Range | Description |
|-----------|---------|-------|-------------|
| PlayerReviveDurationSeconds | 10.0 | 5.0 - 30.0 | Player revival time |
| EnemyBaseDamage | Calculated | 1 - 1000 | Enemy base damage |
| EnemyAttackIntervalSeconds | 2.0 | 0.5 - 10.0 | Enemy attack interval |
| ThreatWeightDefault | 1.0 | 0.1 - 10.0 | Default threat weight |
| ThreatWeightTaunt | 5.0 | 2.0 - 100.0 | Taunt threat weight |
| SkillActivationChance | 1.0 | 0.0 - 1.0 | Skill trigger probability |
| EnhancedDropMultiplier | 2.0 | 1.0 - 5.0 | Enhanced dungeon drops |

---

## 🧪 Test Strategy

### Unit Test Coverage Targets
- TargetSelector: > 90%
- PlayerCombatant: > 95%
- EnemyCombatant: > 90%
- EnemySkillManager: > 90%
- TrackState: > 95%

### Key Integration Test Scenarios
1. **Single enemy, player death & revival**
2. **Multi-enemy, random target distribution** (expect ~33.3% each)
3. **Enemy skill casting** (verify cooldown, trigger conditions)
4. **Enhanced dungeon death reset**
5. **RNG consistency** (same seed = same result)
6. **Offline vs online parity**

---

## ⚠️ Risk Mitigation

| Risk | Mitigation |
|------|------------|
| RNG consistency breaks | Strict code review, force RngContext usage, replay tests |
| Performance degradation | Early benchmarking, hot path optimization |
| Compatibility issues | Minimal intrusion principle, backward compatible tests |
| Schedule delays | Phased delivery, early integration testing |

---

## 📈 Success Metrics

- ✅ All unit tests pass, coverage > 85%
- ✅ Performance degradation < 5%
- ✅ Battle replay 100% consistent (same seed)
- ✅ Offline fast-forward matches online results
- ✅ All requirements implemented
- ✅ Documentation complete

---

## 🚀 Future Evolution (Post 6 months)

1. **Actor Unification**: Refactor Player/Enemy → unified Actor
2. **Multi-Player Battles**: Real threat system, role specialization
3. **Advanced Skills**: Skill chains, combos, position mechanics
4. **AI Enhancement**: Behavior trees, dynamic difficulty
5. **Battle Replay Sharing**: Recording, stats visualization

---

## 📝 Development Principles

1. **Data-Driven**: Configure via JSON/DB, minimize code changes
2. **Event-Driven**: Use EventScheduler for all game logic
3. **Deterministic**: Reproducible battles via RngContext
4. **Testable**: Every feature has unit tests
5. **Gradual**: Phased implementation, early integration
6. **Compatible**: Existing battles continue to work

---

## 📚 Reference Documents

- **Detailed Plan**: 战斗系统拓展详细方案.md (Chinese, comprehensive)
- **Architecture Overview**: 整合设计总结.txt (Current system design)
- **Progress Tracking**: 战斗功能分析与下阶段规划.md (Previous planning)

---

## 🔗 Quick Links

- **Phase 1 Tasks**: See Section 4.1 in detailed plan
- **API Changes**: See Appendix B in detailed plan
- **Configuration Examples**: See Appendix A in detailed plan
- **Test Specifications**: See Section 6 in detailed plan

---

**Last Updated**: 2025-01  
**Status**: Ready for Phase 1 Implementation  
**Next Action**: Review and approve plan, begin Phase 1
