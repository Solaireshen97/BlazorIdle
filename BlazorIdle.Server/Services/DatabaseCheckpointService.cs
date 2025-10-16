using BlazorIdle.Server.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BlazorIdle.Server.Services;

/// <summary>
/// 数据库检查点服务
/// 在应用程序关闭时执行 WAL 检查点，确保数据持久化到主数据库文件
/// 这可以防止 SQLite WAL 文件锁定导致的启动问题
/// </summary>
public class DatabaseCheckpointService : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DatabaseCheckpointService> _logger;

    public DatabaseCheckpointService(
        IServiceProvider serviceProvider,
        ILogger<DatabaseCheckpointService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("数据库检查点服务已启动");
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("正在执行数据库 WAL 检查点...");
        
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<GameDbContext>();
            
            // 执行 WAL 检查点，将 WAL 文件内容刷新到主数据库文件
            // PRAGMA wal_checkpoint(TRUNCATE) 会：
            // 1. 将 WAL 文件中的所有内容写入主数据库
            // 2. 截断 WAL 文件到零长度
            // 3. 这有助于避免在下次启动时出现文件锁定问题
            await dbContext.Database.ExecuteSqlRawAsync(
                "PRAGMA wal_checkpoint(TRUNCATE);",
                cancellationToken);
            
            _logger.LogInformation("数据库 WAL 检查点执行成功");
        }
        catch (Exception ex)
        {
            // 记录错误但不抛出异常，避免影响应用程序关闭
            _logger.LogError(ex, "执行数据库 WAL 检查点时出错");
        }
    }
}
