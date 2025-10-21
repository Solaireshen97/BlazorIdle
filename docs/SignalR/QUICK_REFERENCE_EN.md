# SignalR Design v2.0 - Quick Reference (English)

**Version**: 2.0  
**Date**: October 21, 2025  
**Status**: Design Complete

---

## 📖 Documentation Overview

This folder contains the complete v2.0 SignalR real-time frame broadcasting system design.

### Core Documents (v2.0 - Recommended)

| Document | Description | Read Time |
|----------|-------------|-----------|
| [实时帧推送设计方案.md](./实时帧推送设计方案.md) | **Main Design Doc** - Complete frame broadcasting architecture | 60 min |
| [战斗帧广播系统实现指南.md](./战斗帧广播系统实现指南.md) | Implementation Guide - Server & client code examples | 50 min |
| [前端渲染策略与时间同步.md](./前端渲染策略与时间同步.md) | Frontend Rendering - Interpolation & time sync | 40 min |
| [设计修订摘要v1-to-v2.md](./设计修订摘要v1-to-v2.md) | Design Revision Summary - v1.0 vs v2.0 comparison | 15 min |

### Legacy Documents (v1.0 - Deprecated)

The following documents represent the original design based on CombatSegment aggregation. They are kept for reference but should not be used for new implementation:

- SignalR设计总览.md
- Phase1-基础架构设计.md
- Phase2-战斗事件集成.md
- Phase3-扩展性设计.md

---

## 🎯 Design Goals (v2.0)

1. **Low Latency**: < 200ms push delay (vs 500-5000ms in v1.0)
2. **Smooth Rendering**: 60 FPS interpolated animations
3. **Reliable Delivery**: Version-based ordering with delta recovery
4. **Pure SignalR**: No polling fallback
5. **Easy Maintenance**: Clear architecture and comprehensive documentation

---

## 🔑 Key Concepts

### 1. Frame Types

#### FrameTick (Fixed-frequency broadcast)
- **Frequency**: 5-10Hz (100-200ms interval)
- **Content**: Battle state snapshot + window aggregates
- **Purpose**: Continuous state updates

```typescript
{
  version: 42,
  serverTime: 1729508400000,
  battleId: "battle-123",
  phase: "active",
  metrics: {
    health: { current: 8500, max: 10000, delta: -150 },
    shield: { current: 500, delta: 0 },
    dps: { player: 250, received: 80 },
    castProgress: { skillId: "fireball", progress: 0.6, remaining: 800 },
    buffs: [{ buffId: "haste", stacks: 2, duration: 5000 }]
  },
  aggregates: {
    damage: { total: 250, bySkill: { "fireball": 180, "melee": 70 } },
    hits: { total: 5, critical: 1, miss: 0 }
  }
}
```

#### KeyEvent (Critical battle events)
- **Trigger**: Instant (not throttled)
- **Content**: Important events (monster death, item drops, phase transitions)
- **Purpose**: Independent animations

```typescript
{
  eventId: "evt-456",
  version: 43,
  timestamp: 1729508400100,
  battleId: "battle-123",
  eventType: "monster_death",
  data: {
    monsterId: "goblin-1",
    experience: 50,
    position: { x: 100, y: 200 }
  }
}
```

#### Snapshot (Full state)
- **Trigger**: Periodic (every 30-60s) or on-demand (reconnect)
- **Content**: Complete battle state
- **Purpose**: Fast recovery and sync

### 2. Version Control

Every message has a monotonically increasing version number:

```
version=1  → FrameTick
version=2  → FrameTick
version=3  → KeyEvent (MonsterDeath)
version=4  → FrameTick (may include event from v3)
version=5  → FrameTick
...
version=100 → Snapshot
```

Client maintains `lastVersion` and detects gaps to request delta recovery.

### 3. Message Ordering

```typescript
class BattleFrameReceiver {
  onFrameReceived(frame: FrameTick) {
    if (frame.version === this.lastVersion + 1) {
      // Normal order: apply immediately
      this.applyFrame(frame);
    }
    else if (frame.version > this.lastVersion + 1) {
      // Gap detected: buffer and request delta
      this.bufferFrame(frame);
      this.requestDelta(this.lastVersion + 1, frame.version - 1);
    }
    else {
      // Duplicate/old: discard
      console.debug(`Discarding old frame: ${frame.version}`);
    }
  }
}
```

