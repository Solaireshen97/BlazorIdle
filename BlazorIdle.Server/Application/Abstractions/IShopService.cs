using BlazorIdle.Shared.Models.Shop;

namespace BlazorIdle.Server.Application.Abstractions;

/// <summary>
/// 商店服务接口
/// </summary>
public interface IShopService
{
    /// <summary>
    /// 获取所有可用商店列表
    /// </summary>
    Task<ListShopsResponse> ListShopsAsync(string characterId);

    /// <summary>
    /// 获取指定商店的商品列表
    /// </summary>
    Task<ListShopItemsResponse> GetShopItemsAsync(string shopId, string characterId);

    /// <summary>
    /// 获取指定商店的商品列表（带过滤和排序）
    /// </summary>
    Task<ListShopItemsResponse> GetShopItemsWithFilterAsync(string characterId, ShopItemFilterRequest filter);

    /// <summary>
    /// 购买商品
    /// </summary>
    Task<PurchaseResponse> PurchaseItemAsync(string characterId, PurchaseRequest request);

    /// <summary>
    /// 获取购买历史
    /// </summary>
    Task<PurchaseHistoryResponse> GetPurchaseHistoryAsync(string characterId, int page = 1, int pageSize = 20);
}
