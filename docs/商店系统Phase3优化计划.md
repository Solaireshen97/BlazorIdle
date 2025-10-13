# 商店系统 Phase 3 优化计划

**项目**: BlazorIdle  
**创建日期**: 2025-10-13  
**状态**: 📋 计划中  
**负责人**: 开发团队

---

## 📋 执行摘要

基于已完成的 Phase 1（基础框架）和 Phase 2（配置化与库存集成）工作，Phase 3 将进一步优化商店系统，增强其功能性、性能和可维护性。

### 当前完成状态

✅ **Phase 1 完成**：
- 基础框架搭建完成
- 领域模型实现完毕
- 数据库迁移和种子数据就绪
- 核心 API 端点实现

✅ **Phase 2 完成**：
- 完全配置化（23 个配置参数）
- 库存系统集成
- 物品货币支持
- 52 个测试全部通过
- 事务完整性保证

### Phase 3 目标

本阶段将在稳固的基础上，增加以下优化：

1. **商店刷新机制** - 支持商店定时刷新和手动刷新
2. **促销系统基础** - 为未来的折扣和促销活动预留接口
3. **购买冷却系统** - 防止恶意刷购买请求
4. **声望/忠诚度基础** - 为商店声望系统打下基础
5. **缓存优化** - 改进缓存失效策略
6. **监控和指标** - 添加业务指标收集
7. **日志增强** - 完善业务日志记录

---

## 🎯 优化目标

| 优化领域 | 当前状态 | 目标状态 | 优先级 |
|---------|---------|---------|-------|
| 商店刷新 | 无刷新机制 | 支持定时和手动刷新 | 🔴 高 |
| 促销系统 | 无 | 基础架构就绪 | 🟡 中 |
| 购买冷却 | 无限制 | 防刷机制 | 🔴 高 |
| 声望系统 | 无 | 基础数据结构 | 🟢 低 |
| 缓存策略 | 基础缓存 | 智能失效 | 🟡 中 |
| 监控指标 | 无 | 关键指标收集 | 🟡 中 |
| 日志记录 | 基础日志 | 结构化业务日志 | 🟢 低 |

---

## 📦 详细优化方案

### 1. 商店刷新机制

#### 1.1 需求分析

商店需要支持以下刷新场景：
- **定时自动刷新**：每天固定时间刷新商店内容
- **手动刷新**：玩家使用货币/道具手动刷新
- **事件触发刷新**：特殊活动时刷新商店

#### 1.2 设计方案

**数据模型扩展**：

```csharp
// ShopDefinition 添加刷新配置
public class ShopDefinition
{
    // 现有字段...
    
    // 新增：刷新配置（JSON 存储）
    public string? RefreshConfig { get; set; }
    
    // 新增：上次刷新时间
    public DateTime? LastRefreshTime { get; set; }
}

// 刷新配置值对象
public class ShopRefreshConfig
{
    /// <summary>
    /// 刷新类型：None/Auto/Manual/Both
    /// </summary>
    public RefreshType Type { get; set; } = RefreshType.None;
    
    /// <summary>
    /// 自动刷新间隔（秒）
    /// </summary>
    public int AutoRefreshIntervalSeconds { get; set; }
    
    /// <summary>
    /// 手动刷新成本
    /// </summary>
    public Price? ManualRefreshCost { get; set; }
    
    /// <summary>
    /// 手动刷新冷却时间（秒）
    /// </summary>
    public int ManualRefreshCooldownSeconds { get; set; }
    
    /// <summary>
    /// 每日手动刷新次数限制
    /// </summary>
    public int DailyManualRefreshLimit { get; set; } = -1; // -1 表示无限制
}

public enum RefreshType
{
    None = 0,      // 不支持刷新
    Auto = 1,      // 仅自动刷新
    Manual = 2,    // 仅手动刷新
    Both = 3       // 支持自动和手动
}
```

**配置参数**（添加到 appsettings.json）：

```json
{
  "Shop": {
    // 现有配置...
    
    // 刷新配置（Phase 3 新增）
    "DefaultAutoRefreshSeconds": 86400,        // 默认 24 小时
    "ManualRefreshCooldownSeconds": 3600,      // 手动刷新冷却 1 小时
    "DefaultManualRefreshCost": 100,           // 默认手动刷新成本（金币）
    "MaxDailyManualRefreshes": 5               // 每日最多手动刷新次数
  }
}
```

**API 接口**：

