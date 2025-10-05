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
    Duration = 0,
    Continuous = 1,
    DungeonSingle = 2,
    DungeonLoop = 3
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

    // 刷新等待状态
    private EncounterGroup? _pendingNextGroup;
    private double? _pendingSpawnAt;
    private bool _waitingSpawn; // 已安排刷新但尚未执行

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
        double? continuousRespawnDelaySeconds = null,
        double? dungeonWaveDelaySeconds = null,
        double? dungeonRunDelaySeconds = null,
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

        if (mode == StepBattleMode.DungeonSingle || mode == StepBattleMode.DungeonLoop)
        {
            var dungeon = DungeonRegistry.Resolve(dungeonId ?? "intro_cave");
            _provider = new DungeonEncounterProvider(
                dungeon,
                loop: mode == StepBattleMode.DungeonLoop,
                waveDelayOverride: dungeonWaveDelaySeconds,
                runDelayOverride: dungeonRunDelaySeconds
            );
        }
        else
        {
            _provider = new ContinuousEncounterProvider(enemyDef, EnemyCount, respawnDelaySeconds: continuousRespawnDelaySeconds ?? 3.0);
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

    // 波是否清空：整波全部死亡才算清场
    private bool IsWaveCleared()
    {
        var grp = Context.EncounterGroup;
        if (grp is null)
            return Context.Encounter?.IsDead == true;
        return grp.All.All(e => e.IsDead);
    }

    // 如果主目标已死且仍有存活单位，则切换到下一个存活的主目标
    private bool TryRetargetPrimaryIfDead()
    {
        var grp = Context.EncounterGroup;
        if (grp is null) return false;

        // 若已有刷新安排则不 retarget（下一波即将刷新）
        if (_waitingSpawn) return false;

        // 当前主目标已死，但波未清空 → 选下一个存活
        if (Context.Encounter is null || Context.Encounter.IsDead)
        {
            var next = grp.PrimaryAlive();
            if (next is not null && !next.IsDead)
            {
                Context.RefreshPrimaryEncounter();
                Collector.OnTag("retarget_primary", 1);
                return true;
            }
        }
        return false;
    }

    public void Advance(int maxEvents = 2000, double maxSimSecondsSlice = 0.25)
    {
        if (Completed) return;

        var wallNow = DateTime.UtcNow;
        var wallDelta = (wallNow - _lastAdvanceWallUtc).TotalSeconds;
        if (wallDelta <= 0.0005) return;

        // 入口补救：若错过了刷新点，立即执行刷新
        if (_waitingSpawn && _pendingSpawnAt.HasValue && Clock.CurrentTime + 1e-9 >= _pendingSpawnAt.Value)
            TryPerformPendingSpawn();

        // 入口：如主目标已死但仍有存活单位，先重选主目标，避免后续事件打在已死亡目标上
        TryRetargetPrimaryIfDead();

        var allowedDelta = Math.Min(wallDelta * SimSpeed, Math.Max(0.001, maxSimSecondsSlice));
        var desiredSliceEnd = (Mode == StepBattleMode.Duration)
            ? Math.Min(TargetDurationSeconds, Clock.CurrentTime + allowedDelta)
            : (Clock.CurrentTime + allowedDelta);

        // 切片上限不能回退；若在等待刷新，则卡到 spawnAt
        double effectiveSliceEnd = desiredSliceEnd;
        if (_waitingSpawn && _pendingSpawnAt.HasValue)
        {
            var spawnAt = Math.Max(_pendingSpawnAt.Value, Clock.CurrentTime);
            effectiveSliceEnd = Math.Min(desiredSliceEnd, spawnAt);
        }

        int safety = 0;

        // 队列为空但有待刷新：推进到刷新时刻并执行
        if (Scheduler.Count == 0 && _waitingSpawn && _pendingSpawnAt.HasValue)
        {
            var to = Math.Min(effectiveSliceEnd, Math.Max(_pendingSpawnAt.Value, Clock.CurrentTime));
            if (to > Clock.CurrentTime + 1e-9)
            {
                Clock.AdvanceTo(to);
                Collector.Tick(Clock.CurrentTime);
                TryFlushSegment();
            }
            TryPerformPendingSpawn();
        }

        while (Scheduler.Count > 0 && safety++ < maxEvents)
        {
            var peek = Scheduler.PeekNext();
            if (peek is not null && peek.ExecuteAt > effectiveSliceEnd)
            {
                if (effectiveSliceEnd > Clock.CurrentTime + 1e-9)
                {
                    Clock.AdvanceTo(effectiveSliceEnd);
                    Collector.Tick(Clock.CurrentTime);
                    TryFlushSegment();
                }
                TryPerformPendingSpawn();
                break;
            }

            Context.Buffs.Tick(Clock.CurrentTime);
            SyncTrackHaste(Context);

            var ev = Scheduler.PopNext();
            if (ev is null) break;

            if (Mode == StepBattleMode.Duration && ev.ExecuteAt > TargetDurationSeconds)
            {
                Battle.Finish(Clock.CurrentTime);
                break;
            }

            // 执行事件前再次确保有活体主目标（例如上一事件刚杀死主目标）
            TryRetargetPrimaryIfDead();

            Clock.AdvanceTo(ev.ExecuteAt);

            Collector.OnRngIndex(Context.Rng.Index);
            ev.Execute(Context);
            Collector.OnRngIndex(Context.Rng.Index);

            Collector.Tick(Clock.CurrentTime);
            TryFlushSegment();

            // 执行后：若主目标已死但整波未清空，立刻 retarget 继续本波
            if (!IsWaveCleared())
            {
                // 主目标可能刚被打死，快速切到下一个
                TryRetargetPrimaryIfDead();
            }
            else
            {
                // 波已清空：进入刷新安排（仅安排一次）
                if (Mode == StepBattleMode.Duration)
                {
                    Battle.Finish(Clock.CurrentTime);
                    Completed = true;
                    TryFlushSegment(force: true);
                    _lastAdvanceWallUtc = wallNow;
                    return;
                }
                else
                {
                    if (!_waitingSpawn)
                    {
                        if (_provider.TryAdvance(out var nextGroup, out var runCompleted) && nextGroup is not null)
                        {
                            var delay = Math.Max(0.0, _provider.GetRespawnDelaySeconds(runCompleted));
                            _pendingNextGroup = nextGroup;
                            _pendingSpawnAt = Clock.CurrentTime + delay;
                            _waitingSpawn = true;

                            if (runCompleted) Collector.OnTag("dungeon_run_complete", 1);
                            Collector.OnTag("spawn_scheduled", 1);

                            // 近似展示：以最后一个死亡目标的时间/溢出作为本波的 Killed 信息
                            var last = Context.EncounterGroup?.All
                                .OrderByDescending(e => e.KillTime ?? -1)
                                .FirstOrDefault();
                            Killed = true;
                            KillTime = last?.KillTime ?? KillTime;
                            Overkill = last?.Overkill ?? Overkill;

                            var spawnAt = Math.Max(_pendingSpawnAt.Value, Clock.CurrentTime);
                            effectiveSliceEnd = Math.Min(desiredSliceEnd, spawnAt);
                        }
                        else
                        {
                            // 非循环地城：完成一次后结束
                            Battle.Finish(Clock.CurrentTime);
                            Completed = true;
                            TryFlushSegment(force: true);
                            _lastAdvanceWallUtc = wallNow;
                            return;
                        }
                    }
                }
            }

            if (_waitingSpawn && _pendingSpawnAt.HasValue && Clock.CurrentTime + 1e-9 >= _pendingSpawnAt.Value)
            {
                TryPerformPendingSpawn();
            }

            if (Clock.CurrentTime + 1e-9 >= effectiveSliceEnd) break;
        }

        if (Mode == StepBattleMode.Duration)
        {
            if (Clock.CurrentTime >= TargetDurationSeconds || Scheduler.Count == 0)
            {
                TryFlushSegment(force: true);
                Completed = true;
                Battle.Finish(Clock.CurrentTime);
            }
        }

        _lastAdvanceWallUtc = wallNow;
    }

    public void ForceStopAndSeal()
    {
        if (Completed) return;
        TryFlushSegment(force: true);
        Completed = true;
        Battle.Finish(Clock.CurrentTime);
    }

    private void TryPerformPendingSpawn()
    {
        if (_waitingSpawn && _pendingSpawnAt.HasValue && _pendingNextGroup is not null && Clock.CurrentTime + 1e-9 >= _pendingSpawnAt.Value)
        {
            Context.ResetEncounterGroup(_pendingNextGroup);
            Context.RefreshPrimaryEncounter();

            _pendingNextGroup = null;
            _pendingSpawnAt = null;
            _waitingSpawn = false;

            Collector.OnTag("spawn_performed", 1);
        }
    }

    private void TryFlushSegment(bool force = false)
    {
        if (force)
        {
            if (Collector.EventCount > 0)
                Segments.Add(Collector.Flush(Clock.CurrentTime));
            return;
        }

        if (Collector.ShouldFlush(Clock.CurrentTime))
            Segments.Add(Collector.Flush(Clock.CurrentTime));
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