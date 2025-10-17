using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace BlazorIdle.Server.Infrastructure.Persistence;

/// <summary>
/// 数据库操作重试策略
/// 处理 SQLite 的 SQLITE_BUSY 和其他瞬态错误
/// </summary>
public static class DatabaseRetryPolicy
{
    private const int MaxRetryAttempts = 5;
    private static readonly TimeSpan InitialDelay = TimeSpan.FromMilliseconds(100);
    
    /// <summary>
    /// 创建异步重试策略用于 SaveChangesAsync
    /// </summary>
    public static AsyncRetryPolicy CreateAsyncPolicy(ILogger? logger = null)
    {
        return Policy
            .Handle<DbUpdateException>(ex => IsRetriableException(ex))
            .Or<SqliteException>(ex => IsRetriableSqliteException(ex))
            .WaitAndRetryAsync(
                MaxRetryAttempts,
                retryAttempt => TimeSpan.FromMilliseconds(InitialDelay.TotalMilliseconds * Math.Pow(2, retryAttempt - 1)),
                onRetry: (exception, timespan, retryCount, context) =>
                {
                    logger?.LogWarning(
                        exception,
                        "数据库操作失败，正在重试 ({RetryCount}/{MaxRetries})，等待 {Delay}ms",
                        retryCount,
                        MaxRetryAttempts,
                        timespan.TotalMilliseconds);
                });
    }

    /// <summary>
    /// 执行带重试的数据库保存操作
    /// </summary>
    public static async Task<int> SaveChangesWithRetryAsync(
        this DbContext context,
        CancellationToken cancellationToken = default,
        ILogger? logger = null)
    {
        var policy = CreateAsyncPolicy(logger);
        return await policy.ExecuteAsync(async ct => 
            await context.SaveChangesAsync(ct), 
            cancellationToken);
    }

    /// <summary>
    /// 判断是否为可重试的 DbUpdateException
    /// </summary>
    private static bool IsRetriableException(DbUpdateException ex)
    {
        // 检查内部异常是否为 SQLite 锁定错误
        return ex.InnerException is SqliteException sqliteEx && IsRetriableSqliteException(sqliteEx);
    }

    /// <summary>
    /// 判断是否为可重试的 SqliteException
    /// </summary>
    private static bool IsRetriableSqliteException(SqliteException ex)
    {
        // SQLITE_BUSY (5): 数据库被锁定
        // SQLITE_LOCKED (6): 数据库表被锁定
        // SQLITE_IOERR (10): 磁盘 I/O 错误
        return ex.SqliteErrorCode == 5 || 
               ex.SqliteErrorCode == 6 ||
               ex.SqliteErrorCode == 10;
    }
}
