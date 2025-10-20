namespace BlazorIdle.Server.Infrastructure.DatabaseOptimization.Abstractions;

/// <summary>
/// 标记接口，用于标识可被内存状态管理器管理的实体
/// Marker interface for entities that can be managed by memory state manager
/// </summary>
/// <remarks>
/// 实现此接口的实体必须：
/// 1. 拥有唯一标识符 (Id)
/// 2. 支持并发访问和更新
/// 3. 可序列化到数据库
/// 
/// Entities implementing this interface must:
/// 1. Have a unique identifier (Id)
/// 2. Support concurrent access and updates
/// 3. Be serializable to database
/// </remarks>
public interface IEntity
{
    /// <summary>
    /// 实体的唯一标识符
    /// Unique identifier for the entity
    /// </summary>
    Guid Id { get; }
}
