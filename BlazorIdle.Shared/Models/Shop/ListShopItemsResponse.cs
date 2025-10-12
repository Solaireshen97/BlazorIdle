namespace BlazorIdle.Shared.Models.Shop;

/// <summary>
/// 商品列表响应
/// </summary>
public class ListShopItemsResponse
{
    public string ShopId { get; set; } = "";
    public string ShopName { get; set; } = "";
    public List<ShopItemDto> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}
