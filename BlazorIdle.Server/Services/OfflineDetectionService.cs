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

        _logger.LogInformation("离线检测服务已停止");
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

                        await planService.StopPlanAsync(plan.Id, ct);
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