```csharp
// 添加到 IShopService
public interface IShopService
{
    // 现有方法...
    
    /// <summary>
    /// 检查商店是否需要刷新
    /// </summary>
    Task<bool> ShouldRefreshShopAsync(string shopId);
    
    /// <summary>
    /// 手动刷新商店
    /// </summary>
    Task<RefreshShopResponse> ManualRefreshShopAsync(string shopId, string characterId);
    
    /// <summary>
    /// 获取商店刷新状态
    /// </summary>
    Task<ShopRefreshStatus> GetRefreshStatusAsync(string shopId, string characterId);
}
```

**实施步骤**：
1. 数据库迁移：添加 RefreshConfig 和 LastRefreshTime 字段
2. 实现 ShopRefreshConfig 值对象和序列化
3. 实现刷新逻辑和验证
4. 添加 API 端点
5. 编写单元测试和集成测试

**测试用例**（8 个）：
- 自动刷新时间检查
- 手动刷新成本验证
- 手动刷新冷却验证
- 每日刷新次数限制
- 并发刷新请求处理
- 刷新失败回滚
- 刷新历史记录
- 刷新通知事件

---

### 2. 购买冷却系统

#### 2.1 需求分析

防止恶意刷购买请求，需要实现：
- **全局购买冷却**：角色级别的购买操作冷却
- **商品级购买冷却**：特定商品的购买冷却
- **动态冷却调整**：根据商品价值动态调整冷却时间

#### 2.2 设计方案

**数据模型**：

```csharp
// 购买冷却记录
public class PurchaseCooldown
{
    public string Id { get; set; } = null!;
    public string CharacterId { get; set; } = null!;
    public string? ShopItemId { get; set; }  // null 表示全局冷却
    public DateTime CooldownUntil { get; set; }
    public DateTime CreatedAt { get; set; }
    
    public static string GenerateId(string characterId, string? shopItemId = null)
    {
        return shopItemId == null 
            ? $"{characterId}_global"
            : $"{characterId}_{shopItemId}";
    }
}
```

**配置参数**：

```json
{
  "Shop": {
    // 现有配置...
    
    // 购买冷却配置（Phase 3 新增）
    "EnablePurchaseCooldown": true,
    "GlobalPurchaseCooldownSeconds": 1,        // 全局购买冷却 1 秒
    "ItemPurchaseCooldownSeconds": 5,          // 单品购买冷却 5 秒
    "ExpensiveItemThreshold": 1000,            // 昂贵物品阈值
    "ExpensiveItemCooldownSeconds": 10         // 昂贵物品冷却 10 秒
  }
}
```

**验证逻辑**：

```csharp
// 添加到 PurchaseValidator
public async Task<(bool IsValid, string ErrorMessage)> ValidatePurchaseCooldownAsync(
    string characterId, 
    string shopItemId,
    int itemPrice)
{
    if (!_shopOptions.EnablePurchaseCooldown)
    {
        return (true, string.Empty);
    }
    
    // 检查全局冷却
    var globalCooldown = await _context.PurchaseCooldowns
        .FirstOrDefaultAsync(pc => pc.Id == PurchaseCooldown.GenerateId(characterId));
    
    if (globalCooldown != null && globalCooldown.CooldownUntil > DateTime.UtcNow)
    {
        var remainingSeconds = (globalCooldown.CooldownUntil - DateTime.UtcNow).TotalSeconds;
        return (false, $"购买冷却中，还需等待 {remainingSeconds:F1} 秒");
    }
    
    // 检查商品级冷却
    var itemCooldown = await _context.PurchaseCooldowns
        .FirstOrDefaultAsync(pc => pc.Id == PurchaseCooldown.GenerateId(characterId, shopItemId));
    
    if (itemCooldown != null && itemCooldown.CooldownUntil > DateTime.UtcNow)
    {
        var remainingSeconds = (itemCooldown.CooldownUntil - DateTime.UtcNow).TotalSeconds;
        return (false, $"该物品购买冷却中，还需等待 {remainingSeconds:F1} 秒");
    }
    
    return (true, string.Empty);
}
```

**实施步骤**：
1. 创建 PurchaseCooldown 实体和数据库表
2. 添加配置参数到 ShopOptions
3. 实现冷却验证逻辑
4. 在购买流程中集成冷却检查
5. 添加冷却清理定时任务（清理过期记录）
6. 编写测试

**测试用例**（6 个）：
- 全局冷却生效
- 商品级冷却生效
- 昂贵物品冷却延长
- 冷却过期后允许购买
- 并发购买冷却检查
- 冷却记录清理

---

### 3. 促销系统基础

#### 3.1 需求分析

为未来的促销活动预留基础架构：
- **折扣支持**：商品价格折扣
- **特价时段**：限时特价
- **组合优惠**：未来扩展

