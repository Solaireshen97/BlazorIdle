using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BlazorIdle.Server.Application.Battles.Step;

public sealed class StepBattleHostedService : BackgroundService
{
    private readonly StepBattleCoordinator _coordinator;
    private readonly StepBattleSnapshotService _snapshot;
    private readonly ILogger<StepBattleHostedService> _logger;

    // 每保存一次快照的最短“模拟时间间隔”
    private const double SnapshotIntervalSimSeconds = 2.0;

    public StepBattleHostedService(StepBattleCoordinator coordinator, StepBattleSnapshotService snapshot, ILogger<StepBattleHostedService> logger)
    {
        _coordinator = coordinator;
        _snapshot = snapshot;
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