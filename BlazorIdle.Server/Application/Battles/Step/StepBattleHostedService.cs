using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using BlazorIdle.Server.Infrastructure.SignalR.Broadcasters;

namespace BlazorIdle.Server.Application.Battles.Step;

/// <summary>
/// 战斗后台服务
/// 负责推进所有活跃战斗的模拟、保存快照以及启动SignalR帧广播
/// </summary>
public sealed class StepBattleHostedService : BackgroundService
{
    private readonly StepBattleCoordinator _coordinator;
    private readonly StepBattleSnapshotService _snapshot;
    private readonly CombatBroadcaster _combatBroadcaster;
    private readonly ILogger<StepBattleHostedService> _logger;

    // 每保存一次快照的最短"模拟时间间隔"（秒）
    private const double SnapshotIntervalSimSeconds = 2.0;

    public StepBattleHostedService(
        StepBattleCoordinator coordinator, 
        StepBattleSnapshotService snapshot, 
        CombatBroadcaster combatBroadcaster,
        ILogger<StepBattleHostedService> logger)
    {
        _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
        _snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
        _combatBroadcaster = combatBroadcaster ?? throw new ArgumentNullException(nameof(combatBroadcaster));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                // 1. 推进所有战斗的模拟
                _coordinator.AdvanceAll(maxEventsPerBattle: 1000, maxSliceSeconds: 0.25, stoppingToken);

                // 2. 定期保存快照（墙钟节流 + 每场战斗模拟时间至少前进一小段）
                if ((DateTime.UtcNow - lastSnapAt).TotalMilliseconds >= 500)
                {
                    lastSnapAt = DateTime.UtcNow;
                    foreach (var id in _coordinator.InternalIdsSnapshot())
                    {
                        if (stoppingToken.IsCancellationRequested) break;
                        
                        if (_coordinator.TryGet(id, out var rb) && rb is not null)
                        {
                            if (!rb.Completed)
                            {
                                // 仅当较上次保存后确实前进了足够"模拟秒"时再保存
                                if (!SnapshotThrottler.ShouldSkip(rb.Id, rb.Clock.CurrentTime, SnapshotIntervalSimSeconds))
                                {
                                    try 
                                    { 
                                        await _snapshot.SaveAsync(rb, stoppingToken); 
                                    }
                                    catch (Exception ex) 
                                    { 
                                        _logger.LogDebug(ex, "Save snapshot failed for {Id}", rb.Id); 
                                    }
                                }
                            }
                            else
                            {
                                // 战斗完成后的清理
                                try 
                                { 
                                    // 清理快照
                                    await _snapshot.DeleteAsync(rb.Id, stoppingToken); 
                                    
                                    // 停止SignalR广播
                                    _combatBroadcaster.StopBroadcast(rb.Id.ToString());
                                    
                                    // 清理帧状态
                                    rb.CleanupFrameState();
                                    
                                    _logger.LogInformation("战斗 {BattleId} 已完成，已清理相关资源", rb.Id);
                                } 
                                catch (Exception ex)
                                {
                                    _logger.LogError(ex, "清理战斗 {BattleId} 资源时出错", rb.Id);
                                }
                            }
                        }
                    }
                }

                // 3. 回收已完成的战斗
                _coordinator.PruneCompleted(TimeSpan.FromMinutes(5));
                
                // 4. 短暂休眠，避免CPU满载
                await Task.Delay(50, stoppingToken);
            }
        }
        catch (OperationCanceledException) 
        { 
            _logger.LogInformation("StepBattleHostedService正常停止");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "StepBattleHostedService loop failed.");
        }
        
        _logger.LogInformation("StepBattleHostedService stopped.");
    }

    /// <summary>
    /// 本地节流器：记录最近一次保存时的"模拟时间"
    /// 防止快照保存过于频繁
    /// </summary>
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