#### 3.2 设计方案

**数据模型**：

```csharp
// 促销配置值对象
public class PromotionConfig
{
    /// <summary>
    /// 折扣百分比 (0-100)，0 表示无折扣
    /// </summary>
    public int DiscountPercentage { get; set; } = 0;
    
    /// <summary>
    /// 促销开始时间
    /// </summary>
    public DateTime? StartTime { get; set; }
    
    /// <summary>
    /// 促销结束时间
    /// </summary>
    public DateTime? EndTime { get; set; }
    
    /// <summary>
    /// 促销标签（用于显示）
    /// </summary>
    public string? PromotionTag { get; set; }
    
    /// <summary>
    /// 是否当前活跃
    /// </summary>
    public bool IsActive()
    {
        var now = DateTime.UtcNow;
        return DiscountPercentage > 0
            && (StartTime == null || StartTime <= now)
            && (EndTime == null || EndTime >= now);
    }
    
    /// <summary>
    /// 计算折扣后价格
    /// </summary>
    public int CalculateDiscountedPrice(int originalPrice)
    {
        if (!IsActive()) return originalPrice;
        return (int)(originalPrice * (100 - DiscountPercentage) / 100.0);
    }
}

// ShopItem 添加促销配置
public class ShopItem
{
    // 现有字段...
    
    // 新增：促销配置（JSON 存储）
    public string? PromotionConfig { get; set; }
    
    // 辅助方法
    public PromotionConfig? GetPromotionConfig()
    {
        if (string.IsNullOrEmpty(PromotionConfig))
            return null;
            
        return JsonSerializer.Deserialize<PromotionConfig>(PromotionConfig);
    }
    
    public void SetPromotionConfig(PromotionConfig? config)
    {
        PromotionConfig = config == null 
            ? null 
            : JsonSerializer.Serialize(config);
    }
}
```

**配置参数**：

```json
{
  "Shop": {
    // 现有配置...
    
    // 促销配置（Phase 3 新增）
    "EnablePromotions": true,
    "MaxDiscountPercentage": 90,               // 最大折扣 90%
    "MinDiscountPercentage": 5,                // 最小折扣 5%
    "DefaultPromotionDurationHours": 24        // 默认促销持续时间
  }
}
```

**API 响应扩展**：

```csharp
// ShopItemDto 添加促销信息
public class ShopItemDto
{
    // 现有字段...
    
    // 新增：促销信息
    public bool HasPromotion { get; set; }
    public int DiscountPercentage { get; set; }
    public int OriginalPrice { get; set; }
    public int DiscountedPrice { get; set; }
    public string? PromotionTag { get; set; }
    public DateTime? PromotionEndTime { get; set; }
}
```

**实施步骤**：
1. 数据库迁移：添加 PromotionConfig 字段
2. 实现 PromotionConfig 值对象
3. 修改价格计算逻辑支持折扣
4. 更新 API 响应包含促销信息
5. 编写测试

**测试用例**（5 个）：
- 促销价格计算正确
- 促销时间范围验证
- 过期促销不生效
- 折扣百分比边界检查
- 无促销商品价格不变

---

### 4. 缓存优化

#### 4.1 当前问题

- 缓存失效策略过于简单
- 缺少细粒度的缓存控制
- 无缓存命中率监控

#### 4.2 优化方案

**缓存键策略优化**：

