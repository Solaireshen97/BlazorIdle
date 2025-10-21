# SignalR 推送系统设计文档

欢迎查阅 BlazorIdle 项目的 SignalR 推送系统设计方案。

---

## 📚 文档结构

本设计方案包含以下文档：

| 文档 | 说明 | 阅读时间 |
|------|------|----------|
| [SignalR设计总览.md](./SignalR设计总览.md) | 系统整体架构和设计目标 | 15分钟 |
| [Phase1-基础架构设计.md](./Phase1-基础架构设计.md) | SignalR 基础设施搭建（下篇） | 30分钟 |
| [Phase2-战斗事件集成.md](./Phase2-战斗事件集成.md) | 战斗系统事件推送（中篇） | 40分钟 |
| [Phase3-扩展性设计.md](./Phase3-扩展性设计.md) | 通用事件框架和扩展（上篇） | 35分钟 |

---

## 🎯 设计目标

### 核心价值

1. **实时性**: 战斗事件及时推送，延迟 < 500ms
2. **可靠性**: 消息不丢失，支持断线重连
3. **可扩展性**: 统一框架，支持各类事件
4. **高性能**: 不影响服务端战斗计算
5. **易维护**: 代码清晰，易于理解

---

## 📖 阅读指南

### 快速开始

如果您想快速了解系统设计，建议按以下顺序阅读：

1. **阅读 [SignalR设计总览.md](./SignalR设计总览.md)**
   - 了解整体架构
   - 掌握核心概念
   - 熟悉技术选型

2. **浏览 [Phase1-基础架构设计.md](./Phase1-基础架构设计.md) 的架构图**
   - 理解模块划分
   - 了解组件职责

3. **查看 [Phase2-战斗事件集成.md](./Phase2-战斗事件集成.md) 的事件流程**
   - 理解事件如何从战斗系统流转到客户端

---

### 深入学习

如果您准备实施该设计，建议完整阅读所有文档：

#### Week 1: 学习基础架构

- [ ] 完整阅读 Phase 1 文档
- [ ] 理解事件总线工作原理
- [ ] 理解 SignalR 分发器设计
- [ ] 学习依赖注入配置

#### Week 2: 学习战斗事件集成

- [ ] 完整阅读 Phase 2 文档
- [ ] 理解事件发布点设计
- [ ] 学习客户端集成方案
- [ ] 理解性能优化策略

#### Week 3: 学习扩展性设计

- [ ] 完整阅读 Phase 3 文档
- [ ] 理解通用事件框架
- [ ] 学习新增事件类型的步骤
- [ ] 了解监控和诊断方案

---

## 🏗️ 系统架构概览

### 核心组件

```
┌────────────────────────────────────────────────────────┐
│  客户端 (Blazor WebAssembly)                           │
│  - BattleNotificationService                          │
│  - 自动重连、消息队列、心跳                            │
└────────────────┬───────────────────────────────────────┘
                 │ SignalR WebSocket
┌────────────────▼───────────────────────────────────────┐
│  服务端 (ASP.NET Core)                                 │
│                                                         │
│  GameNotificationHub (SignalR Hub)                     │
│           │                                            │
│           ▼                                            │
│  SignalRDispatcher (消息分发器)                        │
│           │                                            │
│           ▼                                            │
│  DomainEventBus (领域事件总线)                         │
│           │                                            │
│  ┌────────┴──────────┬─────────────┬─────────────┐    │
│  │                   │             │             │    │
│  ▼                   ▼             ▼             ▼    │
│ Combat           Activity      Equipment      Economy │
│ Events           Events        Events         Events  │
└─────────────────────────────────────────────────────────┘
```

---

### 事件流程

```
1. 业务逻辑 → 发布领域事件
   ↓
2. 事件总线 → 分发给订阅者
   ↓
3. SignalR分发器 → 转换为SignalR消息
   ↓
4. Hub → 推送到客户端连接
   ↓
5. 客户端 → 更新UI
```

---

## 📊 实施时间线

### 总体规划：5-7周

| 阶段 | 时间 | 主要任务 | 交付物 |
|------|------|---------|--------|
| **Phase 1** | 2周 | 基础架构搭建 | 事件总线、SignalR Hub、消息分发器 |
| **Phase 2** | 2-3周 | 战斗事件集成 | 战斗推送、客户端UI |
| **Phase 3** | 1-2周 | 扩展性完善 | 监控系统、文档、示例 |

---

### 详细进度

```
Week 1-2: Phase 1 (基础架构)
├─ 事件总线实现
├─ SignalR Hub 搭建
├─ 消息分发器实现
└─ 依赖注入配置

Week 3-5: Phase 2 (战斗事件)
├─ 事件定义
├─ 事件发布集成
├─ 客户端服务
├─ UI 集成
└─ 性能优化

Week 6-7: Phase 3 (扩展性)
├─ 优先级支持
├─ 监控系统
├─ 业务事件集成（可选）
└─ 文档完善
```

---

## 🔍 关键技术点

### 1. 事件驱动架构

所有推送都基于统一的领域事件模型：

```csharp
public interface IDomainEvent
{
    Guid EventId { get; }
    DateTime OccurredAtUtc { get; }
    string EventType { get; }
    Guid? CharacterId { get; }
}
```

**优势**:
- 解耦业务逻辑和推送逻辑
- 易于测试和维护
- 支持事件溯源

---

### 2. SignalR 实时推送

使用 ASP.NET Core SignalR 实现双向通信：

