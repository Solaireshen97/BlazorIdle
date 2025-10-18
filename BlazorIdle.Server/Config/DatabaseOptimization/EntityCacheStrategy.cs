using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace BlazorIdle.Server.Config.DatabaseOptimization;

/// <summary>
/// 单个实体类型的缓存策略
/// Cache strategy for a single entity type
/// </summary>
public class EntityCacheStrategy
{
    /// <summary>
    /// 缓存策略类型（None/Permanent/Temporary）
    /// Cache strategy type
    /// 
    /// 说明 - Description:
    /// - None: 不缓存，直接查询数据库
    /// - Permanent: 永久缓存，适用于静态配置数据（如装备定义）
    /// - Temporary: 临时缓存，带 TTL，适用于用户数据（如角色、装备实例）
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public CacheStrategyType Strategy { get; set; } = CacheStrategyType.Temporary;
    
    /// <summary>
    /// TTL（秒）- 仅对 Temporary 策略有效
    /// Time-to-live in seconds (for Temporary strategy only)
    /// 
    /// 范围 - Range: 60 秒（1分钟）到 86400 秒（24小时）
    /// 默认 - Default: 3600 秒（1小时）
    /// </summary>
    [Range(60, 86400)]
    public int TtlSeconds { get; set; } = 3600;
    
    /// <summary>
    /// 最大缓存数量
    /// Maximum cached entities
    /// 
    /// 范围 - Range: 100 到 1000000
    /// 超过此数量后触发 LRU 清理
    /// </summary>
    [Range(100, 1000000)]
    public int MaxCachedCount { get; set; } = 10000;
    
    /// <summary>
    /// 是否在启动时预加载
    /// Preload on startup
    /// 
    /// 说明 - Description:
    /// - 仅对 Permanent 策略推荐启用
    /// - 启用后会在服务启动时从数据库批量加载所有数据到内存
    /// - 适用于数据量不大的静态配置数据
    /// </summary>
    public bool PreloadOnStartup { get; set; } = false;
    
    /// <summary>
    /// 预加载批量大小
    /// Preload batch size
    /// 
    /// 范围 - Range: 100 到 10000
    /// 默认 - Default: 1000
    /// 
    /// 说明 - Description: 控制每次从数据库读取的记录数，避免一次性加载过多导致内存峰值
    /// </summary>
    [Range(100, 10000)]
    public int PreloadBatchSize { get; set; } = 1000;
}
