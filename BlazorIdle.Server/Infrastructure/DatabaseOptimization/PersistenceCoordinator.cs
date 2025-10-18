using System.Diagnostics;
using BlazorIdle.Server.Config.DatabaseOptimization;
using BlazorIdle.Server.Domain.Characters;
using BlazorIdle.Server.Domain.Activities;
using BlazorIdle.Server.Domain.Records;
using BlazorIdle.Server.Infrastructure.DatabaseOptimization.Abstractions;
using BlazorIdle.Server.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BlazorIdle.Server.Infrastructure.DatabaseOptimization;

/// <summary>
/// 持久化协调器 - 负责定期批量保存和关闭时强制保存
/// Persistence coordinator - handles periodic batch saving and final save on shutdown
/// </summary>
/// <remarks>
/// 核心功能：
/// 1. 后台定期保存所有 Dirty 实体
/// 2. 按配置策略分别保存不同类型的实体
/// 3. 关闭时触发最终保存
/// 4. 保存失败自动重试
/// 5. 统计监控
/// 
/// Core features:
/// 1. Background periodic save of all dirty entities
/// 2. Save different entity types by configured strategy
/// 3. Trigger final save on shutdown
/// 4. Automatic retry on save failure
/// 5. Statistics monitoring
/// </remarks>
public class PersistenceCoordinator : BackgroundService, IPersistenceCoordinator
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly PersistenceOptions _options;
    private readonly ILogger<PersistenceCoordinator> _logger;
    
    // 各类型实体的状态管理器（如果启用了内存缓冲）
    // State managers for different entity types (if memory buffering is enabled)
    private readonly IMemoryStateManager<Character>? _characterManager;
    private readonly IMemoryStateManager<RunningBattleSnapshotRecord>? _battleSnapshotManager;
    private readonly IMemoryStateManager<ActivityPlan>? _activityPlanManager;
    
    // 性能指标收集器（可选）
    // Metrics collector (optional)
    private readonly DatabaseMetricsCollector? _metricsCollector;
    
    // 上次保存时间追踪（用于分实体类型的保存间隔）
    // Last save time tracking (for entity-specific save intervals)
    private readonly Dictionary<string, DateTime> _lastSaveTime = new();
    
    // 保存统计
    // Save statistics
    private SaveStatistics? _lastSaveStatistics;
    
    /// <summary>
    /// 构造函数
    /// </summary>
    public PersistenceCoordinator(
        IServiceScopeFactory scopeFactory,
        IOptions<PersistenceOptions> options,
        ILogger<PersistenceCoordinator> logger,
        IMemoryStateManager<Character>? characterManager = null,
        IMemoryStateManager<RunningBattleSnapshotRecord>? battleSnapshotManager = null,
        IMemoryStateManager<ActivityPlan>? activityPlanManager = null,
        DatabaseMetricsCollector? metricsCollector = null)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
        _characterManager = characterManager;
        _battleSnapshotManager = battleSnapshotManager;
        _activityPlanManager = activityPlanManager;
        _metricsCollector = metricsCollector;
        
        // 初始化上次保存时间
        _lastSaveTime["Character"] = DateTime.UtcNow;
        _lastSaveTime["BattleSnapshot"] = DateTime.UtcNow;
        _lastSaveTime["ActivityPlan"] = DateTime.UtcNow;
    }

    /// <inheritdoc />
    public SaveStatistics? LastSaveStatistics => _lastSaveStatistics;

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // 如果未启用内存缓冲，不运行后台服务
        if (!_options.EnableMemoryBuffering)
        {
            _logger.LogInformation("内存缓冲未启用，持久化协调器不运行");
            return;
        }
        
        _logger.LogInformation(
            "持久化协调器已启动，保存间隔：{IntervalMs}ms，批量大小：{BatchSize}",
            _options.SaveIntervalMs, _options.MaxBatchSize
        );
        
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // 等待指定间隔
                await Task.Delay(_options.SaveIntervalMs, stoppingToken);
                
                // 执行定期保存
                await PeriodicSaveAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("持久化协调器停止信号已接收");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "定期保存发生错误");
                // 继续运行，不因单次错误而停止
            }
        }
        
        // 关闭时最终保存
        _logger.LogWarning("持久化协调器正在关闭，执行最终保存...");
        await FinalSaveAsync(CancellationToken.None);
        _logger.LogInformation("持久化协调器已停止");
    }

    /// <summary>
    /// 定期保存 - 按策略保存不同类型的实体
    /// Periodic save - save different entity types by strategy
    /// </summary>
    private async Task PeriodicSaveAsync(CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        int totalSaved = 0;
        
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GameDbContext>();
        
        try
        {
            // 检查是否需要强制保存（Dirty实体过多）
            bool forceSave = CheckForceSave();
            
            // 角色心跳（如果启用且到了保存时间）
            if (_characterManager != null && (forceSave || ShouldSave("CharacterHeartbeat")))
            {
                int saved = await SaveEntityTypeAsync(
                    _characterManager,
                    db.Characters,
                    "Character",
                    ct
                );
                totalSaved += saved;
                
                if (saved > 0)
                {
                    _lastSaveTime["Character"] = DateTime.UtcNow;
                }
            }
            
            // 战斗快照（如果启用且到了保存时间）
            if (_battleSnapshotManager != null && (forceSave || ShouldSave("BattleSnapshot")))
            {
                int saved = await SaveEntityTypeAsync(
                    _battleSnapshotManager,
                    db.RunningBattleSnapshots,
                    "BattleSnapshot",
                    ct
                );
                totalSaved += saved;
                
                if (saved > 0)
                {
                    _lastSaveTime["BattleSnapshot"] = DateTime.UtcNow;
                }
            }
            
            // 活动计划（如果启用且到了保存时间）
            if (_activityPlanManager != null && (forceSave || ShouldSave("ActivityPlan")))
            {
                int saved = await SaveEntityTypeAsync(
                    _activityPlanManager,
                    db.ActivityPlans,
                    "ActivityPlan",
                    ct
                );
                totalSaved += saved;
                
                if (saved > 0)
                {
                    _lastSaveTime["ActivityPlan"] = DateTime.UtcNow;
                }
            }
            
            sw.Stop();
            
            // 记录内存状态指标
            // Record memory state metrics
            if (_characterManager != null)
            {
                _metricsCollector?.RecordMemoryState(
                    "Character",
                    _characterManager.Count,
                    _characterManager.DirtyCount
                );
            }
            
            if (_battleSnapshotManager != null)
            {
                _metricsCollector?.RecordMemoryState(
                    "BattleSnapshot",
                    _battleSnapshotManager.Count,
                    _battleSnapshotManager.DirtyCount
                );
            }
            
            if (_activityPlanManager != null)
            {
                _metricsCollector?.RecordMemoryState(
                    "ActivityPlan",
                    _activityPlanManager.Count,
                    _activityPlanManager.DirtyCount
                );
            }
            
            // 记录统计信息
            _lastSaveStatistics = new SaveStatistics(
                DateTime.UtcNow,
                totalSaved,
                sw.ElapsedMilliseconds,
                true
            );
            
            if (totalSaved > 0)
            {
                _logger.LogInformation(
                    "定期保存完成：{Count} 个实体，耗时 {ElapsedMs}ms{ForceSave}",
                    totalSaved,
                    sw.ElapsedMilliseconds,
                    forceSave ? " [强制保存]" : ""
                );
            }
            else
            {
                _logger.LogDebug("定期保存完成：无需保存");
            }
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "定期保存失败");
            
            _lastSaveStatistics = new SaveStatistics(
                DateTime.UtcNow,
                totalSaved,
                sw.ElapsedMilliseconds,
                false,
                ex.Message
            );
        }
    }

    /// <summary>
    /// 检查是否需要强制保存（Dirty实体数量超过阈值）
    /// Check if force save is needed (dirty entity count exceeds threshold)
    /// </summary>
    private bool CheckForceSave()
    {
        int totalDirty = 0;
        
        if (_characterManager != null)
            totalDirty += _characterManager.DirtyCount;
            
        if (_battleSnapshotManager != null)
            totalDirty += _battleSnapshotManager.DirtyCount;
            
        if (_activityPlanManager != null)
            totalDirty += _activityPlanManager.DirtyCount;
        
        return totalDirty >= _options.ForceSaveThreshold;
    }

    /// <summary>
    /// 判断指定类型的实体是否应该保存（根据配置的保存间隔）
    /// Check if specified entity type should be saved (based on configured save interval)
    /// </summary>
    private bool ShouldSave(string entityType)
    {
        // 获取该实体类型的保存策略
        if (!_options.EntitySaveStrategies.TryGetValue(entityType, out var strategy))
        {
            // 未配置策略，使用默认间隔
            return true;
        }
        
        // 检查距离上次保存是否超过配置的间隔
        if (!_lastSaveTime.TryGetValue(entityType, out var lastTime))
        {
            return true;
        }
        
        var elapsed = (DateTime.UtcNow - lastTime).TotalMilliseconds;
        return elapsed >= strategy.SaveIntervalMs;
    }

    /// <summary>
    /// 保存指定类型的实体
    /// Save entities of specified type
    /// </summary>
    private async Task<int> SaveEntityTypeAsync<TEntity>(
        IMemoryStateManager<TEntity> manager,
        DbSet<TEntity> dbSet,
        string entityTypeName,
        CancellationToken ct) where TEntity : class, IEntity
    {
        var dirtyEntities = manager.GetDirtyEntities().ToList();
        if (!dirtyEntities.Any())
        {
            _logger.LogTrace("无需保存 {EntityType}（无Dirty实体）", entityTypeName);
            return 0;
        }
        
        _logger.LogDebug(
            "开始保存 {EntityType}：{Count} 个Dirty实体",
            entityTypeName,
            dirtyEntities.Count
        );
        
        // 获取该实体类型的批量大小配置
        int maxBatchSize = _options.MaxBatchSize;
        if (_options.EntitySaveStrategies.TryGetValue(entityTypeName, out var strategy))
        {
            maxBatchSize = strategy.MaxBatchSize;
        }
        
        // 分批保存
        var batches = dirtyEntities.Chunk(maxBatchSize);
        int totalSaved = 0;
        int attemptCount = 0;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        
        foreach (var batch in batches)
        {
            bool success = false;
            Exception? lastException = null;
            
            // 重试机制
            for (int attempt = 1; attempt <= _options.SaveRetryAttempts; attempt++)
            {
                attemptCount++;
                
                try
                {
                    // 使用新的 DbContext scope
                    using var scope = _scopeFactory.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<GameDbContext>();
                    
                    // 批量附加到 DbContext 并标记为 Modified
                    foreach (var (id, entity) in batch)
                    {
                        db.Set<TEntity>().Update(entity);
                    }
                    
                    // 保存
                    await db.SaveChangesAsync(ct);
                    
                    // 清除 Dirty 标记
                    manager.ClearDirty(batch.Select(x => x.Id));
                    
                    totalSaved += batch.Length;
                    success = true;
                    
                    _logger.LogTrace(
                        "批次保存成功：{EntityType} {Count} 个实体（尝试 {Attempt}/{MaxAttempts}）",
                        entityTypeName,
                        batch.Length,
                        attempt,
                        _options.SaveRetryAttempts
                    );
                    
                    // 记录成功指标
                    _metricsCollector?.RecordSaveOperation(
                        entityTypeName,
                        batch.Length,
                        sw.ElapsedMilliseconds,
                        true
                    );
                    
                    break; // 成功，退出重试循环
                }
                catch (DbUpdateConcurrencyException ex)
                {
                    lastException = ex;
                    _logger.LogWarning(
                        ex,
                        "保存 {EntityType} 发生并发冲突（尝试 {Attempt}/{MaxAttempts}）",
                        entityTypeName,
                        attempt,
                        _options.SaveRetryAttempts
                    );
                    
                    // 并发冲突，短暂等待后重试
                    if (attempt < _options.SaveRetryAttempts)
                    {
                        await Task.Delay(100 * attempt, ct); // 指数退避
                    }
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    _logger.LogError(
                        ex,
                        "保存 {EntityType} 失败（尝试 {Attempt}/{MaxAttempts}）",
                        entityTypeName,
                        attempt,
                        _options.SaveRetryAttempts
                    );
                    
                    // 其他错误，重试前稍作等待
                    if (attempt < _options.SaveRetryAttempts)
                    {
                        await Task.Delay(200 * attempt, ct);
                    }
                }
            }
            
            if (!success)
            {
                // 记录失败指标
                _metricsCollector?.RecordSaveOperation(
                    entityTypeName,
                    batch.Length,
                    sw.ElapsedMilliseconds,
                    false
                );
                
                _logger.LogError(
                    lastException,
                    "保存 {EntityType} 批次失败（已重试 {Attempts} 次），跳过此批次",
                    entityTypeName,
                    _options.SaveRetryAttempts
                );
                // 跳过此批次，继续下一批次
            }
        }
        
        if (totalSaved > 0)
        {
            _logger.LogInformation(
                "成功保存 {EntityType}：{Saved}/{Total} 个实体",
                entityTypeName,
                totalSaved,
                dirtyEntities.Count
            );
        }
        
        return totalSaved;
    }

    /// <inheritdoc />
    public async Task TriggerSaveAsync(string entityType, CancellationToken ct = default)
    {
        if (!_options.EnableMemoryBuffering)
        {
            _logger.LogDebug("内存缓冲未启用，TriggerSaveAsync 无操作");
            return;
        }
        
        _logger.LogInformation("手动触发保存：{EntityType}", entityType);
        
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GameDbContext>();
        
        int saved = 0;
        
        try
        {
            switch (entityType)
            {
                case "Character" when _characterManager != null:
                    saved = await SaveEntityTypeAsync(_characterManager, db.Characters, "Character", ct);
                    _lastSaveTime["Character"] = DateTime.UtcNow;
                    break;
                    
                case "BattleSnapshot" when _battleSnapshotManager != null:
                    saved = await SaveEntityTypeAsync(_battleSnapshotManager, db.RunningBattleSnapshots, "BattleSnapshot", ct);
                    _lastSaveTime["BattleSnapshot"] = DateTime.UtcNow;
                    break;
                    
                case "ActivityPlan" when _activityPlanManager != null:
                    saved = await SaveEntityTypeAsync(_activityPlanManager, db.ActivityPlans, "ActivityPlan", ct);
                    _lastSaveTime["ActivityPlan"] = DateTime.UtcNow;
                    break;
                    
                default:
                    _logger.LogWarning("未知的实体类型或管理器未启用：{EntityType}", entityType);
                    break;
            }
            
            _logger.LogInformation("手动触发保存完成：{EntityType}，保存了 {Count} 个实体", entityType, saved);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "手动触发保存失败：{EntityType}", entityType);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task FinalSaveAsync(CancellationToken ct = default)
    {
        if (!_options.EnableMemoryBuffering)
        {
            _logger.LogDebug("内存缓冲未启用，FinalSaveAsync 无操作");
            return;
        }
        
        _logger.LogWarning("========================================");
        _logger.LogWarning("执行最终保存（关闭时）");
        _logger.LogWarning("========================================");
        
        var sw = Stopwatch.StartNew();
        int totalSaved = 0;
        
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<GameDbContext>();
            
            // 强制保存所有 Dirty 实体（不检查保存间隔）
            if (_characterManager != null)
            {
                var saved = await SaveEntityTypeAsync(_characterManager, db.Characters, "Character", ct);
                totalSaved += saved;
                _logger.LogInformation("[1/3] Character: {Count} 个实体已保存", saved);
            }
            
            if (_battleSnapshotManager != null)
            {
                var saved = await SaveEntityTypeAsync(_battleSnapshotManager, db.RunningBattleSnapshots, "BattleSnapshot", ct);
                totalSaved += saved;
                _logger.LogInformation("[2/3] BattleSnapshot: {Count} 个实体已保存", saved);
            }
            
            if (_activityPlanManager != null)
            {
                var saved = await SaveEntityTypeAsync(_activityPlanManager, db.ActivityPlans, "ActivityPlan", ct);
                totalSaved += saved;
                _logger.LogInformation("[3/3] ActivityPlan: {Count} 个实体已保存", saved);
            }
            
            sw.Stop();
            
            _lastSaveStatistics = new SaveStatistics(
                DateTime.UtcNow,
                totalSaved,
                sw.ElapsedMilliseconds,
                true
            );
            
            _logger.LogWarning("========================================");
            _logger.LogWarning(
                "最终保存完成：{Total} 个实体，耗时 {ElapsedMs}ms",
                totalSaved,
                sw.ElapsedMilliseconds
            );
            _logger.LogWarning("========================================");
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "最终保存失败");
            
            _lastSaveStatistics = new SaveStatistics(
                DateTime.UtcNow,
                totalSaved,
                sw.ElapsedMilliseconds,
                false,
                ex.Message
            );
            
            throw;
        }
    }
}
