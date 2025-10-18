using System.Diagnostics;
using BlazorIdle.Server.Application.Abstractions;
using BlazorIdle.Server.Config.Persistence;
using BlazorIdle.Server.Domain.Characters;
using BlazorIdle.Server.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BlazorIdle.Server.Services;

/// <summary>
/// 增强的关闭管理器 - 协调优雅关闭流程
/// 功能：
/// 1. 触发持久化协调器最终保存
/// 2. 设置所有在线角色为离线
/// 3. 执行 WAL 检查点
/// 4. 超时保护
/// </summary>
public class EnhancedShutdownManager : IHostedService
{
    private readonly IHostApplicationLifetime _appLifetime;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ShutdownOptions _options;
    private readonly ILogger<EnhancedShutdownManager> _logger;
    private readonly CancellationTokenSource _shutdownCts = new();
    
    // 全局访问的关闭令牌
    public static CancellationToken ShutdownToken => _instance?._shutdownCts.Token ?? CancellationToken.None;
    private static EnhancedShutdownManager? _instance;
    
    // 持久化协调器（可选，如果未启用内存缓冲则为 null）
    private IPersistenceCoordinator? _persistenceCoordinator;

    public EnhancedShutdownManager(
        IHostApplicationLifetime appLifetime,
        IServiceScopeFactory scopeFactory,
        IOptions<ShutdownOptions> options,
        ILogger<EnhancedShutdownManager> logger,
        IPersistenceCoordinator? persistenceCoordinator = null)
    {
        _appLifetime = appLifetime;
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
        _persistenceCoordinator = persistenceCoordinator;
        _instance = this;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("增强的关闭管理器已启动");
        
        _appLifetime.ApplicationStopping.Register(() =>
        {
            _logger.LogWarning("========================================");
            _logger.LogWarning("服务器关闭开始 - 执行优雅关闭流程");
            _logger.LogWarning("========================================");
            
            // 触发关闭信号
            _shutdownCts.Cancel();
            
            // 执行关闭流程（同步等待）
            try
            {
                ExecuteShutdownAsync().GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "关闭流程执行失败");
            }
        });
        
        return Task.CompletedTask;
    }

    private async Task ExecuteShutdownAsync()
    {
        var sw = Stopwatch.StartNew();
        
        try
        {
            var tasks = new List<Task>();
            
            // Step 1: 触发协调器最终保存（如果存在）
            if (_persistenceCoordinator != null)
            {
                _logger.LogInformation("[1/3] 开始保存所有数据...");
                tasks.Add(_persistenceCoordinator.FinalSaveAsync());
            }
            
            // Step 2: 设置所有角色离线
            if (_options.SetCharactersOfflineOnShutdown)
            {
                _logger.LogInformation("[2/3] 设置所有在线角色为离线...");
                tasks.Add(SetAllCharactersOfflineAsync());
            }
            
            // Step 3: 等待任务完成（带超时）
            var timeoutTask = Task.Delay(
                TimeSpan.FromSeconds(_options.ShutdownTimeoutSeconds)
            );
            
            var completedTask = await Task.WhenAny(
                Task.WhenAll(tasks),
                timeoutTask
            );
            
            if (completedTask == timeoutTask)
            {
                _logger.LogWarning("关闭流程超时（{Timeout}秒），强制继续",
                    _options.ShutdownTimeoutSeconds);
            }
            else
            {
                _logger.LogInformation("[3/3] 所有关闭任务已完成");
            }
            
            // Step 4: 强制执行 WAL 检查点（如果配置启用）
            if (_options.ForceWalCheckpointOnShutdown)
            {
                _logger.LogInformation("执行 WAL 检查点...");
                await ForceWalCheckpointAsync();
            }
            
            sw.Stop();
            
            _logger.LogInformation("========================================");
            _logger.LogInformation("优雅关闭流程完成，耗时 {ElapsedMs}ms", sw.ElapsedMilliseconds);
            _logger.LogInformation("========================================");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "关闭流程发生错误");
        }
    }

    /// <summary>
    /// 将所有在线角色设置为离线
    /// </summary>
    private async Task SetAllCharactersOfflineAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GameDbContext>();
        
        var sw = Stopwatch.StartNew();
        
        try
        {
            // 使用 ExecuteUpdateAsync 批量更新（EF Core 7+）
            int updatedCount = await db.Characters
                .Where(c => c.LastSeenAtUtc != null) // 只更新有心跳记录的角色
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(c => c.LastSeenAtUtc, DateTime.UtcNow)
                );
            
            sw.Stop();
            
            _logger.LogInformation(
                "已将 {Count} 个角色的最后在线时间设置为当前时间（耗时 {ElapsedMs}ms）",
                updatedCount, sw.ElapsedMilliseconds
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "设置角色离线时发生错误");
            
            // 降级方案：逐个更新
            await SetCharactersOfflineFallbackAsync(db);
        }
    }

    /// <summary>
    /// 降级方案：逐个更新角色离线状态
    /// </summary>
    private async Task SetCharactersOfflineFallbackAsync(GameDbContext db)
    {
        try
        {
            var onlineCharacters = await db.Characters
                .Where(c => c.LastSeenAtUtc != null)
                .ToListAsync();
            
            foreach (var character in onlineCharacters)
            {
                character.LastSeenAtUtc = DateTime.UtcNow;
            }
            
            await db.SaveChangesAsync();
            
            _logger.LogInformation(
                "使用降级方案设置了 {Count} 个角色为离线",
                onlineCharacters.Count
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "降级方案也失败了");
        }
    }

    /// <summary>
    /// 强制执行 WAL 检查点
    /// </summary>
    private async Task ForceWalCheckpointAsync()
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<GameDbContext>();
            
            await db.Database.ExecuteSqlRawAsync("PRAGMA wal_checkpoint(TRUNCATE)");
            
            _logger.LogInformation("WAL 检查点已执行");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "执行 WAL 检查点失败");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("增强的关闭管理器已停止");
        _shutdownCts.Dispose();
        return Task.CompletedTask;
    }
}
