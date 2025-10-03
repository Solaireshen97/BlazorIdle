using BlazorIdle.Server.Domain.Characters;
using BlazorIdle.Server.Domain.Combat;
using BlazorIdle.Server.Domain.Combat.Professions;
using BlazorIdle.Shared.Models;
using BlazorWebGame.Domain.Combat;
using System.Collections.Generic;

namespace BlazorIdle.Server.Application.Battles;

public class BattleRunner
{
    public IReadOnlyList<CombatSegment> RunForDuration(Battle battle, double durationSeconds, Profession profession, IProfessionModule? module = null)
    {
        var clock = new GameClock();
        var scheduler = new EventScheduler();
        var collector = new SegmentCollector();

        var professionModule = module ?? ProfessionRegistry.Resolve(profession);

        var context = new BattleContext(battle, clock, scheduler, collector, professionModule, profession);
        // 1. 先注册本职业会用到的 Buff 定义
        professionModule.RegisterBuffDefinitions(context);
        // 2. 资源 / 初始状态
        professionModule.OnBattleStart(context);
        // 3. 技能
        professionModule.BuildSkills(context, context.AutoCaster);
        // 4. 初始化 Track
        var attackTrack = new TrackState(TrackType.Attack, battle.AttackIntervalSeconds, 0);
        var specialTrack = new TrackState(TrackType.Special, battle.SpecialIntervalSeconds, battle.SpecialIntervalSeconds);

        context.Tracks.Add(attackTrack);
        context.Tracks.Add(specialTrack);

        scheduler.Schedule(new AttackTickEvent(attackTrack.NextTriggerAt, attackTrack));
        scheduler.Schedule(new SpecialPulseEvent(specialTrack.NextTriggerAt, specialTrack));

        var segments = new List<CombatSegment>();
        var endTarget = durationSeconds;
        int safetyCounter = 0;
        const int safetyLimit = 100000;
        // 5. 主循环（示意）
        while (scheduler.Count > 0)
        {
            if (safetyCounter++ > safetyLimit)
                throw new System.Exception("Safety limit exceeded in BattleRunner loop");
            context.Buffs.Tick(clock.CurrentTime);
            SyncTrackHaste(context);                // 再调整加速
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
                segments.Add(collector.Flush(clock.CurrentTime));
        }

        if (collector.EventCount > 0)
            segments.Add(collector.Flush(clock.CurrentTime));

        return segments;
    }

    private void SyncTrackHaste(BattleContext context)
    {
        var agg = context.Buffs.Aggregate;
        foreach (var t in context.Tracks)
        {
            if (t.TrackType == TrackType.Attack)
            {
                t.SetHaste(agg.ApplyToBaseHaste(1.0)); // 原始=1.0
            }
            // SpecialTrack 初版不受 haste，可按需加:
            // else if (t.TrackType == TrackType.Special) ...
        }
    }
}