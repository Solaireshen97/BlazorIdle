# Activity Plan System - Implementation Summary

## Overview

This document provides a high-level summary of the Activity Plan System implementation for BlazorIdle.

## What Was Implemented

A complete activity planning system that wraps existing combat functionality, allowing players to create time-limited or infinite-duration combat plans.

### Key Features

✅ **Two Activity Types**
- Combat: Continuous combat with enemy respawning
- Dungeon: Single-run or looped dungeon challenges

✅ **Two Limit Types**
- Duration: Time-based limits (specified in seconds)
- Infinite: Runs until manually stopped

✅ **Complete State Machine**
- Pending → Running → Completed/Cancelled
- Automatic state transitions based on limit conditions

✅ **RESTful API**
- Create combat/dungeon plans
- Start/stop/cancel plans
- Query plan status and history

✅ **Database Persistence**
- EF Core integration
- Migration included
- Full CRUD operations

✅ **Testing & Documentation**
- 10 unit tests (all passing)
- Comprehensive Chinese documentation
- Complete English documentation
- Quick start guide with examples

## Architecture

```
┌─────────────────────────────────────────────────────────┐
│                      API Layer                          │
│  ActivityPlansController (RESTful endpoints)            │
└────────────────┬────────────────────────────────────────┘
                 │
┌────────────────▼────────────────────────────────────────┐
│                  Application Layer                      │
│  ActivityPlanService (business logic)                   │
│  IActivityPlanRepository (interface)                    │
└────────────────┬────────────────────────────────────────┘
                 │
┌────────────────▼────────────────────────────────────────┐
│                   Domain Layer                          │
│  ActivityPlan (entity)                                  │
│  ActivityType, ActivityState, LimitType (enums)        │
│  CombatActivityPayload, DungeonActivityPayload          │
└─────────────────────────────────────────────────────────┘
                 │
┌────────────────▼────────────────────────────────────────┐
│              Infrastructure Layer                       │
│  ActivityPlanRepository (EF Core)                       │
│  GameDbContext (database)                               │
└─────────────────────────────────────────────────────────┘
```

## Integration Points

### With Existing Combat System

The activity plan system integrates seamlessly with `StepBattleCoordinator`:
- No changes to existing combat code
- Plans create and manage battle instances
- Battle state syncs with plan state
- Rewards handled by existing systems

### Database Schema

New table: `ActivityPlans`
```sql
- Id (GUID, PK)
- CharacterId (GUID, FK)
- SlotIndex (INT, 0-4)
- Type (INT, enum)
- LimitType (INT, enum)
- LimitValue (DOUBLE, nullable)
- State (INT, enum)
- CreatedAt (DATETIME)
- StartedAt (DATETIME, nullable)
- CompletedAt (DATETIME, nullable)
- PayloadJson (TEXT)
- BattleId (GUID, nullable)
- ExecutedSeconds (DOUBLE)
```

## Files Added

### Domain Layer (8 files)
- `Domain/Activities/ActivityPlan.cs`
- `Domain/Activities/ActivityType.cs`
- `Domain/Activities/ActivityState.cs`
- `Domain/Activities/LimitType.cs`
- `Domain/Activities/CombatActivityPayload.cs`
- `Domain/Activities/DungeonActivityPayload.cs`

### Application Layer (2 files)
- `Application/Activities/ActivityPlanService.cs`
- `Application/Abstractions/IActivityPlanRepository.cs`

### Infrastructure Layer (2 files)
- `Infrastructure/Persistence/Repositories/ActivityPlanRepository.cs`
- `Migrations/20251007033423_AddActivityPlanTable.cs`

### API Layer (1 file)
- `Api/ActivityPlansController.cs`

### Tests (1 file)
- `tests/BlazorIdle.Tests/ActivityPlanTests.cs`

### Documentation (3 files)
- `docs/活动计划系统文档.md` (Chinese, comprehensive)
- `docs/ActivityPlanSystem.md` (English, comprehensive)
- `docs/活动计划快速开始.md` (Chinese, quick start)

## Files Modified

### Configuration Updates (3 files)
- `Application/DependencyInjection.cs` - Added ActivityPlanService
- `Infrastructure/DependencyInjection/Repositories.cs` - Added IActivityPlanRepository
- `Infrastructure/Persistence/GameDbContext.cs` - Added ActivityPlans DbSet

