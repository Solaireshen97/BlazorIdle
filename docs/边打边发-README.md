# 边打边发 (Combat-Time Reward Distribution) - 完整实现文档

## 项目概述

"边打边发"是 BlazorIdle 游戏中的实时奖励发放系统。该系统允许玩家在战斗进行过程中周期性地获得奖励（金币、经验、物品），而不是等到战斗结束后才统一结算。

### 核心特性
✅ **实时发放**: 战斗过程中每 10 秒（可配置）自动发放奖励  
✅ **幂等保护**: 通过唯一键防止重复发放奖励  
✅ **事务安全**: 所有操作在数据库事务中进行，保证原子性  
✅ **审计追踪**: 完整记录每次奖励发放的历史  
✅ **性能优先**: 静默失败模式，不影响战斗推进

## 快速开始

### 1. 启用边打边发

在 `appsettings.json` 中配置：

```json
{
  "Combat": {
    "EnablePeriodicRewards": true,
    "RewardFlushIntervalSeconds": 10.0
  }
}
```

### 2. 运行数据库迁移

```bash
cd BlazorIdle.Server
dotnet ef database update
```

这会创建以下表：
- `inventory_items`: 存储角色背包物品
- `economy_events`: 记录所有经济事件

### 3. 启动游戏

```bash
dotnet run
```

系统会自动开始工作，在战斗过程中周期性发放奖励。

## 系统架构

### 数据流

```
战斗进行中
    ↓
StepBattleHostedService (每 50ms 推进)
    ↓
StepBattleCoordinator.AdvanceAll()
    ↓
检查奖励发放周期 (每 10 秒)
    ↓
TryFlushPeriodicRewards()
    ↓
聚合新 Segments 的击杀/副本完成
    ↓
计算抽样奖励 (EconomyCalculator)
    ↓
RewardGrantService.GrantRewardsAsync()
    ↓
[事务开始]
    ├─ 更新 Character.Gold/Experience
    ├─ 更新 InventoryItem (合并或创建)
    └─ 创建 EconomyEventRecord (幂等记录)
    ↓
[事务提交]
```

### 核心组件

#### 1. RewardGrantService
- **路径**: `BlazorIdle.Server/Application/Economy/RewardGrantService.cs`
- **职责**: 执行奖励发放，确保幂等性和事务安全
- **接口**: `IRewardGrantService`

#### 2. StepBattleCoordinator
- **路径**: `BlazorIdle.Server/Application/Battles/Step/StepBattleCoordinator.cs`
- **职责**: 协调战斗推进和奖励发放
- **方法**: `TryFlushPeriodicRewards()`

#### 3. 数据模型
- **InventoryItem**: 角色背包物品
- **EconomyEventRecord**: 经济事件记录
- **Character**: 新增 Gold 和 Experience 字段

## 配置说明

### 配置项

| 配置项 | 类型 | 默认值 | 说明 |
|-------|------|--------|------|
| Combat:EnablePeriodicRewards | bool | true | 是否启用边打边发 |
| Combat:RewardFlushIntervalSeconds | double | 10.0 | 奖励发放间隔（秒） |

### 不同环境配置

**开发环境** (`appsettings.Development.json`):
```json
{
  "Combat": {
    "RewardFlushIntervalSeconds": 5.0
  }
}
```

**生产环境** (`appsettings.Production.json`):
```json
{
  "Combat": {
    "RewardFlushIntervalSeconds": 10.0
  }
}
```

## API 使用

### 基本用法

```csharp
public class MyService
{
    private readonly IRewardGrantService _rewardService;
    
    public MyService(IRewardGrantService rewardService)
    {
        _rewardService = rewardService;
    }
    
    public async Task GrantBattleRewards(Guid characterId, Guid battleId)
    {
        var items = new Dictionary<string, int>
        {
            ["item_sword_1"] = 1,
            ["item_potion_hp"] = 5
        };
        
        var granted = await _rewardService.GrantRewardsAsync(
            characterId: characterId,
            gold: 100,
            exp: 50,
            items: items,
            idempotencyKey: $"battle:{battleId}:final",
            eventType: "battle_final_reward",
            battleId: battleId
        );
        
        if (granted)
        {
            Console.WriteLine("Rewards granted successfully");
        }
        else
        {
            Console.WriteLine("Rewards already granted (idempotent)");
        }
    }
}
```

### 幂等键规范

幂等键格式：`{prefix}:{resource}:{type}:{identifier}`

示例：
- 战斗周期奖励: `battle:abc-123:periodic:sim15.50:seg3-7`
- 战斗最终奖励: `battle:abc-123:final`
- 离线奖励: `offline:char-456:2025-10-06T12:00:00Z`

## 数据库

### 表结构

