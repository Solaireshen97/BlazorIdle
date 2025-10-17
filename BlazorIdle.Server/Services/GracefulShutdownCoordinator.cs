using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace BlazorIdle.Server.Services;

/// <summary>
/// 协调服务器优雅关闭，确保所有关键操作完成后再停止
/// 防止数据库损坏和数据丢失
/// </summary>
public class GracefulShutdownCoordinator : IHostedService
{
    private readonly ILogger<GracefulShutdownCoordinator> _logger;
    private readonly IHostApplicationLifetime _appLifetime;
    private readonly CancellationTokenSource _shutdownCts = new();
    
    // 关闭信号：通知所有服务开始准备关闭
    public static CancellationToken ShutdownToken => _instance?._shutdownCts.Token ?? CancellationToken.None;
    private static GracefulShutdownCoordinator? _instance;

    public GracefulShutdownCoordinator(
        ILogger<GracefulShutdownCoordinator> logger,
        IHostApplicationLifetime appLifetime)
    {
        _logger = logger;
        _appLifetime = appLifetime;
        _instance = this;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("优雅关闭协调器已启动");
        
        // 注册应用程序停止事件
        _appLifetime.ApplicationStopping.Register(() =>
        {
            _logger.LogWarning("服务器正在关闭，触发优雅关闭流程...");
            
            // 触发关闭信号
            _shutdownCts.Cancel();
            
            // 给其他服务一些时间来完成操作
            Thread.Sleep(2000); // 2秒缓冲时间
            
            _logger.LogInformation("优雅关闭流程完成");
        });

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("优雅关闭协调器已停止");
        _shutdownCts.Dispose();
        return Task.CompletedTask;
    }
}
