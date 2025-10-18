using System.Diagnostics;
using BlazorIdle.Server.Application.Abstractions;
using BlazorIdle.Server.Config.Persistence;
using BlazorIdle.Server.Domain.Activities;
using BlazorIdle.Server.Domain.Characters;
using BlazorIdle.Server.Domain.Records;
using BlazorIdle.Server.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BlazorIdle.Server.Infrastructure.Memory;

/// <summary>
/// 持久化协调器 - 负责定期批量保存和关闭时强制保存
/// </summary>
public class PersistenceCoordinator : BackgroundService, IPersistenceCoordinator
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly PersistenceOptions _options;
    private readonly ILogger<PersistenceCoordinator> _logger;
    
    // 各类型实体的状态管理器
    private readonly IMemoryStateManager<Character> _characterManager;
    private readonly IMemoryStateManager<RunningBattleSnapshotRecord> _battleSnapshotManager;
    private readonly IMemoryStateManager<ActivityPlan> _activityPlanManager;
    
    // 保存时间跟踪
    private readonly Dictionary<string, DateTime> _lastSaveTimes;

    public PersistenceCoordinator(
        IServiceScopeFactory scopeFactory,
        IOptions<PersistenceOptions> options,
        ILogger<PersistenceCoordinator> logger,
        IMemoryStateManager<Character> characterManager,
        IMemoryStateManager<RunningBattleSnapshotRecord> battleSnapshotManager,
        IMemoryStateManager<ActivityPlan> activityPlanManager)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
        _characterManager = characterManager;
        _battleSnapshotManager = battleSnapshotManager;
        _activityPlanManager = activityPlanManager;
        _lastSaveTimes = new Dictionary<string, DateTime>();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // 如果未启用内存缓冲，则不运行
        if (!_options.EnableMemoryBuffering)
        {
            _logger.LogInformation("内存缓冲未启用，持久化协调器跳过运行");
            return;
        }

        _logger.LogInformation("持久化协调器已启动（保存间隔：{Interval}ms）", _options.SaveIntervalMs);
        
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_options.SaveIntervalMs, stoppingToken);
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
            }
        }
        
        _logger.LogInformation("持久化协调器已停止");
    }

    /// <summary>
    /// 定期保存 - 批量提交所有 Dirty 实体
    /// </summary>
    private async Task PeriodicSaveAsync(CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GameDbContext>();
        
        int totalSaved = 0;
        
        // 战斗快照（如果到了保存时间）
        if (ShouldSave("BattleSnapshot"))
        {
            totalSaved += await SaveEntityTypeAsync(
                _battleSnapshotManager, 
                db.RunningBattleSnapshots, 
                "BattleSnapshot",
                db,
                ct
            );
        }
        
        // 角色心跳
        if (ShouldSave("CharacterHeartbeat"))
        {
            totalSaved += await SaveEntityTypeAsync(
                _characterManager,
                db.Characters,
                "CharacterHeartbeat",
                db,
                ct
            );
        }
        
        // 活动计划
        if (ShouldSave("ActivityPlan"))
        {
            totalSaved += await SaveEntityTypeAsync(
                _activityPlanManager,
                db.ActivityPlans,
                "ActivityPlan",
                db,
                ct
            );
        }
        
        sw.Stop();
        
        if (totalSaved > 0)
        {
            _logger.LogInformation(
                "定期保存完成：{Count} 个实体，耗时 {ElapsedMs}ms",
                totalSaved, sw.ElapsedMilliseconds
            );
        }
    }

    /// <summary>
    /// 保存指定类型的实体
    /// </summary>
    private async Task<int> SaveEntityTypeAsync<T>(
        IMemoryStateManager<T> manager,
        DbSet<T> dbSet,
        string entityTypeName,
        GameDbContext db,
        CancellationToken ct) where T : class, IEntity
    {
        var dirtyEntities = manager.GetDirtyEntities().ToList();
        if (!dirtyEntities.Any())
            return 0;
        
        var maxBatch = GetMaxBatchSize(entityTypeName);
        var batches = dirtyEntities.Chunk(maxBatch);
        
        int saved = 0;
        foreach (var batch in batches)
        {
            foreach (var (id, entity) in batch)
            {
                dbSet.Update(entity);
            }
            
            try
            {
                await DatabaseRetryPolicy.SaveChangesWithRetryAsync(
                    db, 
                    ct, 
                    _logger
                );
                
                manager.ClearDirty(batch.Select(x => x.Id));
                saved += batch.Length;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "保存 {EntityType} 批次失败（批次大小：{BatchSize}）", 
                    entityTypeName, batch.Length);
                // 不抛出异常，继续处理其他批次
            }
        }
        
        // 更新最后保存时间
        _lastSaveTimes[entityTypeName] = DateTime.UtcNow;
        
        return saved;
    }

    /// <summary>
    /// 判断是否应该保存指定类型的实体
    /// </summary>
    private bool ShouldSave(string entityType)
    {
        if (!_options.EntitySaveStrategies.TryGetValue(entityType, out var strategy))
        {
            // 如果没有配置特定策略，使用默认保存间隔
            return true;
        }
        
        if (!_lastSaveTimes.TryGetValue(entityType, out var lastSaveTime))
        {
            // 第一次保存
            return true;
        }
        
        var elapsed = DateTime.UtcNow - lastSaveTime;
        return elapsed.TotalMilliseconds >= strategy.SaveIntervalMs;
    }

    /// <summary>
    /// 获取指定类型的最大批次大小
    /// </summary>
    private int GetMaxBatchSize(string entityType)
    {
        if (_options.EntitySaveStrategies.TryGetValue(entityType, out var strategy))
        {
            return strategy.MaxBatchSize;
        }
        return _options.MaxBatchSize;
    }

    /// <summary>
    /// 手动触发立即保存（管理员工具）
    /// </summary>
    public async Task TriggerImmediateSaveAsync(CancellationToken ct = default)
    {
        _logger.LogWarning("手动触发立即保存");
        await PeriodicSaveAsync(ct);
    }

    /// <summary>
    /// 触发指定类型实体的保存
    /// </summary>
    public async Task TriggerSaveAsync(string entityType, CancellationToken ct = default)
    {
        _logger.LogInformation("触发 {EntityType} 保存", entityType);
        
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GameDbContext>();
        
        switch (entityType.ToLower())
        {
            case "character":
            case "characterheartbeat":
                await SaveEntityTypeAsync(_characterManager, db.Characters, "CharacterHeartbeat", db, ct);
                break;
            case "battlesnapshot":
                await SaveEntityTypeAsync(_battleSnapshotManager, db.RunningBattleSnapshots, "BattleSnapshot", db, ct);
                break;
            case "activityplan":
                await SaveEntityTypeAsync(_activityPlanManager, db.ActivityPlans, "ActivityPlan", db, ct);
                break;
            default:
                _logger.LogWarning("未知的实体类型：{EntityType}", entityType);
                break;
        }
    }

    /// <summary>
    /// 最终保存 - 关闭时强制保存所有数据
    /// </summary>
    public async Task FinalSaveAsync(CancellationToken ct = default)
    {
        _logger.LogWarning("执行最终保存...");
        
        var sw = Stopwatch.StartNew();
        
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GameDbContext>();
        
        // 强制保存所有 Dirty 实体
        int totalSaved = 0;
        totalSaved += await SaveEntityTypeAsync(_battleSnapshotManager, db.RunningBattleSnapshots, "BattleSnapshot", db, ct);
        totalSaved += await SaveEntityTypeAsync(_characterManager, db.Characters, "CharacterHeartbeat", db, ct);
        totalSaved += await SaveEntityTypeAsync(_activityPlanManager, db.ActivityPlans, "ActivityPlan", db, ct);
        
        sw.Stop();
        
        _logger.LogInformation(
            "最终保存完成：{Count} 个实体，耗时 {ElapsedMs}ms",
            totalSaved, sw.ElapsedMilliseconds
        );
    }
}
