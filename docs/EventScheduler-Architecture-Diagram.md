# EventScheduler Architecture Diagrams

## 1. Component Relationship Overview

```
┌──────────────────────────────────────────────────────────────────┐
│                     Application Layer                            │
│                                                                   │
│  ┌─────────────────────────────────────────────────────────┐   │
│  │              BattleSimulator (Unified)                   │   │
│  │         统一战斗模拟组件 - 封装创建和配置逻辑            │   │
│  └───────────┬─────────────┬─────────────┬──────────────────┘   │
│              │             │             │                       │
│   ┌──────────▼──────┐ ┌───▼─────────┐ ┌─▼─────────────────┐   │
│   │ BattleRunner    │ │RunningBattle│ │OfflineSettlement  │   │
│   │ 同步执行        │ │异步/步进    │ │离线结算           │   │
│   │ • Duration模式  │ │• Real-time  │ │• FastForward      │   │
│   │ • 一次性完成    │ │• Slice推进  │ │• 大切片快速       │   │
│   └─────────────────┘ └─────────────┘ └───────────────────┘   │
│                              │                                   │
└──────────────────────────────┼───────────────────────────────────┘
                               │
                               │ Shared Core
                               ▼
┌──────────────────────────────────────────────────────────────────┐
│                      Domain Layer                                │
│                                                                   │
│  ┌───────────────────────────────────────────────────────────┐  │
│  │                    BattleEngine                            │  │
│  │                核心战斗引擎 - 完全共享                      │  │
│  ├───────────────────────────────────────────────────────────┤  │
│  │ Properties:                                                │  │
│  │ • Battle (战斗状态)                                        │  │
│  │ • Clock (逻辑时钟)                                         │  │
│  │ • Scheduler (事件调度器)                                   │  │
│  │ • Context (战斗上下文)                                     │  │
│  │ • Collector (段收集器)                                     │  │
│  │ • Segments (段列表)                                        │  │
│  ├───────────────────────────────────────────────────────────┤  │
│  │ Methods:                                                   │  │
│  │ • AdvanceTo(sliceEnd, maxEvents)    - 推进到指定时间      │  │
│  │ • AdvanceUntil(targetTime)          - 一次性推进完成      │  │
│  │ • FinalizeNow()                     - 完成战斗            │  │
│  └─────┬─────────┬──────────┬───────────┬────────────────────┘  │
│        │         │          │           │                       │
│  ┌─────▼──┐ ┌───▼────┐ ┌───▼──────┐ ┌──▼─────────┐            │
│  │Event   │ │Game    │ │Battle    │ │Segment     │            │
│  │Scheduler│ │Clock   │ │Context   │ │Collector   │            │
│  └─────┬───┘ └───┬────┘ └────┬─────┘ └──┬─────────┘            │
│        │         │           │           │                       │
│  ┌─────▼─────────▼───────────▼───────────▼──────────┐          │
│  │          Core Infrastructure                      │          │
│  │  • PriorityQueue (事件队列)                       │          │
│  │  • RngContext (随机数上下文)                      │          │
│  │  • BuffManager (Buff管理)                         │          │
│  │  • TrackState (双轨状态)                          │          │
│  │  • ResourceBucket (资源桶)                        │          │
│  └────────────────────────────────────────────────────┘          │
│                                                                   │
└──────────────────────────────────────────────────────────────────┘
```

## 2. Event Scheduling Flow

