namespace BlazorIdle.Server.Config.DatabaseOptimization;

/// <summary>
/// 缓存策略类型枚举
/// Cache Strategy Type Enum
/// </summary>
public enum CacheStrategyType
{
    /// <summary>
    /// 无缓存 - 直接查询数据库
    /// No caching - Query database directly
    /// </summary>
    None = 0,
    
    /// <summary>
    /// 永久缓存 - 适用于静态配置数据
    /// Permanent caching - For static configuration data
    /// 
    /// 特点 - Features:
    /// - 启动时预加载
    /// - 不过期（除非手动刷新）
    /// - 适用于：装备定义、词缀定义、装备套装等
    /// </summary>
    Permanent = 1,
    
    /// <summary>
    /// 临时缓存 - 适用于用户数据，带 TTL
    /// Temporary caching - For user data, with TTL
    /// 
    /// 特点 - Features:
    /// - 懒加载（按需加载）
    /// - 带过期时间（TTL）
    /// - LRU 清理策略
    /// - 适用于：角色、装备实例、活动计划等
    /// </summary>
    Temporary = 2
}