#### inventory_items
```sql
CREATE TABLE inventory_items (
    Id TEXT PRIMARY KEY,
    CharacterId TEXT NOT NULL,
    ItemId TEXT NOT NULL,
    Quantity INTEGER NOT NULL,
    CreatedAt TEXT NOT NULL,
    UpdatedAt TEXT NOT NULL,
    FOREIGN KEY (CharacterId) REFERENCES Characters(Id) ON DELETE CASCADE
);

CREATE UNIQUE INDEX IX_InventoryItems_CharacterId_ItemId 
ON inventory_items (CharacterId, ItemId);
```

#### economy_events
```sql
CREATE TABLE economy_events (
    Id TEXT PRIMARY KEY,
    CharacterId TEXT NOT NULL,
    BattleId TEXT,
    EventType TEXT NOT NULL,
    IdempotencyKey TEXT NOT NULL UNIQUE,
    Gold INTEGER NOT NULL,
    Exp INTEGER NOT NULL,
    ItemsJson TEXT,
    CreatedAt TEXT NOT NULL
);

CREATE INDEX IX_EconomyEvents_CharacterId ON economy_events (CharacterId);
CREATE INDEX IX_EconomyEvents_BattleId ON economy_events (BattleId) WHERE BattleId IS NOT NULL;
```

### 常用查询

**查看角色背包**:
```sql
SELECT ItemId, Quantity, UpdatedAt
FROM inventory_items
WHERE CharacterId = '{characterId}'
ORDER BY UpdatedAt DESC;
```

**查看经济历史**:
```sql
SELECT EventType, Gold, Exp, ItemsJson, CreatedAt
FROM economy_events
WHERE CharacterId = '{characterId}'
ORDER BY CreatedAt DESC
LIMIT 50;
```

## 故障排查

### 常见问题

#### Q1: 奖励没有发放
**检查清单**:
1. 确认 `EnablePeriodicRewards` 为 `true`
2. 检查战斗模式是否为 sampled（边打边发仅支持 sampled）
3. 查看日志确认是否有错误
4. 检查数据库连接是否正常

#### Q2: 奖励重复发放
这不应该发生，因为有幂等性保护。如果发生：
1. 检查 `economy_events` 表的 `IdempotencyKey` 唯一索引是否存在
2. 查看是否有多个进程在运行
3. 查看日志确认幂等性检查是否正常工作

#### Q3: 性能问题
如果发现性能下降：
1. 增加 `RewardFlushIntervalSeconds` 到 15-20 秒
2. 检查数据库索引是否正常
3. 监控数据库连接池使用情况
4. 考虑归档历史 `economy_events` 数据

## 性能优化

### 容量规划

| 在线玩家数 | 推荐间隔 | 数据库连接池 | 预期 TPS |
|-----------|---------|-------------|---------|
| < 100     | 5秒     | 20          | ~20     |
| 100-500   | 10秒    | 50          | ~50     |
| 500-1000  | 15秒    | 80          | ~70     |
| > 1000    | 20秒    | 100+        | ~100    |

### 监控指标

建议监控以下指标：
- **奖励发放延迟**: 应 < 100ms
- **数据库事务失败率**: 应 < 1%
- **幂等性拦截率**: 正常情况应为 0
- **每分钟发放次数**: 根据在线玩家数评估

## 文档索引

- [架构文档](./边打边发-Architecture.md) - 详细的系统架构和设计原则
- [数据库架构](./边打边发-Database-Schema.md) - 数据库表结构和查询
- [配置指南](./边打边发-Configuration-Guide.md) - 配置选项和调优建议
- [API 文档](./边打边发-API-Documentation.md) - API 接口和使用示例

## 版本历史

### v1.0.0 (2025-10-06)
- ✅ 初始实现
- ✅ 基础数据模型（InventoryItem, EconomyEventRecord）
- ✅ RewardGrantService 带幂等性
- ✅ StepBattleCoordinator 集成
- ✅ 完整文档

## 未来计划

### 短期（1-2 个月）
- [ ] 单元测试和集成测试
- [ ] 性能基准测试
- [ ] 监控和告警系统
- [ ] 管理后台查询工具

### 中期（3-6 个月）
- [ ] 实时通知系统（WebSocket）
- [ ] 批量奖励优化
- [ ] 奖励预测和分析
- [ ] 经济系统监控仪表板

### 长期（6+ 个月）
- [ ] 分布式事务支持
- [ ] 微服务拆分（奖励服务独立）
- [ ] 大数据分析集成
- [ ] 机器学习预测

## 贡献指南

### 报告问题
在 GitHub Issues 中报告问题，请包含：
1. 问题描述
2. 重现步骤
3. 预期行为
4. 实际行为
5. 相关日志

### 提交改进
1. Fork 项目
2. 创建特性分支
3. 提交改动
4. 推送到分支
5. 创建 Pull Request

## 许可证

[项目许可证信息]

## 联系方式

- 项目维护者: @Solaireshen97
- 邮箱: [邮箱地址]
- 讨论: GitHub Discussions

---

**注意**: 本系统仅在 `sampled` 模式下工作，`expected` 模式会在战斗结束时统一结算。
