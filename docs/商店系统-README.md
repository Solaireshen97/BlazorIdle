# 商店系统 - 使用指南

**版本**: 1.0  
**状态**: ✅ 生产就绪  
**最后更新**: 2025-10-13

---

## 📋 目录

1. [系统概述](#系统概述)
2. [快速开始](#快速开始)
3. [配置指南](#配置指南)
4. [API 使用](#api-使用)
5. [数据模型](#数据模型)
6. [运维指南](#运维指南)
7. [故障排除](#故障排除)
8. [最佳实践](#最佳实践)

---

## 系统概述

商店系统是 BlazorIdle 游戏的核心经济模块，提供完整的商品交易功能。

### 核心功能

- ✅ **多商店支持**: 杂货铺、武器店、炼金术士等
- ✅ **灵活定价**: 金币或物品货币
- ✅ **购买限制**: 每日/每周限购
- ✅ **库存管理**: 有限/无限库存
- ✅ **自动发放**: 购买后自动添加到背包
- ✅ **等级限制**: 支持商品等级要求
- ✅ **高级过滤**: 多维度查询和排序
- ✅ **购买历史**: 完整的交易记录

### 技术特性

- **配置驱动**: 所有参数可通过 appsettings.json 配置
- **高性能**: 数据库索引优化 + 内存缓存
- **事务保证**: 原子操作，数据一致性
- **日志完整**: 结构化日志，便于监控
- **测试覆盖**: 52 个测试用例，100% 通过率

---

## 快速开始

### 1. 数据库初始化

```bash
# 应用数据库迁移
cd BlazorIdle.Server
dotnet ef database update
```

系统会自动创建以下表：
- `ShopDefinitions` - 商店定义
- `ShopItems` - 商品信息
- `PurchaseRecords` - 购买记录
- `PurchaseCounters` - 购买计数器

### 2. 配置商店数据

商店数据存储在配置文件中：
```
BlazorIdle.Server/Config/Shop/
├── ShopDefinitions.json  # 商店定义
└── ShopItems.json        # 商品配置
```

### 3. 启动服务

```bash
cd BlazorIdle.Server
dotnet run
```

### 4. 测试 API

```bash
# 获取商店列表
curl http://localhost:5000/api/shop/list

# 获取商品列表
curl http://localhost:5000/api/shop/{shopId}/items

# 购买商品
curl -X POST http://localhost:5000/api/shop/purchase \
  -H "Content-Type: application/json" \
  -d '{"shopItemId":"item_001","quantity":1}'
```

---

## 配置指南

### appsettings.json 配置

所有配置参数位于 `Shop` 配置节：

```json
{
  "Shop": {
    // ===========================
    // 缓存配置 (3 个参数)
    // ===========================
    "EnableCaching": true,
    "ShopDefinitionCacheMinutes": 60,
    "ShopItemsCacheMinutes": 30,
    
    // ===========================
    // 文件路径配置 (3 个参数)
    // ===========================
    "ConfigPath": "Config/Shop",
    "ShopDefinitionsFile": "ShopDefinitions.json",
    "ShopItemsFile": "ShopItems.json",
    
    // ===========================
    // 商店配置 (3 个参数)
    // ===========================
    "DefaultRefreshIntervalSeconds": 3600,
    "MaxShopNameLength": 50,
    "MaxShopDescriptionLength": 200,
    
    // ===========================
    // 商品配置 (3 个参数)
    // ===========================
    "MaxItemNameLength": 100,
    "MaxItemDescriptionLength": 500,
    "UnlimitedStock": -1,
    
    // ===========================
    // 购买限制配置 (4 个参数)
    // ===========================
    "DailyResetSeconds": 86400,
    "WeeklyResetSeconds": 604800,
    "DefaultDailyLimit": 10,
    "DefaultWeeklyLimit": 5,
    
    // ===========================
    // 价格配置 (2 个参数)
    // ===========================
    "MinPriceAmount": 1,
    "MaxPriceAmount": 1000000,
    
    // ===========================
    // 验证配置 (4 个参数)
    // ===========================
    "MinLevelRequirement": 1,
    "MaxLevelRequirement": 100,
    "MinPurchaseQuantity": 1,
    "MaxPurchaseQuantity": 999,
    
    // ===========================
    // 查询配置 (3 个参数)
    // ===========================
    "DefaultPageSize": 20,
    "MaxPageSize": 100,
    "PurchaseHistoryDefaultDays": 30
  }
}
```

### 环境特定配置

#### 开发环境 (appsettings.Development.json)

```json
{
  "Shop": {
    "EnableCaching": false,           // 禁用缓存便于调试
    "ShopDefinitionCacheMinutes": 1,  // 短缓存时间
    "ShopItemsCacheMinutes": 1,       // 短缓存时间
    "DefaultPageSize": 10,            // 小分页便于测试
    "MaxPageSize": 50                 // 较小的最大值
  },
  "Logging": {
    "LogLevel": {
      "BlazorIdle.Server.Application.Shop": "Debug",
      "BlazorIdle.Server.Domain.Shop": "Debug"
    }
  }
}
```

#### 生产环境 (appsettings.Production.json)

```json
{
  "Shop": {
    "EnableCaching": true,            // 启用缓存提升性能
    "ShopDefinitionCacheMinutes": 60, // 长缓存时间
    "ShopItemsCacheMinutes": 30,      // 长缓存时间
    "DefaultPageSize": 20,            // 标准分页
    "MaxPageSize": 100                // 标准最大值
  },
  "Logging": {
    "LogLevel": {
      "BlazorIdle.Server.Application.Shop": "Information",
      "BlazorIdle.Server.Domain.Shop": "Warning"
    }
  }
}
```

### 配置参数说明

| 参数 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| **缓存配置** ||||
| EnableCaching | bool | true | 是否启用缓存 |
| ShopDefinitionCacheMinutes | int | 60 | 商店定义缓存时间（分钟） |
| ShopItemsCacheMinutes | int | 30 | 商品列表缓存时间（分钟） |
| **文件路径配置** ||||
| ConfigPath | string | "Config/Shop" | 配置文件目录 |
| ShopDefinitionsFile | string | "ShopDefinitions.json" | 商店定义文件名 |
| ShopItemsFile | string | "ShopItems.json" | 商品配置文件名 |
| **商店配置** ||||
| DefaultRefreshIntervalSeconds | int | 3600 | 默认刷新间隔（秒） |
| MaxShopNameLength | int | 50 | 商店名称最大长度 |
| MaxShopDescriptionLength | int | 200 | 商店描述最大长度 |
| **商品配置** ||||
| MaxItemNameLength | int | 100 | 商品名称最大长度 |
| MaxItemDescriptionLength | int | 500 | 商品描述最大长度 |
| UnlimitedStock | int | -1 | 无限库存标识 |
| **购买限制配置** ||||
| DailyResetSeconds | int | 86400 | 每日重置周期（秒） |
| WeeklyResetSeconds | int | 604800 | 每周重置周期（秒） |
| DefaultDailyLimit | int | 10 | 默认每日限购 |
| DefaultWeeklyLimit | int | 5 | 默认每周限购 |
| **价格配置** ||||
| MinPriceAmount | int | 1 | 最小价格 |
| MaxPriceAmount | int | 1000000 | 最大价格 |
| **验证配置** ||||
| MinLevelRequirement | int | 1 | 最小等级要求 |
| MaxLevelRequirement | int | 100 | 最大等级要求 |
| MinPurchaseQuantity | int | 1 | 最小购买数量 |
| MaxPurchaseQuantity | int | 999 | 最大购买数量 |
| **查询配置** ||||
| DefaultPageSize | int | 20 | 默认分页大小 |
| MaxPageSize | int | 100 | 最大分页大小 |
| PurchaseHistoryDefaultDays | int | 30 | 购买历史默认天数 |

---

## API 使用

### 1. 获取商店列表

```http
GET /api/shop/list?characterId={characterId}
```

**响应示例**:
```json
{
  "shops": [
    {
      "id": "general_shop",
      "name": "杂货铺",
      "description": "售卖基础消耗品",
      "type": "General",
      "itemCount": 5,
      "isEnabled": true
    }
  ]
}
```

### 2. 获取商品列表

```http
GET /api/shop/{shopId}/items?characterId={characterId}&page=1&pageSize=20
```

**响应示例**:
```json
{
  "shopId": "general_shop",
  "items": [
    {
      "id": "item_health_potion_small",
      "name": "小型生命药水",
      "description": "恢复 50 点生命值",
      "price": {
        "currencyType": "Gold",
        "currencyId": null,
        "amount": 10
      },
      "minLevel": 1,
      "stockQuantity": 100,
      "isEnabled": true,
      "purchasedCount": 2,
      "remainingPurchases": 8
    }
  ],
  "totalCount": 15,
  "page": 1,
  "pageSize": 20
}
```

### 3. 购买商品

```http
POST /api/shop/purchase
Content-Type: application/json

{
  "characterId": "char-001",
  "shopItemId": "item_health_potion_small",
  "quantity": 1
}
```

**成功响应**:
```json
{
  "success": true,
  "purchaseRecord": {
    "id": "purchase-001",
    "shopItemId": "item_health_potion_small",
    "quantity": 1,
    "price": {
      "currencyType": "Gold",
      "amount": 10
    },
    "purchasedAt": "2025-10-13T03:00:00Z"
  }
}
```

### 4. 获取购买历史

```http
GET /api/shop/history?characterId={characterId}&page=1&pageSize=20
```

### 5. 高级过滤

```http
GET /api/shop/{shopId}/items/filter?characterId={characterId}&category=Consumable&rarity=Common&minPrice=10&maxPrice=100&sortBy=Price&sortDescending=false
```

---

## 数据模型

### 商店定义 (ShopDefinition)

```csharp
public class ShopDefinition
{
    public string Id { get; set; }              // 商店ID
    public string Name { get; set; }            // 商店名称
    public string Description { get; set; }     // 商店描述
    public ShopType Type { get; set; }          // 商店类型
    public bool IsEnabled { get; set; }         // 是否启用
    public int SortOrder { get; set; }          // 排序顺序
    public string? UnlockCondition { get; set; } // 解锁条件 (JSON)
}
```

### 商品 (ShopItem)

```csharp
public class ShopItem
{
    public string Id { get; set; }              // 商品ID
    public string ShopId { get; set; }          // 所属商店ID
    public string Name { get; set; }            // 商品名称
    public string Description { get; set; }     // 商品描述
    public string ItemDefinitionId { get; set; }// 物品定义ID
    public string PriceJson { get; set; }       // 价格 (JSON)
    public int MinLevel { get; set; }           // 最低等级
    public int StockQuantity { get; set; }      // 库存数量
    public string? PurchaseLimitJson { get; set; } // 购买限制 (JSON)
    public bool IsEnabled { get; set; }         // 是否启用
}
```

### 价格 (Price)

```csharp
public class Price
{
    public CurrencyType CurrencyType { get; set; } // 货币类型
    public string? CurrencyId { get; set; }         // 货币ID (物品时使用)
    public int Amount { get; set; }                 // 金额/数量
}
```

### 购买限制 (PurchaseLimit)

```csharp
public class PurchaseLimit
{
    public LimitType Type { get; set; }         // 限制类型
    public int MaxPurchases { get; set; }       // 最大购买次数
    public int? CustomPeriodSeconds { get; set; } // 自定义周期 (秒)
}
```

---

## 运维指南

### 监控指标

建议监控以下关键指标：

1. **购买成功率**
   ```
   成功购买次数 / 总购买请求次数
   目标: > 95%
   ```

2. **API 响应时间**
   ```
   商店列表查询: < 100ms
   商品列表查询: < 150ms
   购买操作: < 200ms
   ```

3. **缓存命中率**
   ```
   缓存命中次数 / 总查询次数
   目标: > 70%
   ```

4. **数据库查询性能**
   - 查看慢查询日志
   - 确认索引使用情况

### 日志级别

| 环境 | 推荐级别 | 说明 |
|------|----------|------|
| 开发 | Debug | 显示详细调试信息 |
| 测试 | Information | 显示关键操作 |
| 生产 | Warning | 仅记录异常情况 |

### 缓存管理

**清除缓存**:
```csharp
// 通过重启服务清除所有缓存
// 或在代码中手动清除特定缓存
```

**禁用缓存** (调试时):
```json
{
  "Shop": {
    "EnableCaching": false
  }
}
```

### 数据备份

定期备份以下数据：

1. **数据库**
   ```bash
   # SQLite 备份
   cp gamedata.db gamedata.db.backup
   ```

2. **配置文件**
   ```bash
   tar -czf shop-config-backup.tar.gz Config/Shop/
   ```

### 性能优化建议

1. **启用缓存** (生产环境)
2. **合理设置缓存过期时间**
3. **定期清理过期的购买计数器**
4. **监控数据库索引使用情况**
5. **使用 CDN 缓存商品图片** (如有)

---

## 故障排除

### 问题 1: 购买失败 - 金币不足

**症状**: 返回错误 "金币不足"

**排查步骤**:
1. 检查角色金币余额
2. 检查商品价格配置
3. 查看购买历史确认是否重复扣款

**解决方案**:
- 确保角色有足够金币
- 检查价格配置是否合理

### 问题 2: 商品不显示

**症状**: 商品列表为空

**排查步骤**:
1. 检查商品 `IsEnabled` 字段
2. 检查角色等级是否满足 `MinLevel`
3. 查看配置文件是否正确加载

**解决方案**:
```json
// 确保商品启用
{
  "isEnabled": true,
  "minLevel": 1  // 或更低的等级
}
```

### 问题 3: 缓存未更新

**症状**: 修改配置后未生效

**排查步骤**:
1. 检查缓存是否启用
2. 查看缓存过期时间

**解决方案**:
- 重启服务清除缓存
- 或等待缓存自动过期
- 或开发环境禁用缓存

### 问题 4: 购买限制未生效

**症状**: 超过限购数量仍可购买

**排查步骤**:
1. 检查 `PurchaseLimit` 配置
2. 查看 `PurchaseCounters` 表数据
3. 检查周期重置逻辑

**解决方案**:
```json
{
  "purchaseLimit": {
    "type": "Daily",
    "maxPurchases": 10
  }
}
```

### 问题 5: 数据库性能下降

**症状**: 查询响应时间增加

**排查步骤**:
1. 检查数据库索引
2. 查看慢查询日志
3. 确认数据量

**解决方案**:
```bash
# 检查索引
dotnet ef migrations list

# 重建索引 (如需要)
dotnet ef database update
```

---

## 最佳实践

### 1. 配置管理

✅ **推荐**:
- 使用环境变量管理敏感配置
- 不同环境使用不同的配置文件
- 定期审查和更新配置参数

❌ **避免**:
- 在代码中硬编码配置值
- 在生产环境使用开发配置
- 修改配置后不测试

### 2. 商品定价

✅ **推荐**:
- 合理设置商品价格
- 定期评估游戏经济平衡
- 提供不同价位的商品

❌ **避免**:
- 价格过高或过低
- 频繁大幅调整价格
- 忽视玩家反馈

### 3. 购买限制

✅ **推荐**:
- 对稀有物品设置购买限制
- 合理设置限购周期
- 提供明确的限购提示

❌ **避免**:
- 所有商品都无限制
- 限制过于严格影响体验
- 限制规则不清晰

### 4. 库存管理

✅ **推荐**:
- 常用消耗品设置充足库存
- 稀有物品限量供应
- 定期补充库存

❌ **避免**:
- 频繁缺货
- 库存设置不合理
- 不监控库存状态

### 5. 性能优化

✅ **推荐**:
- 生产环境启用缓存
- 使用数据库索引
- 监控查询性能

❌ **避免**:
- 禁用所有缓存
- 忽视慢查询
- 不做性能测试

### 6. 日志和监控

✅ **推荐**:
- 记录关键业务操作
- 监控异常和错误
- 定期审查日志

❌ **避免**:
- 过度日志影响性能
- 不记录关键操作
- 忽视警告信息

---

## 相关文档

- [商店系统设计方案（上）](./商店系统设计方案（上）.md) - 系统架构和设计
- [商店系统设计方案（中）](./商店系统设计方案（中）.md) - 详细实现
- [商店系统设计方案（下）](./商店系统设计方案（下）.md) - 实施和测试
- [商店系统优化总结-Phase1-3完整报告](./商店系统优化总结-Phase1-3完整报告.md) - 实施记录
- [商店系统配置化总结](./商店系统配置化总结.md) - 配置化详解

---

## 技术支持

### 问题反馈

如遇到问题，请提供以下信息：
1. 错误信息或日志
2. 复现步骤
3. 环境信息（开发/测试/生产）
4. 相关配置

### 贡献指南

欢迎提交改进建议和 Pull Request。

---

**文档版本**: 1.0  
**最后更新**: 2025-10-13  
**维护团队**: BlazorIdle 开发团队
