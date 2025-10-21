# SignalR 设计修订摘要（v1.0 → v2.0）

**文档版本**: 2.0  
**修订日期**: 2025年10月21日  
**状态**: 设计变更说明

---

## 📋 修订背景

基于用户反馈和实际需求，对原 SignalR 推送系统设计进行了全面修订，以满足低延迟实时战斗推送的要求。

### 用户核心需求

1. ❌ **不需要轮询降级**: 后续前端完全依赖 SignalR，不保留轮询功能
2. ❌ **CombatSegment 延迟过高**: 聚合推送延迟太大，无法满足实时更新需求
3. ✅ **需要低延迟推送**: 要求前端能够及时获取战斗信息更新
4. ✅ **需要平滑渲染**: 前端需要流畅的战斗演出，而非跳跃式更新

---

## 🔄 核心变更对比

### 变更 1：推送机制

| 方面 | v1.0（旧设计） | v2.0（新设计） |
|------|---------------|---------------|
| **推送方式** | CombatSegment 聚合推送 | 固定频率帧广播 (FrameTick) |
| **触发条件** | 200 事件 或 5 秒 | 固定频率 5-10Hz (100-200ms) |
| **延迟** | 500ms - 5000ms | < 200ms |
| **数据结构** | 聚合统计（总伤害、技能次数等） | 状态快照 + 窗口聚合 |

**示例对比**：

```typescript
// v1.0: CombatSegment（聚合200个事件后推送）
{
  startTime: 0,
  endTime: 5000,
  totalDamage: 12345,
  skillCasts: 8,
  eventCount: 200
}

// v2.0: FrameTick（每100ms推送一次）
{
  version: 42,
  serverTime: 1729508400000,
  metrics: {
    health: { current: 8500, max: 10000, delta: -150 },
    dps: { player: 250, received: 80 },
    castProgress: { skillId: "fireball", progress: 0.6 }
  },
  aggregates: {
    damage: { total: 250, bySkill: {...} }
  }
}
```

---

### 变更 2：连接失败处理

| 方面 | v1.0（旧设计） | v2.0（新设计） |
|------|---------------|---------------|
| **降级策略** | SignalR 失败 → 轮询 API | SignalR 失败 → 重连（无轮询） |
| **重连次数** | 无限制 | 最多 5 次 |
| **失败处理** | 自动降级到轮询 | 显示连接失败对话框 |

**代码变更**：

```typescript
// v1.0: 有轮询降级
class BattleConnection {
  async onDisconnected() {
    // 尝试重连
    await this.reconnect();
    // 失败后降级到轮询
    this.fallbackToPolling();
  }
  
  pollBattleState() {
    // 轮询 API 获取状态
  }
}

// v2.0: 无轮询降级
class BattleConnection {
  async onDisconnected() {
    // 尝试重连（最多5次）
    for (let i = 0; i < 5; i++) {
      try {
        await this.reconnect();
        return; // 成功
      } catch {
        await this.sleep(delays[i]);
      }
    }
    
    // 重连失败 → 显示错误对话框
    this.showConnectionError();
  }
}
```

---

### 变更 3：消息顺序保证

| 方面 | v1.0（旧设计） | v2.0（新设计） |
|------|---------------|---------------|
| **顺序机制** | 依赖 SignalR 保证 | 版本号机制 |
| **乱序处理** | 无 | 缓存 + 补发请求 |
| **丢包处理** | 客户端轮询获取 | 增量补发 或 快照恢复 |
| **恢复机制** | 轮询完整状态 | 快照 + 增量帧 |

**版本号机制**：

```typescript
// v2.0: 版本号处理
class BattleFrameReceiver {
  private lastVersion = 0;
  
  onFrameReceived(frame: FrameTick) {
    if (frame.version === this.lastVersion + 1) {
      // 正常顺序
      this.applyFrame(frame);
      this.lastVersion = frame.version;
    }
    else if (frame.version > this.lastVersion + 1) {
      // 检测到缺口
      const gap = frame.version - this.lastVersion;
      
      if (gap > 100) {
        this.requestSnapshot(); // 缺口过大，请求快照
      } else {
        this.requestDelta(this.lastVersion + 1, frame.version - 1); // 请求补发
      }
      
      this.bufferFrame(frame); // 缓存当前帧
    }
    else {
      // 重复或过时的包，丢弃
      console.debug(`Discarding old frame: ${frame.version}`);
    }
  }
}
```

---

### 变更 4：前端渲染策略

