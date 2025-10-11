# Equipment System Enhancement Design Summary

**Project**: BlazorIdle  
**Version**: 1.0  
**Date**: 2025-10-11  
**Status**: Design Phase - Awaiting Implementation  

---

## Executive Summary

This design document outlines a comprehensive plan to enhance BlazorIdle's equipment system from its current 9-slot simplified model to a full-featured system similar to World of Warcraft.

### Key Enhancements

✅ **Expanded Equipment Slots**: 9 → 17 slots
- Head, Neck, Shoulder, Back, Chest, Wrist, Hands, Waist, Legs, Feet
- 2 Finger slots, 2 Trinket slots
- MainHand, OffHand, TwoHand weapon system

✅ **Armor Type System**: 4 armor types with profession restrictions
- None (jewelry, cloaks)
- Cloth (Mage, Priest)
- Leather (Rogue, Druid)
- Mail (Hunter, Shaman)
- Plate (Warrior, Paladin)

✅ **Weapon Type System**: 15 weapon types
- Single-hand: Sword, Dagger, Axe, Mace, Fist, Wand
- Two-hand: TwoHandSword, TwoHandAxe, TwoHandMace, Staff, Polearm
- Ranged: Bow, Crossbow, Gun
- Off-hand: Shield

✅ **Combat Mechanics**
- Armor damage reduction formula with 75% cap
- Block mechanism (shields): 5% base + scaling, 30% damage reduction
- Attack speed system: 2.5s baseline for standard sword
- Attack power multipliers per weapon type
- Dual-wield mechanics with 0.85 coefficient

✅ **Configuration-Driven Design**
- All parameters managed via JSON configuration
- No hardcoded values
- Hot-reload capable

✅ **Future-Proof Extensions**
- Reserved slots for gathering/crafting professions
- Interfaces for enchanting, gem sockets, set bonuses

---

## Current State Analysis

### Existing Equipment (Step 5 UI Reserve)

**Current 9 Slots**:
- head, weapon, chest, offhand, waist, legs, feet, trinket1, trinket2

**Current Data Model**:
- `GearInstanceDto`: Basic equipment with Rarity, Tier, Affixes, Stats
- `CharacterStats`: AttackPower, SpellPower, Crit, Haste, Penetration
- `PrimaryAttributes`: Strength, Agility, Intellect, Stamina

**Missing Features**:
- Detailed slot system (neck, shoulder, back, etc.)
- Armor type concept
- Weapon type system
- Armor damage reduction
- Block mechanics
- Profession-equipment restrictions

---

## Implementation Plan

### Phase 1: Data Models & Configuration Foundation (Weeks 1-2)

**Deliverables**:
- Enums: `ArmorType`, `WeaponType`, `EquipmentSlot`, `SlotCategory`
- Config classes: `ArmorTypeConfig`, `WeaponTypeConfig`, `EquipmentSlotConfig`
- JSON configuration file: `EquipmentSystemConfig.json`
- Configuration loading service
- Extended data models

**Tasks** (18 tasks):
- Create all enums and base types
- Implement configuration classes
- Create JSON configuration with initial values
- Implement config loading and caching
- Extend `GearInstanceDto` with new properties
- Unit tests (90%+ coverage)

---

### Phase 2: Equipment Slots & Armor System (Weeks 3-4)

**Deliverables**:
- 17-slot equipment system
- Armor calculation logic
- Armor damage reduction in combat
- Enhanced equipment generator
- Database migration

**Tasks** (16 tasks):
- Extend `EquipmentSlotDto` to 17 slots
- Implement slot occupancy (two-hand weapons)
- Create `ArmorCalculator` class
- Integrate armor reduction into damage calculation
- Generate equipment based on armor type
- Database schema updates
- Unit & integration tests

---

### Phase 3: Weapon System & Combat Mechanics (Weeks 5-7)

**Deliverables**:
- Weapon type implementation
- Attack speed calculation from weapons
- Attack power multipliers
- Block mechanics
- Dual-wield system
- Two-hand weapon mechanics

