# 商店系统配置文档

本目录包含商店系统的JSON配置文件。这些文件定义了游戏中所有商店和商品的静态配置数据。

## 📁 文件说明

### ShopDefinitions.json
定义游戏中所有的商店。

**字段说明**:
- `id` (string): 商店的唯一标识符，用于内部引用
- `name` (string): 商店显示名称，最长50个字符
- `type` (string): 商店类型，可选值: "General"(普通), "Special"(特殊)
- `icon` (string): 商店图标，使用 emoji 或图标标识
- `description` (string): 商店描述，最长200个字符
- `unlockCondition` (string|null): 解锁条件表达式，如 "level>=10"，null表示无条件
- `isEnabled` (boolean): 商店是否启用
- `sortOrder` (int): 显示排序顺序，数字越小越靠前

**示例**:
```json
{
  "id": "general_shop",
  "name": "杂货铺",
  "type": "General",
  "icon": "🏪",
  "description": "出售各类日常消耗品和基础装备",
  "unlockCondition": null,
  "isEnabled": true,
  "sortOrder": 1
}
```

### ShopItems.json
定义所有商店中的商品。

**字段说明**:
- `id` (string): 商品的唯一标识符
- `shopId` (string): 所属商店ID，必须与 ShopDefinitions.json 中的商店ID匹配
- `itemDefinitionId` (string): 物品定义ID，用于关联游戏中的物品系统
- `itemName` (string): 商品显示名称，最长100个字符
- `itemIcon` (string): 商品图标
- `price` (object): 价格配置
  - `currencyType` (string): 货币类型，可选值: "Gold"(金币), "Item"(物品)
  - `amount` (int): 价格数量，范围: 1 到 1000000
  - `currencyId` (string, 可选): 当货币类型为 "Item" 时，指定物品ID
- `purchaseLimit` (object): 购买限制配置
  - `type` (string): 限制类型
    - "Unlimited": 无限制
    - "Daily": 每日限制
    - "Weekly": 每周限制
    - "PerCharacter": 每角色终身限制
  - `maxPurchases` (int, 可选): 最大购买次数（当type不为Unlimited时必须）
- `stockQuantity` (int): 库存数量，-1表示无限库存
- `minLevel` (int): 最低等级要求，范围: 1 到 100
- `itemCategory` (string): 物品类别，如 "Consumable"(消耗品), "Equipment"(装备), "Material"(材料), "Special"(特殊)
- `rarity` (string): 稀有度，如 "Common"(普通), "Uncommon"(非凡), "Rare"(稀有), "Epic"(史诗), "Legendary"(传说)
- `isEnabled` (boolean): 商品是否启用
- `sortOrder` (int): 在商店内的显示排序

**示例（金币购买）**:
```json
{
  "id": "general_shop_health_potion",
  "shopId": "general_shop",
  "itemDefinitionId": "health_potion_small",
  "itemName": "小型生命药水",
  "itemIcon": "🧪",
  "price": {
    "currencyType": "Gold",
    "amount": 50
  },
  "purchaseLimit": {
    "type": "Unlimited"
  },
  "stockQuantity": -1,
  "minLevel": 1,
  "itemCategory": "Consumable",
  "rarity": "Common",
  "isEnabled": true,
  "sortOrder": 1
}
```

**示例（物品兑换）**:
```json
{
  "id": "alchemist_shop_legendary_sword",
  "shopId": "alchemist_shop",
  "itemDefinitionId": "legendary_sword",
  "itemName": "传说之剑",
  "itemIcon": "⚔️",
  "price": {
    "currencyType": "Item",
    "amount": 100,
    "currencyId": "dragon_scale"
  },
  "purchaseLimit": {
    "type": "PerCharacter",
    "maxPurchases": 1
  },
  "stockQuantity": -1,
  "minLevel": 50,
  "itemCategory": "Equipment",
  "rarity": "Legendary",
  "isEnabled": true,
  "sortOrder": 1
}
```

## ⚙️ 配置参数（appsettings.json）

商店系统的运行参数在 `appsettings.json` 中的 `Shop` 节点配置：

```json
{
  "Shop": {
    // 缓存配置
    "EnableCaching": true,                    // 是否启用缓存
    "ShopDefinitionCacheMinutes": 60,         // 商店定义缓存时长（分钟）
    "ShopItemsCacheMinutes": 30,              // 商品列表缓存时长（分钟）
    
    // 文件路径
    "ConfigPath": "Config/Shop",              // 配置文件目录
    "ShopDefinitionsFile": "ShopDefinitions.json",
    "ShopItemsFile": "ShopItems.json",
    
    // 商店配置
    "DefaultRefreshIntervalSeconds": 3600,    // 默认刷新间隔（秒）
    "MaxShopNameLength": 50,                  // 商店名称最大长度
    "MaxShopDescriptionLength": 200,          // 商店描述最大长度
    
    // 商品配置
    "MaxItemNameLength": 100,                 // 商品名称最大长度
    "MaxItemDescriptionLength": 500,          // 商品描述最大长度
    "UnlimitedStock": -1,                     // 无限库存标识
    
    // 购买限制
    "DailyResetSeconds": 86400,               // 每日重置间隔（秒）
    "WeeklyResetSeconds": 604800,             // 每周重置间隔（秒）
    "DefaultDailyLimit": 10,                  // 默认每日限制
    "DefaultWeeklyLimit": 5,                  // 默认每周限制
    
    // 价格配置
    "MinPriceAmount": 1,                      // 最低价格
    "MaxPriceAmount": 1000000,                // 最高价格
    
    // 验证配置
    "MinLevelRequirement": 1,                 // 最低等级要求
    "MaxLevelRequirement": 100,               // 最高等级要求
    "MinPurchaseQuantity": 1,                 // 最小购买数量
    "MaxPurchaseQuantity": 999,               // 最大购买数量
    
    // 查询配置
    "DefaultPageSize": 20,                    // 默认分页大小
    "MaxPageSize": 100,                       // 最大分页大小
    "PurchaseHistoryDefaultDays": 30          // 购买历史默认天数
  }
}
```

