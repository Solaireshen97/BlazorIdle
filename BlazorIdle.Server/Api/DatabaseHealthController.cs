using BlazorIdle.Server.Domain.Activities;
using BlazorIdle.Server.Domain.Characters;
using BlazorIdle.Server.Domain.Records;
using BlazorIdle.Server.Infrastructure.DatabaseOptimization;
using BlazorIdle.Server.Infrastructure.DatabaseOptimization.Abstractions;
using Microsoft.AspNetCore.Mvc;

namespace BlazorIdle.Server.Api;

/// <summary>
/// 数据库健康检查和诊断 API
/// Database health check and diagnostics API
/// </summary>
/// <remarks>
/// 提供数据库优化系统的实时状态监控和诊断信息。
/// Provides real-time status monitoring and diagnostic information for the database optimization system.
/// 
/// 端点功能：
/// 1. /health - 整体健康状态
/// 2. /metrics - 性能指标摘要
/// 3. /status - 详细状态信息
/// 4. /memory-state - 内存状态快照
/// 
/// Endpoint features:
/// 1. /health - Overall health status
/// 2. /metrics - Performance metrics summary
/// 3. /status - Detailed status information
/// 4. /memory-state - Memory state snapshot
/// </remarks>
[ApiController]
[Route("api/database")]
public class DatabaseHealthController : ControllerBase
{
    private readonly ILogger<DatabaseHealthController> _logger;
    private readonly IPersistenceCoordinator _persistenceCoordinator;
    private readonly IMemoryStateManager<Character>? _characterManager;
    private readonly IMemoryStateManager<RunningBattleSnapshotRecord>? _battleSnapshotManager;
    private readonly IMemoryStateManager<ActivityPlan>? _activityPlanManager;
    private readonly DatabaseMetricsCollector? _metricsCollector;
    
    public DatabaseHealthController(
        ILogger<DatabaseHealthController> logger,
        IPersistenceCoordinator persistenceCoordinator,
        IMemoryStateManager<Character>? characterManager = null,
        IMemoryStateManager<RunningBattleSnapshotRecord>? battleSnapshotManager = null,
        IMemoryStateManager<ActivityPlan>? activityPlanManager = null,
        DatabaseMetricsCollector? metricsCollector = null)
    {
        _logger = logger;
        _persistenceCoordinator = persistenceCoordinator;
        _characterManager = characterManager;
        _battleSnapshotManager = battleSnapshotManager;
        _activityPlanManager = activityPlanManager;
        _metricsCollector = metricsCollector;
    }
    
    /// <summary>
    /// 获取数据库优化系统健康状态
    /// Get database optimization system health status
    /// </summary>
    /// <returns>健康状态信息 / Health status information</returns>
    [HttpGet("health")]
    public IActionResult GetHealth()
    {
        try
        {
            var health = new
            {
                Status = "healthy",
                Timestamp = DateTime.UtcNow,
                MemoryBuffering = new
                {
                    Enabled = _characterManager != null,
                    Characters = new
                    {
                        Cached = _characterManager?.Count ?? 0,
                        Dirty = _characterManager?.DirtyCount ?? 0
                    },
                    BattleSnapshots = new
                    {
                        Cached = _battleSnapshotManager?.Count ?? 0,
                        Dirty = _battleSnapshotManager?.DirtyCount ?? 0
                    },
                    ActivityPlans = new
                    {
                        Cached = _activityPlanManager?.Count ?? 0,
                        Dirty = _activityPlanManager?.DirtyCount ?? 0
                    }
                },
                LastSave = _persistenceCoordinator.LastSaveStatistics
            };
            
            return Ok(health);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取健康状态时发生错误");
            return StatusCode(500, new { Status = "error", Message = ex.Message });
        }
    }
    
