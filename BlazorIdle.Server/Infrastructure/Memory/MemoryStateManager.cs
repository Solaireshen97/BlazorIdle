using System.Collections.Concurrent;
using BlazorIdle.Server.Application.Abstractions;
using BlazorIdle.Server.Config.Persistence;
using BlazorIdle.Server.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BlazorIdle.Server.Infrastructure.Memory;

/// <summary>
/// 内存状态管理器 - 管理实体的内存副本和变更跟踪
/// 线程安全的实现，支持并发访问
/// </summary>
/// <typeparam name="T">实体类型，必须实现 IEntity 接口</typeparam>
public class MemoryStateManager<T> : IMemoryStateManager<T> where T : class, IEntity
{
    private readonly ConcurrentDictionary<Guid, T> _store;
    private readonly ConcurrentDictionary<Guid, DateTime> _dirtyEntities;
    private readonly ConcurrentDictionary<Guid, DateTime> _accessTimes;
    private readonly ReaderWriterLockSlim _lock;
    private readonly MemoryCacheOptions _options;
    private readonly ILogger<MemoryStateManager<T>> _logger;
    private readonly IServiceScopeFactory _scopeFactory;

    public MemoryStateManager(
        IOptions<MemoryCacheOptions> options,
        ILogger<MemoryStateManager<T>> logger,
        IServiceScopeFactory scopeFactory)
    {
        _store = new ConcurrentDictionary<Guid, T>();
        _dirtyEntities = new ConcurrentDictionary<Guid, DateTime>();
        _accessTimes = new ConcurrentDictionary<Guid, DateTime>();
        _lock = new ReaderWriterLockSlim();
        _options = options.Value;
        _logger = logger;
        _scopeFactory = scopeFactory;
    }

    /// <summary>
    /// 获取实体（先查内存，未命中则从数据库加载）
    /// </summary>
    public async Task<T?> GetAsync(Guid id, CancellationToken ct = default)
    {
        // 先查内存
        if (_store.TryGetValue(id, out var entity))
        {
            UpdateAccessTime(id);
            return entity;
        }

        // 未命中，从数据库加载
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GameDbContext>();

        entity = await db.Set<T>().FindAsync(new object[] { id }, ct);
        if (entity != null)
        {
            _store.TryAdd(id, entity);
            UpdateAccessTime(id);
            
            // 检查是否需要清理
            EvictIfNeeded();
        }

        return entity;
    }

    /// <summary>
    /// 添加实体到内存
    /// </summary>
    public void Add(T entity)
    {
        _store.AddOrUpdate(entity.Id, entity, (_, __) => entity);
        UpdateAccessTime(entity.Id);
        
        // 标记为 Dirty（新添加的实体也需要保存）
        _dirtyEntities.TryAdd(entity.Id, DateTime.UtcNow);
        
        // 检查是否需要清理
        EvictIfNeeded();
    }

    /// <summary>
    /// 更新实体（仅更新内存，标记为 Dirty）
    /// </summary>
    public void Update(T entity)
    {
        _store.AddOrUpdate(entity.Id, entity, (_, __) => entity);
        _dirtyEntities.TryAdd(entity.Id, DateTime.UtcNow);
        UpdateAccessTime(entity.Id);
    }

    /// <summary>
    /// 从内存中移除实体
    /// </summary>
    public void Remove(Guid id)
    {
        _store.TryRemove(id, out _);
        _dirtyEntities.TryRemove(id, out _);
        _accessTimes.TryRemove(id, out _);
    }

    /// <summary>
    /// 获取所有 Dirty 实体（用于批量保存）
    /// </summary>
    public IEnumerable<(Guid Id, T Entity)> GetDirtyEntities()
    {
        foreach (var (id, _) in _dirtyEntities)
        {
            if (_store.TryGetValue(id, out var entity))
                yield return (id, entity);
        }
    }

    /// <summary>
    /// 清除 Dirty 标记（保存成功后调用）
    /// </summary>
    public void ClearDirty(IEnumerable<Guid> ids)
    {
        foreach (var id in ids)
        {
            _dirtyEntities.TryRemove(id, out _);
        }
    }

    /// <summary>
    /// 获取所有实体快照（用于关闭时保存）
    /// </summary>
    public IReadOnlyDictionary<Guid, T> GetSnapshot()
    {
        _lock.EnterReadLock();
        try
        {
            return new Dictionary<Guid, T>(_store);
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    /// <summary>
    /// 获取缓存的实体总数
    /// </summary>
    public int Count => _store.Count;

    /// <summary>
    /// 获取 Dirty 实体数量
    /// </summary>
    public int DirtyCount => _dirtyEntities.Count;

    /// <summary>
    /// 更新访问时间（用于 LRU）
    /// </summary>
    private void UpdateAccessTime(Guid id)
    {
        _accessTimes.AddOrUpdate(id, DateTime.UtcNow, (_, __) => DateTime.UtcNow);
    }

    /// <summary>
    /// LRU 清理：当实体数超过阈值时，移除最久未访问的实体
    /// </summary>
    private void EvictIfNeeded()
    {
        if (_store.Count <= _options.MaxCachedEntities)
            return;

        var evictCount = _store.Count - _options.MaxCachedEntities;

        var toEvict = _accessTimes
            .OrderBy(kv => kv.Value) // 最早访问的在前
            .Take(evictCount)
            .Select(kv => kv.Key)
            .ToList();

        foreach (var id in toEvict)
        {
            // 如果实体是 Dirty 状态，记录警告但仍然移除（应该由定期保存来处理）
            if (_dirtyEntities.ContainsKey(id))
            {
                _logger.LogWarning(
                    "正在清理 Dirty 实体 {EntityType}:{Id}，数据可能在下次保存前丢失",
                    typeof(T).Name, id
                );
            }

            _store.TryRemove(id, out _);
            _accessTimes.TryRemove(id, out _);
            // 注意：不清理 _dirtyEntities，以便下次保存时仍能处理
        }

        _logger.LogInformation(
            "LRU 清理完成：{EntityType} 移除 {Count} 个实体，当前缓存 {Current} 个",
            typeof(T).Name, toEvict.Count, _store.Count
        );
    }
}
