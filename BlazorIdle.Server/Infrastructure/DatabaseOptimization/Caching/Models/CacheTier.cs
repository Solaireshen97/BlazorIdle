namespace BlazorIdle.Server.Infrastructure.DatabaseOptimization.Caching.Models;

/// <summary>
/// 缓存层级枚举
/// Cache tier enumeration
/// </summary>
public enum CacheTier
{
    /// <summary>
    /// 会话级缓存 - 使用 ASP.NET Core MemoryCache
    /// Session-level cache - uses ASP.NET Core MemoryCache
    /// 适用于用户会话期间的数据 (Character, User)
    /// </summary>
    Session,

    /// <summary>
    /// 实体级缓存 - 使用 ConcurrentDictionary + LRU
    /// Entity-level cache - uses ConcurrentDictionary + LRU
    /// 适用于低频变更的实体 (GearInstance, BattleRecord)
    /// </summary>
    Entity,

    /// <summary>
    /// 静态配置缓存 - 永久缓存直到手动刷新
    /// Static configuration cache - permanent until manual refresh
    /// 适用于静态配置数据 (GearDefinition, Affix)
    /// </summary>
    Static
}