```
┌─────────────────────────────────────────────────────────────────┐
│                    Event Execution Loop                         │
│                    (BattleEngine.AdvanceTo)                     │
└─────────────────────────────────────────────────────────────────┘
                               │
                               ▼
                    ┌──────────────────────┐
                    │  1. Pre-checks       │
                    │  • Is completed?     │
                    │  • Pending spawn?    │
                    │  • Slice boundary    │
                    └──────────┬───────────┘
                               │
                               ▼
        ┌──────────────────────────────────────────┐
        │  2. Event Loop (while events exist)      │
        └──────────────────────────────────────────┘
                               │
           ┌───────────────────┼───────────────────┐
           │                   │                   │
           ▼                   ▼                   ▼
    ┌──────────┐        ┌──────────┐       ┌──────────┐
    │2.1 Bound-│        │2.2 State │       │2.3 Event │
    │ary Check │        │  Update  │       │ Execute  │
    └──────────┘        └──────────┘       └──────────┘
    • PeekNext         • Buff Tick         • PopNext
    • Compare time     • Sync Haste        • Retarget
    • Return if        • Check ready       • AdvanceTo
      exceeded                              • Execute!
                                            • Record RNG
           │                   │                   │
           └───────────────────┼───────────────────┘
                               │
           ┌───────────────────┼───────────────────┐
           │                   │                   │
           ▼                   ▼                   ▼
    ┌──────────┐        ┌──────────┐       ┌──────────┐
    │2.4 Post- │        │2.5 Wave  │       │2.6 Spawn │
    │Processing│        │  Logic   │       │  Check   │
    └──────────┘        └──────────┘       └──────────┘
    • Capture          • Is cleared?       • Perform
      deaths           • Retarget or         spawn if
    • Update             schedule            ready
      collector          next wave
    • Try flush
      segment
           │                   │                   │
           └───────────────────┴───────────────────┘
                               │
                               ▼
                    ┌──────────────────────┐
                    │  3. Loop End Check   │
                    │  • Reached slice?    │
                    │  • Events empty?     │
                    │  • Max events hit?   │
                    └──────────────────────┘
```

## 3. Online vs Offline Comparison

```
┌─────────────────────────────────────────────────────────────────┐
│                    Online Battle (Async)                        │
└─────────────────────────────────────────────────────────────────┘
                               │
                               ▼
            ┌──────────────────────────────────┐
            │   User clicks "Start Battle"     │
            └──────────────┬───────────────────┘
                           │
                           ▼
            ┌──────────────────────────────────┐
            │ StepBattleController.Start       │
            └──────────────┬───────────────────┘
                           │
                           ▼
            ┌──────────────────────────────────┐
            │ BattleSimulator.CreateRunning    │
            └──────────────┬───────────────────┘
                           │
                           ▼
            ┌──────────────────────────────────┐
            │  RunningBattle constructed       │
            │  • BattleEngine created          │
            │  • Scheduler initialized         │
            └──────────────┬───────────────────┘
                           │
                    ┌──────┴──────┐
                    │             │
                    ▼             ▼
        ┌─────────────────┐   ┌──────────────────┐
        │ Periodic Tick   │   │ User Actions     │
        │ (e.g., 60Hz)    │   │ (pause, stop)    │
        └────────┬────────┘   └────────┬─────────┘
                 │                     │
                 └──────────┬──────────┘
                            │
                            ▼
            ┌──────────────────────────────────┐
            │ RunningBattle.Advance            │
            │ • Small slice (0.25s sim)        │
            │ • Max 2,000 events               │
            │ • Wall time synchronized         │
            └──────────────┬───────────────────┘
                           │
                           ▼
            ┌──────────────────────────────────┐
            │ BattleEngine.AdvanceTo           │
            │ (SHARED CORE LOGIC)              │
            └──────────────┬───────────────────┘
                           │
                           ▼
            ┌──────────────────────────────────┐
            │ SegmentCollector.Flush           │
            │ • Generate segment               │
            │ • Trigger rewards (边打边发)     │
            └──────────────────────────────────┘


┌─────────────────────────────────────────────────────────────────┐
│                  Offline Settlement (Sync)                      │
└─────────────────────────────────────────────────────────────────┘
                               │
                               ▼
            ┌──────────────────────────────────┐
            │   User logs in (offline time)    │
            └──────────────┬───────────────────┘
                           │
                           ▼
            ┌──────────────────────────────────┐
            │ OfflineSettlementService.Simulate│
            │ • Calculate offline duration     │
            │ • Cap at 12 hours                │
            └──────────────┬───────────────────┘
                           │
                           ▼
            ┌──────────────────────────────────┐
            │ BattleSimulator.CreateRunning    │
            └──────────────┬───────────────────┘
                           │
                           ▼
            ┌──────────────────────────────────┐
            │  RunningBattle constructed       │
            │  • BattleEngine created          │
            │  • Scheduler initialized         │
            └──────────────┬───────────────────┘
                           │
                           ▼
            ┌──────────────────────────────────┐
            │ RunningBattle.FastForwardTo      │
            │ • Large slice (5s sim)           │
            │ • Max 1,000,000 events           │
            │ • No wall time sync              │
            └──────────────┬───────────────────┘
                           │
                           ▼ (Loop until target)
            ┌──────────────────────────────────┐
            │ BattleEngine.AdvanceTo           │
            │ (SHARED CORE LOGIC)              │
            └──────────────┬───────────────────┘
                           │
                           ▼
            ┌──────────────────────────────────┐
            │ SegmentCollector.Flush           │
            │ • Generate segments              │
            └──────────────┬───────────────────┘
                           │
                           ▼
            ┌──────────────────────────────────┐
            │ Aggregate Statistics             │
            │ • Sum kills by enemy             │
            │ • Calculate rewards (expected)   │
            └──────────────┬───────────────────┘
                           │
                           ▼
            ┌──────────────────────────────────┐
            │ Return OfflineSettleResult       │
            │ • Gold, Exp, Items               │
            │ • Statistics                     │
            └──────────────────────────────────┘
```

