namespace BlazorIdle.Shared.Models.Shop;

/// <summary>
/// 商品 DTO
/// </summary>
public class ShopItemDto
{
    public Guid ItemId { get; set; }
    public string ItemType { get; set; } = "";
    public string ItemDefinitionId { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Icon { get; set; } = "";
    public string Description { get; set; } = "";
    public PriceDto Price { get; set; } = new();
    public int StockLimit { get; set; }
    public int CurrentStock { get; set; }
    public PurchaseLimitDto? PurchaseLimit { get; set; }
    public int RequiredLevel { get; set; }
    public bool CanPurchase { get; set; }
    public string? PurchaseBlockReason { get; set; }
    public int PurchasedCount { get; set; }
}

public class PriceDto
{
    public string CurrencyType { get; set; } = "Gold";
    public int Amount { get; set; }
    public string? CurrencyId { get; set; }
    public string DisplayText { get; set; } = "";
}

public class PurchaseLimitDto
{
    public string LimitType { get; set; } = "None";
    public int MaxPurchases { get; set; }
    public int RemainingPurchases { get; set; }
    public DateTime? ResetTime { get; set; }
}
