# 边打边发 - 数据库架构文档

## 概述

本文档详细说明边打边发系统涉及的数据库表结构和关系。

## 表结构

### 1. inventory_items (背包物品)

存储玩家的背包物品，支持物品数量累加。

| 列名 | 类型 | 约束 | 说明 |
|------|------|------|------|
| Id | GUID | PRIMARY KEY | 物品记录ID |
| CharacterId | GUID | NOT NULL, FOREIGN KEY | 角色ID |
| ItemId | VARCHAR(100) | NOT NULL | 物品ID（关联物品定义） |
| Quantity | INTEGER | NOT NULL | 物品数量 |
| CreatedAt | DATETIME | NOT NULL | 创建时间 |
| UpdatedAt | DATETIME | NOT NULL | 更新时间 |

**索引**:
- PRIMARY KEY: `Id`
- UNIQUE INDEX: `(CharacterId, ItemId)` - 确保每个角色的每种物品只有一条记录
- FOREIGN KEY: `CharacterId` → `Characters(Id)` ON DELETE CASCADE

**设计说明**:
- 使用 CharacterId + ItemId 组合唯一索引，防止重复创建同一物品
- 支持数量累加：获得物品时更新 Quantity，不创建新记录
- ON DELETE CASCADE：角色删除时自动清理背包

### 2. economy_events (经济事件记录)

记录所有经济事件，用于审计和幂等性检查。

| 列名 | 类型 | 约束 | 说明 |
|------|------|------|------|
| Id | GUID | PRIMARY KEY | 事件ID |
| CharacterId | GUID | NOT NULL | 角色ID |
| BattleId | GUID | NULL | 战斗ID（可选） |
| EventType | VARCHAR(100) | NOT NULL | 事件类型 |
| IdempotencyKey | VARCHAR(200) | NOT NULL, UNIQUE | 幂等键 |
| Gold | BIGINT | NOT NULL | 金币数量 |
| Exp | BIGINT | NOT NULL | 经验数量 |
| ItemsJson | TEXT | NULL | 物品JSON |
| CreatedAt | DATETIME | NOT NULL | 创建时间 |

**索引**:
- PRIMARY KEY: `Id`
- UNIQUE INDEX: `IdempotencyKey` - 确保幂等性
- INDEX: `CharacterId` - 快速查询角色的经济历史
- INDEX: `BattleId` (filtered WHERE BattleId IS NOT NULL) - 查询战斗相关奖励

**EventType 枚举值**:
- `battle_periodic_reward`: 战斗周期性奖励
- `battle_final_reward`: 战斗最终奖励（未来）
- `dungeon_completion_reward`: 副本完成奖励（未来）
- `offline_reward`: 离线奖励（未来）

**IdempotencyKey 格式**:
```
battle:{battleId}:periodic:sim{simTime}:seg{fromIndex}-{toIndex}
```

**ItemsJson 格式**:
```json
{
  "item_sword_1": 1,
  "item_potion_hp": 5,
  "item_material_wood": 10
}
```

### 3. Characters (角色) - 新增字段

现有表新增经济相关字段：

| 列名 | 类型 | 约束 | 说明 |
|------|------|------|------|
| Gold | BIGINT | NOT NULL, DEFAULT 0 | 金币余额 |
| Experience | BIGINT | NOT NULL, DEFAULT 0 | 经验值 |

**说明**:
- Gold 和 Experience 直接累加，不需要历史记录
- 所有变更都通过 economy_events 记录审计日志

## 数据关系图

```
Characters (角色)
    ├── 1:N → inventory_items (背包物品)
    └── 1:N → economy_events (经济事件)

Battles (战斗)
    └── 1:N → economy_events (经济事件)
```

## 查询示例

### 查询角色背包
```sql
SELECT ItemId, Quantity, UpdatedAt
FROM inventory_items
WHERE CharacterId = @characterId
ORDER BY UpdatedAt DESC;
```

