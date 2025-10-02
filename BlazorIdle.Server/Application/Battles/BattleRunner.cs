using System.Collections.Generic;
using BlazorWebGame.Domain.Combat;

namespace BlazorWebGame.Application.Battles;

public class BattleRunner
{
    public IReadOnlyList<CombatSegment> RunForDuration(Battle battle, double durationSeconds)
    {
        var clock = new GameClock();
        var scheduler = new EventScheduler();
        var collector = new SegmentCollector();
        var context = new BattleContext(battle, clock, scheduler, collector);

        scheduler.Schedule(new AttackTickEvent(0));

        var segments = new List<CombatSegment>();
        var endTarget = durationSeconds;

        while (scheduler.Count > 0)
        {
            var ev = scheduler.PopNext();
            if (ev == null) break;
            if (ev.ExecuteAt > endTarget)
            {
                // 超出模拟窗口，结束
                battle.Finish(clock.CurrentTime);
                break;
            }
            clock.AdvanceTo(ev.ExecuteAt);
            ev.Execute(context);
            collector.Tick(clock.CurrentTime);

            if (collector.ShouldFlush(clock.CurrentTime))
            {
                segments.Add(collector.Flush(clock.CurrentTime));
            }
        }

        // 最后一段
        if (collector.EventCount > 0)
        {
            segments.Add(collector.Flush(clock.CurrentTime));
        }

        return segments;
    }
}