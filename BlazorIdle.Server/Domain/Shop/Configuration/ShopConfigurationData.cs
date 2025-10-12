using BlazorIdle.Server.Domain.Shop.ValueObjects;

namespace BlazorIdle.Server.Domain.Shop.Configuration;

/// <summary>
/// 商店配置数据容器
/// </summary>
public class ShopDefinitionsConfig
{
    /// <summary>商店定义列表</summary>
    public List<ShopDefinitionData> Shops { get; set; } = new();
}

/// <summary>
/// 商店定义数据
/// </summary>
public class ShopDefinitionData
{
    /// <summary>商店唯一标识</summary>
    public string Id { get; set; } = string.Empty;
    
    /// <summary>商店名称</summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>商店类型（General/Special）</summary>
    public string Type { get; set; } = string.Empty;
    
    /// <summary>商店图标</summary>
    public string Icon { get; set; } = string.Empty;
    
    /// <summary>商店描述</summary>
    public string Description { get; set; } = string.Empty;
    
    /// <summary>解锁条件表达式</summary>
    public string? UnlockCondition { get; set; }
    
    /// <summary>是否启用</summary>
    public bool IsEnabled { get; set; }
    
    /// <summary>排序顺序</summary>
    public int SortOrder { get; set; }
}

/// <summary>
/// 商品配置数据容器
/// </summary>
public class ShopItemsConfig
{
    /// <summary>商品定义列表</summary>
    public List<ShopItemData> Items { get; set; } = new();
}

/// <summary>
/// 商品数据
/// </summary>
public class ShopItemData
{
    /// <summary>商品唯一标识</summary>
    public string Id { get; set; } = string.Empty;
    
    /// <summary>所属商店ID</summary>
    public string ShopId { get; set; } = string.Empty;
    
    /// <summary>物品定义ID（关联到库存系统）</summary>
    public string ItemDefinitionId { get; set; } = string.Empty;
    
    /// <summary>商品显示名称</summary>
    public string ItemName { get; set; } = string.Empty;
    
    /// <summary>商品图标</summary>
    public string ItemIcon { get; set; } = string.Empty;
    
    /// <summary>价格信息</summary>
    public PriceData Price { get; set; } = new();
    
    /// <summary>购买限制</summary>
    public PurchaseLimitData PurchaseLimit { get; set; } = new();
    
    /// <summary>库存数量（-1表示无限）</summary>
    public int StockQuantity { get; set; }
    
    /// <summary>最低等级要求</summary>
    public int MinLevel { get; set; }
    
    /// <summary>是否启用</summary>
    public bool IsEnabled { get; set; }
    
    /// <summary>排序顺序</summary>
    public int SortOrder { get; set; }
}

/// <summary>
/// 价格数据
/// </summary>
public class PriceData
{
    /// <summary>货币类型（Gold/Item）</summary>
    public string CurrencyType { get; set; } = "Gold";
    
    /// <summary>货币ID（用于物品货币交易）</summary>
    public string? CurrencyId { get; set; }
    
    /// <summary>价格金额</summary>
    public int Amount { get; set; }
    
    /// <summary>
    /// 转换为领域模型 Price 对象
    /// </summary>
    public Price ToPrice()
    {
        return new Price
        {
            CurrencyType = Enum.Parse<CurrencyType>(CurrencyType),
            CurrencyId = CurrencyId,
            Amount = Amount
        };
    }
}

/// <summary>
/// 购买限制数据
/// </summary>
public class PurchaseLimitData
{
    /// <summary>限制类型（Unlimited/Daily/Weekly/PerCharacter/CustomPeriod）</summary>
    public string Type { get; set; } = "Unlimited";
    
    /// <summary>最大购买次数</summary>
    public int? MaxPurchases { get; set; }
    
    /// <summary>自定义重置周期（秒）</summary>
    public int? ResetPeriodSeconds { get; set; }
    
    /// <summary>
    /// 转换为领域模型 PurchaseLimit 对象
    /// </summary>
    public PurchaseLimit ToPurchaseLimit()
    {
        return new PurchaseLimit
        {
            Type = Enum.Parse<LimitType>(Type),
            MaxPurchases = MaxPurchases ?? 0,
            ResetPeriodSeconds = ResetPeriodSeconds
        };
    }
}
