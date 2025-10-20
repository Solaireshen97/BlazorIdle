using System.Collections.Concurrent;
using System.Diagnostics;
using BlazorIdle.Server.Config.DatabaseOptimization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BlazorIdle.Server.Infrastructure.DatabaseOptimization;

/// <summary>
/// 数据库性能指标收集器
/// Database performance metrics collector
/// </summary>
/// <remarks>
/// 收集和聚合数据库优化相关的性能指标，用于监控和诊断。
/// Collects and aggregates database optimization performance metrics for monitoring and diagnostics.
/// 
/// 核心功能：
/// 1. 保存操作统计（频率、耗时、成功率）
/// 2. 内存使用统计（缓存数量、dirty实体数量）
/// 3. 清理操作统计（LRU清理频率）
/// 4. 性能指标聚合（平均值、P95、P99）
/// 
/// Core features:
/// 1. Save operation statistics (frequency, duration, success rate)
/// 2. Memory usage statistics (cache count, dirty entity count)
/// 3. Eviction statistics (LRU eviction frequency)
/// 4. Performance metrics aggregation (average, P95, P99)
/// </remarks>
public class DatabaseMetricsCollector
{
    private readonly ConcurrentDictionary<string, MetricCounter> _counters = new();
    private readonly ConcurrentQueue<SaveOperationMetric> _recentSaves = new();
    private readonly ConcurrentQueue<EvictionMetric> _recentEvictions = new();
    private readonly MonitoringOptions _options;
    private readonly ILogger<DatabaseMetricsCollector> _logger;
    
    /// <summary>
    /// 构造函数
    /// Constructor
    /// </summary>
    public DatabaseMetricsCollector(
        IOptions<MonitoringOptions> options,
        ILogger<DatabaseMetricsCollector> logger)
    {
        _options = options.Value;
        _logger = logger;
        
        _logger.LogInformation(
            "DatabaseMetricsCollector 已初始化，MaxRecentOperations={MaxRecent}, EnableDetailedLogging={DetailedLogging}",
            _options.MaxRecentOperations, _options.EnableDetailedLogging
        );
    }
    
    /// <summary>
    /// 记录保存操作指标
    /// Record save operation metric
    /// </summary>
    /// <param name="entityType">实体类型 / Entity type</param>
    /// <param name="entityCount">保存的实体数量 / Number of entities saved</param>
    /// <param name="durationMs">操作耗时（毫秒）/ Duration in milliseconds</param>
    /// <param name="success">是否成功 / Whether operation succeeded</param>
    public void RecordSaveOperation(string entityType, int entityCount, long durationMs, bool success)
    {
        var metric = new SaveOperationMetric
        {
            Timestamp = DateTime.UtcNow,
            EntityType = entityType,
            EntityCount = entityCount,
            DurationMs = durationMs,
            Success = success
        };
        
        _recentSaves.Enqueue(metric);
        
        // 限制队列大小
        // Limit queue size
        while (_recentSaves.Count > _options.MaxRecentOperations)
        {
            _recentSaves.TryDequeue(out _);
        }
        
        // 详细日志（如果启用）
        // Detailed logging (if enabled)
        if (_options.EnableDetailedLogging)
        {
            _logger.LogDebug(
                "保存操作记录: {EntityType}, Count={Count}, Duration={Duration}ms, Success={Success}",
                entityType, entityCount, durationMs, success
            );
        }
        
        // 更新计数器
        // Update counters
        IncrementCounter($"{entityType}.SaveOperations");
        if (success)
        {
            IncrementCounter($"{entityType}.SaveSuccess");
            AddToCounter($"{entityType}.SaveDurationMs", durationMs);
            AddToCounter($"{entityType}.EntitiesSaved", entityCount);
        }
        else
        {
            IncrementCounter($"{entityType}.SaveFailure");
        }
    }
    