### 4. Interpolation Rendering

```typescript
class BattleRenderer {
  render(renderTime: number) {
    const targetTime = this.getServerTime() - 100; // 100ms delay for interpolation
    
    if (targetTime between currentFrame and nextFrame) {
      // Interpolation: smooth transition between frames
      const t = (targetTime - currentFrame.time) / (nextFrame.time - currentFrame.time);
      const health = lerp(currentFrame.health, nextFrame.health, t);
      this.updateHealthBar(health);
    } else {
      // Extrapolation: predict when next frame hasn't arrived
      const deltaTime = targetTime - currentFrame.time;
      const health = this.extrapolate(currentFrame, deltaTime);
      this.updateHealthBar(health);
    }
  }
}
```

---

## 🔄 v1.0 vs v2.0 Comparison

| Aspect | v1.0 (Old) | v2.0 (New) |
|--------|-----------|-----------|
| **Push Method** | CombatSegment aggregation | Fixed-frequency FrameTick |
| **Trigger** | 200 events OR 5 seconds | 5-10Hz (100-200ms) |
| **Latency** | 500ms - 5000ms | < 200ms |
| **Fallback** | SignalR → Polling | SignalR → Retry (no polling) |
| **Ordering** | Rely on SignalR | Version number mechanism |
| **Recovery** | Client polling | Delta recovery + Snapshot |
| **Rendering** | Direct display | Interpolation + Extrapolation |
| **Bandwidth** | ~24 KB/min | ~240 KB/min |

---

## 🏗️ Architecture Overview

```
┌─────────────────────────────────────────────────────────┐
│  Blazor WebAssembly Client                              │
│  ┌────────────────────────────────────────────────┐     │
│  │  BattleFrameReceiver                           │     │
│  │  - Version management (lastVersion)            │     │
│  │  - Gap detection & delta requests              │     │
│  └──────────────────┬─────────────────────────────┘     │
│  ┌──────────────────▼─────────────────────────────┐     │
│  │  BattleRenderer                                │     │
│  │  - Time synchronization                        │     │
│  │  - Interpolation/Extrapolation                 │     │
│  │  - Smooth correction                           │     │
│  └────────────────────────────────────────────────┘     │
└────────────────────┬─────────────────────────────────────┘
                     │ SignalR WebSocket (No polling)
┌────────────────────▼─────────────────────────────────────┐
│  ASP.NET Core Server                                     │
│  ┌────────────────────────────────────────────────┐     │
│  │  BattleHub (SignalR Hub)                       │     │
│  │  - Battle group management                     │     │
│  │  - Version sync (SyncBattleState)              │     │
│  └──────────────────┬─────────────────────────────┘     │
│  ┌──────────────────▼─────────────────────────────┐     │
│  │  BattleFrameBroadcaster                        │     │
│  │  - Fixed-frequency broadcasting (5-10Hz)       │     │
│  │  - Frame buffering & version management        │     │
│  └──────────────────┬─────────────────────────────┘     │
│  ┌──────────────────▼─────────────────────────────┐     │
│  │  BattleInstance (Domain)                       │     │
│  │  - Generate FrameTick                          │     │
│  │  - Emit KeyEvent                               │     │
│  │  - Generate Snapshot                           │     │
│  └────────────────────────────────────────────────┘     │
└──────────────────────────────────────────────────────────┘
```

---

## 🚀 Implementation Roadmap

### Phase 1: Core Foundation (2-3 weeks)
- [ ] BattleInstance frame generation logic
- [ ] BattleFrameBuffer management
- [ ] BattleFrameBroadcaster background service
- [ ] BattleHub basic connection management

### Phase 2: Version Control (1-2 weeks)
- [ ] Client BattleFrameReceiver version management
- [ ] Server delta recovery logic (SyncBattleState)
- [ ] Snapshot generation and application
- [ ] Test out-of-order/packet loss scenarios

### Phase 3: Frontend Rendering (1-2 weeks)
- [ ] Time synchronization algorithm
- [ ] Interpolation/extrapolation rendering
- [ ] Smooth correction strategy
- [ ] Animation rhythm control