```csharp
public class ShopCacheService : IShopCacheService
{
    private const string ShopsKey = "Shops_All";
    private const string ShopItemsKeyPrefix = "ShopItems_";
    
    // 新增：细粒度缓存键
    private const string ShopKeyPrefix = "Shop_";
    private const string ItemKeyPrefix = "Item_";
    private const string PurchaseCounterKeyPrefix = "PurchaseCounter_";
    
    // 新增：缓存统计
    private int _cacheHits = 0;
    private int _cacheMisses = 0;
    
    /// <summary>
    /// 获取单个商店定义
    /// </summary>
    public async Task<ShopDefinition?> GetShopAsync(string shopId)
    {
        if (!_cachingEnabled)
        {
            Interlocked.Increment(ref _cacheMisses);
            return null;
        }
        
        var cacheKey = $"{ShopKeyPrefix}{shopId}";
        
        if (_cache.TryGetValue(cacheKey, out ShopDefinition? shop))
        {
            Interlocked.Increment(ref _cacheHits);
            _logger.LogDebug("Cache hit for shop {ShopId}", shopId);
            return shop;
        }
        
        Interlocked.Increment(ref _cacheMisses);
        _logger.LogDebug("Cache miss for shop {ShopId}", shopId);
        return null;
    }
    
    /// <summary>
    /// 智能缓存失效：仅失效相关缓存
    /// </summary>
    public void InvalidateShopCache(string shopId)
    {
        // 失效单个商店缓存
        _cache.Remove($"{ShopKeyPrefix}{shopId}");
        
        // 失效该商店的商品缓存
        _cache.Remove($"{ShopItemsKeyPrefix}{shopId}");
        
        // 失效商店列表缓存
        _cache.Remove(ShopsKey);
        
        _logger.LogInformation("Invalidated cache for shop {ShopId}", shopId);
    }
    
    /// <summary>
    /// 获取缓存统计信息
    /// </summary>
    public CacheStatistics GetStatistics()
    {
        var hits = Interlocked.CompareExchange(ref _cacheHits, 0, 0);
        var misses = Interlocked.CompareExchange(ref _cacheMisses, 0, 0);
        var total = hits + misses;
        
        return new CacheStatistics
        {
            Hits = hits,
            Misses = misses,
            Total = total,
            HitRate = total > 0 ? (double)hits / total : 0
        };
    }
}

public class CacheStatistics
{
    public int Hits { get; set; }
    public int Misses { get; set; }
    public int Total { get; set; }
    public double HitRate { get; set; }
}
```

**实施步骤**：
1. 添加细粒度缓存方法
2. 实现智能失效策略
3. 添加缓存统计功能
4. 添加缓存监控端点
5. 编写测试

**测试用例**（4 个）：
- 细粒度缓存正确工作
- 智能失效不影响无关缓存
- 缓存统计准确
- 并发访问统计准确

---

### 5. 监控和指标

#### 5.1 业务指标

需要收集的关键指标：

```csharp
// 商店业务指标
public class ShopMetrics
{
    // 购买相关
    public long TotalPurchases { get; set; }
    public long SuccessfulPurchases { get; set; }
    public long FailedPurchases { get; set; }
    
    // 收入相关
    public long TotalGoldSpent { get; set; }
    public long TotalItemsTraded { get; set; }
    
    // 热门商品
    public Dictionary<string, int> TopSellingItems { get; set; } = new();
    
    // 刷新相关
    public long ManualRefreshCount { get; set; }
    public long AutoRefreshCount { get; set; }
    
    // 缓存相关
    public CacheStatistics CacheStats { get; set; } = new();
    
    // 性能相关
    public double AveragePurchaseTimeMs { get; set; }
    public double AverageQueryTimeMs { get; set; }
}
```

**实施步骤**：
1. 创建 ShopMetrics 类
2. 在关键操作点埋点收集指标
3. 实现指标聚合和持久化
4. 添加指标查询 API
5. 编写测试

---

### 6. 日志增强

#### 6.1 结构化日志

使用结构化日志记录关键业务事件：

```csharp
// 购买事件日志
_logger.LogInformation(
    "Purchase completed: CharacterId={CharacterId}, ShopId={ShopId}, ItemId={ItemId}, " +
    "Quantity={Quantity}, TotalPrice={TotalPrice}, CurrencyType={CurrencyType}, " +
    "PurchaseId={PurchaseId}, Timestamp={Timestamp}",
    characterId, shopId, shopItemId, quantity, totalPrice, currencyType, 
    purchaseRecord.Id, DateTime.UtcNow);

// 刷新事件日志
_logger.LogInformation(
    "Shop refreshed: ShopId={ShopId}, RefreshType={RefreshType}, " +
    "Cost={Cost}, CharacterId={CharacterId}, Timestamp={Timestamp}",
    shopId, refreshType, cost, characterId, DateTime.UtcNow);

// 冷却触发日志
_logger.LogWarning(
    "Purchase cooldown triggered: CharacterId={CharacterId}, ItemId={ItemId}, " +
    "RemainingSeconds={RemainingSeconds}, Timestamp={Timestamp}",
    characterId, itemId, remainingSeconds, DateTime.UtcNow);
```

**实施步骤**：
1. 识别关键业务事件
2. 添加结构化日志
3. 配置日志级别和输出
4. 验证日志可查询性

---

## 📅 实施计划

### 时间安排

| 任务 | 工作量 | 开始日期 | 结束日期 | 负责人 |
|-----|--------|---------|---------|--------|
| 商店刷新机制 | 3 天 | 待定 | 待定 | 待分配 |
| 购买冷却系统 | 2 天 | 待定 | 待定 | 待分配 |
| 促销系统基础 | 2 天 | 待定 | 待定 | 待分配 |
| 缓存优化 | 1.5 天 | 待定 | 待定 | 待分配 |
| 监控和指标 | 1.5 天 | 待定 | 待定 | 待分配 |
| 日志增强 | 1 天 | 待定 | 待定 | 待分配 |
| **总计** | **11 天** | - | - | - |

