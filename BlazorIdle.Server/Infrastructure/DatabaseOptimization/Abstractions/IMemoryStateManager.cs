namespace BlazorIdle.Server.Infrastructure.DatabaseOptimization.Abstractions;

/// <summary>
/// 内存状态管理器接口
/// Memory state manager interface
/// </summary>
/// <typeparam name="T">实体类型 / Entity type</typeparam>
/// <remarks>
/// 职责：
/// 1. 管理实体的内存副本
/// 2. 追踪变更（Dirty Tracking）
/// 3. 提供线程安全的并发访问
/// 4. 支持快照隔离
/// 
/// Responsibilities:
/// 1. Manage in-memory copies of entities
/// 2. Track changes (Dirty Tracking)
/// 3. Provide thread-safe concurrent access
/// 4. Support snapshot isolation
/// </remarks>
public interface IMemoryStateManager<T> where T : class, IEntity
{
    /// <summary>
    /// 获取实体（先查内存，未命中则从数据库加载）
    /// Get entity (check memory first, load from database if not found)
    /// </summary>
    /// <param name="id">实体ID / Entity ID</param>
    /// <param name="ct">取消令牌 / Cancellation token</param>
    /// <returns>实体实例，如果不存在则返回 null / Entity instance, or null if not found</returns>
    Task<T?> GetAsync(Guid id, CancellationToken ct = default);
    
    /// <summary>
    /// 添加新实体到内存（不立即保存到数据库）
    /// Add new entity to memory (do not save to database immediately)
    /// </summary>
    /// <param name="entity">实体实例 / Entity instance</param>
    void Add(T entity);
    
    /// <summary>
    /// 更新实体（仅更新内存，标记为 Dirty）
    /// Update entity (update memory only, mark as dirty)
    /// </summary>
    /// <param name="entity">实体实例 / Entity instance</param>
    void Update(T entity);
    
    /// <summary>
    /// 从内存中移除实体
    /// Remove entity from memory
    /// </summary>
    /// <param name="id">实体ID / Entity ID</param>
    void Remove(Guid id);
    
    /// <summary>
    /// 获取所有 Dirty 实体（用于批量保存）
    /// Get all dirty entities (for batch saving)
    /// </summary>
    /// <returns>Dirty 实体集合 / Collection of dirty entities</returns>
    IEnumerable<(Guid Id, T Entity)> GetDirtyEntities();
    
    /// <summary>
    /// 清除 Dirty 标记（保存成功后调用）
    /// Clear dirty flags (called after successful save)
    /// </summary>
    /// <param name="ids">实体ID集合 / Collection of entity IDs</param>
    void ClearDirty(IEnumerable<Guid> ids);
    
    /// <summary>
    /// 获取所有实体的快照（用于关闭时保存）
    /// Get snapshot of all entities (for shutdown save)
    /// </summary>
    /// <returns>只读的实体字典 / Read-only dictionary of entities</returns>
    IReadOnlyDictionary<Guid, T> GetSnapshot();
    
    /// <summary>
    /// 获取当前缓存的实体数量
    /// Get current count of cached entities
    /// </summary>
    int Count { get; }
    
    /// <summary>
    /// 获取当前 Dirty 实体数量
    /// Get current count of dirty entities
    /// </summary>
    int DirtyCount { get; }
    
    #region 读取缓存增强功能 - Read Cache Enhancement Methods
    
    /// <summary>
    /// 尝试获取实体（缓存优先，支持自定义数据库加载器）
    /// Try to get entity (cache first, with custom database loader)
    /// </summary>
    /// <param name="id">实体 ID / Entity ID</param>
    /// <param name="databaseLoader">数据库查询委托 / Database query delegate</param>
    /// <param name="ct">取消令牌 / Cancellation token</param>
    /// <returns>实体对象，未找到返回 null / Entity or null if not found</returns>
    Task<T?> TryGetAsync(
        Guid id,
        Func<Guid, CancellationToken, Task<T?>> databaseLoader,
        CancellationToken ct = default);
    
    /// <summary>
    /// 批量预加载实体（不标记为 Dirty）
    /// Batch preload entities (without marking as Dirty)
    /// </summary>
    /// <param name="entities">实体集合 / Entity collection</param>
    void PreloadBatch(IEnumerable<T> entities);
    
    /// <summary>
    /// 从数据库批量预加载
    /// Batch preload from database
    /// 
    /// 注意：需要传入 GameDbContext 类型的实例
    /// Note: Requires GameDbContext type instance
    /// </summary>
    /// <param name="dbContext">数据库上下文 / Database context</param>
    /// <param name="batchSize">批量大小 / Batch size</param>
    /// <param name="ct">取消令牌 / Cancellation token</param>
    Task PreloadFromDatabaseAsync(
        object dbContext,
        int batchSize = 1000,
        CancellationToken ct = default);
    
    /// <summary>
    /// 获取缓存命中率
    /// Get cache hit rate
    /// </summary>
    /// <returns>命中率（0.0-1.0） / Hit rate (0.0-1.0)</returns>
    double GetCacheHitRate();
    
    /// <summary>
    /// 获取缓存统计信息
    /// Get cache statistics
    /// </summary>
    CacheStatistics GetCacheStatistics();
    
    /// <summary>
    /// 清理过期缓存（基于 TTL）
    /// Clear expired cache entries (based on TTL)
    /// </summary>
    /// <param name="ttlSeconds">过期时间（秒） / TTL in seconds</param>
    /// <returns>移除的实体数量 / Number of entities removed</returns>
    int ClearExpired(int ttlSeconds);
    
    /// <summary>
    /// 清空所有缓存
    /// Clear all cache
    /// </summary>
    void ClearAll();
    
    /// <summary>
    /// 获取所有缓存的实体
    /// Get all cached entities
    /// </summary>
    /// <returns>所有实体集合 / Collection of all entities</returns>
    IEnumerable<T> GetAll();
    
    /// <summary>
    /// 使特定实体缓存失效
    /// Invalidate specific entity cache
    /// </summary>
    /// <param name="id">实体 ID / Entity ID</param>
    void InvalidateCache(Guid id);
    
    #endregion
}
