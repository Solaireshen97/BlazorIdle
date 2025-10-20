namespace BlazorIdle.Server.Infrastructure.DatabaseOptimization.Caching.Models;

/// <summary>
/// 缓存统计信息
/// Cache statistics
/// </summary>
public class CacheStatistics
{
    /// <summary>
    /// 总操作次数
    /// Total operations
    /// </summary>
    public long TotalOperations { get; set; }

    /// <summary>
    /// 命中次数
    /// Hit count
    /// </summary>
    public long Hits { get; set; }

    /// <summary>
    /// 未命中次数
    /// Miss count
    /// </summary>
    public long Misses { get; set; }

    /// <summary>
    /// 命中率
    /// Hit rate
    /// </summary>
    public double HitRate => TotalOperations > 0 ? (double)Hits / TotalOperations : 0;

    /// <summary>
    /// 平均操作耗时（毫秒）
    /// Average operation duration in milliseconds
    /// </summary>
    public double AvgDurationMs { get; set; }

    /// <summary>
    /// P95 操作耗时（毫秒）
    /// P95 operation duration in milliseconds
    /// </summary>
    public double P95DurationMs { get; set; }

    /// <summary>
    /// P99 操作耗时（毫秒）
    /// P99 operation duration in milliseconds
    /// </summary>
    public double P99DurationMs { get; set; }

    /// <summary>
    /// 各层级统计
    /// Statistics by tier
    /// </summary>
    public Dictionary<CacheTier, TierStatistics> TierStatistics { get; set; } = new();

    /// <summary>
    /// 当前缓存项总数
    /// Current total cache entries
    /// </summary>
    public int TotalEntries { get; set; }

    /// <summary>
    /// 预估内存使用（MB）
    /// Estimated memory usage in MB
    /// </summary>
    public double EstimatedMemoryMB { get; set; }
}

/// <summary>
/// 层级统计信息
/// Tier statistics
/// </summary>
public class TierStatistics
{
    /// <summary>
    /// 该层级总操作次数
    /// Total operations in this tier
    /// </summary>
    public int TotalOps { get; set; }

    /// <summary>
    /// 命中次数
    /// Hit count
    /// </summary>
    public int Hits { get; set; }

    /// <summary>
    /// 命中率
    /// Hit rate
    /// </summary>
    public double HitRate => TotalOps > 0 ? (double)Hits / TotalOps : 0;

    /// <summary>
    /// 当前缓存项数
    /// Current entry count
    /// </summary>
    public int EntryCount { get; set; }
}
