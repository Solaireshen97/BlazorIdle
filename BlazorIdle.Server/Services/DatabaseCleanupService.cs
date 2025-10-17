using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using BlazorIdle.Server.Infrastructure.Persistence;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace BlazorIdle.Server.Services;

/// <summary>
/// 数据库清理服务：确保应用关闭时正确关闭数据库连接并执行 WAL 检查点
/// 这可以防止 SQLite 数据库在服务器非正常关闭时损坏
/// </summary>
public class DatabaseCleanupService : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DatabaseCleanupService> _logger;

    public DatabaseCleanupService(
        IServiceProvider serviceProvider,
        ILogger<DatabaseCleanupService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("数据库清理服务已启动");
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("数据库清理服务正在关闭，执行数据库清理...");
        
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<GameDbContext>();
            
            // 保存所有待处理的更改
            var pendingChanges = dbContext.ChangeTracker.HasChanges();
            if (pendingChanges)
            {
                _logger.LogWarning("数据库有待处理的更改，正在保存...");
                await dbContext.SaveChangesAsync(cancellationToken);
                _logger.LogInformation("已保存待处理的数据库更改");
            }
            
            // 执行 SQLite WAL 检查点，将 WAL 文件中的更改写入主数据库文件
            try
            {
                await dbContext.Database.ExecuteSqlRawAsync("PRAGMA wal_checkpoint(TRUNCATE);", cancellationToken);
                _logger.LogInformation("已执行 SQLite WAL 检查点");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "执行 WAL 检查点失败（可能不是 SQLite 数据库）");
            }
            
            // 关闭数据库连接
            await dbContext.Database.CloseConnectionAsync();
            _logger.LogInformation("已关闭数据库连接");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "数据库清理过程中发生错误");
        }
        
        _logger.LogInformation("数据库清理服务已停止");
    }
}
