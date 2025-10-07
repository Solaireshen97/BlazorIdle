using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using BlazorIdle.Server.Application.Activities;

namespace BlazorIdle.Server.Application.Battles.Step;

public sealed class StepBattleHostedService : BackgroundService
{
    private readonly StepBattleCoordinator _coordinator;
    private readonly StepBattleSnapshotService _snapshot;
    private readonly ILogger<StepBattleHostedService> _logger;

    // 每保存一次快照的最短“模拟时间间隔”
    private const double SnapshotIntervalSimSeconds = 2.0;

    private readonly IServiceScopeFactory _scopeFactory;

    public StepBattleHostedService(
        StepBattleCoordinator coordinator, 
        StepBattleSnapshotService snapshot, 
        IServiceScopeFactory scopeFactory,
        ILogger<StepBattleHostedService> logger)
    {
        _coordinator = coordinator;
        _snapshot = snapshot;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("StepBattleHostedService starting; recovering snapshots...");
        try
        {
            await _snapshot.RecoverAllAsync(_coordinator, stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RecoverAllAsync failed.");
        }

        _logger.LogInformation("StepBattleHostedService started.");
        var lastSnapAt = DateTime.UtcNow;
        var lastPlanCheckAt = DateTime.UtcNow;

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                // 推进
                _coordinator.AdvanceAll(maxEventsPerBattle: 1000, maxSliceSeconds: 0.25, stoppingToken);

                // 定期保存快照（墙钟节流 + 每场战斗模拟时间至少前进一小段）
                if ((DateTime.UtcNow - lastSnapAt).TotalMilliseconds >= 500)
                {
                    lastSnapAt = DateTime.UtcNow;
                    foreach (var id in _coordinator.InternalIdsSnapshot())
                    {
                        if (stoppingToken.IsCancellationRequested) break;
                        if (_coordinator.TryGet(id, out var rb) && rb is not null && !rb.Completed)
                        {
                            // 仅当较上次保存后确实前进了足够“模拟秒”时再保存
                            if (!SnapshotThrottler.ShouldSkip(rb.Id, rb.Clock.CurrentTime, SnapshotIntervalSimSeconds))
                            {
                                try { await _snapshot.SaveAsync(rb, stoppingToken); }
                                catch (Exception ex) { _logger.LogDebug(ex, "Save snapshot failed for {Id}", rb.Id); }
                            }
                        }
                        // 完成后确保清理快照
                        else if (rb is not null && rb.Completed)
                        {
                            try { await _snapshot.DeleteAsync(rb.Id, stoppingToken); } catch { /* ignore */ }
                        }
                    }
                }

                // 定期检查活动计划进度并自动停止达到限制的计划
                if ((DateTime.UtcNow - lastPlanCheckAt).TotalMilliseconds >= 1000)
                {
                    lastPlanCheckAt = DateTime.UtcNow;
                    await CheckAndUpdateActivityPlansAsync(stoppingToken);
                }

                // 回收
                _coordinator.PruneCompleted(TimeSpan.FromMinutes(5));
                await Task.Delay(50, stoppingToken);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _logger.LogError(ex, "StepBattleHostedService loop failed.");
        }
        _logger.LogInformation("StepBattleHostedService stopped.");
    }


    /// <summary>
    /// 检查所有运行中的活动计划，更新进度并自动停止达到限制的计划
    /// </summary>
    private async Task CheckAndUpdateActivityPlansAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var activityPlanService = scope.ServiceProvider.GetService<ActivityPlanService>();
            if (activityPlanService == null)
                return;

            var planRepo = scope.ServiceProvider.GetService<BlazorIdle.Server.Application.Abstractions.IActivityPlanRepository>();
            if (planRepo == null)
                return;

            // 获取所有运行中的计划
            var runningPlans = await planRepo.GetAllRunningPlansAsync(ct);
            
            foreach (var plan in runningPlans)
            {
                if (ct.IsCancellationRequested)
                    break;

                try
                {
                    // 更新计划进度（会自动检查限制并停止）
                    await activityPlanService.UpdatePlanProgressAsync(plan.Id, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Failed to update plan progress for {PlanId}", plan.Id);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "CheckAndUpdateActivityPlansAsync failed");
        }
    }
    // 本地节流器：记录最近一次保存时的“模拟时间”
    private static class SnapshotThrottler
    {
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<Guid, double> _lastSim = new();

        public static bool ShouldSkip(Guid id, double nowSim, double minDelta)
        {
            var last = _lastSim.GetOrAdd(id, -1);
            if (last < 0 || nowSim - last >= minDelta)
            {
                _lastSim[id] = nowSim;
                return false;
            }
            return true;
        }
    }
}