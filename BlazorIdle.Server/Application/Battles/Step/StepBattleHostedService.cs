using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BlazorIdle.Server.Application.Battles.Step;

public sealed class StepBattleHostedService : BackgroundService
{
    private readonly StepBattleCoordinator _coordinator;
    private readonly ILogger<StepBattleHostedService> _logger;

    public StepBattleHostedService(StepBattleCoordinator coordinator, ILogger<StepBattleHostedService> logger)
    {
        _coordinator = coordinator;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("StepBattleHostedService started.");
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                // 推进所有运行中的战斗（事件预算与时间片大小可调）
                _coordinator.AdvanceAll(maxEventsPerBattle: 1000, maxSliceSeconds: 0.5, stoppingToken);

                // 可选清理（完成后 5 分钟回收）
                _coordinator.PruneCompleted(TimeSpan.FromMinutes(5));

                await Task.Delay(50, stoppingToken); // 20fps 心跳
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _logger.LogError(ex, "StepBattleHostedService loop failed.");
        }
        _logger.LogInformation("StepBattleHostedService stopped.");
    }
}