## 4. Event Types and Scheduling

```
┌─────────────────────────────────────────────────────────────────┐
│                    EventScheduler (Min-Heap)                    │
│              Priority Queue<IGameEvent, double>                 │
└─────────────────────────────────────────────────────────────────┘
                               │
            Schedule(event) ───┤
                               │
                         ┌─────┴─────┐
                         │  Enqueue  │
                         │  by       │
                         │  ExecuteAt│
                         └─────┬─────┘
                               │
            ┌──────────────────┼──────────────────┐
            │                  │                  │
            ▼                  ▼                  ▼
    ┌──────────────┐  ┌───────────────┐  ┌──────────────┐
    │   t = 0.0s   │  │   t = 1.5s    │  │   t = 3.0s   │
    │              │  │               │  │              │
    │ Attack       │  │ Special       │  │ Attack       │
    │ TickEvent    │  │ PulseEvent    │  │ TickEvent    │
    └──────────────┘  └───────────────┘  └──────────────┘
            │                  │                  │
            ▼                  ▼                  ▼
    ┌──────────────┐  ┌───────────────┐  ┌──────────────┐
    │Execute(ctx)  │  │Execute(ctx)   │  │Execute(ctx)  │
    │• Deal damage │  │• Check skills │  │• Deal damage │
    │• Schedule    │  │• Try cast     │  │• Schedule    │
    │  next attack │  │• Schedule     │  │  next attack │
    │  at 1.5s     │  │  next pulse   │  │  at 4.5s     │
    └──────────────┘  └───────────────┘  └──────────────┘

Event Types:
┌─────────────────────┬────────────────┬─────────────────────┐
│ Event Type          │ Frequency      │ Purpose             │
├─────────────────────┼────────────────┼─────────────────────┤
│ AttackTickEvent     │ Continuous     │ Regular attacks     │
│ SpecialPulseEvent   │ Continuous     │ Special track pulse │
│ ProcPulseEvent      │ Every 1s       │ Proc effect checks  │
│ SkillCast...Event   │ On-demand      │ Skill casting       │
│ GcdReadyEvent       │ On-demand      │ GCD recovery        │
└─────────────────────┴────────────────┴─────────────────────┘
```

## 5. Optimization Strategy Overview

