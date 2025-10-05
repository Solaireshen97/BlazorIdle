using BlazorIdle.Server.Domain.Characters;
using BlazorIdle.Server.Domain.Combat;
using BlazorIdle.Server.Domain.Combat.Enemies;
using BlazorIdle.Server.Domain.Combat.Procs;
using BlazorIdle.Server.Domain.Combat.Professions;
using BlazorIdle.Server.Domain.Combat.Rng;
using BlazorIdle.Shared.Models;
using BlazorWebGame.Domain.Combat;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BlazorIdle.Server.Application.Battles.Step;

public enum StepBattleMode
{
    Duration = 0,      // 原有：按目标秒数结束
    Continuous = 1,    // 持续：怪死就重生，同配置
    DungeonSingle = 2, // 地城：按波完成一次后结束
    DungeonLoop = 3    // 地城循环：完成后重置第一波继续
}

public sealed class RunningBattle
{
    public Guid Id { get; }
    public Guid CharacterId { get; }
    public Profession Profession { get; }
    public double TargetDurationSeconds { get; }
    public string EnemyId { get; }
    public int EnemyCount { get; }

    public StepBattleMode Mode { get; }
    public string? DungeonId { get; }
    public int WaveIndex => _provider?.CurrentWaveIndex ?? 1;
    public int RunCount => _provider?.CompletedRunCount ?? 0;

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

    private readonly IEncounterProvider _provider;

    public RunningBattle(
        Guid id,
        Guid characterId,
        Profession profession,
        ulong seed,
        double targetSeconds,
        EnemyDefinition enemyDef,
        int enemyCount,
        CharacterStats stats,
        StepBattleMode mode = StepBattleMode.Duration,
        string? dungeonId = null,
        IProfessionModule? module = null)
    {
        Id = id;
        CharacterId = characterId;
        Profession = profession;
        TargetDurationSeconds = targetSeconds;
        EnemyId = enemyDef.Id;
        EnemyCount = Math.Max(1, enemyCount);
        Mode = mode;
        DungeonId = dungeonId;
        Seed = seed;

        var rng = new RngContext(seed);
        SeedIndexStart = rng.Index;

        // Provider & 初始敌群
        if (mode == StepBattleMode.DungeonSingle || mode == StepBattleMode.DungeonLoop)
        {
            var dungeon = DungeonRegistry.Resolve(dungeonId ?? "intro_cave");
            _provider = new DungeonEncounterProvider(dungeon, loop: mode == StepBattleMode.DungeonLoop);
        }
        else
        {
            _provider = new ContinuousEncounterProvider(enemyDef, EnemyCount);
        }

        var encounterGroup = _provider.CurrentGroup;

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
        var sliceEnd = (Mode == StepBattleMode.Duration)
            ? Math.Min(TargetDurationSeconds, Clock.CurrentTime + allowedDelta)
            : (Clock.CurrentTime + allowedDelta);

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

            if (Mode == StepBattleMode.Duration && ev.ExecuteAt > TargetDurationSeconds)
            {
                Battle.Finish(Clock.CurrentTime);
                break;
            }

            Clock.AdvanceTo(ev.ExecuteAt);

            // 段级 RNG 记录
            Collector.OnRngIndex(Context.Rng.Index);
            ev.Execute(Context);
            Collector.OnRngIndex(Context.Rng.Index);

            Collector.Tick(Clock.CurrentTime);

            if (Collector.ShouldFlush(Clock.CurrentTime))
                Segments.Add(Collector.Flush(Clock.CurrentTime));

            // 死亡处理：根据模式切波/循环/结束
            if (Context.Encounter?.IsDead == true)
            {
                Killed = true;
                KillTime = Context.Encounter.KillTime;
                Overkill = Context.Encounter.Overkill;

                Collector.OnTag("encounter_cleared", 1);

                if (Mode == StepBattleMode.Duration)
                {
                    Battle.Finish(Clock.CurrentTime);
                    Completed = true;

                    if (Collector.EventCount > 0)
                        Segments.Add(Collector.Flush(Clock.CurrentTime));
                    _lastAdvanceWallUtc = wallNow;
                    return;
                }
                else
                {
                    // 尝试获取下一波/下一轮
                    if (_provider.TryAdvance(out var nextGroup, out var runCompleted) && nextGroup is not null)
                    {
                        if (runCompleted)
                            Collector.OnTag("dungeon_run_complete", 1);

                        Context.ResetEncounterGroup(nextGroup);
                        // 切换主目标
                        Context.RefreshPrimaryEncounter();

                        // 重置“本次击杀状态”标志，仅用于状态展示（可选）
                        Killed = false;
                        KillTime = null;
                        Overkill = 0;

                        // 继续 while 循环，战斗不停
                        continue;
                    }
                    else
                    {
                        // 非循环地城：完成一次后结束
                        Battle.Finish(Clock.CurrentTime);
                        Completed = true;

                        if (Collector.EventCount > 0)
                            Segments.Add(Collector.Flush(Clock.CurrentTime));
                        _lastAdvanceWallUtc = wallNow;
                        return;
                    }
                }
            }
        }

        // 仅在 Duration 模式下因时间达标结束；其他模式不因时间自然结束
        if ((Mode == StepBattleMode.Duration && Clock.CurrentTime >= TargetDurationSeconds) || Scheduler.Count == 0)
        {
            if (Collector.EventCount > 0)
                Segments.Add(Collector.Flush(Clock.CurrentTime));

            Completed = true;
            Battle.Finish(Clock.CurrentTime);
        }

        _lastAdvanceWallUtc = wallNow;
    }

    public void ForceStopAndSeal()
    {
        if (Completed) return;

        if (Collector.EventCount > 0)
            Segments.Add(Collector.Flush(Clock.CurrentTime));

        // 持续/地城模式：Stop 时强制封盘
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

    public void FastForwardTo(double targetSimSeconds)
    {
        if (Completed) return;
        targetSimSeconds = Math.Max(0, (Mode == StepBattleMode.Duration
            ? Math.Min(TargetDurationSeconds, targetSimSeconds)
            : targetSimSeconds));

        while (Clock.CurrentTime + 1e-6 < targetSimSeconds && !Completed)
        {
            _lastAdvanceWallUtc = _lastAdvanceWallUtc.AddSeconds(-3600);
            var remain = targetSimSeconds - Clock.CurrentTime;
            var slice = Math.Min(5.0, Math.Max(0.001, remain));
            Advance(maxEvents: 1_000_000, maxSimSecondsSlice: slice);
            if (Scheduler.Count == 0) break;
        }
    }
}