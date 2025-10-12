namespace BlazorIdle.Shared.Models.Shop;

/// <summary>
/// 商店列表响应
/// </summary>
public class ListShopsResponse
{
    public List<ShopDto> Shops { get; set; } = new();
}
