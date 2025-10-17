using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
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
        // 第一步：清理孤立的运行中计划（服务器崩溃或异常关闭后的状态）
        _logger.LogInformation("StepBattleHostedService starting; cleaning up orphaned running plans...");
        try
        {
            await CleanupOrphanedRunningPlansAsync(stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CleanupOrphanedRunningPlansAsync failed.");
        }

        // 第二步：恢复战斗快照
        _logger.LogInformation("Recovering battle snapshots...");
        try
        {
            await _snapshot.RecoverAllAsync(_coordinator, stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RecoverAllAsync failed.");
        }

        // 第三步：恢复暂停的计划（服务器重启后）
        _logger.LogInformation("Recovering paused plans...");
        try
        {
            await RecoverPausedPlansAsync(stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RecoverPausedPlansAsync failed.");
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
        catch (OperationCanceledException) 
        { 
            _logger.LogInformation("StepBattleHostedService 收到停止信号");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "StepBattleHostedService loop failed.");
        }
        
        // 优雅关闭：保存所有运行中的战斗快照并暂停计划
        _logger.LogInformation("StepBattleHostedService 正在优雅关闭，保存所有运行中的战斗快照并暂停计划...");
        await SaveAllRunningBattleSnapshotsAsync(CancellationToken.None);
        await PauseAllRunningPlansAsync(CancellationToken.None);
        
        _logger.LogInformation("StepBattleHostedService stopped.");
    }

    /// <summary>
    /// 优雅关闭时保存所有运行中的战斗快照
    /// </summary>
    private async Task SaveAllRunningBattleSnapshotsAsync(CancellationToken ct)
    {
        try
        {
            var savedCount = 0;
            var failedCount = 0;
            
            foreach (var id in _coordinator.InternalIdsSnapshot())
            {
                if (_coordinator.TryGet(id, out var rb) && rb is not null && !rb.Completed)
                {
                    try
                    {
                        await _snapshot.SaveAsync(rb, ct);
                        savedCount++;
                        _logger.LogInformation("已保存战斗快照: {BattleId}", rb.Id);
                    }
                    catch (Exception ex)
                    {
                        failedCount++;
                        _logger.LogError(ex, "保存战斗快照失败: {BattleId}", rb.Id);
                    }
                }
            }
            
            _logger.LogInformation(
                "战斗快照保存完成: 成功 {SavedCount} 个，失败 {FailedCount} 个",
                savedCount,
                failedCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "保存所有战斗快照时发生错误");
        }
    }


    /// <summary>
    /// 恢复暂停的计划（服务器重启后）
    /// 暂停的计划会在玩家下次上线时通过离线结算自动恢复，
    /// 或者如果玩家仍然在线（可能是服务器重启），则直接恢复运行
    /// </summary>
    private async Task RecoverPausedPlansAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<Infrastructure.Persistence.GameDbContext>();
            var characterRepo = scope.ServiceProvider.GetRequiredService<BlazorIdle.Server.Application.Abstractions.ICharacterRepository>();
            var activityPlanService = scope.ServiceProvider.GetService<ActivityPlanService>();
            
            if (activityPlanService == null)
                return;

            // 查找所有暂停的计划
            var pausedPlans = await db.ActivityPlans
                .Where(p => p.State == Domain.Activities.ActivityState.Paused)
                .ToListAsync(ct);

            foreach (var plan in pausedPlans)
            {
                if (ct.IsCancellationRequested)
                    break;

                try
                {
                    // 检查玩家是否在线（最近60秒内有心跳）
                    var character = await characterRepo.GetAsync(plan.CharacterId, ct);
                    if (character?.LastSeenAtUtc != null)
                    {
                        var offlineSeconds = (DateTime.UtcNow - character.LastSeenAtUtc.Value).TotalSeconds;
                        
                        // 如果玩家在线（心跳在60秒内），尝试恢复运行
                        if (offlineSeconds < 60)
                        {
                            _logger.LogInformation(
                                "服务器重启后恢复暂停的计划 {PlanId} (玩家 {CharacterId} 在线)",
                                plan.Id, plan.CharacterId);
                            
                            await activityPlanService.StartPlanAsync(plan.Id, ct);
                        }
                        // 否则保持暂停状态，等待玩家上线后通过离线结算恢复
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Failed to recover paused plan {PlanId}", plan.Id);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "RecoverPausedPlansAsync failed");
        }
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

    /// <summary>
    /// 清理孤立的运行中计划（服务器启动时）
    /// 这些计划在上次服务器运行时处于Running状态，但没有对应的战斗实例
    /// 将它们标记为Paused状态，以便后续恢复
    /// </summary>
    private async Task CleanupOrphanedRunningPlansAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<Infrastructure.Persistence.GameDbContext>();
            
            // 查找所有Running状态的计划
            var runningPlans = await db.ActivityPlans
                .Where(p => p.State == Domain.Activities.ActivityState.Running)
                .ToListAsync(ct);

            if (runningPlans.Count == 0)
            {
                _logger.LogInformation("没有发现孤立的运行中计划");
                return;
            }

            _logger.LogWarning("发现 {Count} 个孤立的运行中计划，将它们标记为暂停状态", runningPlans.Count);

            foreach (var plan in runningPlans)
            {
                try
                {
                    // 将计划标记为暂停状态
                    plan.State = Domain.Activities.ActivityState.Paused;
                    // 清除BattleId引用（因为内存中的战斗实例已经不存在）
                    plan.BattleId = null;
                    
                    db.ActivityPlans.Update(plan);
                    
                    _logger.LogInformation(
                        "已将孤立的计划 {PlanId} (角色 {CharacterId}) 标记为暂停状态",
                        plan.Id, plan.CharacterId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "处理孤立计划 {PlanId} 时发生错误", plan.Id);
                }
            }

            // 批量保存更改
            await Infrastructure.Persistence.DatabaseRetryPolicy.SaveChangesWithRetryAsync(db, ct, _logger);
            
            _logger.LogInformation("孤立计划清理完成，已处理 {Count} 个计划", runningPlans.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CleanupOrphanedRunningPlansAsync failed");
            // 不抛出异常，允许服务继续启动
        }
    }

    /// <summary>
    /// 暂停所有运行中的计划（服务器关闭时）
    /// 确保所有计划都能在下次启动时正确恢复
    /// </summary>
    private async Task PauseAllRunningPlansAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var activityPlanService = scope.ServiceProvider.GetService<ActivityPlanService>();
            var planRepo = scope.ServiceProvider.GetService<BlazorIdle.Server.Application.Abstractions.IActivityPlanRepository>();
            
            if (activityPlanService == null || planRepo == null)
            {
                _logger.LogWarning("无法获取ActivityPlanService或IActivityPlanRepository，跳过暂停计划");
                return;
            }

            // 获取所有运行中的计划
            var runningPlans = await planRepo.GetAllRunningPlansAsync(ct);
            
            if (runningPlans.Count == 0)
            {
                _logger.LogInformation("没有需要暂停的运行中计划");
                return;
            }

            _logger.LogInformation("开始暂停 {Count} 个运行中的计划", runningPlans.Count);
            
            var pausedCount = 0;
            var failedCount = 0;

            foreach (var plan in runningPlans)
            {
                try
                {
                    var result = await activityPlanService.PausePlanAsync(plan.Id, ct);
                    if (result)
                    {
                        pausedCount++;
                        _logger.LogInformation("已暂停计划 {PlanId} (角色 {CharacterId})", plan.Id, plan.CharacterId);
                    }
                    else
                    {
                        failedCount++;
                        _logger.LogWarning("无法暂停计划 {PlanId}", plan.Id);
                    }
                }
                catch (Exception ex)
                {
                    failedCount++;
                    _logger.LogError(ex, "暂停计划 {PlanId} 时发生错误", plan.Id);
                }
            }
            
            _logger.LogInformation(
                "计划暂停完成: 成功 {PausedCount} 个，失败 {FailedCount} 个",
                pausedCount,
                failedCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PauseAllRunningPlansAsync failed");
        }
    }

    // 本地节流器：记录最近一次保存时的"模拟时间"
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