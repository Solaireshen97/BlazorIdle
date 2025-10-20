using System.ComponentModel.DataAnnotations;

namespace BlazorIdle.Server.Config.DatabaseOptimization;

/// <summary>
/// 读缓存配置选项
/// Read cache configuration options
/// </summary>
public class ReadCacheOptions
{
    /// <summary>
    /// 是否启用读缓存（主开关）
    /// Enable read cache (master switch)
    /// </summary>
    public bool EnableReadCache { get; set; } = false;  // 默认禁用，待Phase 4完成后启用

    /// <summary>
    /// 最大缓存项数
    /// Maximum cache entries
    /// </summary>
    [Range(1000, 1000000)]
    public int MaxCacheSize { get; set; } = 100000;

    /// <summary>
    /// 是否启用统计
    /// Enable statistics
    /// </summary>
    public bool EnableStatistics { get; set; } = true;

    /// <summary>
    /// 统计间隔（秒）
    /// Statistics interval in seconds
    /// </summary>
    [Range(10, 3600)]
    public int StatisticsIntervalSeconds { get; set; } = 60;

    /// <summary>
    /// Session Cache 配置
    /// Session cache options
    /// </summary>
    public SessionCacheOptions SessionCache { get; set; } = new();

    /// <summary>
    /// Entity Cache 配置
    /// Entity cache options
    /// </summary>
    public EntityCacheOptions EntityCache { get; set; } = new();

    /// <summary>
    /// Static Cache 配置
    /// Static cache options
    /// </summary>
    public StaticCacheOptions StaticCache { get; set; } = new();

    /// <summary>
    /// 实体级缓存策略
    /// Entity-level cache strategies
    /// </summary>
    public Dictionary<string, EntityCacheStrategy> EntityStrategies { get; set; } = new();

    /// <summary>
    /// 失效策略配置
    /// Invalidation options
    /// </summary>
    public InvalidationOptions Invalidation { get; set; } = new();

    /// <summary>
    /// 性能优化配置
    /// Performance options
    /// </summary>
    public PerformanceOptions Performance { get; set; } = new();
}

/// <summary>
/// Session Cache 配置
/// Session cache configuration
/// </summary>
public class SessionCacheOptions
{
    /// <summary>
    /// 是否启用
    /// Enabled
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// 默认 TTL（分钟）
    /// Default TTL in minutes
    /// </summary>
    [Range(1, 1440)]
    public int DefaultTtlMinutes { get; set; } = 30;

    /// <summary>
    /// 滑动过期（访问时刷新TTL）
    /// Sliding expiration (refresh TTL on access)
    /// </summary>
    public bool SlidingExpiration { get; set; } = true;

    /// <summary>
    /// 最大缓存项数
    /// Maximum cache size
    /// </summary>
    [Range(100, 50000)]
    public int MaxSize { get; set; } = 10000;
}

/// <summary>
/// Entity Cache 配置
/// Entity cache configuration
/// </summary>
public class EntityCacheOptions
{
    /// <summary>
    /// 是否启用
    /// Enabled
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// 默认 TTL（分钟）
    /// Default TTL in minutes
    /// </summary>
    [Range(1, 1440)]
    public int DefaultTtlMinutes { get; set; } = 15;

    /// <summary>
    /// 最大缓存项数
    /// Maximum cache size
    /// </summary>
    [Range(1000, 100000)]
    public int MaxSize { get; set; } = 50000;

    /// <summary>
    /// 淘汰策略
    /// Eviction policy
    /// </summary>
    public string EvictionPolicy { get; set; } = "LRU";

    /// <summary>
    /// 压缩百分比（达到最大值时清理的比例）
    /// Compaction percentage (percentage to clean when max is reached)
    /// </summary>
    [Range(0.1, 0.5)]
    public double CompactionPercentage { get; set; } = 0.2;
}

/// <summary>
/// Static Cache 配置
/// Static cache configuration
/// </summary>
public class StaticCacheOptions
{
    /// <summary>
    /// 是否启用
    /// Enabled
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// 启动时加载
    /// Load on startup
    /// </summary>
    public bool LoadOnStartup { get; set; } = true;

    /// <summary>
    /// 启用热重载
    /// Enable hot reload
    /// </summary>
    public bool EnableHotReload { get; set; } = true;

    /// <summary>
    /// 最大缓存项数
    /// Maximum cache size
    /// </summary>
    [Range(1000, 100000)]
    public int MaxSize { get; set; } = 50000;
}

/// <summary>
/// 实体缓存策略
/// Entity cache strategy
/// </summary>
public class EntityCacheStrategy
{
    /// <summary>
    /// 缓存层级（Session/Entity/Static）
    /// Cache tier (Session/Entity/Static)
    /// </summary>
    public string Tier { get; set; } = "Entity";

    /// <summary>
    /// TTL（分钟）
    /// TTL in minutes
    /// </summary>
    public int TtlMinutes { get; set; } = 15;

    /// <summary>
    /// 更新时是否失效缓存
    /// Invalidate on update
    /// </summary>
    public bool InvalidateOnUpdate { get; set; } = true;

    /// <summary>
    /// 级联失效列表（缓存键模式）
    /// Cascade invalidation patterns
    /// </summary>
    public List<string> CascadeInvalidation { get; set; } = new();
}

/// <summary>
/// 失效策略配置
/// Invalidation configuration
/// </summary>
public class InvalidationOptions
{
    /// <summary>
    /// 启用级联失效
    /// Enable cascading invalidation
    /// </summary>
    public bool EnableCascading { get; set; } = true;

    /// <summary>
    /// 启用模式匹配失效
    /// Enable pattern match invalidation
    /// </summary>
    public bool EnablePatternMatch { get; set; } = true;

    /// <summary>
    /// 记录失效日志（调试用）
    /// Log invalidations (for debugging)
    /// </summary>
    public bool LogInvalidations { get; set; } = false;
}

/// <summary>
/// 性能优化配置
/// Performance configuration
/// </summary>
public class PerformanceOptions
{
    /// <summary>
    /// 启用防缓存击穿（信号量保护）
    /// Enable anti-cache-crashing (semaphore protection)
    /// </summary>
    public bool EnableAntiCrashing { get; set; } = true;

    /// <summary>
    /// 防击穿信号量超时（毫秒）
    /// Anti-crashing semaphore timeout in milliseconds
    /// </summary>
    [Range(1000, 30000)]
    public int AntiCrashingSemaphoreTimeout { get; set; } = 5000;

    /// <summary>
    /// 启动时预加载的实体类型
    /// Entity types to preload on startup
    /// </summary>
    public List<string> PreloadOnStartup { get; set; } = new();
}
