# SignalR 推送系统设计文档

欢迎查阅 BlazorIdle 项目的 SignalR 推送系统设计方案。

---

## ⚠️ 设计版本说明

本目录包含三个版本的设计文档：

### **版本 3.0（最新推荐）- 统一管理系统** 🌟

**最新设计**：基于整合设计总结，提供统一的SignalR管理框架，支持所有功能模块：

| 文档 | 说明 | 阅读时间 | 优先级 |
|------|------|----------|--------|
| [SignalR统一管理系统-完整方案总览.md](./SignalR统一管理系统-完整方案总览.md) | **导航文档** - 设计方案概览和文档导航 | 20分钟 | ⭐⭐⭐ |
| [SignalR需求分析与边界定义.md](./SignalR需求分析与边界定义.md) | 功能模块需求分析，实时消息vs API边界 | 40分钟 | ⭐ |
| [SignalR统一管理系统-总体架构.md](./SignalR统一管理系统-总体架构.md) | **核心架构** - 统一Hub、消息分发、事件总线 | 60分钟 | ⭐⭐⭐ |
| [API与SignalR选择指南.md](./API与SignalR选择指南.md) | 决策树、判断标准、最佳实践 | 30分钟 | ⭐⭐ |
| [SignalR实施计划-分步指南.md](./SignalR实施计划-分步指南.md) | **实施手册** - 5阶段详细开发指南 | 90分钟 | ⭐⭐⭐ |

**核心特性**：
- ✅ 统一的SignalR管理框架（单Hub、单连接）
- ✅ 支持所有功能模块（战斗、生产、采集、组队等）
- ✅ 事件驱动架构（解耦业务逻辑）
- ✅ 高性能（异步队列、批量发送、优先级调度）
- ✅ 可靠传输（版本号、缓存、快照恢复）
- ✅ 易于扩展（插件式Broadcaster）
- ✅ 完整的实施计划（新手可按步骤执行）

**推荐阅读顺序**：
1. 先读完整方案总览（了解全貌）
2. 再读需求分析（理解为什么）
3. 然后读总体架构（理解怎么做）
4. 参考选择指南（开发时决策）
5. 跟随实施计划（动手实施）

---

### **版本 2.0（战斗系统）- 实时帧推送系统**

聚焦战斗系统的实时帧推送设计，与版本3.0兼容：

| 文档 | 说明 | 阅读时间 |
|------|------|----------|
| [实时帧推送设计方案.md](./实时帧推送设计方案.md) | 战斗系统帧推送架构 | 60分钟 |
| [战斗帧广播系统实现指南.md](./战斗帧广播系统实现指南.md) | 战斗系统服务端和客户端实现 | 50分钟 |
| [前端渲染策略与时间同步.md](./前端渲染策略与时间同步.md) | 前端渲染、插值和时间同步 | 40分钟 |

**核心特性**：
- ✅ 持续 SignalR 事件流（无轮询降级）
- ✅ 5-10Hz 固定频率帧广播
- ✅ 版本号机制处理乱序/丢包
- ✅ 快照 + 增量补发
- ✅ 前端插值/外推渲染
- ✅ 低延迟 (< 200ms)

**注意**：版本2.0专注于战斗系统，版本3.0提供了统一框架，两者可以结合使用。

---

### **版本 1.0（已过时）- CombatSegment 聚合系统**

原始设计，基于事件聚合推送：

| 文档 | 说明 | 状态 |
|------|------|------|
| [SignalR设计总览.md](./SignalR设计总览.md) | 原始系统整体架构 | ⚠️ 已过时 |
| [Phase1-基础架构设计.md](./Phase1-基础架构设计.md) | SignalR 基础设施搭建 | ⚠️ 已过时 |
| [Phase2-战斗事件集成.md](./Phase2-战斗事件集成.md) | CombatSegment 推送方案 | ⚠️ 已过时 |
| [Phase3-扩展性设计.md](./Phase3-扩展性设计.md) | 通用事件框架 | ⚠️ 已过时 |