| 方面 | v1.0（旧设计） | v2.0（新设计） |
|------|---------------|---------------|
| **渲染方式** | 直接显示聚合数据 | 插值 / 外推渲染 |
| **时间同步** | 无 | 服务器时间校准 |
| **平滑度** | 跳跃式更新 | 60 FPS 流畅动画 |
| **纠正策略** | 直接替换 | 平滑纠正到权威值 |

**插值渲染示例**：

```typescript
// v2.0: 插值渲染
class BattleRenderer {
  render(renderTime: number) {
    const serverTime = this.timeManager.getServerTime();
    const targetTime = serverTime - 100; // 100ms 插值延迟
    
    if (targetTime >= currentFrame.time && targetTime <= nextFrame.time) {
      // 插值模式：在两帧之间
      const t = (targetTime - currentFrame.time) / (nextFrame.time - currentFrame.time);
      const health = lerp(currentFrame.health, nextFrame.health, t);
      
      this.updateHealthBar(health); // 平滑显示
    } else {
      // 外推模式：下一帧未到达，预测状态
      const deltaTime = targetTime - currentFrame.time;
      const health = this.extrapolate(currentFrame, deltaTime);
      
      this.updateHealthBar(health);
    }
  }
}
```

---

## 📊 新增概念

### 1. 帧类型 (Frame Types)

v2.0 引入三种帧类型：

#### FrameTick（固定频率广播帧）
- **频率**: 5-10Hz (100-200ms)
- **内容**: 战斗状态快照 + 窗口聚合
- **用途**: 提供持续的状态更新

#### KeyEvent（关键事件）
- **触发**: 即时（不受频率限制）
- **内容**: 重要战斗事件（怪物死亡、掉落、阶段转换等）
- **用途**: 独立展示动画，增强战斗感

#### Snapshot（快照）
- **触发**: 定期（30-60秒）或 按需（重连恢复）
- **内容**: 完整战斗状态
- **用途**: 快速恢复和状态同步

### 2. 版本号机制 (Version Control)

每个消息分配单调递增的版本号：

```
version=1  → FrameTick
version=2  → FrameTick
version=3  → KeyEvent (MonsterDeath)
version=4  → FrameTick (可能携带 version=3 的事件)
version=5  → FrameTick
...
version=100 → Snapshot
```

客户端维护 `lastVersion`，检测缺口并请求补发。

### 3. 时间同步 (Time Synchronization)

客户端与服务器时间对齐：

```typescript
// 每帧校准时间偏移
calibrateTime(frame.serverTime);

// 计算偏移量
const offset = serverTime - clientTime;

// 使用中位数减少抖动
this.serverTimeOffset = median(offsetSamples);

// 获取当前服务器时间
const serverTime = Date.now() + this.serverTimeOffset;
```

### 4. 插值与外推 (Interpolation & Extrapolation)

**插值**：在两帧之间平滑过渡
```typescript
const t = (targetTime - fromTime) / (toTime - fromTime);
const value = lerp(fromValue, toValue, t);
```

**外推**：下一帧未到达时预测
```typescript
const deltaTime = targetTime - currentTime;
const predictedValue = currentValue + velocity * deltaTime;
```

---

## 🎯 设计优势

### 低延迟
- **v1.0**: 500ms - 5000ms（等待聚合）
- **v2.0**: < 200ms（固定频率）

### 流畅渲染
- **v1.0**: 跳跃式更新，不平滑
- **v2.0**: 插值渲染，60 FPS

### 可靠传输
- **v1.0**: 依赖 SignalR + 轮询兜底
- **v2.0**: 版本号 + 补发 + 快照

### 架构简化
- **v1.0**: SignalR + 轮询 API 双通道
- **v2.0**: 纯 SignalR 单通道

---

## 🚧 迁移影响

### 需要修改的部分

#### 服务端

1. **BattleInstance 扩展**
   - 添加帧生成逻辑 (`GenerateFrameTick`)
   - 添加快照生成 (`GenerateSnapshot`)
   - 添加版本管理 (`currentVersion`)
   - 添加帧缓冲 (`BattleFrameBuffer`)

2. **新增后台服务**
   - `BattleFrameBroadcaster`: 固定频率广播

3. **SignalR Hub 扩展**
   - 添加版本同步方法 (`SyncBattleState`)
   - 添加 Battle Group 管理
   - 处理补发请求

#### 客户端

1. **移除轮询相关代码**
   - 删除 `pollBattleState()` 方法
   - 删除 `fallbackToPolling()` 逻辑

2. **新增帧接收器**
   - `BattleFrameReceiver`: 版本管理 + 补发请求

3. **新增渲染器**
   - `BattleRenderer`: 插值/外推渲染
   - `BattleTimeManager`: 时间同步

