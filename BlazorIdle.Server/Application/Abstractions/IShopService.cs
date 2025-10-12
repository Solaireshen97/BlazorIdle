using BlazorIdle.Shared.Models.Shop;

namespace BlazorIdle.Server.Application.Abstractions;

/// <summary>
/// 商店服务接口
/// </summary>
public interface IShopService
{
    /// <summary>
    /// 获取商店列表
    /// </summary>
    Task<ListShopsResponse> ListShopsAsync(
        Guid characterId,
        bool includeDisabled,
        CancellationToken ct = default);

    /// <summary>
    /// 获取商店商品列表
    /// </summary>
    Task<ListShopItemsResponse> ListShopItemsAsync(
        string shopId,
        Guid characterId,
        int page,
        int pageSize,
        CancellationToken ct = default);

    /// <summary>
    /// 购买商品
    /// </summary>
    Task<PurchaseResponse> PurchaseItemAsync(
        PurchaseRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// 获取购买历史
    /// </summary>
    Task<PurchaseHistoryResponse> GetPurchaseHistoryAsync(
        Guid characterId,
        string? shopId,
        DateTime? startDate,
        DateTime? endDate,
        int page,
        int pageSize,
        CancellationToken ct = default);
}
