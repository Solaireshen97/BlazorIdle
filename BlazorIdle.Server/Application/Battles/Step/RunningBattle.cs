using System;
using System.Collections.Generic;
using System.Linq;
using BlazorIdle.Server.Domain.Characters;
using BlazorIdle.Server.Domain.Combat;
using BlazorIdle.Server.Domain.Combat.Enemies;
using BlazorIdle.Server.Domain.Combat.Professions;
using BlazorIdle.Server.Domain.Combat.Rng;
using BlazorIdle.Server.Domain.Combat.Procs;
using BlazorWebGame.Domain.Combat;
using BlazorIdle.Shared.Models;

namespace BlazorIdle.Server.Application.Battles.Step;

public sealed class RunningBattle
{
    public Guid Id { get; }
    public Guid CharacterId { get; }
    public Profession Profession { get; }
    public double TargetDurationSeconds { get; }
    public string EnemyId { get; }
    public int EnemyCount { get; }

    public Battle Battle { get; }
    public IGameClock Clock { get; }
    public IEventScheduler Scheduler { get; }
    public SegmentCollector Collector { get; }
    public BattleContext Context { get; }
    public List<CombatSegment> Segments { get; } = new();

    public bool Completed { get; internal set; }
    public bool Killed { get; private set; }
    public double? KillTime { get; private set; }
    public int Overkill { get; private set; }

    public ulong Seed { get; }
    public long SeedIndexStart { get; }
    public long SeedIndexEnd => Context.Rng.Index;

    public bool Persisted { get; internal set; }
    public Guid? PersistedBattleId { get; internal set; }

    public DateTime StartedWallUtc { get; }
    private DateTime _lastAdvanceWallUtc;
    public double SimSpeed { get; } = 1.0;

    public RunningBattle(
        Guid id,
        Guid characterId,
        Profession profession,
        ulong seed,
        double targetSeconds,
        EnemyDefinition enemyDef,
        int enemyCount,
        CharacterStats stats,
        IProfessionModule? module = null)
    {
        Id = id;
        CharacterId = characterId;
        Profession = profession;
        TargetDurationSeconds = targetSeconds;
        EnemyId = enemyDef.Id;
        EnemyCount = Math.Max(1, enemyCount);
        Seed = seed;

        var rng = new RngContext(seed);
        SeedIndexStart = rng.Index;

        var groupDefs = Enumerable.Range(0, EnemyCount).Select(_ => enemyDef).ToList();
        var encounterGroup = new EncounterGroup(groupDefs);

        Battle = new Battle
        {
            Id = id,
            CharacterId = characterId,
            AttackIntervalSeconds = (module ?? ProfessionRegistry.Resolve(profession)).BaseAttackInterval,
            SpecialIntervalSeconds = (module ?? ProfessionRegistry.Resolve(profession)).BaseSpecialInterval,
            StartedAt = 0
        };

        Clock = new GameClock();
        Scheduler = new EventScheduler();
        Collector = new SegmentCollector();

        var professionModule = module ?? ProfessionRegistry.Resolve(profession);
        Context = new BattleContext(Battle, Clock, Scheduler, Collector, professionModule, profession, rng,
            encounter: null, encounterGroup: encounterGroup, stats: stats);

        professionModule.RegisterBuffDefinitions(Context);
        professionModule.OnBattleStart(Context);
        professionModule.BuildSkills(Context, Context.AutoCaster);
        Scheduler.Schedule(new ProcPulseEvent(Clock.CurrentTime + 1.0, 1.0));

        var attackTrack = new TrackState(TrackType.Attack, Battle.AttackIntervalSeconds, 0);
        var specialTrack = new TrackState(TrackType.Special, Battle.SpecialIntervalSeconds, Battle.SpecialIntervalSeconds);
        Context.Tracks.Add(attackTrack);
        Context.Tracks.Add(specialTrack);

        Scheduler.Schedule(new AttackTickEvent(attackTrack.NextTriggerAt, attackTrack));
        Scheduler.Schedule(new SpecialPulseEvent(specialTrack.NextTriggerAt, specialTrack));

        StartedWallUtc = DateTime.UtcNow;
        _lastAdvanceWallUtc = StartedWallUtc;
    }

