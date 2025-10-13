using BlazorIdle.Shared.Models.Shop;
using BlazorIdle.Client.Services;

namespace BlazorIdle.Services;

/// <summary>
/// 商店服务接口
/// </summary>
public interface IShopService
{
    Task<ListShopsResponse?> GetShopsAsync(string characterId);
    Task<ListShopItemsResponse?> GetShopItemsAsync(string shopId, string characterId);
    Task<PurchaseResponse?> PurchaseItemAsync(string characterId, PurchaseRequest request);
    Task<PurchaseHistoryResponse?> GetPurchaseHistoryAsync(string characterId, int page = 1, int pageSize = 20);
}

/// <summary>
/// 商店服务实现 - 简单包装 ApiClient
/// </summary>
public class ShopService : IShopService
{
    private readonly ApiClient _apiClient;

    public ShopService(ApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public Task<ListShopsResponse?> GetShopsAsync(string characterId)
    {
        return _apiClient.GetShopsAsync(characterId);
    }

    public Task<ListShopItemsResponse?> GetShopItemsAsync(string shopId, string characterId)
    {
        return _apiClient.GetShopItemsAsync(shopId, characterId);
    }

    public Task<PurchaseResponse?> PurchaseItemAsync(string characterId, PurchaseRequest request)
    {
        return _apiClient.PurchaseItemAsync(characterId, request);
    }

    public Task<PurchaseHistoryResponse?> GetPurchaseHistoryAsync(string characterId, int page = 1, int pageSize = 20)
    {
        return _apiClient.GetPurchaseHistoryAsync(characterId, page, pageSize);
    }
}
