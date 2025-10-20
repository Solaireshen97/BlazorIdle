using BlazorIdle.Server.Infrastructure.DatabaseOptimization.Caching.Models;

namespace BlazorIdle.Server.Infrastructure.DatabaseOptimization.Caching.Abstractions;

/// <summary>
/// 多层缓存管理器接口
/// Multi-tier cache manager interface
/// 统一管理 L1/L2/L3 三层缓存
/// </summary>
public interface IMultiTierCacheManager
{
    /// <summary>
    /// 获取缓存项（穿透三层缓存）
    /// Get cache entry (penetrates through all three tiers)
    /// </summary>
    /// <typeparam name="T">缓存值类型</typeparam>
    /// <param name="cacheKey">缓存键</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>缓存的值，如果不存在返回null</returns>
    Task<T?> GetAsync<T>(string cacheKey, CancellationToken ct = default) where T : class;

    /// <summary>
    /// 获取或加载缓存项（如果缓存未命中则从数据源加载）
    /// Get or load cache entry (loads from data source if cache miss)
    /// 包含防缓存击穿保护（信号量）
    /// </summary>
    /// <typeparam name="T">缓存值类型</typeparam>
    /// <param name="cacheKey">缓存键</param>
    /// <param name="loader">数据加载函数</param>
    /// <param name="tier">缓存层级</param>
    /// <param name="ttl">生存时间（null使用默认值）</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>缓存或加载的值</returns>
    Task<T?> GetOrLoadAsync<T>(
        string cacheKey,
        Func<Task<T?>> loader,
        CacheTier tier,
        TimeSpan? ttl = null,
        CancellationToken ct = default) where T : class;

    /// <summary>
    /// 设置缓存项
    /// Set cache entry
    /// </summary>
    /// <typeparam name="T">缓存值类型</typeparam>
    /// <param name="cacheKey">缓存键</param>
    /// <param name="value">缓存值</param>
    /// <param name="tier">缓存层级</param>
    /// <param name="ttl">生存时间（null表示使用默认值）</param>
    Task SetAsync<T>(string cacheKey, T value, CacheTier tier, TimeSpan? ttl = null) where T : class;

    /// <summary>
    /// 失效指定缓存项
    /// Invalidate specific cache entry
    /// </summary>
    /// <param name="cacheKey">缓存键</param>
    Task InvalidateAsync(string cacheKey);

    /// <summary>
    /// 批量失效缓存（支持模式匹配）
    /// Batch invalidate caches (supports pattern matching)
    /// 例如: "Character:*" 失效所有角色缓存
    /// </summary>
    /// <param name="pattern">缓存键模式（支持通配符*）</param>
    Task InvalidateByPatternAsync(string pattern);

    /// <summary>
    /// 清理过期缓存
    /// Clean up expired cache entries
    /// </summary>
    /// <returns>清理的缓存项数量</returns>
    int CleanupExpired();

    /// <summary>
    /// 获取缓存统计信息
    /// Get cache statistics
    /// </summary>
    CacheStatistics GetStatistics();

    /// <summary>
    /// 获取缓存内容摘要
    /// Get cache content summary
    /// </summary>
    CacheContentSummary GetContentSummary();
}

/// <summary>
/// 缓存内容摘要
/// Cache content summary
/// </summary>
public class CacheContentSummary
{
    /// <summary>
    /// 总缓存项数
    /// Total cache entries
    /// </summary>
    public int TotalEntries { get; set; }

    /// <summary>
    /// 按层级统计
    /// Statistics by tier
    /// </summary>
    public Dictionary<string, int> ByTier { get; set; } = new();

    /// <summary>
    /// 按实体类型统计
    /// Statistics by entity type
    /// </summary>
    public Dictionary<string, int> ByEntityType { get; set; } = new();

    /// <summary>
    /// 预估内存使用（MB）
    /// Estimated memory usage in MB
    /// </summary>
    public double MemoryUsageEstimateMB { get; set; }
}