**注意**：版本 1.0 的设计因以下问题被替代：
- ❌ 包含轮询降级（不再需要）
- ❌ CombatSegment 延迟过高
- ❌ 不满足低延迟实时更新需求
- ❌ 缺少统一管理框架

---

## 🎯 设计目标（版本 3.0 - 统一管理系统）

### 核心价值

1. **统一管理**: 单一Hub、单一连接，支持所有功能模块
2. **高性能**: 异步队列、批量发送、优先级调度
3. **可靠性**: 版本号、消息缓存、快照恢复
4. **可扩展**: 插件式Broadcaster，易于添加新功能
5. **易维护**: 清晰的架构、完善的文档、丰富的日志

---

## 📖 快速开始（版本 3.0 - 推荐）

### 推荐阅读顺序

1. **首先阅读** [SignalR统一管理系统-完整方案总览.md](./SignalR统一管理系统-完整方案总览.md)
   - 了解整体设计思路
   - 理解为什么需要统一管理
   - 掌握文档导航

2. **然后阅读** [SignalR需求分析与边界定义.md](./SignalR需求分析与边界定义.md)
   - 各功能模块的SignalR需求
   - 实时消息 vs API查询的边界
   - SignalR需求优先级

3. **深入学习** [SignalR统一管理系统-总体架构.md](./SignalR统一管理系统-总体架构.md)
   - **最重要的文档** - 核心架构设计
   - GameHub、ConnectionManager、SignalRDispatcher
   - 事件驱动架构、消息流程
   - 代码示例和扩展机制

4. **参考决策** [API与SignalR选择指南.md](./API与SignalR选择指南.md)
   - 快速决策树
   - 详细判断标准
   - 典型场景和最佳实践

5. **开始实施** [SignalR实施计划-分步指南.md](./SignalR实施计划-分步指南.md)
   - 5个阶段详细开发计划
   - 每步的任务清单和验收标准
   - 完整代码示例（可直接使用）

### 快速参考

**核心架构**：
```
Client (单连接)
    │
    ▼
GameHub (统一Hub)
    │
    ▼
SignalRDispatcher (消息队列 + 批量发送)
    │
    ├─> CombatBroadcaster      (战斗)
    ├─> ActivityBroadcaster    (活动)
    ├─> CraftingBroadcaster    (制作)
    ├─> GatheringBroadcaster   (采集)
    ├─> PartyBroadcaster       (组队)
    └─> GeneralBroadcaster     (通用)
         ▲
         │
    Domain Event Bus (事件驱动)
         ▲
         │
    Business Logic (业务逻辑)
```

**关键特性**：
```
✅ 单Hub单连接 → 资源高效
✅ 事件驱动 → 业务解耦
✅ 模块化Broadcaster → 易于扩展
✅ 异步队列 → 高性能
✅ 优先级调度 → 重要消息优先
✅ 版本号机制 → 可靠传输
```

---

## 🆚 版本对比

### 版本 3.0 vs 版本 2.0 vs 版本 1.0

| 方面 | 版本 1.0 | 版本 2.0 | 版本 3.0（最新） |
|------|---------|---------|----------------|
| **覆盖范围** | 通用框架（未实现） | 仅战斗系统 | **所有功能模块** |
| **架构** | 多Hub | 单Hub（战斗） | **统一Hub框架** |
| **推送机制** | CombatSegment聚合 | FrameTick固定频率 | **多种机制（按需）** |
| **事件驱动** | 无 | 无 | **✅ EventBus解耦** |
| **扩展性** | 低 | 低 | **✅ 插件式Broadcaster** |
| **实施指南** | 概念性 | 战斗系统具体实施 | **✅ 完整5阶段计划** |

### 为什么需要版本 3.0？

**版本 2.0 的局限**：
1. ❌ 仅覆盖战斗系统
2. ❌ 其他功能（生产、采集、组队）需要重新设计
3. ❌ 缺少统一管理框架
4. ❌ 架构扩展困难

**版本 3.0 的优势**：
1. ✅ **统一框架**: 支持所有功能模块
2. ✅ **单一连接**: 降低资源消耗
3. ✅ **事件驱动**: 业务逻辑解耦
4. ✅ **易于扩展**: 3步添加新功能
5. ✅ **完整计划**: 新手可按步骤执行