## 🔧 配置验证

系统在启动时会自动验证所有配置参数的有效性：

### 缓存配置验证
- 缓存时长必须 ≥ 0 分钟

### 文件路径验证
- 所有文件路径必须非空

### 商店配置验证
- 刷新间隔必须 ≥ 60 秒
- 商店名称长度：1-500 字符
- 商店描述长度：1-2000 字符

### 商品配置验证
- 商品名称长度：1-500 字符
- 商品描述长度：1-5000 字符

### 购买限制验证
- 每日重置间隔 ≥ 3600 秒（1小时）
- 每周重置间隔 ≥ 86400 秒（1天）
- 默认限制数量必须 > 0

### 价格验证
- 最低价格 ≥ 0
- 最高价格 > 最低价格

### 等级验证
- 最低等级 ≥ 1
- 最高等级 > 最低等级

### 购买数量验证
- 最小数量 ≥ 1
- 最大数量 > 最小数量

### 查询验证
- 默认分页大小：1 到最大分页大小
- 最大分页大小：10-1000
- 购买历史天数 > 0

## 📝 配置最佳实践

### 1. 商店ID命名规范
推荐使用 `{shop_type}_{shop_name}` 格式：
- `general_shop` - 普通商店
- `weapon_shop` - 武器商店
- `alchemist_shop` - 炼金术士商店

### 2. 商品ID命名规范
推荐使用 `{shop_id}_{item_name}` 格式：
- `general_shop_health_potion` - 杂货铺的生命药水
- `weapon_shop_iron_sword` - 武器店的铁剑

### 3. 解锁条件语法
- 简单条件：`level>=10`
- 复杂条件：`level>=20 AND questCompleted:dragon_slayer`
- 无条件：`null`

### 4. 价格设置建议
- 日常消耗品：10-100 金币
- 基础装备：100-1000 金币
- 高级装备：1000-10000 金币
- 稀有物品：10000+ 金币或物品兑换

### 5. 购买限制建议
- 日常消耗品：`Unlimited`
- 强力药剂：`Daily` 或 `Weekly`
- 稀有材料：`Weekly` 或 `PerCharacter`
- 唯一物品：`PerCharacter` 且 `maxPurchases: 1`

### 6. 库存设置建议
- 普通商品：`-1`（无限库存）
- 限时商品：设置具体数量
- 稀有物品：较小的库存数量

## 🧪 测试配置

在修改配置后，建议执行以下测试：

1. **语法验证**: 使用 JSON validator 检查文件格式
2. **启动测试**: 启动应用确保配置加载成功
3. **功能测试**: 在游戏中验证商店显示和购买功能
4. **单元测试**: 运行 `dotnet test --filter "FullyQualifiedName~Shop"`

## 🔄 配置更新流程

1. **备份**: 修改前备份现有配置文件
2. **修改**: 按照规范修改配置
3. **验证**: 使用 JSON validator 验证格式
4. **测试**: 在开发环境测试
5. **部署**: 部署到生产环境
6. **监控**: 观察日志和性能指标

## 🚨 常见问题

### Q: 修改配置后需要重启服务器吗？
A: 是的，当前版本需要重启服务器才能加载新配置。未来可能会支持热重载。

### Q: 如何临时禁用某个商店或商品？
A: 将 `isEnabled` 字段设置为 `false` 即可。

### Q: 如何添加新的商店？
A: 在 `ShopDefinitions.json` 的 `shops` 数组中添加新对象，确保 `id` 唯一。

### Q: 如何添加新的商品？
A: 在 `ShopItems.json` 的 `items` 数组中添加新对象，确保 `id` 唯一且 `shopId` 匹配已存在的商店。

### Q: 如何设置物品兑换？
A: 将 `price.currencyType` 设置为 `"Item"`，并指定 `price.currencyId` 为所需物品的ID。

## 📚 相关文档

- [商店系统设计方案（上）](../../../docs/商店系统设计方案（上）.md)
- [商店系统设计方案（中）](../../../docs/商店系统设计方案（中）.md)
- [商店系统设计方案（下）](../../../docs/商店系统设计方案（下）.md)
- [商店系统优化总结](../../../docs/商店系统优化总结-Phase1-3完整报告.md)

## 📞 支持

如有问题或建议，请联系开发团队或提交 Issue。

---

**最后更新**: 2025-10-13  
**版本**: 1.0
