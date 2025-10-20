using BlazorIdle.Server.Config.DatabaseOptimization;
using BlazorIdle.Server.Infrastructure.DatabaseOptimization.Caching.Abstractions;
using BlazorIdle.Server.Infrastructure.DatabaseOptimization.Caching.Models;
using Microsoft.Extensions.Options;

namespace BlazorIdle.Server.Infrastructure.Persistence;

/// <summary>
/// 缓存感知仓储基类
/// Cache-aware repository base class
/// 提供统一的缓存读取逻辑
/// </summary>
public abstract class CacheAwareRepository<TEntity, TKey> where TEntity : class
{
    /// <summary>
    /// 缓存管理器
    /// Cache manager
    /// </summary>
    protected readonly IMultiTierCacheManager CacheManager;
    
    /// <summary>
    /// 缓存配置选项
    /// Cache configuration options
    /// </summary>
    protected readonly ReadCacheOptions CacheOptions;
    
    /// <summary>
    /// 日志记录器
    /// Logger
    /// </summary>
    protected readonly ILogger Logger;
    
    /// <summary>
    /// 构造函数
    /// </summary>
    protected CacheAwareRepository(
        IMultiTierCacheManager cacheManager,
        IOptions<ReadCacheOptions> cacheOptions,
        ILogger logger)
    {
        CacheManager = cacheManager;
        CacheOptions = cacheOptions.Value;
        Logger = logger;
    }
    
    /// <summary>
    /// 通过缓存获取实体
    /// Get entity with cache
    /// </summary>
    /// <param name="key">实体键</param>
    /// <param name="loader">数据加载函数（从数据库）</param>
    /// <param name="entityType">实体类型名称（默认使用TEntity的类型名）</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>实体对象或null</returns>
    protected async Task<TEntity?> GetWithCacheAsync(
        TKey key,
        Func<Task<TEntity?>> loader,
        string? entityType = null,
        CancellationToken ct = default)
    {
        // 检查是否启用缓存
        if (!CacheOptions.EnableReadCache)
        {
            return await loader();
        }
        
        // 确定实体类型
        entityType ??= typeof(TEntity).Name;
        
        // 获取缓存策略
        var strategy = GetEntityStrategy(entityType);
        
        // 构建缓存键
        var cacheKey = BuildCacheKey(key, entityType);
        
        // 确定缓存层级
        var tier = ParseCacheTier(strategy.Tier);
        
        // 确定 TTL
        var ttl = TimeSpan.FromMinutes(strategy.TtlMinutes);
        
        // 通过缓存管理器加载
        return await CacheManager.GetOrLoadAsync(
            cacheKey,
            loader,
            tier,
            ttl,
            ct
        );
    }
    
    /// <summary>
    /// 通过缓存获取实体列表
    /// Get entity list with cache
    /// </summary>
    /// <param name="key">缓存键（自定义）</param>
    /// <param name="loader">数据加载函数</param>
    /// <param name="entityType">实体类型名称</param>
    /// <param name="qualifier">限定符（如 "Equipped", "All" 等）</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>实体列表</returns>
    protected async Task<List<TEntity>> GetListWithCacheAsync(
        TKey key,
        Func<Task<List<TEntity>>> loader,
        string? entityType = null,
        string? qualifier = null,
        CancellationToken ct = default)
    {
        // 检查是否启用缓存
        if (!CacheOptions.EnableReadCache)
        {
            return await loader();
        }
        
        // 确定实体类型
        entityType ??= typeof(TEntity).Name;
        
        // 获取缓存策略
        var strategy = GetEntityStrategy(entityType);
        
        // 构建缓存键
        var cacheKey = BuildCacheKey(key, entityType, qualifier);
        
        // 确定缓存层级
        var tier = ParseCacheTier(strategy.Tier);
        
        // 确定 TTL
        var ttl = TimeSpan.FromMinutes(strategy.TtlMinutes);
        
        // 使用一个包装类来存储列表（因为泛型约束要求 class）
        var wrapper = await CacheManager.GetOrLoadAsync(
            cacheKey,
            async () =>
            {
                var list = await loader();
                return new ListWrapper<TEntity> { Items = list };
            },
            tier,
            ttl,
            ct
        );
        
        return wrapper?.Items ?? new List<TEntity>();
    }
    
    /// <summary>
    /// 失效实体缓存
    /// Invalidate entity cache
    /// </summary>
    /// <param name="key">实体键</param>
    /// <param name="entityType">实体类型名称</param>
    protected async Task InvalidateCacheAsync(TKey key, string? entityType = null)
    {
        if (!CacheOptions.EnableReadCache)
        {
            return;
        }

        entityType ??= typeof(TEntity).Name;
        var cacheKey = BuildCacheKey(key, entityType);
        
        await CacheManager.InvalidateAsync(cacheKey);
        
        Logger.LogDebug("失效缓存: {CacheKey}", cacheKey);
    }
    
    /// <summary>
    /// 批量失效缓存（支持模式）
    /// Batch invalidate cache (supports pattern)
    /// </summary>
    /// <param name="pattern">缓存键模式（如 "Character:*"）</param>
    protected async Task InvalidateCacheByPatternAsync(string pattern)
    {
        if (!CacheOptions.EnableReadCache)
        {
            return;
        }

        await CacheManager.InvalidateByPatternAsync(pattern);
        
        Logger.LogDebug("模式失效缓存: {Pattern}", pattern);
    }
    
    /// <summary>
    /// 构建缓存键
    /// Build cache key
    /// </summary>
    /// <param name="key">实体键</param>
    /// <param name="entityType">实体类型</param>
    /// <param name="qualifier">可选限定符</param>
    /// <returns>缓存键</returns>
    protected virtual string BuildCacheKey(TKey key, string entityType, string? qualifier = null)
    {
        if (string.IsNullOrEmpty(qualifier))
        {
            return $"{entityType}:{key}";
        }
        else
        {
            return $"{entityType}:{qualifier}:{key}";
        }
    }
    
    /// <summary>
    /// 获取实体缓存策略
    /// Get entity cache strategy
    /// </summary>
    /// <param name="entityType">实体类型名称</param>
    /// <returns>缓存策略</returns>
    protected EntityCacheStrategy GetEntityStrategy(string entityType)
    {
        if (CacheOptions.EntityStrategies.TryGetValue(entityType, out var strategy))
        {
            return strategy;
        }
        
        // 返回默认策略
        return new EntityCacheStrategy
        {
            Tier = "Entity",
            TtlMinutes = 15,
            InvalidateOnUpdate = true
        };
    }
    
    /// <summary>
    /// 解析缓存层级
    /// Parse cache tier
    /// </summary>
    /// <param name="tier">层级字符串</param>
    /// <returns>缓存层级枚举</returns>
    protected static CacheTier ParseCacheTier(string tier)
    {
        return tier?.ToLowerInvariant() switch
        {
            "session" => CacheTier.Session,
            "entity" => CacheTier.Entity,
            "static" => CacheTier.Static,
            _ => CacheTier.Entity
        };
    }
}

/// <summary>
/// 列表包装类（用于缓存列表）
/// List wrapper class (for caching lists)
/// </summary>
internal class ListWrapper<T> where T : class
{
    public List<T> Items { get; set; } = new();
}