**版本关系**：
- 版本 3.0 提供统一框架
- 版本 2.0 的战斗系统设计可以整合到版本 3.0
- 两者可以结合使用

---

## 🏗️ 系统架构概览（版本 3.0）

### 核心组件

```
┌─────────────────────────────────────────────────────────────┐
│  Blazor WebAssembly Client                                  │
│                                                              │
│  SignalRConnectionManager (单一连接)                         │
│           │                                                  │
│           ▼                                                  │
│  MessageRouter ─┬─> CombatHandler                           │
│                 ├─> ActivityHandler                         │
│                 ├─> PartyHandler                            │
│                 └─> GeneralHandler                          │
│                                                              │
└────────────────────┬────────────────────────────────────────┘
                     │ WebSocket
┌────────────────────▼────────────────────────────────────────┐
│  ASP.NET Core Server                                        │
│                                                              │
│  GameHub (统一入口)                                          │
│       │                                                      │
│       ▼                                                      │
│  SignalRDispatcher (消息队列 + 批量发送)                     │
│       │                                                      │
│       ├─> CombatBroadcaster                                 │
│       ├─> ActivityBroadcaster                               │
│       ├─> PartyBroadcaster                                  │
│       └─> GeneralBroadcaster                                │
│            ▲                                                 │
│            │                                                 │
│       Domain Event Bus                                       │
│            ▲                                                 │
│            │                                                 │
│  Business Logic (Combat/Activity/Party/etc.)                │
│                                                              │
└──────────────────────────────────────────────────────────────┘
```

### 关键特性说明

**1. 单Hub单连接**
- 一个用户只有一个SignalR连接
- 所有消息通过GameHub路由
- 降低连接管理复杂度

**2. 事件驱动架构**
- 业务逻辑发布领域事件
- Broadcaster订阅事件并推送
- 业务逻辑不依赖SignalR

**3. 模块化Broadcaster**
- 每个功能有独立Broadcaster
- 易于添加新功能
- 职责清晰，易于维护

**4. 高性能分发**
- 异步消息队列
- 批量发送优化
- 优先级调度

---

## 🚀 实施路线（版本 3.0）

### 5个阶段计划

```
阶段一：基础架构搭建 (2周)
├─ 环境准备
├─ GameHub实现
├─ ConnectionManager实现
├─ SignalRDispatcher实现
└─ 客户端连接管理

阶段二：战斗系统集成 (2-3周)
├─ CombatBroadcaster
├─ BattleFrameBuffer
├─ BattleInstance集成
└─ 客户端战斗状态管理

阶段三：活动与生产系统 (2-3周)
├─ ActivityBroadcaster
├─ CraftingBroadcaster
├─ GatheringBroadcaster
└─ 客户端集成

阶段四：组队与社交系统 (2-3周, 可选)
├─ PartyBroadcaster
├─ 多人战斗同步
└─ 客户端集成

阶段五：优化与监控 (1-2周)
├─ 性能优化
├─ 监控面板
└─ 文档完善

总计：9-13周（约2-3个月）
```

详细实施指南请参考 [SignalR实施计划-分步指南.md](./SignalR实施计划-分步指南.md)

---

## 💡 关键设计决策

### 1. 为什么使用单Hub而不是多Hub？

**传统方式（多Hub）**：
```
Client ──┬─> CombatHub      (连接1)
         ├─> ActivityHub    (连接2)
         ├─> PartyHub       (连接3)
         └─> NotificationHub(连接4)

问题：
❌ 多个连接，资源消耗大
❌ 连接管理复杂
❌ 状态同步困难
```

**统一方式（单Hub）**：
```
Client ─> GameHub ─┬─> CombatBroadcaster
                   ├─> ActivityBroadcaster
                   ├─> PartyBroadcaster
                   └─> GeneralBroadcaster

优势：
✅ 单一连接，资源高效
✅ 统一管理，简单清晰
✅ 消息路由灵活
```

