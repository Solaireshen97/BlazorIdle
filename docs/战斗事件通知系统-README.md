# 战斗事件通知系统

## 概述

战斗事件通知系统为前端提供详细的战斗消息通知，支持完全可配置的消息模板。

## 特性

✅ **完全配置化** - 所有消息模板都在配置文件中，无需修改代码  
✅ **高可扩展性** - 易于添加新的事件类型和消息模板  
✅ **向后兼容** - 不影响现有功能，可独立启用/禁用  
✅ **全面测试** - 9个单元测试，全部通过  
✅ **详细文档** - 包含实施报告和快速开始指南

## 支持的事件类型

| 事件类型 | 说明 | 示例消息 |
|---------|------|---------|
| **AttackStart** | 攻击开始 | "勇者 开始攻击 史莱姆" |
| **DamageDealt** | 伤害造成 | "勇者 对 史莱姆 造成 50 点物理伤害" |
| **DamageReceived** | 伤害接收 | "勇者 受到 哥布林 的 15 点物理伤害（剩余 85/100）" |

## 快速开始

### 1. 配置文件位置

主配置：`BlazorIdle.Server/Config/BattleEvents/battle-events-config.json`

### 2. 自定义消息模板

```json
{
  "Messages": {
    "AttackStart": {
      "PlayerAttacksEnemy": "⚔️ {attacker} 冲向 {target}！"
    },
    "DamageDealt": {
      "Normal": "💥 {attacker} 对 {target} 造成 {damage} 点{damageType}伤害",
      "Critical": "🔥 暴击！{attacker} 对 {target} 造成 {damage} 点{damageType}伤害！"
    }
  }
}
```

### 3. 可用占位符

- `{attacker}` - 攻击者名称
- `{target}` - 目标名称
- `{receiver}` - 接收者名称
- `{damage}` - 伤害值
- `{damageType}` - 伤害类型（自动翻译）
- `{currentHp}` - 当前血量
- `{maxHp}` - 最大血量

### 4. 启用/禁用事件

在 `battle-events-config.json` 中：

```json
{
  "EnableBattleEventMessages": true,
  "Messages": {
    "AttackStart": {
      "Enabled": true
    }
  }
}
```

或在 `signalr-config.json` 中：

```json
{
  "Notification": {
    "EnableAttackStartNotification": true,
    "EnableDamageDealtNotification": true,
    "EnableDamageReceivedNotification": true
  }
}
```

## 文档导航

- **[战斗事件通知系统实施报告](./战斗事件通知系统实施报告.md)** - 详细的技术实现说明
  - 系统架构设计
  - 配置系统详解
  - 代码实现细节
  - 测试覆盖报告
  - 可扩展性指南

- **[战斗事件通知系统快速开始](./战斗事件通知系统快速开始.md)** - 使用指南
  - 配置文件说明
  - 消息模板示例
  - 前端集成示例
  - 常见问题解答
  - 性能优化建议

## 技术栈

- **.NET 9.0** - 后端框架
- **SignalR** - 实时通信
- **xUnit** - 单元测试
- **JSON** - 配置文件格式

## 文件结构

```
BlazorIdle/
├── BlazorIdle.Server/
│   ├── Config/
│   │   ├── BattleEvents/
│   │   │   └── battle-events-config.json         # 战斗事件配置
│   │   ├── SignalR/
│   │   │   └── signalr-config.json               # SignalR配置
│   │   ├── BattleEventsOptions.cs                # 配置类定义
│   │   └── SignalROptions.cs                     # SignalR选项
│   ├── Domain/Combat/
│   │   ├── AttackTickEvent.cs                    # 玩家攻击事件（已修改）
│   │   └── EnemyAttackEvent.cs                   # 敌人攻击事件（已修改）
│   └── Services/
│       ├── BattleEventMessageFormatter.cs        # 消息格式化服务
│       └── BattleNotificationService.cs          # 通知服务（已修改）
├── BlazorIdle.Shared/
│   └── Models/
│       └── BattleNotifications.cs                # 事件DTO定义（已修改）
├── tests/
│   └── BlazorIdle.Tests/
│       └── BattleEventNotificationTests.cs       # 单元测试
└── docs/
    ├── 战斗事件通知系统-README.md                  # 本文档
    ├── 战斗事件通知系统实施报告.md                  # 实施报告
    └── 战斗事件通知系统快速开始.md                  # 快速开始
```

## 测试结果

```
Test Run Successful.
Total tests: 9
     Passed: 9
     Failed: 0
 Total time: 0.9316 Seconds
```

### 测试覆盖

- ✅ 玩家攻击事件通知
- ✅ 敌人攻击事件通知
- ✅ 伤害造成事件通知
- ✅ 伤害接收事件通知
- ✅ 消息模板格式化
- ✅ 占位符替换
- ✅ 暴击消息处理

## 使用示例

### 前端监听事件

```typescript
connection.on("BattleEvent", (eventData) => {
    switch (eventData.eventType) {
        case "AttackStart":
            showMessage(`${eventData.attackerName} 开始攻击 ${eventData.targetName}`);
            break;
        case "DamageDealt":
            const crit = eventData.isCrit ? "暴击！" : "";
            showMessage(`${crit}造成 ${eventData.damage} 点伤害`);
            break;
        case "DamageReceived":
            showMessage(`受到 ${eventData.damage} 点伤害（剩余 ${eventData.currentHp}/${eventData.maxHp}）`, "danger");
            break;
    }
});
```

## 扩展性

系统支持轻松添加新的事件类型：

1. 在 `BattleNotifications.cs` 中定义新的 DTO
2. 在 `BattleEventsOptions.cs` 中添加配置类
3. 在 `battle-events-config.json` 中添加模板
4. 在事件触发点发送通知
5. 在 `BattleEventMessageFormatter` 中添加格式化方法

详细步骤请参考[实施报告](./战斗事件通知系统实施报告.md#可扩展性设计)。

## 性能特点

- **异步通知**：使用 `_ = Task` 不等待，不阻塞战斗逻辑
- **配置过滤**：通过配置禁用不需要的事件，减少网络流量
- **最小侵入**：仅在事件触发点添加通知代码，不影响核心逻辑

## 版本信息

- **初始版本**：1.0.0
- **发布日期**：2025-10-14
- **兼容性**：BlazorIdle v2.x+

## 后续计划

- [ ] 添加技能施放事件
- [ ] 添加Buff生效/失效事件
- [ ] 添加波次刷新事件
- [ ] 支持多语言消息模板
- [ ] 前端战斗日志组件

## 贡献者

- Solaireshen97 - 项目维护者

## 许可证

本项目遵循 MIT 许可证。

## 相关链接

- [SignalR 集成优化方案](./SignalR集成优化方案.md)
- [SignalR 轻量事件优化指南](./SignalR轻量事件优化指南.md)
- [战斗系统 Phase 4 完成报告](./战斗系统Phase4完成报告.md)