    /// <summary>
    /// 记录缓存清理操作
    /// Record cache eviction operation
    /// </summary>
    /// <param name="entityType">实体类型 / Entity type</param>
    /// <param name="evictedCount">清理的实体数量 / Number of entities evicted</param>
    /// <param name="remainingCount">剩余实体数量 / Number of remaining entities</param>
    public void RecordEviction(string entityType, int evictedCount, int remainingCount)
    {
        var metric = new EvictionMetric
        {
            Timestamp = DateTime.UtcNow,
            EntityType = entityType,
            EvictedCount = evictedCount,
            RemainingCount = remainingCount
        };
        
        _recentEvictions.Enqueue(metric);
        
        // 限制队列大小
        while (_recentEvictions.Count > _options.MaxRecentOperations)
        {
            _recentEvictions.TryDequeue(out _);
        }
        
        // 详细日志（如果启用）
        // Detailed logging (if enabled)
        if (_options.EnableDetailedLogging)
        {
            _logger.LogDebug(
                "清理操作记录: {EntityType}, Evicted={Evicted}, Remaining={Remaining}",
                entityType, evictedCount, remainingCount
            );
        }
        
        // 更新计数器
        IncrementCounter($"{entityType}.Evictions");
        AddToCounter($"{entityType}.EntitiesEvicted", evictedCount);
    }
    
    /// <summary>
    /// 记录当前内存状态
    /// Record current memory state
    /// </summary>
    /// <param name="entityType">实体类型 / Entity type</param>
    /// <param name="cachedCount">缓存实体数量 / Number of cached entities</param>
    /// <param name="dirtyCount">Dirty实体数量 / Number of dirty entities</param>
    public void RecordMemoryState(string entityType, int cachedCount, int dirtyCount)
    {
        SetCounter($"{entityType}.CachedCount", cachedCount);
        SetCounter($"{entityType}.DirtyCount", dirtyCount);
    }
    
    /// <summary>
    /// 获取保存操作摘要
    /// Get save operation summary
    /// </summary>
    /// <param name="entityType">实体类型（可选）/ Entity type (optional)</param>
    /// <param name="minutes">统计时间窗口（分钟）/ Time window in minutes</param>
    public SaveOperationSummary GetSaveOperationSummary(string? entityType = null, int minutes = 10)
    {
        var cutoff = DateTime.UtcNow.AddMinutes(-minutes);
        var operations = _recentSaves
            .Where(m => m.Timestamp >= cutoff)
            .Where(m => entityType == null || m.EntityType == entityType)
            .ToList();
        
        if (operations.Count == 0)
        {
            return new SaveOperationSummary
            {
                TimeWindowMinutes = minutes,
                OperationCount = 0
            };
        }
        
        var durations = operations.Select(o => o.DurationMs).OrderBy(d => d).ToList();
        
        return new SaveOperationSummary
        {
            TimeWindowMinutes = minutes,
            OperationCount = operations.Count,
            SuccessCount = operations.Count(o => o.Success),
            FailureCount = operations.Count(o => !o.Success),
            TotalEntitiesSaved = operations.Sum(o => o.EntityCount),
            AverageDurationMs = operations.Average(o => o.DurationMs),
            P95DurationMs = GetPercentile(durations, 0.95),
            P99DurationMs = GetPercentile(durations, 0.99),
            MaxDurationMs = durations.Max()
        };
    }
    
    /// <summary>
    /// 获取清理操作摘要
    /// Get eviction operation summary
    /// </summary>
    /// <param name="entityType">实体类型（可选）/ Entity type (optional)</param>
    /// <param name="minutes">统计时间窗口（分钟）/ Time window in minutes</param>
    public EvictionSummary GetEvictionSummary(string? entityType = null, int minutes = 60)
    {
        var cutoff = DateTime.UtcNow.AddMinutes(-minutes);
        var evictions = _recentEvictions
            .Where(m => m.Timestamp >= cutoff)
            .Where(m => entityType == null || m.EntityType == entityType)
            .ToList();
        
        return new EvictionSummary
        {
            TimeWindowMinutes = minutes,
            EvictionCount = evictions.Count,
            TotalEntitiesEvicted = evictions.Sum(e => e.EvictedCount),
            AverageEntitiesPerEviction = evictions.Count > 0 
                ? evictions.Average(e => e.EvictedCount) 
                : 0
        };
    }
    
