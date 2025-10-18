using System.Collections.Concurrent;
using BlazorIdle.Server.Config.DatabaseOptimization;
using BlazorIdle.Server.Infrastructure.DatabaseOptimization.Abstractions;
using BlazorIdle.Server.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BlazorIdle.Server.Infrastructure.DatabaseOptimization;

/// <summary>
/// 内存状态管理器实现
/// Memory state manager implementation
/// </summary>
/// <typeparam name="T">实体类型 / Entity type</typeparam>
/// <remarks>
/// 线程安全的内存管理器，用于：
/// 1. 缓存实体避免频繁数据库访问
/// 2. 追踪变更（Dirty Tracking）
/// 3. 支持LRU缓存清理策略
/// 4. 提供快照隔离
/// 
/// Thread-safe memory manager for:
/// 1. Caching entities to avoid frequent database access
/// 2. Tracking changes (Dirty Tracking)
/// 3. Supporting LRU cache eviction policy
/// 4. Providing snapshot isolation
/// </remarks>
public class MemoryStateManager<T> : IMemoryStateManager<T> where T : class, IEntity
{
    private readonly ConcurrentDictionary<Guid, T> _store = new();
    private readonly ConcurrentDictionary<Guid, DateTime> _dirtyEntities = new();
    private readonly ConcurrentDictionary<Guid, DateTime> _accessTimes = new();
    private readonly ReaderWriterLockSlim _lock = new();
    private readonly MemoryCacheOptions _options;
    private readonly ILogger<MemoryStateManager<T>> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    
    // 缓存统计 - Cache statistics
    private long _cacheHits = 0;
    private long _cacheMisses = 0;

    public MemoryStateManager(
        IOptions<MemoryCacheOptions> options,
        ILogger<MemoryStateManager<T>> logger,
        IServiceScopeFactory scopeFactory)
    {
        _options = options.Value;
        _logger = logger;
        _scopeFactory = scopeFactory;
        
        _logger.LogInformation(
            "MemoryStateManager<{EntityType}> 已初始化，最大缓存数：{MaxEntities}，清理策略：{Policy}",
            typeof(T).Name, _options.MaxCachedEntities, _options.EvictionPolicy
        );
    }

    /// <inheritdoc />
    public async Task<T?> GetAsync(Guid id, CancellationToken ct = default)
    {
        // 先查内存
        if (_store.TryGetValue(id, out var entity))
        {
            // 缓存命中
            Interlocked.Increment(ref _cacheHits);
            UpdateAccessTime(id);
            _logger.LogTrace("从内存获取实体 {EntityType}#{Id} - 缓存命中", typeof(T).Name, id);
            return entity;
        }

        // 缓存未命中，从数据库加载
        Interlocked.Increment(ref _cacheMisses);
        _logger.LogDebug("内存未命中，从数据库加载 {EntityType}#{Id}", typeof(T).Name, id);
        
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GameDbContext>();
        
        entity = await db.Set<T>().FindAsync(new object[] { id }, ct);
        
        if (entity != null)
        {
            // 加载到内存
            _store.TryAdd(id, entity);
            UpdateAccessTime(id);
            
            // 检查是否需要清理
            EvictIfNeeded();
            
            _logger.LogDebug("实体 {EntityType}#{Id} 已从数据库加载到内存", typeof(T).Name, id);
        }
        
        return entity;
    }

    /// <inheritdoc />
    public void Add(T entity)
    {
        if (_store.TryAdd(entity.Id, entity))
        {
            UpdateAccessTime(entity.Id);
            _dirtyEntities.TryAdd(entity.Id, DateTime.UtcNow);
            
            _logger.LogTrace("新增实体到内存 {EntityType}#{Id}", typeof(T).Name, entity.Id);
            
            // 检查是否需要清理
            EvictIfNeeded();
        }
        else
        {
            _logger.LogWarning("尝试添加已存在的实体 {EntityType}#{Id}", typeof(T).Name, entity.Id);
        }
    }

