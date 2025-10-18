using System.ComponentModel.DataAnnotations;

namespace BlazorIdle.Server.Config.DatabaseOptimization;

/// <summary>
/// 数据库持久化优化配置选项
/// Database persistence optimization configuration options
/// </summary>
public class PersistenceOptions
{
    /// <summary>
    /// 是否启用内存缓冲（用于回退到原有实现）
    /// Enable memory buffering (for rollback to original implementation)
    /// </summary>
    public bool EnableMemoryBuffering { get; set; } = true;
    
    /// <summary>
    /// 定期保存间隔（毫秒）
    /// Periodic save interval (milliseconds)
    /// Default: 30000 (30 seconds)
    /// </summary>
    [Range(1000, 300000)]
    public int SaveIntervalMs { get; set; } = 30000;
    
    /// <summary>
    /// 批量保存大小限制
    /// Maximum batch size for saves
    /// Default: 1000
    /// </summary>
    [Range(100, 10000)]
    public int MaxBatchSize { get; set; } = 1000;
    
    /// <summary>
    /// 强制保存阈值（Dirty 实体数量）
    /// Force save threshold (number of dirty entities)
    /// Default: 5000
    /// </summary>
    [Range(1000, 100000)]
    public int ForceSaveThreshold { get; set; } = 5000;
    
    /// <summary>
    /// 保存失败重试次数
    /// Number of retry attempts on save failure
    /// Default: 3
    /// </summary>
    [Range(1, 10)]
    public int SaveRetryAttempts { get; set; } = 3;
    
    /// <summary>
    /// 不同实体类型的保存策略
    /// Save strategies for different entity types
    /// </summary>
    public Dictionary<string, EntitySaveStrategy> EntitySaveStrategies { get; set; } = new();
}

/// <summary>
/// 实体保存策略
/// Entity-specific save strategy
/// </summary>
public class EntitySaveStrategy
{
    /// <summary>
    /// 保存间隔（毫秒）
    /// Save interval (milliseconds)
    /// </summary>
    [Range(1000, 600000)]
    public int SaveIntervalMs { get; set; }
    
    /// <summary>
    /// 批量保存大小限制
    /// Maximum batch size
    /// </summary>
    [Range(10, 5000)]
    public int MaxBatchSize { get; set; }
}
