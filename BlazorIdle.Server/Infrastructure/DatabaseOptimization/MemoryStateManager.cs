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
            // 更新访问时间（LRU）
            UpdateAccessTime(id);
            _logger.LogTrace("从内存获取实体 {EntityType}#{Id}", typeof(T).Name, id);
            return entity;
        }

        // 内存未命中，从数据库加载
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
}
