namespace BlazorIdle.Server.Infrastructure.DatabaseOptimization.Abstractions;

/// <summary>
/// 持久化协调器接口
/// Persistence coordinator interface
/// </summary>
/// <remarks>
/// 职责：
/// 1. 定期批量保存 Dirty 实体
/// 2. 按优先级和策略保存
/// 3. 关闭时强制保存所有数据
/// 4. 处理保存失败和重试
/// 
/// Responsibilities:
/// 1. Periodically batch save dirty entities
/// 2. Save by priority and strategy
/// 3. Force save all data on shutdown
/// 4. Handle save failures and retries
/// </remarks>
public interface IPersistenceCoordinator
{
    /// <summary>
    /// 触发立即保存指定类型的实体
    /// Trigger immediate save for specified entity type
    /// </summary>
    /// <param name="entityType">实体类型名称 / Entity type name (e.g., "Character", "BattleSnapshot")</param>
    /// <param name="ct">取消令牌 / Cancellation token</param>
    Task TriggerSaveAsync(string entityType, CancellationToken ct = default);
    
    /// <summary>
    /// 执行最终保存（关闭时调用）
    /// Execute final save (called on shutdown)
    /// </summary>
    /// <param name="ct">取消令牌 / Cancellation token</param>
    Task FinalSaveAsync(CancellationToken ct = default);
    
    /// <summary>
    /// 获取上次保存的统计信息
    /// Get statistics of last save operation
    /// </summary>
    SaveStatistics? LastSaveStatistics { get; }
}

/// <summary>
/// 保存操作统计信息
/// Save operation statistics
/// </summary>
public record SaveStatistics(
    /// <summary>保存时间 / Save timestamp</summary>
    DateTime Timestamp,
    
    /// <summary>保存的实体数量 / Number of entities saved</summary>
    int EntitiesSaved,
    
    /// <summary>保存耗时（毫秒）/ Duration in milliseconds</summary>
    long DurationMs,
    
    /// <summary>是否成功 / Success flag</summary>
    bool Success,
    
    /// <summary>错误信息（如果失败）/ Error message (if failed)</summary>
    string? ErrorMessage = null
);