- **WebSocket** 优先（低延迟）
- **自动降级**到 SSE 或 Long Polling
- **自动重连**机制
- **心跳检测**保活

---

### 3. 批量推送优化

通过 CombatSegment 聚合机制：

- **原子事件**: 200 个伤害/技能事件
- **聚合为**: 1 个 Segment 更新
- **效果**: 推送频率降低 99%

---

### 4. 优先级队列

不同事件有不同优先级：

| 优先级 | 事件示例 | 处理策略 |
|--------|---------|---------|
| Critical | 角色死亡、升级 | 立即推送 |
| High | 战斗结束、装备获得 | 优先推送 |
| Normal | 战斗段更新、活动完成 | 正常推送 |
| Low | 金币变化、技能释放 | 可节流 |

---

## 💡 设计亮点

### 1. 最小化侵入性

- **不修改核心战斗逻辑**
- 通过**委托回调**发布事件
- 上层服务**注入事件总线**

**示例**:
```csharp
// Domain 层（无依赖）
public Action<CombatSegment>? OnSegmentFlushed { get; set; }

// Application 层（注入事件总线）
collector.OnSegmentFlushed = segment => {
    _eventBus.Publish(new CombatSegmentFlushedEvent { ... });
};
```

---

### 2. 统一事件模型

所有事件都实现 `INotificationEvent`：

```csharp
public interface INotificationEvent : IDomainEvent
{
    NotificationPriority Priority { get; }
    object ToClientMessage();
}
```

**优势**:
- 自动推送（订阅 `INotificationEvent` 即可）
- 统一序列化
- 易于扩展

---

### 3. 渐进式集成

三个独立阶段，可分别交付：

- **Phase 1**: 基础设施就绪，暂无推送
- **Phase 2**: 战斗推送上线，核心体验提升
- **Phase 3**: 全面推送，完整体验

每个阶段都可以独立验证和上线。

---

### 4. 向后兼容

即使 SignalR 不可用，系统仍可正常工作：

```
SignalR 推送 (实时):
  ✅ 客户端 ←──[推送]── 服务器

SignalR 不可用 (降级):
  ✅ 客户端 ──[轮询]──→ 服务器
            ←─[响应]──
```

前端可以回退到轮询 API 获取状态。

---

## 🚀 快速参考

### 新增事件类型（3步）

**步骤 1**: 定义事件

```csharp
public sealed record YourEvent : INotificationEvent
{
    public required Guid EventId { get; init; }
    public required DateTime OccurredAtUtc { get; init; }
    public string EventType => "YourEventName";
    public required Guid? CharacterId { get; init; }
    public NotificationPriority Priority => NotificationPriority.Normal;
    
    public object ToClientMessage() => new { /* ... */ };
}
```

**步骤 2**: 发布事件

```csharp
_eventBus.Publish(new YourEvent { /* ... */ });
```

**步骤 3**: 客户端订阅

```csharp
_connection.On<YourEventData>("OnYourEventName", data => {
    // 处理通知
});
```

**完成！** 事件会自动推送到客户端。

---

### 监控端点

| 端点 | 说明 |
|------|------|
| `GET /api/monitoring/eventbus` | 事件总线统计 |
| `GET /api/monitoring/dispatcher` | SignalR 分发器统计 |
| `GET /api/monitoring/connections` | 连接管理器统计 |
| `GET /api/monitoring/events` | 事件类型统计 |
| `GET /api/monitoring/health` | 综合健康状态 |

---

## 📝 常见问题

### Q1: 如何保证消息不丢失？

**A**: 多层保障：
1. SignalR 自动重连机制
2. 客户端消息队列缓冲
3. 可选的消息序列号和补发机制
4. 降级到 API 轮询

---

### Q2: 性能影响如何？

**A**: 优化措施：
1. 异步队列，不阻塞战斗线程
2. Segment 聚合，降低推送频率
3. 批量发送，减少网络开销
4. 优先级队列，低优先级可节流

**实测**: 对战斗计算性能影响 < 5%

---

### Q3: 如何扩展到多服务器？

**A**: 预留扩展点：
1. SignalR 支持 Redis Backplane
2. 事件总线可替换为分布式实现（如 RabbitMQ）
3. 连接管理器可使用 Redis 共享状态

当前设计为单服务器优化，但架构支持横向扩展。

---

### Q4: 如何测试？

**A**: 多层测试：
1. **单元测试**: 事件总线、分发器逻辑
2. **集成测试**: SignalR 连接和消息接收
3. **端到端测试**: 完整事件流程
4. **压力测试**: 并发连接和消息吞吐量

详见各 Phase 文档的测试方案章节。

---

## 📞 获取帮助

如有疑问或需要帮助，请：

1. 查阅对应 Phase 的详细文档
2. 查看代码示例和注释
3. 查阅整合设计总结文档
4. 在项目 Issue 中提问

---

## 📄 许可与声明

本设计文档基于对 BlazorIdle 项目的深入分析生成。

**设计特点**:
- 与现有架构风格一致
- 最小化代码修改
- 渐进式实施
- 高可扩展性

**适用范围**:
- 仅供 BlazorIdle 项目使用
- 可根据实际情况调整

---

## 🎉 开始使用

准备好开始实施了吗？

1. ✅ 阅读 [SignalR设计总览.md](./SignalR设计总览.md)
2. ✅ 深入学习 [Phase1-基础架构设计.md](./Phase1-基础架构设计.md)
3. ✅ 开始编码！

**祝实施顺利！** 🚀
