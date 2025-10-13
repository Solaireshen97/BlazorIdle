namespace BlazorIdle.Shared.Models.Shop;

/// <summary>
/// 商店 DTO
/// </summary>
public class ShopDto
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
    public string Icon { get; set; } = "";
    public string Description { get; set; } = "";
    public bool IsEnabled { get; set; }
    public int ItemCount { get; set; }
    public bool IsUnlocked { get; set; } = true;
}

/// <summary>
/// 商品 DTO
/// </summary>
public class ShopItemDto
{
    public string Id { get; set; } = "";
    public string ShopId { get; set; } = "";
    public string ItemDefinitionId { get; set; } = "";
    public string ItemName { get; set; } = "";
    public string ItemIcon { get; set; } = "";
    public PriceDto Price { get; set; } = new();
    public PurchaseLimitDto PurchaseLimit { get; set; } = new();
    public int StockQuantity { get; set; }
    public int MinLevel { get; set; }
    public string? ItemCategory { get; set; }
    public string? Rarity { get; set; }
    public bool IsEnabled { get; set; }
    public int CurrentPurchaseCount { get; set; } = 0;
    public bool CanPurchase { get; set; } = true;
    public string? PurchaseBlockReason { get; set; }
}

/// <summary>
/// 价格 DTO
/// </summary>
public class PriceDto
{
    public string CurrencyType { get; set; } = "Gold";
    public string? CurrencyId { get; set; }
    public int Amount { get; set; }
}

/// <summary>
/// 购买限制 DTO
/// </summary>
public class PurchaseLimitDto
{
    public string Type { get; set; } = "Unlimited";
    public int MaxPurchases { get; set; }
    public int? ResetPeriodSeconds { get; set; }
}

/// <summary>
/// 购买请求
/// </summary>
public class PurchaseRequest
{
    public string ShopItemId { get; set; } = "";
    public int Quantity { get; set; } = 1;
}

/// <summary>
/// 购买响应
/// </summary>
public class PurchaseResponse
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public PurchaseRecordDto? Record { get; set; }
    
    /// <summary>
    /// 错误码（Phase 6.2 新增）
    /// </summary>
    public ShopErrorCode ErrorCode { get; set; } = ShopErrorCode.None;
    
    /// <summary>
    /// 错误详情（Phase 6.2 新增）
    /// </summary>
    public ShopErrorResponse? Error { get; set; }
}

/// <summary>
/// 购买记录 DTO
/// </summary>
public class PurchaseRecordDto
{
    public string Id { get; set; } = "";
    public string CharacterId { get; set; } = "";
    public string ShopId { get; set; } = "";
    public string ShopItemId { get; set; } = "";
    public string ItemDefinitionId { get; set; } = "";
    public int Quantity { get; set; }
    public PriceDto Price { get; set; } = new();
    public DateTime PurchasedAt { get; set; }
}

/// <summary>
/// 商店列表响应
/// </summary>
public class ListShopsResponse
{
    public List<ShopDto> Shops { get; set; } = new();
}

/// <summary>
/// 商品列表响应
/// </summary>
public class ListShopItemsResponse
{
    public List<ShopItemDto> Items { get; set; } = new();
}

/// <summary>
/// 购买历史响应
/// </summary>
public class PurchaseHistoryResponse
{
    public List<PurchaseRecordDto> Records { get; set; } = new();
    public int TotalCount { get; set; }
}

/// <summary>
/// 商品过滤请求
/// </summary>
public class ShopItemFilterRequest
{
    /// <summary>
    /// 商店ID（必填）
    /// </summary>
    public string ShopId { get; set; } = "";

    /// <summary>
    /// 物品类别过滤（可选）
    /// </summary>
    public string? ItemCategory { get; set; }

    /// <summary>
    /// 稀有度过滤（可选）
    /// </summary>
    public string? Rarity { get; set; }

    /// <summary>
    /// 最小价格（可选）
    /// </summary>
    public int? MinPrice { get; set; }

    /// <summary>
    /// 最大价格（可选）
    /// </summary>
    public int? MaxPrice { get; set; }

    /// <summary>
    /// 最小等级要求（可选）
    /// </summary>
    public int? MinLevel { get; set; }

    /// <summary>
    /// 最大等级要求（可选）
    /// </summary>
    public int? MaxLevel { get; set; }

    /// <summary>
    /// 排序字段："Price", "Level", "Name", "Rarity"
    /// </summary>
    public string? SortBy { get; set; }

    /// <summary>
    /// 排序方向："Asc", "Desc"
    /// </summary>
    public string? SortDirection { get; set; } = "Asc";
}
