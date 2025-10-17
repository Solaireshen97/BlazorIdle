using BlazorIdle.Server.Application.Abstractions;
using BlazorIdle.Server.Application.Activities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace BlazorIdle.Server.Services;

/// <summary>
/// 后台服务：定期检测离线玩家并暂停他们的在线任务
/// 这样可以节省服务器资源，并确保服务器重启后能正常处理任务
/// </summary>
public class OfflineDetectionService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;
    private readonly ILogger<OfflineDetectionService> _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromSeconds(30);

    public OfflineDetectionService(
        IServiceProvider serviceProvider,
        IConfiguration configuration,
        ILogger<OfflineDetectionService> logger)
    {
        _serviceProvider = serviceProvider;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("离线检测服务已启动");

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await CheckAndPauseOfflinePlayers(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "检测离线玩家时发生错误");
                }

                await Task.Delay(_checkInterval, stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // 正常的取消操作，不记录错误
        }

        // 优雅关闭：最后一次检查并暂停所有运行中的计划
        _logger.LogInformation("离线检测服务正在关闭，执行最后一次检查...");
        await ShutdownCheckAsync();
        _logger.LogInformation("离线检测服务已停止");
    }

    /// <summary>
    /// 服务器关闭时的最后检查：暂停所有运行中的计划（不管玩家是否离线）
    /// 这确保服务器重启后能够恢复所有计划
    /// </summary>
    private async Task ShutdownCheckAsync()
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var planRepository = scope.ServiceProvider.GetRequiredService<IActivityPlanRepository>();
            var planService = scope.ServiceProvider.GetRequiredService<ActivityPlanService>();

            var runningPlans = await planRepository.GetAllRunningPlansAsync(CancellationToken.None);
            
            if (runningPlans.Count > 0)
            {
                _logger.LogInformation("服务器关闭：发现 {Count} 个运行中的计划，正在暂停...", runningPlans.Count);

                foreach (var plan in runningPlans)
                {
                    try
                    {
                        _logger.LogInformation(
                            "服务器关闭：暂停计划 {PlanId} (角色 {CharacterId})",
                            plan.Id, plan.CharacterId);

                        await planService.PausePlanAsync(plan.Id, CancellationToken.None);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "服务器关闭：暂停计划 {PlanId} 时发生错误", plan.Id);
                    }
                }

                _logger.LogInformation("服务器关闭：已完成暂停所有运行中的计划");
            }
            else
            {
                _logger.LogInformation("服务器关闭：没有运行中的计划需要暂停");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "服务器关闭时检查计划发生错误");
        }
    }

    private async Task CheckAndPauseOfflinePlayers(CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var planRepository = scope.ServiceProvider.GetRequiredService<IActivityPlanRepository>();
        var characterRepository = scope.ServiceProvider.GetRequiredService<ICharacterRepository>();
        var planService = scope.ServiceProvider.GetRequiredService<ActivityPlanService>();

        var offlineThresholdSeconds = _configuration.GetValue<int>("Offline:OfflineDetectionSeconds", 60);
        var now = DateTime.UtcNow;

        var runningPlans = await planRepository.GetAllRunningPlansAsync(ct);
        
        foreach (var plan in runningPlans)
        {
            var character = await characterRepository.GetAsync(plan.CharacterId, ct);
            if (character == null)
                continue;

            if (character.LastSeenAtUtc.HasValue)
            {
                var offlineSeconds = (now - character.LastSeenAtUtc.Value).TotalSeconds;
                
                if (offlineSeconds >= offlineThresholdSeconds)
                {
                    try
                    {
                        _logger.LogInformation(
                            "检测到玩家 {CharacterId} 已离线 {OfflineSeconds:F0} 秒，暂停计划 {PlanId}",
                            character.Id, offlineSeconds, plan.Id);

                        // 使用 PausePlanAsync 而不是 StopPlanAsync，保存当前进度以便恢复
                        await planService.PausePlanAsync(plan.Id, ct);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "暂停计划 {PlanId} 时发生错误", plan.Id);
                    }
                }
            }
        }
    }
}
