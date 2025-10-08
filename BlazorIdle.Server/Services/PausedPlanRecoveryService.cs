using BlazorIdle.Server.Application.Abstractions;
using BlazorIdle.Server.Application.Activities;
using BlazorIdle.Server.Domain.Activities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace BlazorIdle.Server.Services;

/// <summary>
/// 启动时恢复暂停的活动计划
/// 用于服务器重启后自动恢复玩家离线前的任务
/// </summary>
public class PausedPlanRecoveryService : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<PausedPlanRecoveryService> _logger;

    public PausedPlanRecoveryService(
        IServiceProvider serviceProvider,
        ILogger<PausedPlanRecoveryService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("开始恢复暂停的活动计划...");

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var planRepository = scope.ServiceProvider.GetRequiredService<IActivityPlanRepository>();
            var characterRepository = scope.ServiceProvider.GetRequiredService<ICharacterRepository>();
            var planService = scope.ServiceProvider.GetRequiredService<ActivityPlanService>();

            // 获取所有暂停的计划
            var pausedPlans = await planRepository
                .GetAllPausedPlansAsync(cancellationToken);

            _logger.LogInformation("找到 {Count} 个暂停的计划需要恢复", pausedPlans.Count);

            int successCount = 0;
            int failureCount = 0;

            foreach (var plan in pausedPlans)
            {
                try
                {
                    // 检查该角色是否在线（通过LastSeenAtUtc判断）
                    var character = await characterRepository.GetAsync(plan.CharacterId, cancellationToken);
                    if (character is null)
                    {
                        _logger.LogWarning("计划 {PlanId} 的角色 {CharacterId} 不存在，跳过恢复", 
                            plan.Id, plan.CharacterId);
                        continue;
                    }

                    // 检查该角色是否有其他正在运行的计划
                    var runningPlan = await planRepository.GetRunningPlanAsync(plan.CharacterId, cancellationToken);
                    if (runningPlan is not null)
                    {
                        _logger.LogInformation("角色 {CharacterId} 已有运行中的计划，跳过恢复暂停计划 {PlanId}", 
                            plan.CharacterId, plan.Id);
                        continue;
                    }

                    // 恢复计划
                    var resumed = await planService.ResumePlanAsync(plan.Id, cancellationToken);
                    if (resumed)
                    {
                        successCount++;
                        _logger.LogInformation("成功恢复计划 {PlanId} (角色: {CharacterId})", 
                            plan.Id, plan.CharacterId);
                    }
                    else
                    {
                        failureCount++;
                        _logger.LogWarning("恢复计划 {PlanId} 失败", plan.Id);
                    }
                }
                catch (Exception ex)
                {
                    failureCount++;
                    _logger.LogError(ex, "恢复计划 {PlanId} 时发生错误", plan.Id);
                }
            }

            _logger.LogInformation("计划恢复完成：成功 {SuccessCount} 个，失败 {FailureCount} 个", 
                successCount, failureCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "恢复暂停计划服务执行失败");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("暂停计划恢复服务已停止");
        return Task.CompletedTask;
    }
}
