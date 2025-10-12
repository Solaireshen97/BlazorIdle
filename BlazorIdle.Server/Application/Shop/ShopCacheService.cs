using BlazorIdle.Server.Domain.Shop;
using BlazorIdle.Server.Domain.Shop.Configuration;
using Microsoft.Extensions.Caching.Memory;

namespace BlazorIdle.Server.Application.Shop;

/// <summary>
/// 商店缓存服务接口
/// </summary>
public interface IShopCacheService
{
    /// <summary>
    /// 获取缓存的商店定义列表
    /// </summary>
    Task<List<ShopDefinition>?> GetShopsAsync();
    
    /// <summary>
    /// 设置商店定义缓存
    /// </summary>
    void SetShops(List<ShopDefinition> shops);
    
    /// <summary>
    /// 获取缓存的商店商品列表
    /// </summary>
    Task<List<ShopItem>?> GetShopItemsAsync(string shopId);
    
    /// <summary>
    /// 设置商店商品缓存
    /// </summary>
    void SetShopItems(string shopId, List<ShopItem> items);
    
    /// <summary>
    /// 清除所有商店缓存
    /// </summary>
    void ClearAllCache();
    
    /// <summary>
    /// 清除指定商店的商品缓存
    /// </summary>
    void ClearShopItemsCache(string shopId);
}

/// <summary>
/// 商店缓存服务实现
/// </summary>
public class ShopCacheService : IShopCacheService
{
    private readonly IMemoryCache _cache;
    private readonly ILogger<ShopCacheService> _logger;
    private readonly bool _cachingEnabled;
    private readonly TimeSpan _shopsCacheExpiration;
    private readonly TimeSpan _itemsCacheExpiration;
    
    private const string ShopsKey = "Shops_All";
    private const string ShopItemsKeyPrefix = "ShopItems_";

    public ShopCacheService(
        IMemoryCache cache,
        ILogger<ShopCacheService> logger,
        IConfiguration configuration)
    {
        _cache = cache;
        _logger = logger;
        
        // 从配置读取缓存设置
        _cachingEnabled = configuration.GetValue<bool>("Shop:EnableCaching", 
            ShopSystemConfig.CacheConfig.EnableCaching);
        _shopsCacheExpiration = TimeSpan.FromMinutes(
            configuration.GetValue<int>("Shop:ShopDefinitionCacheMinutes", 
                ShopSystemConfig.CacheConfig.ShopDefinitionCacheMinutes));
        _itemsCacheExpiration = TimeSpan.FromMinutes(
            configuration.GetValue<int>("Shop:ShopItemsCacheMinutes", 
                ShopSystemConfig.CacheConfig.ShopItemsCacheMinutes));
    }

    public Task<List<ShopDefinition>?> GetShopsAsync()
    {
        if (!_cachingEnabled)
        {
            return Task.FromResult<List<ShopDefinition>?>(null);
        }

        if (_cache.TryGetValue<List<ShopDefinition>>(ShopsKey, out var shops))
        {
            _logger.LogDebug("Shop definitions cache hit");
            return Task.FromResult<List<ShopDefinition>?>(shops);
        }

        _logger.LogDebug("Shop definitions cache miss");
        return Task.FromResult<List<ShopDefinition>?>(null);
    }

    public void SetShops(List<ShopDefinition> shops)
    {
        if (!_cachingEnabled)
        {
            return;
        }

        var options = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = _shopsCacheExpiration,
            Priority = CacheItemPriority.Normal
        };

        _cache.Set(ShopsKey, shops, options);
        _logger.LogInformation("Cached {Count} shop definitions for {Minutes} minutes", 
            shops.Count, _shopsCacheExpiration.TotalMinutes);
    }

    public Task<List<ShopItem>?> GetShopItemsAsync(string shopId)
    {
        if (!_cachingEnabled)
        {
            return Task.FromResult<List<ShopItem>?>(null);
        }

        var key = $"{ShopItemsKeyPrefix}{shopId}";
        if (_cache.TryGetValue<List<ShopItem>>(key, out var items))
        {
            _logger.LogDebug("Shop items cache hit for shop: {ShopId}", shopId);
            return Task.FromResult<List<ShopItem>?>(items);
        }

        _logger.LogDebug("Shop items cache miss for shop: {ShopId}", shopId);
        return Task.FromResult<List<ShopItem>?>(null);
    }

    public void SetShopItems(string shopId, List<ShopItem> items)
    {
        if (!_cachingEnabled)
        {
            return;
        }

        var key = $"{ShopItemsKeyPrefix}{shopId}";
        var options = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = _itemsCacheExpiration,
            Priority = CacheItemPriority.Normal
        };

        _cache.Set(key, items, options);
        _logger.LogInformation("Cached {Count} shop items for shop {ShopId} for {Minutes} minutes", 
            items.Count, shopId, _itemsCacheExpiration.TotalMinutes);
    }

    public void ClearAllCache()
    {
        _cache.Remove(ShopsKey);
        _logger.LogInformation("Cleared all shop caches");
    }

    public void ClearShopItemsCache(string shopId)
    {
        var key = $"{ShopItemsKeyPrefix}{shopId}";
        _cache.Remove(key);
        _logger.LogInformation("Cleared shop items cache for shop: {ShopId}", shopId);
    }
}
