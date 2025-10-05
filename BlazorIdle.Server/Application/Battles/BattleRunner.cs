using BlazorIdle.Server.Domain.Characters;
using BlazorIdle.Server.Domain.Combat;
using BlazorIdle.Server.Domain.Combat.Enemies;
using BlazorIdle.Server.Domain.Combat.Procs;
using BlazorIdle.Server.Domain.Combat.Professions;
using BlazorIdle.Server.Domain.Combat.Rng;
using BlazorIdle.Shared.Models;
using BlazorWebGame.Domain.Combat;
using System.Collections.Generic;

namespace BlazorIdle.Server.Application.Battles;

public class BattleRunner
{
    public IReadOnlyList<CombatSegment> RunForDuration(
        Battle battle,
        double durationSeconds,
        Profession profession,
        RngContext rng,
        out bool killed,
        out double? killTime,
        out int overkill,
        IProfessionModule? module = null,
        Encounter? encounter = null,
        EncounterGroup? encounterGroup = null,
        CharacterStats? stats = null)
    {
        var clock = new GameClock();
        var scheduler = new EventScheduler();
        var collector = new SegmentCollector();

        var professionModule = module ?? ProfessionRegistry.Resolve(profession);
        var context = new BattleContext(battle, clock, scheduler, collector, professionModule, profession, rng, encounter, encounterGroup, stats);

        professionModule.RegisterBuffDefinitions(context);
        professionModule.OnBattleStart(context);
        professionModule.BuildSkills(context, context.AutoCaster);
        scheduler.Schedule(new ProcPulseEvent(clock.CurrentTime + 1.0, 1.0));

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

        while (scheduler.Count > 0)
        {
            if (safetyCounter++ > safetyLimit)
                throw new System.Exception("Safety limit exceeded in BattleRunner loop");

            context.Buffs.Tick(clock.CurrentTime);
            SyncTrackHaste(context);

            var ev = scheduler.PopNext();
            if (ev == null) break;
            if (ev.ExecuteAt > endTarget)
            {
                battle.Finish(clock.CurrentTime);
                break;
            }

            clock.AdvanceTo(ev.ExecuteAt);

            // 段级 RNG 记录边界：执行前与执行后各记一次
            collector.OnRngIndex(context.Rng.Index);
            ev.Execute(context);
            collector.OnRngIndex(context.Rng.Index);

            collector.Tick(clock.CurrentTime);

            if (collector.ShouldFlush(clock.CurrentTime))
                segments.Add(collector.Flush(clock.CurrentTime));

            if (context.Encounter?.IsDead == true)
            {
                battle.Finish(clock.CurrentTime);
                break;
            }
        }

        if (collector.EventCount > 0)
            segments.Add(collector.Flush(clock.CurrentTime));

        killed = context.Encounter?.IsDead ?? false;
        killTime = context.Encounter?.KillTime;
        overkill = context.Encounter?.Overkill ?? 0;

        return segments;
    }

    private static void SyncTrackHaste(BattleContext context)
    {
        var agg = context.Buffs.Aggregate;
        foreach (var t in context.Tracks)
        {
            if (t.TrackType == TrackType.Attack)
                t.SetHaste(agg.ApplyToBaseHaste(1.0 + context.Stats.HastePercent));
        }
    }
}