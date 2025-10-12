namespace BlazorIdle.Shared.Models.Shop;

/// <summary>
/// 购买历史响应
/// </summary>
public class PurchaseHistoryResponse
{
    public List<PurchaseHistoryItemDto> History { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

public class PurchaseHistoryItemDto
{
    public Guid PurchaseId { get; set; }
    public string ShopId { get; set; } = "";
    public string ShopName { get; set; } = "";
    public string ItemDefinitionId { get; set; } = "";
    public string ItemName { get; set; } = "";
    public int Quantity { get; set; }
    public PriceDto Price { get; set; } = new();
    public DateTime PurchasedAt { get; set; }
}
