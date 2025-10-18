using System.ComponentModel.DataAnnotations;

namespace BlazorIdle.Server.Config.Persistence;

/// <summary>
/// 持久化配置选项
/// </summary>
public class PersistenceOptions
{
    /// <summary>
    /// 是否启用内存缓冲（用于回退）
    /// </summary>
    public bool EnableMemoryBuffering { get; set; } = true;
    
    /// <summary>
    /// 定期保存间隔（毫秒）
    /// </summary>
    [Range(1000, 300000)]
    public int SaveIntervalMs { get; set; } = 30000; // 30秒
    
    /// <summary>
    /// 批量保存大小限制
    /// </summary>
    [Range(100, 10000)]
    public int MaxBatchSize { get; set; } = 1000;
    
    /// <summary>
    /// 强制保存阈值（Dirty 实体数量）
    /// </summary>
    [Range(1000, 100000)]
    public int ForceSaveThreshold { get; set; } = 5000;
    
    /// <summary>
    /// 保存失败重试次数
    /// </summary>
    [Range(1, 10)]
    public int SaveRetryAttempts { get; set; } = 3;
    
    /// <summary>
    /// 不同实体类型的保存策略
    /// </summary>
    public Dictionary<string, EntitySaveStrategy> EntitySaveStrategies { get; set; } = new();
}

/// <summary>
/// 实体保存策略
/// </summary>
public class EntitySaveStrategy
{
    [Range(1000, 600000)]
    public int SaveIntervalMs { get; set; }
    
    [Range(10, 5000)]
    public int MaxBatchSize { get; set; }
}
