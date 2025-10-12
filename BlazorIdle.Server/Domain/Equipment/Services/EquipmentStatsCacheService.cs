using BlazorIdle.Server.Domain.Equipment.Models;
using Microsoft.Extensions.Caching.Memory;

namespace BlazorIdle.Server.Domain.Equipment.Services;

/// <summary>
/// 装备属性缓存服务
/// 缓存角色装备属性，避免频繁重复计算
/// </summary>
public class EquipmentStatsCacheService
{
    private readonly IMemoryCache _cache;
    private const int CACHE_DURATION_MINUTES = 10;
    private const string CACHE_KEY_PREFIX = "equipment_stats_";

    public EquipmentStatsCacheService(IMemoryCache cache)
    {
        _cache = cache;
    }

    /// <summary>
    /// 获取或计算装备属性
    /// 优先从缓存获取，缓存未命中时调用计算函数并缓存结果
    /// </summary>
    /// <param name="characterId">角色ID</param>
    /// <param name="calculateFunc">计算函数</param>
    /// <returns>装备属性字典</returns>
    public async Task<Dictionary<StatType, double>> GetOrCalculateAsync(
        Guid characterId,
        Func<Task<Dictionary<StatType, double>>> calculateFunc)
    {
        var cacheKey = GetCacheKey(characterId);

        // 尝试从缓存获取
        if (_cache.TryGetValue(cacheKey, out Dictionary<StatType, double>? cachedStats) && cachedStats != null)
        {
            return cachedStats;
        }

        // 缓存未命中，执行计算
        var stats = await calculateFunc();

        // 缓存结果
        var cacheOptions = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(CACHE_DURATION_MINUTES),
            Priority = CacheItemPriority.Normal
        };

        _cache.Set(cacheKey, stats, cacheOptions);

        return stats;
    }

    /// <summary>
    /// 使角色装备属性缓存失效
    /// 在装备变更（装备/卸下）后调用
    /// </summary>
    /// <param name="characterId">角色ID</param>
    public void Invalidate(Guid characterId)
    {
        var cacheKey = GetCacheKey(characterId);
        _cache.Remove(cacheKey);
    }

    /// <summary>
    /// 批量使缓存失效
    /// </summary>
    /// <param name="characterIds">角色ID列表</param>
    public void InvalidateBatch(IEnumerable<Guid> characterIds)
    {
        foreach (var characterId in characterIds)
        {
            Invalidate(characterId);
        }
    }

    /// <summary>
    /// 清空所有装备属性缓存
    /// </summary>
    public void ClearAll()
    {
        // Note: IMemoryCache没有提供ClearAll方法
        // 这里只能通过重启应用或依赖过期时间来清理
        // 如果需要立即清空，需要维护一个缓存键列表
    }

    private string GetCacheKey(Guid characterId)
    {
        return $"{CACHE_KEY_PREFIX}{characterId}";
    }
}
