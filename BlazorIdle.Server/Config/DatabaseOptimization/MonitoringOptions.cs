using System.ComponentModel.DataAnnotations;

namespace BlazorIdle.Server.Config.DatabaseOptimization;

/// <summary>
/// 数据库优化监控配置选项
/// Database optimization monitoring configuration options
/// </summary>
/// <remarks>
/// 配置数据库性能指标收集器的行为参数。
/// Configures behavior parameters for database performance metrics collector.
/// </remarks>
public class MonitoringOptions
{
    /// <summary>
    /// 保留的最近操作记录数量（用于统计和分析）
    /// Number of recent operation records to keep (for statistics and analysis)
    /// </summary>
    /// <remarks>
    /// 较大的值可以提供更准确的统计数据，但会消耗更多内存。
    /// 建议范围：50-500
    /// 
    /// Larger values provide more accurate statistics but consume more memory.
    /// Recommended range: 50-500
    /// </remarks>
    [Range(10, 1000, ErrorMessage = "MaxRecentOperations must be between 10 and 1000")]
    public int MaxRecentOperations { get; set; } = 100;

    /// <summary>
    /// 是否启用详细的性能日志
    /// Whether to enable detailed performance logging
    /// </summary>
    /// <remarks>
    /// 启用后会记录更详细的操作信息，但可能影响性能。
    /// 建议仅在调试或故障排查时启用。
    /// 
    /// When enabled, logs more detailed operation information but may impact performance.
    /// Recommended to enable only during debugging or troubleshooting.
    /// </remarks>
    public bool EnableDetailedLogging { get; set; } = false;

    /// <summary>
    /// 指标统计的默认时间窗口（分钟）
    /// Default time window for metrics statistics (minutes)
    /// </summary>
    /// <remarks>
    /// 用于计算统计摘要时的默认时间范围。
    /// Default time range when calculating statistical summaries.
    /// </remarks>
    [Range(1, 1440, ErrorMessage = "DefaultTimeWindowMinutes must be between 1 and 1440 (24 hours)")]
    public int DefaultTimeWindowMinutes { get; set; } = 10;

    /// <summary>
    /// 内存状态快照的采集间隔（秒）
    /// Memory state snapshot collection interval (seconds)
    /// </summary>
    /// <remarks>
    /// 控制 PersistenceCoordinator 记录内存状态指标的频率。
    /// 0 表示每次保存操作都记录。
    /// 
    /// Controls how frequently PersistenceCoordinator records memory state metrics.
    /// 0 means record on every save operation.
    /// </remarks>
    [Range(0, 3600, ErrorMessage = "MemoryStateSnapshotIntervalSeconds must be between 0 and 3600")]
    public int MemoryStateSnapshotIntervalSeconds { get; set; } = 0;
}