```
┌─────────────────────────────────────────────────────────────────┐
│                    Optimization Roadmap                         │
└─────────────────────────────────────────────────────────────────┘

Phase 1: Short-Term (1-2 weeks)
├─ Optimization 1: Event Statistics Sampling
│  └─ Expected: 30-50% performance boost
├─ Optimization 2: Dynamic Segment Thresholds
│  └─ Expected: Reduce segment count by 10x
└─ Optimization 3: RNG Index Recording Optimization
   └─ Expected: Reduce memory overhead

Phase 2: Mid-Term (4-6 weeks)
├─ Optimization 4: Event Batching
│  └─ Expected: Reduce event count by 50%
├─ Optimization 5: Chunked Persistence
│  └─ Expected: Support arbitrary offline duration
└─ Optimization 6: State Snapshot & Restore
   └─ Expected: Recovery from interruptions

Phase 3: Long-Term (As needed)
├─ Optimization 7: Plugin-based Event System
│  └─ Trigger: Need for user-generated content
├─ Optimization 8: Hierarchical Scheduling
│  └─ Trigger: Performance bottlenecks at scale
└─ Optimization 9: Streaming Event Processing
   └─ Trigger: Need for real-time monitoring


Performance Targets:
┌─────────────────────┬────────────┬────────────┬─────────────┐
│ Metric              │ Current    │ Short-Term │ Long-Term   │
├─────────────────────┼────────────┼────────────┼─────────────┤
│ 12h offline time    │ ~10s       │ <5s        │ <2s         │
│ Memory usage (12h)  │ ~150MB     │ <100MB     │ <50MB       │
│ Segment count (12h) │ ~100       │ <20        │ <5          │
│ Speedup ratio       │ ~500x      │ >1000x     │ >5000x      │
└─────────────────────┴────────────┴────────────┴─────────────┘
```

## 6. Key Decision Points

```
Decision Tree for Event Scheduling:

    Start Battle
        │
        ▼
    ┌─────────────────────┐
    │  Scenario Type?     │
    └─────────┬───────────┘
              │
    ┌─────────┼─────────┐
    │         │         │
    ▼         ▼         ▼
┌──────┐ ┌──────┐ ┌─────────┐
│Online│ │Offline│ │Testing │
└───┬──┘ └───┬──┘ └────┬────┘
    │        │         │
    ▼        ▼         ▼
┌──────┐ ┌──────┐ ┌─────────┐
│Small │ │Large │ │Specific │
│slice │ │slice │ │duration │
│0.25s │ │5.0s  │ │custom   │
└───┬──┘ └───┬──┘ └────┬────┘
    │        │         │
    └────────┼─────────┘
             │
             ▼
    ┌──────────────────┐
    │  BattleEngine    │
    │  (Shared Core)   │
    └────────┬─────────┘
             │
    ┌────────┼────────┐
    │        │        │
    ▼        ▼        ▼
 Events   Clock   Context
   │        │        │
   └────────┼────────┘
            │
            ▼
        Results


Configuration Matrix:

┌──────────┬────────────┬──────────────┬─────────────┐
│ Scenario │ Slice Size │ Max Events   │ Time Sync   │
├──────────┼────────────┼──────────────┼─────────────┤
│ Online   │ 0.25s      │ 2,000        │ Yes (wall)  │
│ Offline  │ 5.0s       │ 1,000,000    │ No          │
│ Test     │ Custom     │ Custom       │ No          │
│ Debug    │ 0.01s      │ 100          │ No          │
└──────────┴────────────┴──────────────┴─────────────┘
```

---

## Legend

```
┌─────┐
│ Box │  Component or Process
└─────┘

  │
  ▼     Flow Direction
  
  ┼     Decision Point / Branch
  
 ===    Shared Component Boundary
 
┌─────┐
│ *   │  Emphasis or Key Component
└─────┘

┌═════┐
║ === ║  Critical Path
└═════┘
```

---

**Related Documents**:
- `EventScheduler重构分析报告.md` - Full analysis (Chinese)
- `EventScheduler-Refactoring-Analysis-Summary.md` - Executive summary (English)
- `docs/BattleSimulator-Refactoring.md` - Prior refactoring work
