using System.Diagnostics;
using BlazorIdle.Server.Config.DatabaseOptimization;
using BlazorIdle.Server.Infrastructure.DatabaseOptimization.Abstractions;
using BlazorIdle.Server.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BlazorIdle.Server.Infrastructure.DatabaseOptimization;

/// <summary>
/// 增强的优雅关闭管理器
/// Enhanced graceful shutdown manager
/// </summary>
/// <remarks>
/// 相比原有的 GracefulShutdownCoordinator，增强功能包括：
/// 1. 集成 PersistenceCoordinator 的最终保存
/// 2. 将所有在线角色设置为离线
/// 3. 强制执行 WAL 检查点
/// 4. 更长的超时保护和更详细的日志
/// 5. 任务失败降级处理
/// 
/// Enhancements over original GracefulShutdownCoordinator:
/// 1. Integrate PersistenceCoordinator final save
/// 2. Set all online characters to offline
/// 3. Force WAL checkpoint
/// 4. Longer timeout protection and detailed logging
/// 5. Task failure degradation handling
/// </remarks>
public class EnhancedShutdownManager : IHostedService
{
    private readonly IHostApplicationLifetime _appLifetime;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IPersistenceCoordinator _persistenceCoordinator;
    private readonly ShutdownOptions _options;
    private readonly PersistenceOptions _persistenceOptions;
    private readonly ILogger<EnhancedShutdownManager> _logger;
    private readonly CancellationTokenSource _shutdownCts = new();
    
    /// <summary>
    /// 全局关闭信号（供其他服务使用）
    /// Global shutdown token (for other services)
    /// </summary>
    public static CancellationToken ShutdownToken => _instance?._shutdownCts.Token ?? CancellationToken.None;
    private static EnhancedShutdownManager? _instance;

    public EnhancedShutdownManager(
        IHostApplicationLifetime appLifetime,
        IServiceScopeFactory scopeFactory,
        IPersistenceCoordinator persistenceCoordinator,
        IOptions<ShutdownOptions> options,
        IOptions<PersistenceOptions> persistenceOptions,
        ILogger<EnhancedShutdownManager> logger)
    {
        _appLifetime = appLifetime;
        _scopeFactory = scopeFactory;
        _persistenceCoordinator = persistenceCoordinator;
        _options = options.Value;
        _persistenceOptions = persistenceOptions.Value;
        _logger = logger;
        _instance = this;
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("增强的优雅关闭管理器已启动");
        
        _appLifetime.ApplicationStopping.Register(() =>
        {
            _logger.LogWarning("========================================");
            _logger.LogWarning("服务器关闭开始 - 执行优雅关闭流程");
            _logger.LogWarning("========================================");
            
            // 触发关闭信号（通知所有服务）
            _shutdownCts.Cancel();
            
            // 执行关闭流程（同步等待）
            ExecuteShutdownAsync().GetAwaiter().GetResult();
        });
        
        return Task.CompletedTask;
    }

