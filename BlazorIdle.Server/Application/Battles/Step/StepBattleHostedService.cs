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
        _logger.LogInformation("StepBattleHostedService starting; recovering snapshots...");
        try
        {
            await _snapshot.RecoverAllAsync(_coordinator, stoppingToken);
            _logger.LogInformation("Snapshots recovered successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RecoverAllAsync failed. Service will continue without snapshot recovery.");
        }

        // 恢复暂停的计划（服务器重启后）
        _logger.LogInformation("Recovering paused plans...");
        try
        {
            // 添加延迟以确保数据库完全就绪
            await Task.Delay(1000, stoppingToken);
            await RecoverPausedPlansAsync(stoppingToken);
            _logger.LogInformation("Paused plans recovered successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RecoverPausedPlansAsync failed. Service will continue without plan recovery.");
        }

        _logger.LogInformation("StepBattleHostedService started successfully.");
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
    /// 恢复暂停的计划（服务器重启后）
    /// 暂停的计划会在玩家下次上线时通过离线结算自动恢复，
    /// 或者如果玩家仍然在线（可能是服务器重启），则直接恢复运行
    /// </summary>
    private async Task RecoverPausedPlansAsync(CancellationToken ct)
    {
        List<Guid> pausedPlanIds;
        
        try
        {
            // 第一步：在一个独立的作用域中查询所有暂停的计划ID
            using (var scope = _scopeFactory.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<Infrastructure.Persistence.GameDbContext>();
                
                pausedPlanIds = await db.ActivityPlans
                    .Where(p => p.State == Domain.Activities.ActivityState.Paused)
                    .Select(p => p.Id)
                    .ToListAsync(ct);
            }
            
            _logger.LogInformation("Found {Count} paused plans to recover", pausedPlanIds.Count);

            // 第二步：逐个恢复计划，每个计划使用独立的作用域
            foreach (var planId in pausedPlanIds)
            {
                if (ct.IsCancellationRequested)
                    break;

                try
                {
                    // 为每个计划创建独立的作用域，避免DbContext冲突
                    using var scope = _scopeFactory.CreateScope();
                    var characterRepo = scope.ServiceProvider.GetRequiredService<BlazorIdle.Server.Application.Abstractions.ICharacterRepository>();
                    var planRepo = scope.ServiceProvider.GetRequiredService<BlazorIdle.Server.Application.Abstractions.IActivityPlanRepository>();
                    var activityPlanService = scope.ServiceProvider.GetService<ActivityPlanService>();
                    
                    if (activityPlanService == null)
                        continue;

                    // 重新获取计划（使用当前作用域的DbContext）
                    var plan = await planRepo.GetAsync(planId, ct);
                    if (plan == null || plan.State != Domain.Activities.ActivityState.Paused)
                        continue;

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
                            
                            // 使用新的作用域启动计划
                            await activityPlanService.StartPlanAsync(plan.Id, ct);
                        }
                        // 否则保持暂停状态，等待玩家上线后通过离线结算恢复
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to recover paused plan {PlanId}", planId);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RecoverPausedPlansAsync failed");
        }
    }

    /// <summary>
    /// 检查所有运行中的活动计划，更新进度并自动停止达到限制的计划
    /// </summary>
    private async Task CheckAndUpdateActivityPlansAsync(CancellationToken ct)
    {
        List<Guid> runningPlanIds;
        
        try
        {
            // 第一步：在一个独立的作用域中查询所有运行中的计划ID
            using (var scope = _scopeFactory.CreateScope())
            {
                var planRepo = scope.ServiceProvider.GetService<BlazorIdle.Server.Application.Abstractions.IActivityPlanRepository>();
                if (planRepo == null)
                    return;

                // 获取所有运行中的计划ID
                var runningPlans = await planRepo.GetAllRunningPlansAsync(ct);
                runningPlanIds = runningPlans.Select(p => p.Id).ToList();
            }
            
            // 第二步：逐个更新计划，每个计划使用独立的作用域
            foreach (var planId in runningPlanIds)
            {
                if (ct.IsCancellationRequested)
                    break;

                try
                {
                    // 为每个计划创建独立的作用域，避免DbContext冲突
                    using var scope = _scopeFactory.CreateScope();
                    var activityPlanService = scope.ServiceProvider.GetService<ActivityPlanService>();
                    if (activityPlanService == null)
                        continue;

                    // 更新计划进度（会自动检查限制并停止）
                    await activityPlanService.UpdatePlanProgressAsync(planId, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Failed to update plan progress for {PlanId}", planId);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CheckAndUpdateActivityPlansAsync failed");
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