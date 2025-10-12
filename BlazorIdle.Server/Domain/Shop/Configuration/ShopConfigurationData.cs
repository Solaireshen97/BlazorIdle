using BlazorIdle.Server.Domain.Shop.ValueObjects;

namespace BlazorIdle.Server.Domain.Shop.Configuration;

/// <summary>
/// 商店配置数据容器
/// </summary>
public class ShopDefinitionsConfig
{
    public List<ShopDefinitionData> Shops { get; set; } = new();
}

/// <summary>
/// 商店定义数据
/// </summary>
public class ShopDefinitionData
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? UnlockCondition { get; set; }
    public bool IsEnabled { get; set; }
    public int SortOrder { get; set; }
}

/// <summary>
/// 商品配置数据容器
/// </summary>
public class ShopItemsConfig
{
    public List<ShopItemData> Items { get; set; } = new();
}

/// <summary>
/// 商品数据
/// </summary>
public class ShopItemData
{
    public string Id { get; set; } = string.Empty;
    public string ShopId { get; set; } = string.Empty;
    public string ItemDefinitionId { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public string ItemIcon { get; set; } = string.Empty;
    public PriceData Price { get; set; } = new();
    public PurchaseLimitData PurchaseLimit { get; set; } = new();
    public int StockQuantity { get; set; }
    public int MinLevel { get; set; }
    public bool IsEnabled { get; set; }
    public int SortOrder { get; set; }
}

/// <summary>
/// 价格数据
/// </summary>
public class PriceData
{
    public string CurrencyType { get; set; } = "Gold";
    public string? CurrencyId { get; set; }
    public int Amount { get; set; }
    
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
    public string Type { get; set; } = "Unlimited";
    public int? MaxPurchases { get; set; }
    public int? ResetPeriodSeconds { get; set; }
    
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
