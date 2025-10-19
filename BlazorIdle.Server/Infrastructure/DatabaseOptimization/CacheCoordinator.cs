using BlazorIdle.Server.Config.DatabaseOptimization;
using BlazorIdle.Server.Domain.Characters;
using BlazorIdle.Server.Domain.Equipment.Models;
using BlazorIdle.Server.Domain.Activities;
using BlazorIdle.Server.Domain.Records;
using BlazorIdle.Server.Infrastructure.DatabaseOptimization.Abstractions;
using BlazorIdle.Server.Infrastructure.Persistence;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Configuration;

namespace BlazorIdle.Server.Infrastructure.DatabaseOptimization;

/// <summary>
/// 缓存协调器 - 管理缓存的预加载、清理和失效
/// Cache Coordinator - Manages cache preloading, cleanup, and invalidation
/// 
/// 职责 - Responsibilities:
/// 1. 启动时预加载静态配置数据（GearDefinition, Affix 等）
/// 2. 定期清理过期缓存（基于 TTL）
/// 3. 提供手动刷新缓存的接口
/// 4. 记录缓存监控指标
/// </summary>
public class CacheCoordinator : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<CacheConfiguration> _cacheConfig;
    private readonly ILogger<CacheCoordinator> _logger;
    private readonly DatabaseMetricsCollector? _metricsCollector;
    private readonly IConfiguration _configuration;
    
    // 各类型实体的内存管理器 - Memory managers for each entity type
    private readonly IMemoryStateManager<Character>? _characterManager;
    private readonly IMemoryStateManager<ActivityPlan>? _activityPlanManager;
    private readonly IMemoryStateManager<RunningBattleSnapshotRecord>? _snapshotManager;
    
    // 注意：GearDefinition, Affix, GearSet, GearInstance 暂不支持，因为它们未实现 IEntity
    // Note: GearDefinition, Affix, GearSet, GearInstance not supported yet as they don't implement IEntity
    // 将在 Phase 2 迁移 Repository 时添加支持
    // Support will be added in Phase 2 when migrating repositories
    
    public CacheCoordinator(
        IServiceScopeFactory scopeFactory,
        IOptions<CacheConfiguration> cacheConfig,
        ILogger<CacheCoordinator> logger,
        IConfiguration configuration,
        DatabaseMetricsCollector? metricsCollector = null,
        IMemoryStateManager<Character>? characterManager = null,
        IMemoryStateManager<ActivityPlan>? activityPlanManager = null,
        IMemoryStateManager<RunningBattleSnapshotRecord>? snapshotManager = null)
    {
        _scopeFactory = scopeFactory;
        _cacheConfig = cacheConfig;
        _logger = logger;
        _configuration = configuration;
        _metricsCollector = metricsCollector;
        _characterManager = characterManager;
        _activityPlanManager = activityPlanManager;
        _snapshotManager = snapshotManager;
    }
    
    /// <summary>
    /// 启动时预加载静态数据
    /// Preload static data on startup
    /// </summary>
    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("=== 缓存协调器启动 CacheCoordinator Starting ===");
        
        // 检查是否启用读取缓存
        if (!_cacheConfig.Value.GlobalSettings.EnableReadCaching)
        {
            _logger.LogWarning("读取缓存已禁用（EnableReadCaching = false）");
            return;
        }
        
        try
        {
            // 预加载静态配置数据
            await PreloadStaticDataAsync(cancellationToken);
            
            _logger.LogInformation("=== 缓存预加载完成 Cache Preloading Completed ===");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "缓存预加载失败 Cache preloading failed");
            // 不抛出异常，允许服务继续启动
        }
        
        await base.StartAsync(cancellationToken);
    }
    
    /// <summary>
    /// 预加载静态配置数据
    /// Preload static configuration data
    /// </summary>
    private async Task PreloadStaticDataAsync(CancellationToken ct)
    {
        _logger.LogInformation("Phase 1 暂时跳过静态数据预加载（GearDefinition, Affix 等需要实现 IEntity 接口）");
        _logger.LogInformation("Phase 1 temporarily skipping static data preload (GearDefinition, Affix etc need to implement IEntity)");
        _logger.LogInformation("这些实体将在 Phase 2 Repository 迁移时添加支持");
        _logger.LogInformation("Support for these entities will be added in Phase 2 during repository migration");
        
        await Task.CompletedTask;
        
        // TODO: Phase 2 - 添加 GearDefinition, Affix, GearSet 预加载
        // TODO: Phase 2 - Add GearDefinition, Affix, GearSet preloading
        // 需要先让这些实体实现 IEntity 接口
        // These entities need to implement IEntity interface first
    }
    
    /// <summary>
    /// 根据配置预加载特定实体类型
    /// Preload specific entity type based on configuration
    /// </summary>
    private async Task PreloadEntityIfConfiguredAsync<T>(
        string entityTypeName,
        IMemoryStateManager<T>? manager,
        GameDbContext db,
        CancellationToken ct) where T : class, IEntity
    {
        if (manager == null)
        {
            _logger.LogWarning(
                "跳过 {EntityType} 预加载：MemoryStateManager 未注册",
                entityTypeName
            );
            return;
        }
        
        // 检查配置
        if (!_cacheConfig.Value.EntityStrategies.TryGetValue(entityTypeName, out var strategy))
        {
            _logger.LogDebug(
                "跳过 {EntityType} 预加载：未配置缓存策略",
                entityTypeName
            );
            return;
        }
        
        if (!strategy.PreloadOnStartup)
        {
            _logger.LogDebug(
                "跳过 {EntityType} 预加载：PreloadOnStartup = false",
                entityTypeName
            );
            return;
        }
        
        // 执行预加载
        var sw = System.Diagnostics.Stopwatch.StartNew();
        
        await manager.PreloadFromDatabaseAsync(db, strategy.PreloadBatchSize, ct);
        
        sw.Stop();
        
        var stats = manager.GetCacheStatistics();
        _logger.LogInformation(
            "✓ {EntityType} 预加载完成: {Count} 条记录，耗时 {ElapsedMs}ms",
            entityTypeName, stats.CachedCount, sw.ElapsedMilliseconds
        );
    }
    
    /// <summary>
    /// 后台定期任务：清理过期缓存 + 输出统计信息
    /// Background periodic task: Clean expired cache + Log statistics
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_cacheConfig.Value.GlobalSettings.EnableReadCaching)
            return;
        
        var cleanupInterval = TimeSpan.FromMinutes(
            _cacheConfig.Value.GlobalSettings.CleanupIntervalMinutes
        );
        
        var hitRateLogInterval = TimeSpan.FromMinutes(
            _cacheConfig.Value.GlobalSettings.HitRateLogIntervalMinutes
        );
        
        _logger.LogInformation(
            "缓存清理任务启动，清理间隔: {CleanupMinutes} 分钟, 统计输出间隔: {HitRateMinutes} 分钟",
            cleanupInterval.TotalMinutes, hitRateLogInterval.TotalMinutes
        );
        
        var lastHitRateLog = DateTime.UtcNow;
        
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(cleanupInterval, stoppingToken);
                
                // 清理过期缓存
                await CleanupExpiredCachesAsync(stoppingToken);
                
                // 定期输出命中率统计
                if (_cacheConfig.Value.GlobalSettings.TrackCacheHitRate &&
                    DateTime.UtcNow - lastHitRateLog >= hitRateLogInterval)
                {
                    LogCacheStatistics();
                    lastHitRateLog = DateTime.UtcNow;
                }
            }
            catch (OperationCanceledException)
            {
                // 正常关闭
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "缓存清理任务出错");
            }
        }
        
        _logger.LogInformation("缓存清理任务已停止");
    }
    
    /// <summary>
    /// 清理过期缓存
    /// Clean expired cache entries
    /// </summary>
    private async Task CleanupExpiredCachesAsync(CancellationToken ct)
    {
        _logger.LogDebug("开始清理过期缓存...");
        
        var totalRemoved = 0;
        
        // 清理各类型实体的过期缓存
        totalRemoved += CleanupEntityIfConfigured<Character>(
            "Character", _characterManager);
        totalRemoved += CleanupEntityIfConfigured<ActivityPlan>(
            "ActivityPlan", _activityPlanManager);
        totalRemoved += CleanupEntityIfConfigured<RunningBattleSnapshotRecord>(
            "RunningBattleSnapshot", _snapshotManager);
        
        if (totalRemoved > 0)
        {
            _logger.LogInformation(
                "过期缓存清理完成: 总计移除 {Count} 个实体",
                totalRemoved
            );
        }
        else
        {
            _logger.LogDebug("过期缓存清理完成: 无过期项");
        }
        
        await Task.CompletedTask;
    }
    
    /// <summary>
    /// 清理单个实体类型的过期缓存
    /// Clean expired cache for a single entity type
    /// </summary>
    private int CleanupEntityIfConfigured<T>(
        string entityTypeName,
        IMemoryStateManager<T>? manager) where T : class, IEntity
    {
        if (manager == null)
            return 0;
        
        if (!_cacheConfig.Value.EntityStrategies.TryGetValue(entityTypeName, out var strategy))
            return 0;
        
        // 只清理 Temporary 策略的缓存
        if (strategy.Strategy != CacheStrategyType.Temporary)
            return 0;
        
        var removed = manager.ClearExpired(strategy.TtlSeconds);
        
        if (removed > 0)
        {
            _logger.LogDebug(
                "清理 {EntityType} 过期缓存: {Count} 个实体",
                entityTypeName, removed
            );
        }
        
        return removed;
    }
    
    /// <summary>
    /// 输出缓存统计信息
    /// Log cache statistics
    /// </summary>
    private void LogCacheStatistics()
    {
        _logger.LogInformation("=== 缓存统计报告 Cache Statistics Report ===");
        
        LogEntityStatistics("Character", _characterManager);
        LogEntityStatistics("ActivityPlan", _activityPlanManager);
        LogEntityStatistics("RunningBattleSnapshot", _snapshotManager);
        
        _logger.LogInformation("==========================================");
    }
    
    /// <summary>
    /// 输出单个实体类型的统计信息
    /// Log statistics for a single entity type
    /// </summary>
    private void LogEntityStatistics<T>(
        string entityType,
        IMemoryStateManager<T>? manager) where T : class, IEntity
    {
        if (manager == null)
            return;
        
        var stats = manager.GetCacheStatistics();
        
        // 检查命中率阈值
        var hitRateThreshold = _configuration.GetValue<double>(
            "Monitoring:CacheMonitoring:CacheHitRateThreshold", 70.0);
        var hitRatePercentage = stats.HitRate * 100.0;
        
        if (hitRatePercentage < hitRateThreshold && stats.CacheHits + stats.CacheMisses > 100)
        {
            _logger.LogWarning(
                "⚠️  {EntityType}: 缓存命中率过低 ({HitRate:F2}% < {Threshold:F2}%), " +
                "缓存 {CachedCount} 个, Dirty {DirtyCount} 个, " +
                "命中 {Hits} 次, 未命中 {Misses} 次",
                entityType, hitRatePercentage, hitRateThreshold,
                stats.CachedCount, stats.DirtyCount,
                stats.CacheHits, stats.CacheMisses
            );
        }
        else
        {
            _logger.LogInformation(
                "{EntityType}: 缓存 {CachedCount} 个, Dirty {DirtyCount} 个, " +
                "命中 {Hits} 次, 未命中 {Misses} 次, 命中率 {HitRate:P}",
                entityType, stats.CachedCount, stats.DirtyCount,
                stats.CacheHits, stats.CacheMisses, stats.HitRate
            );
        }
        
        // 记录到监控指标
        if (_metricsCollector != null)
        {
            for (int i = 0; i < stats.CacheHits; i++)
                _metricsCollector.RecordCacheHit(entityType);
            for (int i = 0; i < stats.CacheMisses; i++)
                _metricsCollector.RecordCacheMiss(entityType);
            _metricsCollector.RecordCacheSize(entityType, stats.CachedCount, stats.DirtyCount);
        }
    }
}