    public void Advance(int maxEvents = 2000, double maxSimSecondsSlice = 0.25)
    {
        if (Completed) return;

        var wallNow = DateTime.UtcNow;
        var wallDelta = (wallNow - _lastAdvanceWallUtc).TotalSeconds;
        if (wallDelta <= 0.0005) return;

        var allowedDelta = Math.Min(wallDelta * SimSpeed, Math.Max(0.001, maxSimSecondsSlice));
        var sliceEnd = Math.Min(TargetDurationSeconds, Clock.CurrentTime + allowedDelta);
        int safety = 0;

        while (Scheduler.Count > 0 && safety++ < maxEvents)
        {
            Context.Buffs.Tick(Clock.CurrentTime);
            SyncTrackHaste(Context);

            var ev = Scheduler.PopNext();
            if (ev is null) break;

            if (ev.ExecuteAt > sliceEnd)
            {
                Scheduler.Schedule(ev);

                if (sliceEnd > Clock.CurrentTime)
                {
                    Clock.AdvanceTo(sliceEnd);
                    Collector.Tick(Clock.CurrentTime);
                    if (Collector.ShouldFlush(Clock.CurrentTime))
                        Segments.Add(Collector.Flush(Clock.CurrentTime));
                }
                break;
            }

            if (ev.ExecuteAt > TargetDurationSeconds)
            {
                Battle.Finish(Clock.CurrentTime);
                break;
            }

            Clock.AdvanceTo(ev.ExecuteAt);
            ev.Execute(Context);
            Collector.Tick(Clock.CurrentTime);

            if (Collector.ShouldFlush(Clock.CurrentTime))
                Segments.Add(Collector.Flush(Clock.CurrentTime));

            if (Context.Encounter?.IsDead == true)
            {
                Battle.Finish(Clock.CurrentTime);
                Killed = true;
                KillTime = Context.Encounter.KillTime;
                Overkill = Context.Encounter.Overkill;
                Completed = true;

                if (Collector.EventCount > 0)
                    Segments.Add(Collector.Flush(Clock.CurrentTime));
                _lastAdvanceWallUtc = wallNow;
                return;
            }
        }

        if (Clock.CurrentTime >= TargetDurationSeconds || Scheduler.Count == 0)
        {
            if (Collector.EventCount > 0)
                Segments.Add(Collector.Flush(Clock.CurrentTime));

            Killed = Context.Encounter?.IsDead ?? false;
            KillTime = Context.Encounter?.KillTime;
            Overkill = Context.Encounter?.Overkill ?? 0;

            Completed = true;
            Battle.Finish(Clock.CurrentTime);
        }

        _lastAdvanceWallUtc = wallNow;
    }

    // 手动终止：不再推进模拟时间，直接按当前时刻封盘。
    public void ForceStopAndSeal()
    {
        if (Completed) return;

        // 不推进 Clock，直接以当前时刻封盘
        if (Collector.EventCount > 0)
            Segments.Add(Collector.Flush(Clock.CurrentTime));

        Killed = Context.Encounter?.IsDead ?? false;
        KillTime = Context.Encounter?.KillTime;
        Overkill = Context.Encounter?.Overkill ?? 0;

        Completed = true;
        Battle.Finish(Clock.CurrentTime);
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

    // 快速推进到目标模拟秒（忽略墙钟限速），用于恢复
    public void FastForwardTo(double targetSimSeconds)
    {
        if (Completed) return;
        targetSimSeconds = Math.Max(0, Math.Min(TargetDurationSeconds, targetSimSeconds));

        // 采用“大切片 + 极大事件预算”的推进；每步让 allowedDelta >= 剩余
        while (Clock.CurrentTime + 1e-6 < targetSimSeconds && !Completed)
        {
            // 让墙钟增量看起来很大，allowedDelta 由 maxSlice 控制
            _lastAdvanceWallUtc = _lastAdvanceWallUtc.AddSeconds(-3600);
            var remain = targetSimSeconds - Clock.CurrentTime;
            var slice = Math.Min(5.0, Math.Max(0.001, remain));
            Advance(maxEvents: 1_000_000, maxSimSecondsSlice: slice);
            if (Scheduler.Count == 0) break;
        }
    }
}