### 2. 为什么使用事件驱动架构？

**直接依赖（不推荐）**：
```csharp
// 业务逻辑直接依赖SignalR
public class BattleRunner
{
    private readonly IHubContext<GameHub> _hubContext;
    
    public async Task ExecuteTick()
    {
        // 业务逻辑...
        await _hubContext.Clients.Group("battle:123")
            .SendAsync("FrameTick", frame);  // 紧耦合
    }
}
```

**事件驱动（推荐）**：
```csharp
// 业务逻辑发布事件
public class BattleRunner
{
    private readonly IDomainEventBus _eventBus;
    
    public async Task ExecuteTick()
    {
        // 业务逻辑...
        await _eventBus.PublishAsync(
            new BattleFrameGeneratedEvent { Frame = frame });  // 解耦
    }
}

// Broadcaster订阅并推送
public class CombatBroadcaster
{
    public CombatBroadcaster(IDomainEventBus eventBus, ISignalRDispatcher dispatcher)
    {
        eventBus.Subscribe<BattleFrameGeneratedEvent>(async e =>
        {
            await dispatcher.SendToGroupAsync("battle:123", "FrameTick", e.Frame);
        });
    }
}
```

**优势**：
- ✅ 业务逻辑不依赖SignalR
- ✅ 易于单元测试（Mock EventBus）
- ✅ 支持多个订阅者
- ✅ 异步处理不阻塞

### 3. 如何决策使用API还是SignalR？

参考 [API与SignalR选择指南.md](./API与SignalR选择指南.md)

**快速决策**：
```
需要服务器主动通知？
    │
 ┌──┴──┐
是    否 → 使用 API
 │
实时性 < 2秒？
 │
┌┴┐
是 否 → 可选SignalR或API
│
SignalR
```

**典型场景**：
- 战斗状态更新 → SignalR（高频实时）
- 查看背包 → API（低频查询）
- 活动完成 → SignalR（事件通知）
- 购买物品 → API（操作请求）

---

## 📝 后续工作

### 已完成（版本 3.0）
- ✅ 完整的需求分析
- ✅ 统一管理架构设计
- ✅ API vs SignalR决策指南
- ✅ 详细的5阶段实施计划
- ✅ 完整代码示例

### 待实施
- [ ] 阶段一：基础架构搭建
- [ ] 阶段二：战斗系统集成
- [ ] 阶段三：活动与生产系统
- [ ] 阶段四：组队与社交系统
- [ ] 阶段五：优化与监控

---

## 📞 获取帮助

如有疑问或需要帮助：

1. **首先**: 阅读 [SignalR统一管理系统-完整方案总览.md](./SignalR统一管理系统-完整方案总览.md)
2. **需求相关**: 参考 [SignalR需求分析与边界定义.md](./SignalR需求分析与边界定义.md)
3. **架构相关**: 参考 [SignalR统一管理系统-总体架构.md](./SignalR统一管理系统-总体架构.md)
4. **决策相关**: 参考 [API与SignalR选择指南.md](./API与SignalR选择指南.md)
5. **实施相关**: 参考 [SignalR实施计划-分步指南.md](./SignalR实施计划-分步指南.md)
6. **项目背景**: 查阅 docs/整合设计总结.txt
7. **问题反馈**: 在项目 Issue 中提问

---

## 📄 文档版本历史

| 版本 | 日期 | 变更说明 |
|------|------|---------|
| 3.0 | 2025-10-21 | 统一管理系统设计：单Hub、事件驱动、完整实施计划 |
| 2.0 | 2025-10-21 | 战斗系统实时帧推送：固定频率 + 版本机制 + 插值渲染 |
| 1.0 | 2025-10 | 初始版本：CombatSegment 聚合推送 + 轮询降级 |

---

**当前推荐版本**: 3.0（统一管理系统）  
**文档状态**: ✅ 完成  
**最后更新**: 2025年10月21日

---

**🎉 开始使用版本 3.0 设计！**

推荐从 [SignalR统一管理系统-完整方案总览.md](./SignalR统一管理系统-完整方案总览.md) 开始阅读。