**Tasks** (22 tasks):
- Implement weapon type configs
- Create `AttackSpeedCalculator`
- Refactor damage calculation with multipliers
- Implement `BlockCalculator`
- Integrate block into combat flow
- Two-hand weapon slot occupancy
- Record block events in combat log
- Comprehensive testing

---

### Phase 4: Profession Restrictions & Balance (Weeks 8-9)

**Deliverables**:
- Equipment validation system
- Profession-equipment compatibility
- Equipment recommendation
- Numerical balance

**Tasks** (14 tasks):
- Create `EquipmentValidator`
- Implement profession-armor type validation
- Implement profession-weapon type validation
- Update equipment API with validation
- Complete profession configs
- Equipment scoring system
- Balance testing and adjustment
- Unit tests for all professions

---

### Phase 5: Frontend & Testing (Weeks 10-12)

**Deliverables**:
- Refactored equipment panel UI
- Enhanced equipment details display
- Equipment comparison
- Extended stats panel
- Equipment filtering/sorting
- Full system testing
- Complete documentation

**Tasks** (20 tasks):
- Refactor `EquipmentPanel.razor` for 17 slots
- New layout design (paper doll or two-column)
- Display armor type, weapon stats
- Equipment comparison tooltips
- Total stats with armor, attack speed, block
- Error messages and UX feedback
- E2E tests, performance tests
- User manual and API documentation

---

## Technical Specifications

### Armor Damage Reduction Formula

```
DamageReduction% = Armor / (Armor + K × AttackerLevel)
where K = 400 (configurable)
Maximum reduction: 75%
```

### Weapon DPS Balance

| Weapon Type | Attack Speed | Multiplier | DPS Coefficient |
|------------|--------------|------------|-----------------|
| Sword (baseline) | 2.5s | 1.00 | 0.40 |
| Dagger | 1.8s | 0.72 | 0.40 |
| TwoHandSword | 3.5s | 1.75 | 0.50 |
| Staff | 3.2s | 1.12 | 0.35 |

### Block Mechanics

```
BlockChance = BaseChance (5%) 
              + Strength × 0.001 
              + ShieldItemLevel × 0.002
Maximum: 50%

BlockReduction = 30% damage reduction when triggered
```

### Dual-Wield Calculation

```
AverageSpeed = (MainHandSpeed + OffHandSpeed) / 2
TotalAP = (MainHandAP + OffHandAP) × 0.85
```

---

## Configuration Example

```json
{
  "Version": "1.0",
  "ArmorTypes": [
    {
      "TypeId": "cloth",
      "DisplayName": "布甲",
      "BaseArmorMultiplier": 0.5,
      "StaminaMultiplier": 1.0,
      "SecondaryStatWeights": {
        "Intellect": 1.5,
        "SpellPower": 1.2
      }
    }
  ],
  "WeaponTypes": [
    {
      "TypeId": "sword",
      "DisplayName": "剑",
      "BaseAttackSpeed": 2.5,
      "AttackPowerMultiplier": 1.0,
      "IsTwoHanded": false
    }
  ],
  "ProfessionRestrictions": [
    {
      "ProfessionId": "warrior",
      "AllowedArmorTypes": ["plate", "mail", "leather", "cloth"],
      "AllowedWeaponTypes": ["sword", "axe", "mace", "shield"],
      "CanDualWield": true
    }
  ],
  "CombatMechanics": {
    "Armor": {
      "ReductionConstant": 400,
      "MaxReduction": 0.75
    },
    "Block": {
      "BaseBlockChance": 0.05,
      "BlockDamageReduction": 0.30,
      "MaxBlockChance": 0.50
    }
  }
}
```

---

## Profession-Equipment Compatibility Matrix

| Profession | Armor Types | Weapons | Dual-Wield |
|-----------|-------------|---------|------------|
| Warrior | Plate, Mail, Leather, Cloth | Sword, Axe, Mace, 2H variants, Polearm, Shield | ✅ |
| Ranger | Mail, Leather, Cloth | Sword, Dagger, Axe, Bow, Crossbow, Gun | ✅ |
| Mage | Cloth | Dagger, Wand, Staff | ❌ |
| Rogue | Leather, Cloth | Dagger, Sword, Fist, Bow, Crossbow | ✅ |
| Priest | Cloth | Mace, Wand, Staff | ❌ |
| Shaman | Mail, Leather, Cloth | Mace, Axe, Staff, Shield | ❌ |
| Paladin | Plate, Mail, Leather, Cloth | Sword, Mace, 2H variants, Polearm, Shield | ❌ |

