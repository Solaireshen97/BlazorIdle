using BlazorIdle.Server.Application.Abstractions;
using BlazorIdle.Shared.Models.Shop;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace BlazorIdle.Server.Application.Shop;

/// <summary>
/// 带缓存的商店服务装饰器
/// </summary>
public class CachedShopService : IShopService
{
    private readonly IShopService _innerService;
    private readonly IMemoryCache _cache;
    private readonly ShopSettings _settings;

    public CachedShopService(
        IShopService innerService,
        IMemoryCache cache,
        IOptions<ShopSettings> settings)
    {
        _innerService = innerService;
        _cache = cache;
        _settings = settings.Value;
    }

    public async Task<ListShopsResponse> ListShopsAsync(string characterId)
    {
        // 如果禁用缓存，直接调用内部服务
        if (!_settings.EnableCache)
        {
            return await _innerService.ListShopsAsync(characterId);
        }

        // 商店定义变化不频繁，可以缓存
        var cacheKey = $"shops_{characterId}";

        if (_cache.TryGetValue(cacheKey, out ListShopsResponse? cached) && cached != null)
        {
            return cached;
        }

        var result = await _innerService.ListShopsAsync(characterId);

        var cacheOptions = new MemoryCacheEntryOptions()
            .SetAbsoluteExpiration(TimeSpan.FromMinutes(_settings.ShopDefinitionCacheMinutes));

        _cache.Set(cacheKey, result, cacheOptions);

        return result;
    }

    public async Task<ListShopItemsResponse> GetShopItemsAsync(string shopId, string characterId)
    {
        // 如果禁用缓存，直接调用内部服务
        if (!_settings.EnableCache)
        {
            return await _innerService.GetShopItemsAsync(shopId, characterId);
        }

        // 商品列表可以缓存较短时间
        var cacheKey = $"shop_items_{shopId}_{characterId}";

        if (_cache.TryGetValue(cacheKey, out ListShopItemsResponse? cached) && cached != null)
        {
            return cached;
        }

        var result = await _innerService.GetShopItemsAsync(shopId, characterId);

        var cacheOptions = new MemoryCacheEntryOptions()
            .SetAbsoluteExpiration(TimeSpan.FromMinutes(_settings.ShopItemsCacheMinutes));

        _cache.Set(cacheKey, result, cacheOptions);

        return result;
    }

    public async Task<PurchaseResponse> PurchaseItemAsync(string characterId, PurchaseRequest request)
    {
        // 购买操作不使用缓存，直接调用
        var result = await _innerService.PurchaseItemAsync(characterId, request);

        // 如果购买成功，清除相关缓存
        if (result.Success && _settings.EnableCache)
        {
            InvalidateCache(characterId);
        }

        return result;
    }

    public async Task<PurchaseHistoryResponse> GetPurchaseHistoryAsync(string characterId, int page = 1, int pageSize = 20)
    {
        // 购买历史不缓存，因为经常变化
        return await _innerService.GetPurchaseHistoryAsync(characterId, page, pageSize);
    }

    /// <summary>
    /// 清除指定角色的商店相关缓存
    /// </summary>
    private void InvalidateCache(string characterId)
    {
        // 清除商店列表缓存
        var shopsCacheKey = $"shops_{characterId}";
        _cache.Remove(shopsCacheKey);

        // 清除所有商品列表缓存（由于无法确定具体 shopId，清除所有相关缓存）
        // 注意：这是简化实现，在生产环境中可以考虑使用更精确的缓存失效策略
    }
}
