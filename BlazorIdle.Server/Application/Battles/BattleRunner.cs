using BlazorIdle.Server.Domain.Combat;
using BlazorWebGame.Domain.Combat;
using System.Collections.Generic;

namespace BlazorIdle.Server.Application.Battles;

public class BattleRunner
{
    public IReadOnlyList<CombatSegment> RunForDuration(Battle battle, double durationSeconds)
    {
        var clock = new GameClock();
        var scheduler = new EventScheduler();
        var collector = new SegmentCollector();
        var context = new BattleContext(battle, clock, scheduler, collector);

        // 初始化 Track
        var attackTrack = new TrackState(TrackType.Attack, battle.AttackIntervalSeconds, 0);
        // Special 默认首个脉冲在间隔末尾（如果想 0 立即触发可把 startAt 改为 0）
        var specialTrack = new TrackState(TrackType.Special, battle.SpecialIntervalSeconds, battle.SpecialIntervalSeconds);

        context.Tracks.Add(attackTrack);
        context.Tracks.Add(specialTrack);

        scheduler.Schedule(new AttackTickEvent(attackTrack.NextTriggerAt, attackTrack));
        scheduler.Schedule(new SpecialPulseEvent(specialTrack.NextTriggerAt, specialTrack));

        var segments = new List<CombatSegment>();
        var endTarget = durationSeconds;
        int safetyCounter = 0;
        const int safetyLimit = 100000; // 防御异常循环

        while (scheduler.Count > 0)
        {
            if (safetyCounter++ > safetyLimit)
                throw new System.Exception("Safety limit exceeded in BattleRunner loop (possible scheduling bug)");

            var ev = scheduler.PopNext();
            if (ev == null) break;
            if (ev.ExecuteAt > endTarget)
            {
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

        if (collector.EventCount > 0)
        {
            segments.Add(collector.Flush(clock.CurrentTime));
        }

        return segments;
    }
}