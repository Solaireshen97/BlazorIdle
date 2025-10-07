# Activity Plan Auto-Execution Flow Diagram

## Simple Flow

```
┌─────────────────────────────────────────────────────────────────┐
│                      ACTIVITY PLAN SYSTEM                        │
│                    (Auto-Execution Enabled)                      │
└─────────────────────────────────────────────────────────────────┘

Frontend                    Server                         Result
───────                     ──────                         ──────

POST /plans/combat    →    No running task?               
  characterId=A              ├─ YES → Auto-Start! ───→    Task Running ✅
  limitValue=300             └─ NO  → Queue ──────→       Task Pending ⏳

POST /plans/combat    →    Character A busy?
  characterId=A              ├─ YES → Queue ──────→       Task Pending ⏳
  limitValue=600             └─ NO  → Auto-Start!

POST /plans/{id}/stop →    Stop current task
                            ↓
                       Find next pending?
                            ├─ YES → Auto-Start! ───→    Next Task Running ✅
                            └─ NO  → Done ──────────→    No Tasks Running
```

## Detailed State Machine

```
┌──────────────────────────────────────────────────────────────────────┐
│                         Task Lifecycle                                │
└──────────────────────────────────────────────────────────────────────┘

                            CREATE PLAN
                                 │
                                 ▼
                    ┌────────────────────────┐
                    │   Check Running Task   │
                    └────────────────────────┘
                                 │
                    ┌────────────┴────────────┐
                    │                         │
                    ▼                         ▼
           ┌─────────────────┐      ┌─────────────────┐
           │  No Running     │      │  Has Running    │
           │  Task Found     │      │  Task           │
           └─────────────────┘      └─────────────────┘
                    │                         │
                    ▼                         ▼
           ┌─────────────────┐      ┌─────────────────┐
           │   AUTO-START    │      │    PENDING      │
           │   state=Running │      │   state=Pending │
           │   battleId=xxx  │      │   (Queued)      │
           └─────────────────┘      └─────────────────┘
                    │                         │
                    │                         │
                    │ Task Completes          │ Wait in Queue
                    │ or Stopped              │
                    ▼                         │
           ┌─────────────────┐               │
           │   COMPLETED     │◄──────────────┘
           │  state=Completed│     Auto-Start
           └─────────────────┘     Next Task
                    │
                    ▼
           ┌─────────────────┐
           │ Find Next       │
           │ Pending Task    │
           └─────────────────┘
                    │
        ┌───────────┴───────────┐
        │                       │
        ▼                       ▼
  ┌──────────┐          ┌──────────┐
  │  Found   │          │Not Found │
  │AUTO-START│          │   Done   │
  └──────────┘          └──────────┘
```

## Queue Priority Example

```
┌──────────────────────────────────────────────────────────────────────┐
│                        Task Queue Order                               │
└──────────────────────────────────────────────────────────────────────┘

Character A's Tasks:

Time    API Call                          SlotIndex  State      Priority
─────   ──────────────────────────────    ─────────  ─────      ────────
10:00   POST /plans (combat, goblin)         0       Running       -
10:01   POST /plans (combat, orc)            1       Pending       3
10:02   POST /plans (dungeon, cave)          0       Pending       1  ←─ Next
10:03   POST /plans (combat, troll)          0       Pending       2

Execution Order:
1. goblin (slot 0, 10:00) - Currently Running
2. cave   (slot 0, 10:02) - Next (slot 0 priority)
3. troll  (slot 0, 10:03) - Then (slot 0, later time)
4. orc    (slot 1, 10:01) - Last (slot 1 lower priority)

Priority Rule: ORDER BY SlotIndex ASC, CreatedAt ASC
```

## Multi-Character Scenario

```
┌──────────────────────────────────────────────────────────────────────┐
│              Multiple Characters (Independent Queues)                 │
└──────────────────────────────────────────────────────────────────────┘

Character A                    Character B
───────────                    ───────────

Task A1: Running ✅           Task B1: Running ✅
Task A2: Pending ⏳           Task B2: Pending ⏳
Task A3: Pending ⏳           Task B3: Pending ⏳

When A1 completes:            When B1 completes:
  → A2 auto-starts              → B2 auto-starts

When A2 completes:            When B2 completes:
  → A3 auto-starts              → B3 auto-starts

✅ Each character has independent task execution
✅ No interference between characters
```

## Error Handling Flow

```
┌──────────────────────────────────────────────────────────────────────┐
│                     Auto-Start Error Handling                         │
└──────────────────────────────────────────────────────────────────────┘

    Create Plan
         │
         ▼
    Save to DB ✅
         │
         ▼
    Auto-Start?
         │
    ┌────┴────┐
    │         │
    ▼         ▼
Success     Failure
    │         │
    │         ├─ Log Error
    │         ├─ Plan stays Pending
    │         └─ Can retry manually
    │
    ▼
State=Running
BattleId set


Key Point: Plan creation ALWAYS succeeds
           Auto-start failure doesn't affect plan
```

## API Usage Pattern

### Old Pattern (Manual Start)
```
┌─────────────┐      ┌─────────────┐      ┌─────────────┐
│   Create    │      │    Start    │      │   Monitor   │
│    Plan     │ ───→ │    Plan     │ ───→ │   Battle    │
│             │      │             │      │             │
│ POST /plans │      │POST /start  │      │GET /status  │
└─────────────┘      └─────────────┘      └─────────────┘
  2 API calls         Manual step          Polling
```

### New Pattern (Auto-Execution)
```
┌─────────────┐      ┌─────────────┐
│   Create    │      │   Monitor   │
│    Plan     │ ───→ │   Battle    │
│             │      │             │
│ POST /plans │      │GET /status  │
│ Auto-starts!│      │             │
└─────────────┘      └─────────────┘
  1 API call         Polling

✨ 50% fewer API calls
✨ Automatic execution
✨ Queue management
```

## Task Queue Visualization

```
Character's Task Queue

┌──────────────────────────────────────────────┐
│                                              │
│  Slot 0: [Running] → [Pending] → [Pending]  │
│            ▲           │            │        │
│            │           └────────────┘        │
│            │            Auto-transition      │
│            │                                 │
│  Slot 1: [Pending] ──────────────────────→   │
│            │                                 │
│            └── Waits for Slot 0 to finish   │
│                                              │
└──────────────────────────────────────────────┘

Execution Flow:
1. Slot 0 Running task completes
2. Slot 0 first Pending → Auto-start
3. Slot 0 second Pending → Wait
4. Eventually Slot 1 Pending → Auto-start
```