    /// <summary>
    /// 执行关闭流程
    /// Execute shutdown procedure
    /// </summary>
    private async Task ExecuteShutdownAsync()
    {
        var sw = Stopwatch.StartNew();
        var tasks = new List<Task>();
        
        try
        {
            // 步骤 1: 触发协调器最终保存（如果启用了内存缓冲）
            if (_persistenceOptions.EnableMemoryBuffering)
            {
                _logger.LogInformation("[1/4] 开始保存所有数据（PersistenceCoordinator）...");
                var saveTask = _persistenceCoordinator.FinalSaveAsync(CancellationToken.None);
                tasks.Add(saveTask);
            }
            else
            {
                _logger.LogInformation("[1/4] 内存缓冲未启用，跳过 PersistenceCoordinator 保存");
            }
            
            // 步骤 2: 设置所有在线角色为离线
            if (_options.SetCharactersOfflineOnShutdown)
            {
                _logger.LogInformation("[2/4] 设置所有在线角色为离线...");
                var offlineTask = SetAllCharactersOfflineAsync();
                tasks.Add(offlineTask);
            }
            else
            {
                _logger.LogInformation("[2/4] 配置禁用了角色离线设置，跳过");
            }
            
            // 步骤 3: 等待所有任务完成（带超时）
            if (tasks.Any())
            {
                _logger.LogInformation("[3/4] 等待所有关闭任务完成（超时 {Timeout} 秒）...", _options.ShutdownTimeoutSeconds);
                
                var timeoutTask = Task.Delay(TimeSpan.FromSeconds(_options.ShutdownTimeoutSeconds));
                var completedTask = await Task.WhenAny(Task.WhenAll(tasks), timeoutTask);
                
                if (completedTask == timeoutTask)
                {
                    _logger.LogWarning(
                        "关闭流程超时（{Timeout} 秒），强制继续",
                        _options.ShutdownTimeoutSeconds
                    );
                }
                else
                {
                    _logger.LogInformation("[3/4] 所有关闭任务已完成");
                }
            }
            else
            {
                _logger.LogInformation("[3/4] 无需执行的关闭任务");
            }
            
            // 步骤 4: 强制执行 WAL 检查点（如果配置启用）
            if (_options.ForceWalCheckpointOnShutdown)
            {
                _logger.LogInformation("[4/4] 执行 WAL 检查点...");
                try
                {
                    await ForceWalCheckpointAsync();
                    _logger.LogInformation("[4/4] WAL 检查点已完成");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "执行 WAL 检查点失败（非致命错误）");
                }
            }
            else
            {
                _logger.LogInformation("[4/4] 配置禁用了 WAL 检查点，跳过");
            }
            
            sw.Stop();
            
            _logger.LogWarning("========================================");
            _logger.LogWarning(
                "优雅关闭流程完成，耗时 {ElapsedMs}ms",
                sw.ElapsedMilliseconds
            );
            _logger.LogWarning("========================================");
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "关闭流程发生错误（耗时 {ElapsedMs}ms）", sw.ElapsedMilliseconds);
        }
    }

    /// <summary>
    /// 将所有角色标记为离线（更新 LastSeenAtUtc）
    /// Mark all characters as offline (update LastSeenAtUtc)
    /// </summary>
    /// <remarks>
    /// 由于当前设计使用 LastSeenAtUtc 来追踪在线状态（而非 IsOnline 属性），
    /// 这里将所有角色的 LastSeenAtUtc 设置为当前时间，确保离线检测服务能正确识别
    /// 
    /// Current design uses LastSeenAtUtc to track online status (not IsOnline property),
    /// so we set LastSeenAtUtc to current time for all characters to ensure offline detection works correctly
    /// </remarks>
    private async Task SetAllCharactersOfflineAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GameDbContext>();
        
        var sw = Stopwatch.StartNew();
        
        try
        {
            // 获取有心跳记录的角色（可能在线）
            // Get characters with heartbeat records (potentially online)
            var recentThreshold = DateTime.UtcNow.AddHours(-1); // 最近1小时有活动的角色
            
            int updatedCount = await db.Characters
                .Where(c => c.LastSeenAtUtc.HasValue && c.LastSeenAtUtc.Value > recentThreshold)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(c => c.LastSeenAtUtc, DateTime.UtcNow)
                );
            
            sw.Stop();
            
            _logger.LogInformation(
                "已更新 {Count} 个角色的 LastSeenAtUtc（标记为离线，耗时 {ElapsedMs}ms）",
                updatedCount,
                sw.ElapsedMilliseconds
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新角色离线状态时发生错误，尝试降级方案...");
            
            try
            {
                // 降级方案：逐个更新（兼容性更好，但性能较低）
                // Fallback: update one by one (better compatibility, lower performance)
                await SetCharactersOfflineFallbackAsync(db);
            }
            catch (Exception fallbackEx)
            {
                _logger.LogError(fallbackEx, "降级方案也失败，无法设置角色离线");
                throw;
            }
        }
    }

    /// <summary>
    /// 设置角色离线的降级方案（逐个更新）
    /// Fallback method for setting characters offline (update one by one)
    /// </summary>
    private async Task SetCharactersOfflineFallbackAsync(GameDbContext db)
    {
        var sw = Stopwatch.StartNew();
        
        var recentThreshold = DateTime.UtcNow.AddHours(-1);
        var recentCharacters = await db.Characters
            .Where(c => c.LastSeenAtUtc.HasValue && c.LastSeenAtUtc.Value > recentThreshold)
            .ToListAsync();
        
        if (!recentCharacters.Any())
        {
            _logger.LogInformation("没有最近活跃的角色需要更新");
            return;
        }
        
        foreach (var character in recentCharacters)
        {
            character.LastSeenAtUtc = DateTime.UtcNow;
        }
        
        await db.SaveChangesAsync();
        
        sw.Stop();
        
        _logger.LogInformation(
            "使用降级方案更新了 {Count} 个角色的离线状态（耗时 {ElapsedMs}ms）",
            recentCharacters.Count,
            sw.ElapsedMilliseconds
        );
    }

    /// <summary>
    /// 强制执行 WAL 检查点
    /// Force WAL checkpoint
    /// </summary>
    /// <remarks>
    /// SQLite WAL 模式下，检查点将 WAL 文件的内容合并回主数据库文件
    /// TRUNCATE 模式会在检查点后截断 WAL 文件
    /// 
    /// In SQLite WAL mode, checkpoint merges WAL file content back to main database
    /// TRUNCATE mode will truncate WAL file after checkpoint
    /// </remarks>
    private async Task ForceWalCheckpointAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GameDbContext>();
        
        var sw = Stopwatch.StartNew();
        
        try
        {
            // PRAGMA wal_checkpoint(TRUNCATE):
            // - 执行完整的检查点
            // - 将 WAL 文件截断为0字节
            // - 释放磁盘空间
            await db.Database.ExecuteSqlRawAsync("PRAGMA wal_checkpoint(TRUNCATE);");
            
            sw.Stop();
            
            _logger.LogInformation(
                "WAL 检查点已执行（TRUNCATE 模式，耗时 {ElapsedMs}ms）",
                sw.ElapsedMilliseconds
            );
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(
                ex,
                "执行 WAL 检查点失败（耗时 {ElapsedMs}ms）",
                sw.ElapsedMilliseconds
            );
            throw;
        }
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("增强的优雅关闭管理器已停止");
        _shutdownCts.Dispose();
        return Task.CompletedTask;
    }
}