### 查询角色经济历史
```sql
SELECT EventType, Gold, Exp, ItemsJson, CreatedAt
FROM economy_events
WHERE CharacterId = @characterId
ORDER BY CreatedAt DESC
LIMIT 100;
```

### 查询战斗相关奖励
```sql
SELECT *
FROM economy_events
WHERE BattleId = @battleId
ORDER BY CreatedAt;
```

### 检查幂等键是否存在
```sql
SELECT COUNT(*)
FROM economy_events
WHERE IdempotencyKey = @key;
```

## 数据一致性

### 事务边界
所有奖励发放操作都在一个事务中进行：

```csharp
using var transaction = await _db.Database.BeginTransactionAsync();
try
{
    // 1. 更新角色 Gold/Experience
    character.Gold += gold;
    character.Experience += exp;
    
    // 2. 更新背包物品
    foreach (var item in items)
    {
        // 更新或插入
    }
    
    // 3. 创建 EconomyEventRecord
    _db.EconomyEvents.Add(economyEvent);
    
    // 4. 保存所有更改
    await _db.SaveChangesAsync();
    await transaction.CommitAsync();
}
catch
{
    await transaction.RollbackAsync();
    throw;
}
```

### 并发控制
- 使用数据库唯一约束防止重复
- 乐观并发：EF Core 的 SaveChanges 会检测并发冲突
- 幂等性检查在事务内进行，确保一致性

## 性能优化

### 索引策略
1. **IdempotencyKey UNIQUE**: 快速幂等性检查
2. **CharacterId + ItemId UNIQUE**: 快速背包查询和更新
3. **CharacterId INDEX**: 快速查询角色经济历史
4. **BattleId INDEX**: 快速查询战斗奖励

### 查询优化
- 使用覆盖索引减少回表
- 批量操作减少数据库往返
- 定期清理历史数据（economy_events 可按时间归档）

## 数据维护

### 备份策略
- economy_events 是重要审计数据，需要定期备份
- inventory_items 可以从 economy_events 重建

### 归档策略
- 定期将旧的 economy_events 归档到历史表
- 建议保留最近 3-6 个月的活跃数据
- 归档数据可用于数据分析和用户申诉

### 清理策略
- 删除角色时级联删除 inventory_items
- economy_events 保留用于审计（不删除）
- 定期清理无效的 IdempotencyKey（如关联战斗已删除）

## 迁移脚本

### 创建表的迁移
```bash
dotnet ef migrations add AddInventoryAndEconomyEvents
dotnet ef database update
```

### 添加 Character 字段的迁移
```bash
dotnet ef migrations add AddCharacterGoldAndExperience
dotnet ef database update
```

## 监控指标

### 建议监控的数据库指标
1. **economy_events 表增长速率**: 监控是否有异常大量插入
2. **inventory_items 表大小**: 监控背包膨胀
3. **幂等性冲突频率**: 监控 IdempotencyKey 重复次数
4. **查询响应时间**: 监控索引效率

### SQL 监控查询
```sql
-- 每小时经济事件数
SELECT DATE_TRUNC('hour', CreatedAt) AS hour,
       COUNT(*) AS event_count,
       SUM(Gold) AS total_gold,
       SUM(Exp) AS total_exp
FROM economy_events
WHERE CreatedAt > NOW() - INTERVAL '24 hours'
GROUP BY hour
ORDER BY hour DESC;

-- 背包物品统计
SELECT ItemId, SUM(Quantity) AS total_quantity, COUNT(*) AS owner_count
FROM inventory_items
GROUP BY ItemId
ORDER BY total_quantity DESC
LIMIT 20;
```

## 故障排查

### 常见问题

**Q: 幂等键冲突导致发放失败**
- 检查是否有重复的 IdempotencyKey
- 查看日志确认是否是预期的幂等拦截

**Q: 背包物品数量不正确**
- 查询 economy_events 验证发放记录
- 检查事务是否正确提交

**Q: 性能下降**
- 检查索引是否存在
- 分析慢查询日志
- 考虑分区或归档历史数据