    /// <summary>
    /// 获取性能指标摘要
    /// Get performance metrics summary
    /// </summary>
    /// <param name="minutes">统计时间窗口（分钟），默认10分钟 / Time window in minutes, default 10</param>
    /// <returns>性能指标摘要 / Performance metrics summary</returns>
    [HttpGet("metrics")]
    public IActionResult GetMetrics([FromQuery] int minutes = 10)
    {
        try
        {
            if (_metricsCollector == null)
            {
                return Ok(new
                {
                    Message = "指标收集器未启用 / Metrics collector not enabled",
                    Timestamp = DateTime.UtcNow
                });
            }
            
            var metrics = new
            {
                Timestamp = DateTime.UtcNow,
                TimeWindowMinutes = minutes,
                SaveOperations = new
                {
                    Overall = _metricsCollector.GetSaveOperationSummary(null, minutes),
                    ByEntityType = new
                    {
                        Character = _metricsCollector.GetSaveOperationSummary("Character", minutes),
                        BattleSnapshot = _metricsCollector.GetSaveOperationSummary("BattleSnapshot", minutes),
                        ActivityPlan = _metricsCollector.GetSaveOperationSummary("ActivityPlan", minutes)
                    }
                },
                Evictions = new
                {
                    Overall = _metricsCollector.GetEvictionSummary(null, 60),
                    ByEntityType = new
                    {
                        Character = _metricsCollector.GetEvictionSummary("Character", 60),
                        BattleSnapshot = _metricsCollector.GetEvictionSummary("BattleSnapshot", 60),
                        ActivityPlan = _metricsCollector.GetEvictionSummary("ActivityPlan", 60)
                    }
                },
                Counters = _metricsCollector.GetAllCounters()
            };
            
            return Ok(metrics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取性能指标时发生错误");
            return StatusCode(500, new { Status = "error", Message = ex.Message });
        }
    }
    
    /// <summary>
    /// 获取详细状态信息
    /// Get detailed status information
    /// </summary>
    /// <returns>详细状态信息 / Detailed status information</returns>
    [HttpGet("status")]
    public IActionResult GetStatus()
    {
        try
        {
            var status = new
            {
                Timestamp = DateTime.UtcNow,
                MemoryBuffering = new
                {
                    Enabled = _characterManager != null,
                    Managers = new
                    {
                        Character = _characterManager != null ? new
                        {
                            CachedCount = _characterManager.Count,
                            DirtyCount = _characterManager.DirtyCount,
                            DirtyPercentage = _characterManager.Count > 0 
                                ? Math.Round((double)_characterManager.DirtyCount / _characterManager.Count * 100, 2)
                                : 0
                        } : null,
                        BattleSnapshot = _battleSnapshotManager != null ? new
                        {
                            CachedCount = _battleSnapshotManager.Count,
                            DirtyCount = _battleSnapshotManager.DirtyCount,
                            DirtyPercentage = _battleSnapshotManager.Count > 0
                                ? Math.Round((double)_battleSnapshotManager.DirtyCount / _battleSnapshotManager.Count * 100, 2)
                                : 0
                        } : null,
                        ActivityPlan = _activityPlanManager != null ? new
                        {
                            CachedCount = _activityPlanManager.Count,
                            DirtyCount = _activityPlanManager.DirtyCount,
                            DirtyPercentage = _activityPlanManager.Count > 0
                                ? Math.Round((double)_activityPlanManager.DirtyCount / _activityPlanManager.Count * 100, 2)
                                : 0
                        } : null
                    }
                },
                LastSaveStatistics = _persistenceCoordinator.LastSaveStatistics,
                SystemInfo = new
                {
                    ProcessId = Environment.ProcessId,
                    MachineName = Environment.MachineName,
                    OSVersion = Environment.OSVersion.ToString(),
                    ProcessorCount = Environment.ProcessorCount,
                    WorkingSet = GC.GetTotalMemory(false) / 1024 / 1024, // MB
                    GCGen0Collections = GC.CollectionCount(0),
                    GCGen1Collections = GC.CollectionCount(1),
                    GCGen2Collections = GC.CollectionCount(2)
                }
            };
            
            return Ok(status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取状态信息时发生错误");
            return StatusCode(500, new { Status = "error", Message = ex.Message });
        }
    }
    
    /// <summary>
    /// 获取内存状态快照（仅返回摘要信息，不返回具体实体数据）
    /// Get memory state snapshot (returns summary only, not entity data)
    /// </summary>
    /// <returns>内存状态快照 / Memory state snapshot</returns>
    [HttpGet("memory-state")]
    public IActionResult GetMemoryState()
    {
        try
        {
            var memoryState = new
            {
                Timestamp = DateTime.UtcNow,
                Enabled = _characterManager != null,
                EntityTypes = new Dictionary<string, object>
                {
                    ["Character"] = _characterManager != null ? new
                    {
                        TotalCached = _characterManager.Count,
                        DirtyEntities = _characterManager.DirtyCount,
                        CleanEntities = _characterManager.Count - _characterManager.DirtyCount,
                        DirtyPercentage = _characterManager.Count > 0
                            ? Math.Round((double)_characterManager.DirtyCount / _characterManager.Count * 100, 2)
                            : 0
                    } : (object)"Not enabled",
                    
                    ["BattleSnapshot"] = _battleSnapshotManager != null ? new
                    {
                        TotalCached = _battleSnapshotManager.Count,
                        DirtyEntities = _battleSnapshotManager.DirtyCount,
                        CleanEntities = _battleSnapshotManager.Count - _battleSnapshotManager.DirtyCount,
                        DirtyPercentage = _battleSnapshotManager.Count > 0
                            ? Math.Round((double)_battleSnapshotManager.DirtyCount / _battleSnapshotManager.Count * 100, 2)
                            : 0
                    } : (object)"Not enabled",
                    
                    ["ActivityPlan"] = _activityPlanManager != null ? new
                    {
                        TotalCached = _activityPlanManager.Count,
                        DirtyEntities = _activityPlanManager.DirtyCount,
                        CleanEntities = _activityPlanManager.Count - _activityPlanManager.DirtyCount,
                        DirtyPercentage = _activityPlanManager.Count > 0
                            ? Math.Round((double)_activityPlanManager.DirtyCount / _activityPlanManager.Count * 100, 2)
                            : 0
                    } : (object)"Not enabled"
                },
                TotalSummary = new
                {
                    TotalCached = (_characterManager?.Count ?? 0) +
                                  (_battleSnapshotManager?.Count ?? 0) +
                                  (_activityPlanManager?.Count ?? 0),
                    TotalDirty = (_characterManager?.DirtyCount ?? 0) +
                                 (_battleSnapshotManager?.DirtyCount ?? 0) +
                                 (_activityPlanManager?.DirtyCount ?? 0)
                }
            };
            
            return Ok(memoryState);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取内存状态时发生错误");
            return StatusCode(500, new { Status = "error", Message = ex.Message });
        }
    }
    
    /// <summary>
    /// 触发立即保存（用于维护和紧急情况）
    /// Trigger immediate save (for maintenance and emergency situations)
    /// </summary>
    /// <param name="entityType">实体类型（可选）/ Entity type (optional)</param>
    /// <returns>保存操作结果 / Save operation result</returns>
    [HttpPost("trigger-save")]
    public async Task<IActionResult> TriggerSave([FromQuery] string? entityType = null)
    {
        try
        {
            // 验证和清理实体类型参数以防止日志伪造
            // Validate and sanitize entity type parameter to prevent log forging
            var validEntityTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "Character",
                "BattleSnapshot",
                "ActivityPlan"
            };
            
            var sanitizedEntityType = entityType != null && validEntityTypes.Contains(entityType)
                ? entityType
                : "All";
            
            _logger.LogInformation("手动触发立即保存，实体类型：{EntityType}", sanitizedEntityType);
            
            await _persistenceCoordinator.TriggerSaveAsync(entityType ?? string.Empty);
            
            return Ok(new
            {
                Message = "保存操作已触发 / Save operation triggered",
                Timestamp = DateTime.UtcNow,
                EntityType = sanitizedEntityType,
                LastSaveStatistics = _persistenceCoordinator.LastSaveStatistics
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "触发保存操作时发生错误");
            return StatusCode(500, new { Status = "error", Message = ex.Message });
        }
    }
}