### Phase 4: Optimization & Monitoring (1 week)
- [ ] Performance monitoring metrics
- [ ] Adaptive frequency adjustment
- [ ] Foreground/background switching
- [ ] Stress testing

**Total: 5-8 weeks**

---

## 💡 Key Technical Decisions

### Why fixed-frequency broadcasting?

**Pros**:
- ✅ Predictable latency (100-200ms)
- ✅ Easy frontend interpolation
- ✅ Controlled bandwidth
- ✅ Simple implementation

**Alternatives considered**:
- ❌ Event-based: Unstable latency
- ❌ CombatSegment: Too high latency

### Why remove polling fallback?

**Reasons**:
- ✅ Polling no longer needed
- ✅ Simplified architecture
- ✅ SignalR retry is reliable enough
- ✅ Lower maintenance cost

**Reconnection strategy**:
```
Failed → Wait 0s → Retry
Failed → Wait 2s → Retry
Failed → Wait 5s → Retry
Failed → Wait 10s → Retry
Failed → Wait 20s → Retry
Failed → Show connection error dialog
```

### Why version numbers?

**Purpose**:
- ✅ Handle network out-of-order/packet loss
- ✅ Support reconnection recovery
- ✅ Improve reliability
- ✅ Low implementation cost

### Why interpolation rendering?

**Reasons**:
- ✅ Push frequency 8Hz << Render frequency 60 FPS
- ✅ Interpolation enables smooth animations
- ✅ Reduces perceived latency
- ✅ Better user experience

---

## 📊 Performance Impact

### Network Bandwidth

| Scenario | v1.0 | v2.0 |
|----------|------|------|
| Push Rate | 0.2 Hz (every 5s) | 8 Hz (every 125ms) |
| Data Size | ~2 KB (aggregated) | ~500 B (snapshot) |
| Per Minute | ~24 KB/min | ~240 KB/min |

**Conclusion**: 10x bandwidth increase, but still acceptable (< 4 KB/s)

### Server Performance

| Metric | v1.0 | v2.0 |
|--------|------|------|
| CPU | Battle 100% | Battle 95% + Frame 5% |
| Memory | Battle state | Battle state + Frame buffer (~1MB/battle) |

**Conclusion**: < 5% overhead, acceptable

### Client Performance

| Metric | v1.0 | v2.0 |
|--------|------|------|
| Render FPS | Limited by push (< 1 FPS) | 60 FPS (interpolated) |
| CPU | Low | Medium (interpolation) |

**Conclusion**: Significantly better UX, acceptable CPU overhead

---

## 🧪 Testing Strategies

### Unit Tests
```typescript
describe("BattleFrameReceiver", () => {
  it("should handle missing frames", () => {
    receiver.onFrame({ version: 1 });
    receiver.onFrame({ version: 3 }); // Skip 2
    
    expect(requestDelta).toHaveBeenCalledWith(2, 2);
  });
});
```

### Integration Tests
```csharp
[Fact]
public async Task BattleFrameBroadcaster_ShouldSendFramesAtConfiguredFrequency()
{
    var broadcaster = CreateBroadcaster();
    var battle = CreateMockBattle();
    
    broadcaster.StartBroadcast(battle.Id, frequency: 10);
    await Task.Delay(1100);
    
    Assert.InRange(receivedFrames.Count, 9, 12);
}
```

### Network Simulation
```typescript
// Simulate network delay
connection.simulateDelay(100, 300); // 100-300ms random delay
connection.simulatePacketLoss(0.05); // 5% packet loss
```

---

## 📞 Getting Help

For questions or assistance:

1. Read the detailed documentation
2. Check code examples and comments
3. Refer to the integration design summary (docs/整合设计总结.txt)
4. Open an issue in the project

---

## 📄 Document Version History

| Version | Date | Changes |
|---------|------|---------|
| 2.0 | 2025-10-21 | Complete redesign: Fixed-frequency frame broadcasting + Version control + Interpolation rendering |
| 1.0 | 2025-10 | Initial version: CombatSegment aggregation + Polling fallback |

---

**Current Version**: 2.0  
**Status**: ✅ Design Complete  
**Last Updated**: October 21, 2025

---

**🎉 Start with v2.0 Design!**

Begin by reading [实时帧推送设计方案.md](./实时帧推送设计方案.md).
