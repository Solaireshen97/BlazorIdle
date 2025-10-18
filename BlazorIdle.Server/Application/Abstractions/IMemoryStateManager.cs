namespace BlazorIdle.Server.Application.Abstractions;

/// <summary>
/// 内存状态管理器接口 - 管理实体的内存副本和变更跟踪
/// </summary>
/// <typeparam name="T">实体类型，必须实现 IEntity 接口</typeparam>
public interface IMemoryStateManager<T> where T : class, IEntity
{
    /// <summary>
    /// 获取实体（先查内存，未命中则从数据库加载）
    /// </summary>
    /// <param name="id">实体ID</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>实体对象，如果不存在则返回 null</returns>
    Task<T?> GetAsync(Guid id, CancellationToken ct = default);
    
    /// <summary>
    /// 添加实体到内存
    /// </summary>
    /// <param name="entity">要添加的实体</param>
    void Add(T entity);
    
    /// <summary>
    /// 更新实体（仅更新内存，标记为 Dirty）
    /// </summary>
    /// <param name="entity">要更新的实体</param>
    void Update(T entity);
    
    /// <summary>
    /// 从内存中移除实体
    /// </summary>
    /// <param name="id">实体ID</param>
    void Remove(Guid id);
    
    /// <summary>
    /// 获取所有 Dirty 实体（用于批量保存）
    /// </summary>
    /// <returns>Dirty 实体集合</returns>
    IEnumerable<(Guid Id, T Entity)> GetDirtyEntities();
    
    /// <summary>
    /// 清除 Dirty 标记（保存成功后调用）
    /// </summary>
    /// <param name="ids">要清除标记的实体ID集合</param>
    void ClearDirty(IEnumerable<Guid> ids);
    
    /// <summary>
    /// 获取所有实体快照（用于关闭时保存）
    /// </summary>
    /// <returns>所有实体的只读字典</returns>
    IReadOnlyDictionary<Guid, T> GetSnapshot();
    
    /// <summary>
    /// 获取缓存的实体总数
    /// </summary>
    int Count { get; }
    
    /// <summary>
    /// 获取 Dirty 实体数量
    /// </summary>
    int DirtyCount { get; }
}