4. **新增动画管理**
   - `BattleEventAnimator`: 关键事件动画
   - `DamageNumberManager`: 伤害数字演出

### 保持不变的部分

- ✅ 战斗核心逻辑（`BattleRunner`、`BattleContext`）
- ✅ 领域事件结构（`IDomainEvent`）
- ✅ 装备、技能、职业系统
- ✅ 持久化和事件溯源机制

---

## 📈 性能影响

### 网络带宽

| 场景 | v1.0 | v2.0 |
|------|------|------|
| **推送频率** | 0.2 Hz (每 5 秒) | 8 Hz (每 125ms) |
| **单次数据量** | ~2 KB (聚合) | ~500 B (快照) |
| **每分钟带宽** | ~24 KB/min | ~240 KB/min |

**结论**: v2.0 带宽增加 10 倍，但仍在可接受范围（< 4 KB/s）

### 服务端性能

| 指标 | v1.0 | v2.0 |
|------|------|------|
| **CPU 占用** | 战斗计算 100% | 战斗计算 95% + 帧生成 5% |
| **内存占用** | 战斗状态 | 战斗状态 + 帧缓冲（~1MB/战斗） |

**结论**: v2.0 性能开销 < 5%，可接受

### 客户端性能

| 指标 | v1.0 | v2.0 |
|------|------|------|
| **渲染帧率** | 受推送频率限制（< 1 FPS） | 60 FPS（插值渲染） |
| **CPU 占用** | 低 | 中（增加插值计算） |

**结论**: v2.0 显著提升用户体验，CPU 开销可接受

---

## 🛠️ 实施建议

### 阶段 1：原型验证（1周）

- [ ] 实现简化版 FrameTick 生成
- [ ] 实现基础 BattleFrameBroadcaster
- [ ] 测试推送延迟和稳定性
- [ ] 验证网络带宽占用

### 阶段 2：核心功能（3-4周）

- [ ] 完整实现服务端帧生成和广播
- [ ] 实现客户端版本管理和补发
- [ ] 实现时间同步机制
- [ ] 实现插值渲染

### 阶段 3：优化和完善（2-3周）

- [ ] 实现快照机制
- [ ] 优化性能和带宽
- [ ] 完善动画和演出
- [ ] 压力测试

**总计**: 6-8 周

---

## 📝 文档资源

### 核心文档（v2.0）

1. **[实时帧推送设计方案.md](./实时帧推送设计方案.md)** - 完整架构设计
2. **[战斗帧广播系统实现指南.md](./战斗帧广播系统实现指南.md)** - 实现细节
3. **[前端渲染策略与时间同步.md](./前端渲染策略与时间同步.md)** - 前端策略

### 参考文档（v1.0，已过时）

1. [SignalR设计总览.md](./SignalR设计总览.md)
2. [Phase1-基础架构设计.md](./Phase1-基础架构设计.md)
3. [Phase2-战斗事件集成.md](./Phase2-战斗事件集成.md)
4. [Phase3-扩展性设计.md](./Phase3-扩展性设计.md)

---

## 🎓 关键决策

### 决策 1: 为什么选择固定频率推送？

**理由**：
- ✅ 延迟可控且稳定
- ✅ 便于前端插值渲染
- ✅ 带宽可预测
- ✅ 实现简单

**对比方案**：
- ❌ 按事件推送：延迟不稳定
- ❌ CombatSegment：延迟过高

### 决策 2: 为什么移除轮询降级？

**理由**：
- ✅ 后续不需要轮询功能
- ✅ 简化架构
- ✅ SignalR 重连已足够可靠
- ✅ 降低维护成本

### 决策 3: 为什么引入版本号机制？

**理由**：
- ✅ 处理网络乱序/丢包
- ✅ 支持断线重连恢复
- ✅ 可靠性提升
- ✅ 实现成本低

### 决策 4: 为什么需要插值渲染？

**理由**：
- ✅ 推送频率 8Hz << 渲染频率 60 FPS
- ✅ 插值可实现流畅动画
- ✅ 降低延迟感知
- ✅ 提升用户体验

---

## ✅ 总结

v2.0 设计相比 v1.0 的核心改进：

1. **低延迟**: < 200ms（vs 500-5000ms）
2. **流畅渲染**: 60 FPS 插值（vs 跳跃式）
3. **纯 SignalR**: 移除轮询降级
4. **可靠传输**: 版本号 + 补发 + 快照
5. **架构简化**: 单一通道

**推荐立即采用 v2.0 设计进行后续实施。**

---

**文档状态**: ✅ 完成  
**最后更新**: 2025年10月21日