    /// <inheritdoc />
    public void Update(T entity)
    {
        _store.AddOrUpdate(entity.Id, entity, (_, _) => entity);
        _dirtyEntities.TryAdd(entity.Id, DateTime.UtcNow);
        UpdateAccessTime(entity.Id);
        
        _logger.LogTrace("更新实体（内存）{EntityType}#{Id}", typeof(T).Name, entity.Id);
    }

    /// <inheritdoc />
    public void Remove(Guid id)
    {
        _store.TryRemove(id, out _);
        _dirtyEntities.TryRemove(id, out _);
        _accessTimes.TryRemove(id, out _);
        
        _logger.LogTrace("从内存移除实体 {EntityType}#{Id}", typeof(T).Name, id);
    }

    /// <inheritdoc />
    public IEnumerable<(Guid Id, T Entity)> GetDirtyEntities()
    {
        foreach (var (id, _) in _dirtyEntities)
        {
            if (_store.TryGetValue(id, out var entity))
            {
                yield return (id, entity);
            }
        }
    }

    /// <inheritdoc />
    public void ClearDirty(IEnumerable<Guid> ids)
    {
        foreach (var id in ids)
        {
            _dirtyEntities.TryRemove(id, out _);
        }
        
        _logger.LogTrace("已清除 {Count} 个实体的 Dirty 标记", ids.Count());
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<Guid, T> GetSnapshot()
    {
        _lock.EnterReadLock();
        try
        {
            return _store.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    /// <inheritdoc />
    public int Count => _store.Count;

    /// <inheritdoc />
    public int DirtyCount => _dirtyEntities.Count;

    /// <summary>
    /// 更新访问时间（用于LRU）
    /// Update access time (for LRU)
    /// </summary>
    private void UpdateAccessTime(Guid id)
    {
        _accessTimes.AddOrUpdate(id, DateTime.UtcNow, (_, _) => DateTime.UtcNow);
    }

    /// <summary>
    /// 如果超过阈值则清理缓存
    /// Evict cache if threshold exceeded
    /// </summary>
    private void EvictIfNeeded()
    {
        if (_store.Count <= _options.MaxCachedEntities)
            return;

        _logger.LogWarning(
            "内存缓存超过阈值 {Current}/{Max}，开始清理...",
            _store.Count, _options.MaxCachedEntities
        );

        if (_options.EvictionPolicy == "LRU")
        {
            EvictLRU();
        }
        else if (_options.EvictionPolicy == "TTL")
        {
            EvictTTL();
        }
    }

    /// <summary>
    /// LRU清理策略
    /// LRU eviction policy
    /// </summary>
    private void EvictLRU()
    {
        var evictCount = _store.Count - _options.MaxCachedEntities;
        if (evictCount <= 0) return;

        // 找出最久未访问且非Dirty的实体
        var candidates = _accessTimes
            .Where(kvp => !_dirtyEntities.ContainsKey(kvp.Key))
            .OrderBy(kvp => kvp.Value)
            .Take(evictCount)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var id in candidates)
        {
            _store.TryRemove(id, out _);
            _accessTimes.TryRemove(id, out _);
        }

        _logger.LogInformation(
            "LRU清理完成：移除 {Count} 个实体，当前缓存 {Current} 个",
            candidates.Count, _store.Count
        );
    }

    /// <summary>
    /// TTL清理策略
    /// TTL eviction policy
    /// </summary>
    private void EvictTTL()
    {
        var threshold = DateTime.UtcNow.AddSeconds(-_options.TimeToLiveSeconds);
        
        var candidates = _accessTimes
            .Where(kvp => !_dirtyEntities.ContainsKey(kvp.Key) && kvp.Value < threshold)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var id in candidates)
        {
            _store.TryRemove(id, out _);
            _accessTimes.TryRemove(id, out _);
        }

        _logger.LogInformation(
            "TTL清理完成：移除 {Count} 个过期实体，当前缓存 {Current} 个",
            candidates.Count, _store.Count
        );
    }

    #region 读取缓存增强功能 - Read Cache Enhancements

    /// <summary>
    /// 尝试获取实体（缓存优先，支持自定义数据库加载器）
    /// Try to get entity (cache first, with custom database loader)
    /// 
    /// 逻辑说明 - Logic:
    /// 1. 先查内存缓存（_store）
    /// 2. 命中则更新访问时间，增加命中计数，返回
    /// 3. 未命中则调用 databaseLoader 查询数据库
    /// 4. 查到后加载到内存，返回
    /// 5. 未查到则返回 null
    /// </summary>
    /// <param name="id">实体 ID / Entity ID</param>
    /// <param name="databaseLoader">数据库查询委托 / Database query delegate</param>
    /// <param name="ct">取消令牌 / Cancellation token</param>
    /// <returns>实体对象，未找到返回 null / Entity or null if not found</returns>
    public async Task<T?> TryGetAsync(
        Guid id,
        Func<Guid, CancellationToken, Task<T?>> databaseLoader,
        CancellationToken ct = default)
    {
        // 1. 先查内存缓存
        if (_store.TryGetValue(id, out var cached))
        {
            // 命中：更新统计
            Interlocked.Increment(ref _cacheHits);
            UpdateAccessTime(id);
            
            _logger.LogTrace(
                "[MemoryStateManager<{EntityType}>] 缓存命中: {Id}",
                typeof(T).Name, id
            );
            
            return cached;
        }
        
        // 2. 未命中：查询数据库
        Interlocked.Increment(ref _cacheMisses);
        
        _logger.LogDebug(
            "[MemoryStateManager<{EntityType}>] 缓存未命中，查询数据库: {Id}",
            typeof(T).Name, id
        );
        
        var entity = await databaseLoader(id, ct);
        
        // 3. 加载到内存（如果查到了）
        if (entity != null)
        {
            _store.TryAdd(id, entity);
            UpdateAccessTime(id);
            
            // 检查是否超过容量限制
            EvictIfNeeded();
        }
        
        return entity;
    }

    /// <summary>
    /// 批量预加载实体（不标记为 Dirty）
    /// Batch preload entities (without marking as Dirty)
    /// </summary>
    /// <param name="entities">实体集合 / Entity collection</param>
    public void PreloadBatch(IEnumerable<T> entities)
    {
        var now = DateTime.UtcNow;
        var count = 0;
        
        foreach (var entity in entities)
        {
            if (_store.TryAdd(entity.Id, entity))
            {
                _accessTimes.TryAdd(entity.Id, now);
                count++;
            }
        }
        
        _logger.LogInformation(
            "[MemoryStateManager<{EntityType}>] 预加载完成: {Count} 个实体",
            typeof(T).Name, count
        );
    }

    /// <summary>
    /// 从数据库批量预加载
    /// Batch preload from database
    /// </summary>
    /// <param name="dbContext">数据库上下文 / Database context</param>
    /// <param name="batchSize">批量大小 / Batch size</param>
    /// <param name="ct">取消令牌 / Cancellation token</param>
    public async Task PreloadFromDatabaseAsync(
        object dbContext,
        int batchSize = 1000,
        CancellationToken ct = default)
    {
        if (dbContext is not GameDbContext gameDb)
        {
            _logger.LogError("Invalid dbContext type. Expected GameDbContext.");
            return;
        }
        
        var skip = 0;
        int loaded;
        var totalLoaded = 0;
        
        do
        {
            var batch = await gameDb.Set<T>()
                .Skip(skip)
                .Take(batchSize)
                .ToListAsync(ct);
            
            loaded = batch.Count;
            PreloadBatch(batch);
            
            skip += batchSize;
            totalLoaded += loaded;
        }
        while (loaded == batchSize && !ct.IsCancellationRequested);
        
        _logger.LogInformation(
            "[MemoryStateManager<{EntityType}>] 从数据库预加载完成: {Total} 个实体",
            typeof(T).Name, totalLoaded
        );
    }

    /// <summary>
    /// 获取缓存命中率
    /// Get cache hit rate
    /// </summary>
    /// <returns>命中率（0.0-1.0） / Hit rate (0.0-1.0)</returns>
    public double GetCacheHitRate()
    {
        var total = _cacheHits + _cacheMisses;
        return total > 0 ? (double)_cacheHits / total : 0.0;
    }

    /// <summary>
    /// 获取缓存统计信息
    /// Get cache statistics
    /// </summary>
    public CacheStatistics GetCacheStatistics()
    {
        return new CacheStatistics
        {
            EntityType = typeof(T).Name,
            CachedCount = _store.Count,
            DirtyCount = _dirtyEntities.Count,
            CacheHits = _cacheHits,
            CacheMisses = _cacheMisses,
            HitRate = GetCacheHitRate()
        };
    }

    /// <summary>
    /// 清理过期缓存（基于 TTL）
    /// Clear expired cache entries (based on TTL)
    /// </summary>
    /// <param name="ttlSeconds">过期时间（秒） / TTL in seconds</param>
    /// <returns>移除的实体数量 / Number of entities removed</returns>
    public int ClearExpired(int ttlSeconds)
    {
        var cutoff = DateTime.UtcNow.AddSeconds(-ttlSeconds);
        var expiredIds = _accessTimes
            .Where(kvp => kvp.Value < cutoff)
            .Select(kvp => kvp.Key)
            .ToList();
        
        var removedCount = 0;
        foreach (var id in expiredIds)
        {
            // 不移除 Dirty 的实体（还未保存）
            if (!_dirtyEntities.ContainsKey(id))
            {
                if (_store.TryRemove(id, out _))
                {
                    _accessTimes.TryRemove(id, out _);
                    removedCount++;
                }
            }
        }
        
        if (removedCount > 0)
        {
            _logger.LogInformation(
                "[MemoryStateManager<{EntityType}>] 清理过期缓存: {Count} 个实体",
                typeof(T).Name, removedCount
            );
        }
        
        return removedCount;
    }

    /// <summary>
    /// 清空所有缓存
    /// Clear all cache
    /// </summary>
    public void ClearAll()
    {
        var count = _store.Count;
        _store.Clear();
        _accessTimes.Clear();
        // 注意：不清空 _dirtyEntities，保留未保存的实体
        
        _logger.LogWarning(
            "[MemoryStateManager<{EntityType}>] 已清空所有缓存: {Count} 个实体（保留Dirty）",
            typeof(T).Name, count
        );
    }

    /// <summary>
    /// 获取所有缓存的实体
    /// Get all cached entities
    /// </summary>
    /// <returns>所有实体集合 / Collection of all entities</returns>
    public IEnumerable<T> GetAll()
    {
        return _store.Values.ToList();
    }

    /// <summary>
    /// 使特定实体缓存失效
    /// Invalidate specific entity cache
    /// </summary>
    /// <param name="id">实体 ID / Entity ID</param>
    public void InvalidateCache(Guid id)
    {
        if (!_dirtyEntities.ContainsKey(id))
        {
            _store.TryRemove(id, out _);
            _accessTimes.TryRemove(id, out _);
            
            _logger.LogDebug(
                "[MemoryStateManager<{EntityType}>] 使缓存失效: {Id}",
                typeof(T).Name, id
            );
        }
    }

    #endregion
}

/// <summary>
/// 缓存统计信息
/// Cache Statistics
/// </summary>
public class CacheStatistics
{
    /// <summary>
    /// 实体类型名称 / Entity type name
    /// </summary>
    public string EntityType { get; set; } = string.Empty;
    
    /// <summary>
    /// 缓存数量 / Cached count
    /// </summary>
    public int CachedCount { get; set; }
    
    /// <summary>
    /// Dirty 数量 / Dirty count
    /// </summary>
    public int DirtyCount { get; set; }
    
    /// <summary>
    /// 缓存命中次数 / Cache hits
    /// </summary>
    public long CacheHits { get; set; }
    
    /// <summary>
    /// 缓存未命中次数 / Cache misses
    /// </summary>
    public long CacheMisses { get; set; }
    
    /// <summary>
    /// 缓存命中率 / Hit rate
    /// </summary>
    public double HitRate { get; set; }
}
