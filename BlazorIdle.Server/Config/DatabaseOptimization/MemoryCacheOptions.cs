using System.ComponentModel.DataAnnotations;

namespace BlazorIdle.Server.Config.DatabaseOptimization;

/// <summary>
/// 内存缓存配置选项
/// Memory cache configuration options
/// </summary>
public class MemoryCacheOptions
{
    /// <summary>
    /// 内存缓存最大实体数（防止内存溢出）
    /// Maximum number of entities in memory cache (prevent memory overflow)
    /// Default: 100000
    /// </summary>
    [Range(1000, 1000000)]
    public int MaxCachedEntities { get; set; } = 100000;
    
    /// <summary>
    /// 超过阈值时的清理策略（LRU / TTL）
    /// Eviction policy when threshold exceeded (LRU / TTL)
    /// Default: LRU
    /// </summary>
    public string EvictionPolicy { get; set; } = "LRU";
    
    /// <summary>
    /// TTL（秒，仅当 EvictionPolicy = TTL）
    /// Time To Live (seconds, only when EvictionPolicy = TTL)
    /// Default: 3600 (1 hour)
    /// </summary>
    [Range(60, 86400)]
    public int TimeToLiveSeconds { get; set; } = 3600;
}
