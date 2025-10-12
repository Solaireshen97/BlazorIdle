namespace BlazorIdle.Shared.Models.Shop;

/// <summary>
/// 商店DTO
/// </summary>
public record ShopDto(
    string Id,
    string Name,
    string Type,
    string Icon,
    string Description,
    bool IsEnabled,
    bool IsUnlocked,
    int ItemCount
);

/// <summary>
/// 商品DTO
/// </summary>
public record ShopItemDto(
    Guid Id,
    string ShopId,
    string ItemType,
    string ItemDefinitionId,
    string DisplayName,
    string Icon,
    string Description,
    PriceDto Price,
    PurchaseLimitDto? PurchaseLimit,
    int RequiredLevel,
    bool IsEnabled,
    bool IsUnlocked,
    bool HasStock,
    int? CurrentStock,
    int? RemainingPurchases
);

/// <summary>
/// 价格DTO
/// </summary>
public record PriceDto(
    string CurrencyType,
    int Amount,
    string? ItemId,
    int ItemQuantity
);

/// <summary>
/// 购买限制DTO
/// </summary>
public record PurchaseLimitDto(
    string LimitType,
    int MaxPurchases,
    int? ResetPeriodSeconds
);

/// <summary>
/// 购买请求
/// </summary>
public record PurchaseRequest(
    Guid CharacterId,
    Guid ShopItemId,
    int Quantity = 1
);

/// <summary>
/// 购买响应
/// </summary>
public record PurchaseResponse(
    bool Success,
    string Message,
    Guid? PurchaseRecordId,
    int? RemainingGold,
    int? RemainingStock,
    int? RemainingPurchases
);

/// <summary>
/// 商店列表响应
/// </summary>
public record ListShopsResponse(
    List<ShopDto> Shops
);

/// <summary>
/// 商品列表响应
/// </summary>
public record ListShopItemsResponse(
    string ShopId,
    string ShopName,
    List<ShopItemDto> Items
);

/// <summary>
/// 购买历史记录DTO
/// </summary>
public record PurchaseHistoryDto(
    Guid Id,
    string ShopId,
    string ShopName,
    string ItemDefinitionId,
    string ItemName,
    int Quantity,
    int GoldPaid,
    string? ItemPaidId,
    int ItemPaidQuantity,
    DateTime PurchasedAt
);

/// <summary>
/// 购买历史响应
/// </summary>
public record PurchaseHistoryResponse(
    List<PurchaseHistoryDto> Records,
    int TotalCount,
    int PageNumber,
    int PageSize
);