## Code Quality

✅ **Build**: No errors, only pre-existing warnings
✅ **Tests**: 10/10 passing (100%)
✅ **Code Style**: Consistent with existing codebase
✅ **Architecture**: Follows DDD principles
✅ **Documentation**: Comprehensive in both languages

## API Quick Reference

```http
# Create a 1-hour combat plan
POST /api/activity-plans/combat?characterId={id}&limitType=duration&limitValue=3600&enemyId=goblin

# Create an infinite combat plan
POST /api/activity-plans/combat?characterId={id}&limitType=infinite&enemyId=orc

# Create a 2-hour looped dungeon plan
POST /api/activity-plans/dungeon?characterId={id}&limitType=duration&limitValue=7200&dungeonId=intro_cave&loop=true

# Start a plan
POST /api/activity-plans/{planId}/start

# Stop a plan
POST /api/activity-plans/{planId}/stop

# Get all plans for a character
GET /api/activity-plans/character/{characterId}

# Get a single plan
GET /api/activity-plans/{planId}

# Delete a plan (must be stopped first)
DELETE /api/activity-plans/{planId}
```

## Design Decisions

### Why Two Activity Types Only?

Per requirements, the current implementation focuses on combat activities:
- Combat: Continuous enemy respawning
- Dungeon: Structured dungeon runs

Gathering and crafting activities are reserved for future extensions, with the infrastructure ready to support them.

### Why JSON Payload Storage?

Using `PayloadJson` for activity-specific configuration provides:
- Flexibility for different activity types
- Easy extension without schema changes
- Type-safe deserialization in code

### Why Single Running Plan Per Character?

Current implementation enforces one active plan per character to:
- Simplify the initial implementation
- Avoid complex conflict resolution
- Match existing combat coordinator behavior

The `SlotIndex` field (0-4) is already in place for future multi-slot support.

## Future Extensions

The system is designed for easy extension:

### 1. Multi-Slot Parallel Execution
- Allow multiple plans running simultaneously
- Use exclusion tags to prevent conflicts
- Already supported by `SlotIndex` field

### 2. Additional Activity Types
```csharp
public enum ActivityType {
    Combat = 1,
    Dungeon = 2,
    Gather = 3,    // Future
    Craft = 4,     // Future
}
```

### 3. Count-Based Limits
```csharp
public enum LimitType {
    Duration = 1,
    Infinite = 2,
    Count = 3,     // Future: Kill count, gather count, etc.
}
```

### 4. Plan Queues
- Queue multiple plans per slot
- Auto-start next plan on completion
- Support plan reordering

## Performance Considerations

- Database indexes on `CharacterId` and `SlotIndex` recommended
- Batch updates for `ExecutedSeconds` to reduce I/O
- Plan state managed in-memory by coordinator
- Rewards handled by existing periodic system

## Migration Guide

### For Existing Databases

Run the migration:
```bash
cd BlazorIdle.Server
dotnet ef database update
```

This creates the `ActivityPlans` table with all necessary columns.

### For New Deployments

The migration runs automatically on first startup with the new code.

## Testing

Run activity plan tests:
```bash
dotnet test --filter "FullyQualifiedName~ActivityPlanTests"
```

Expected output: 10 tests, 0 failures

## Documentation

- **Full Documentation**: See `docs/活动计划系统文档.md` (Chinese) or `docs/ActivityPlanSystem.md` (English)
- **Quick Start**: See `docs/活动计划快速开始.md` (Chinese)
- **API Reference**: Included in full documentation
- **Architecture**: Included in full documentation

## Success Criteria

✅ All requirements from the problem statement met
✅ Minimal changes to existing code
✅ Consistent code style maintained
✅ Time-based and infinite limits supported
✅ Combat and dungeon activities implemented
✅ Integration with existing battle system working
✅ Database persistence functional
✅ Tests passing
✅ Comprehensive documentation provided

## Summary

The Activity Plan System has been successfully implemented as a clean, extensible layer on top of the existing combat system. It provides players with flexible activity planning capabilities while maintaining code quality and architectural integrity. The system is production-ready and well-documented for both users and developers.