### 里程碑

- **M1**: 刷新机制和冷却系统完成（5 天）
- **M2**: 促销基础和缓存优化完成（3.5 天）
- **M3**: 监控和日志完善（2.5 天）

---

## 🧪 测试策略

### 测试覆盖目标

| 测试类型 | 数量 | 说明 |
|---------|-----|------|
| 单元测试 | 30+ | 覆盖新增业务逻辑 |
| 集成测试 | 15+ | 覆盖端到端流程 |
| 性能测试 | 5+ | 验证性能指标 |

### 关键测试场景

**刷新机制**（8 个测试）：
- 自动刷新触发
- 手动刷新成功
- 手动刷新冷却
- 刷新成本验证
- 刷新次数限制
- 并发刷新处理
- 刷新失败回滚
- 刷新历史记录

**购买冷却**（6 个测试）：
- 全局冷却触发
- 商品冷却触发
- 昂贵物品冷却延长
- 冷却过期允许购买
- 并发购买冷却检查
- 冷却记录清理

**促销系统**（5 个测试）：
- 促销价格计算
- 促销时间验证
- 过期促销失效
- 折扣百分比限制
- 无促销价格不变

**缓存优化**（4 个测试）：
- 细粒度缓存工作
- 智能失效策略
- 缓存统计准确
- 并发统计准确

---

## 📊 验收标准

### 功能验收

- [ ] 商店刷新机制完整可用
- [ ] 购买冷却系统有效防刷
- [ ] 促销价格计算准确
- [ ] 缓存命中率显著提升
- [ ] 监控指标正确收集
- [ ] 日志结构化完整

### 质量验收

- [ ] 单元测试覆盖率 ≥ 85%
- [ ] 所有集成测试通过
- [ ] 性能测试达标
- [ ] 无 P0/P1 Bug
- [ ] 代码审查通过

### 性能验收

- [ ] 购买操作响应时间 < 200ms（P95）
- [ ] 商店列表查询 < 100ms（P95）
- [ ] 缓存命中率 > 80%
- [ ] 并发购买处理正确

---

## 🔒 向后兼容性

所有 Phase 3 优化都遵循向后兼容原则：

✅ **默认禁用新功能**：
- 刷新机制：默认 RefreshType.None
- 购买冷却：配置可关闭
- 促销系统：默认无促销

✅ **可选字段**：
- 所有新增数据库字段都是可选的
- 现有数据不受影响

✅ **配置向后兼容**：
- 所有新配置参数都有默认值
- 不添加配置也能正常运行

---

## 🎓 最佳实践

### 1. 渐进式启用

建议按以下顺序启用新功能：
1. 先启用日志和监控（无风险）
2. 再启用缓存优化（性能提升）
3. 然后启用购买冷却（防刷）
4. 最后启用刷新和促销（需要配置）

### 2. 监控先行

- 先部署监控和日志
- 观察系统行为
- 根据数据调整参数

### 3. 测试充分

- 每个功能独立测试
- 组合功能集成测试
- 压力测试验证性能

---

## 📝 后续 Phase 4+ 展望

Phase 3 完成后，可以考虑的高级功能：

### Phase 4: 高级功能
- **声望系统完整实现**：商店声望等级和奖励
- **组合优惠**：购买套餐和批量折扣
- **限时商店**：活动商店和节日商店
- **VIP 系统**：会员价格和专属商品

### Phase 5: 社交功能
- **玩家商店**：玩家间交易
- **拍卖行**：物品拍卖系统
- **交易税**：经济调控机制

### Phase 6: 高级分析
- **推荐系统**：基于购买历史推荐商品
- **动态定价**：根据市场供需调整价格
- **经济报表**：详细的经济数据分析

---

## 📞 相关文档

- [商店系统配置化总结](./商店系统配置化总结.md)
- [商店系统优化完成总结](./商店系统优化完成总结.md)
- [商店系统 Phase 2 - 完全配置化改进报告](./商店系统Phase2-完全配置化改进报告.md)
- [商店系统设计方案（上）](./商店系统设计方案（上）.md)
- [商店系统设计方案（中）](./商店系统设计方案（中）.md)
- [商店系统设计方案（下）](./商店系统设计方案（下）.md)

---

**文档状态**: 📋 计划中  
**下一步**: 评审计划并启动实施

**创建者**: 开发团队  
**最后更新**: 2025-10-13