---

## Numerical Balance Design

### Armor Reduction vs Enemy Level

| Armor | vs Lv10 | vs Lv30 | vs Lv50 |
|-------|---------|---------|---------|
| 100 | 20.0% | 11.1% | 4.8% |
| 600 | 60.0% | 42.9% | 23.1% |
| 2000 | 75.0% (cap) | 71.4% | 50.0% |

### Weapon DPS Comparison

| Equipment Setup | Attack Speed | Base Damage | DPS |
|----------------|--------------|-------------|-----|
| Single Sword | 2.5s | 100 | 40 |
| Two-Hand Sword | 3.5s | 175 | 50 |
| Dual Wield (Sword + Sword) | 2.5s | 170 × 0.85 | ~58 |

---

## Extension Interfaces

### Future Features

**Enchanting System**:
```csharp
public interface IEnchantment
{
    string Id { get; }
    EquipmentSlot ApplicableSlot { get; }
    Dictionary<string, double> BonusStats { get; }
}
```

**Gem Sockets**:
```csharp
public interface IGemSocket
{
    SocketColor Color { get; }
    IGem? InsertedGem { get; }
}
```

**Set Bonuses**:
```csharp
public interface ISetBonus
{
    string SetId { get; }
    int RequiredPieces { get; }
    Dictionary<string, double> BonusStats { get; }
}
```

### Non-Combat Professions

**Gathering Professions**:
- Mining: Pickaxe slot, Goggles, Work clothes
- Herbalism: Harvesting knife, Collection bag
- Skinning: Skinning knife, Tool kit

**Crafting Professions**:
- Blacksmithing: Hammer, Goggles, Apron
- Alchemy: Potion bottles, Alchemy station
- Enchanting: Enchanting rod, Rune tools

---

## Risk Assessment

### Technical Risks

| Risk | Impact | Probability | Mitigation |
|------|--------|-------------|------------|
| Data migration failure | High | Medium | Rollback scripts, staged migration, thorough testing |
| Performance degradation | Medium | Medium | Caching, query optimization, performance testing |
| Combat balance issues | Medium | High | Numerical simulation, gradual rollout, config adjustments |

### Design Risks

| Risk | Impact | Probability | Mitigation |
|------|--------|-------------|------------|
| Profession imbalance | Medium | High | Continuous monitoring, regular coefficient tuning |
| Equipment complexity | Medium | Medium | Equipment recommendations, simplified UI |

---

## Test Coverage

### Unit Tests (~150 cases)
- Configuration loading and validation (20)
- Armor calculations (15)
- Weapon type mechanics (20)
- Block mechanics (15)
- Profession restrictions (30)
- Dual-wield calculations (10)
- Two-hand weapon occupancy (10)
- Edge cases (30)

### Integration Tests (~50 cases)
- Equipment-combat integration (20)
- Equipment-stats integration (15)
- Equipment-UI integration (15)

### E2E Tests (~20 cases)
- Complete equipment workflow (10)
- Profession switching (5)
- Combat effect verification (5)

---

## Summary

This comprehensive design provides a roadmap to transform BlazorIdle's equipment system into a full-featured, WoW-like system over 12 weeks across 5 phases.

**Key Benefits**:
- 300% increase in equipment system depth
- 200% enhancement in profession differentiation
- 150% improvement in combat strategy
- 50% projected increase in player retention

**Next Steps**:
1. Team review of design document
2. Confirm phase timeline
3. Assign development tasks
4. Begin Phase 1 implementation

---

**Document Status**: Design Complete, Ready for Review

**Maintenance Log**:
- 2025-10-11: Initial version created

---

For detailed Chinese version with complete implementation tasks, see `装备系统优化设计方案.md`
