using System.ComponentModel.DataAnnotations;

namespace BlazorIdle.Server.Config.Persistence;

/// <summary>
/// 内存缓存配置选项
/// </summary>
public class MemoryCacheOptions
{
    /// <summary>
    /// 内存缓存最大实体数（防止内存溢出）
    /// </summary>
    [Range(1000, 1000000)]
    public int MaxCachedEntities { get; set; } = 100000;
    
    /// <summary>
    /// 超过阈值时的清理策略（LRU / TTL）
    /// </summary>
    public string EvictionPolicy { get; set; } = "LRU";
    
    /// <summary>
    /// TTL（秒，仅当 EvictionPolicy = TTL）
    /// </summary>
    [Range(60, 86400)]
    public int TimeToLiveSeconds { get; set; } = 3600;
}