    /// <summary>
    /// 获取所有指标计数器
    /// Get all metric counters
    /// </summary>
    public Dictionary<string, long> GetAllCounters()
    {
        return _counters.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.Value
        );
    }
    
    /// <summary>
    /// 重置所有指标
    /// Reset all metrics
    /// </summary>
    public void Reset()
    {
        _counters.Clear();
        while (_recentSaves.TryDequeue(out _)) { }
        while (_recentEvictions.TryDequeue(out _)) { }
    }
    
    // 辅助方法 / Helper methods
    
    private void IncrementCounter(string key)
    {
        _counters.AddOrUpdate(key, 
            _ => new MetricCounter { Value = 1 }, 
            (_, counter) => { counter.Value++; return counter; });
    }
    
    private void AddToCounter(string key, long value)
    {
        _counters.AddOrUpdate(key,
            _ => new MetricCounter { Value = value },
            (_, counter) => { counter.Value += value; return counter; });
    }
    
    private void SetCounter(string key, long value)
    {
        _counters.AddOrUpdate(key,
            _ => new MetricCounter { Value = value },
            (_, counter) => { counter.Value = value; return counter; });
    }
    
    private static double GetPercentile(List<long> sortedValues, double percentile)
    {
        if (sortedValues.Count == 0) return 0;
        
        var index = (int)Math.Ceiling(sortedValues.Count * percentile) - 1;
        index = Math.Max(0, Math.Min(sortedValues.Count - 1, index));
        return sortedValues[index];
    }
    
    // 内部类型 / Internal types
    
    private class MetricCounter
    {
        public long Value { get; set; }
    }
    
    private class SaveOperationMetric
    {
        public DateTime Timestamp { get; set; }
        public string EntityType { get; set; } = string.Empty;
        public int EntityCount { get; set; }
        public long DurationMs { get; set; }
        public bool Success { get; set; }
    }
    
    private class EvictionMetric
    {
        public DateTime Timestamp { get; set; }
        public string EntityType { get; set; } = string.Empty;
        public int EvictedCount { get; set; }
        public int RemainingCount { get; set; }
    }
}

/// <summary>
/// 保存操作摘要
/// Save operation summary
/// </summary>
public class SaveOperationSummary
{
    /// <summary>统计时间窗口（分钟）/ Time window in minutes</summary>
    public int TimeWindowMinutes { get; set; }
    
    /// <summary>操作总数 / Total operations</summary>
    public int OperationCount { get; set; }
    
    /// <summary>成功次数 / Success count</summary>
    public int SuccessCount { get; set; }
    
    /// <summary>失败次数 / Failure count</summary>
    public int FailureCount { get; set; }
    
    /// <summary>总保存实体数 / Total entities saved</summary>
    public int TotalEntitiesSaved { get; set; }
    
    /// <summary>平均耗时（毫秒）/ Average duration in ms</summary>
    public double AverageDurationMs { get; set; }
    
    /// <summary>P95 耗时（毫秒）/ P95 duration in ms</summary>
    public double P95DurationMs { get; set; }
    
    /// <summary>P99 耗时（毫秒）/ P99 duration in ms</summary>
    public double P99DurationMs { get; set; }
    
    /// <summary>最大耗时（毫秒）/ Max duration in ms</summary>
    public long MaxDurationMs { get; set; }
    
    /// <summary>成功率 / Success rate</summary>
    public double SuccessRate => OperationCount > 0 
        ? (double)SuccessCount / OperationCount * 100 
        : 0;
}

/// <summary>
/// 清理操作摘要
/// Eviction operation summary
/// </summary>
public class EvictionSummary
{
    /// <summary>统计时间窗口（分钟）/ Time window in minutes</summary>
    public int TimeWindowMinutes { get; set; }
    
    /// <summary>清理次数 / Eviction count</summary>
    public int EvictionCount { get; set; }
    
    /// <summary>总清理实体数 / Total entities evicted</summary>
    public int TotalEntitiesEvicted { get; set; }
    
    /// <summary>每次平均清理数 / Average entities per eviction</summary>
    public double AverageEntitiesPerEviction { get; set; }
}
