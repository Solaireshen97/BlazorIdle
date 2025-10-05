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

    public bool Completed { get; private set; }
    public bool Killed { get; private set; }
    public double? KillTime { get; private set; }
    public int Overkill { get; private set; }

    public ulong Seed { get; }
    public long SeedIndexStart { get; }
    public long SeedIndexEnd => Context.Rng.Index;

    // 真实时间推进锚点
    public DateTime StartedWallUtc { get; }
    private DateTime _lastAdvanceWallUtc;
    public double SimSpeed { get; } = 1.0; // 1x 实时；可扩展为加速回放

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

        // 初始化
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

        // 真实时间锚点
        StartedWallUtc = DateTime.UtcNow;
        _lastAdvanceWallUtc = StartedWallUtc;
    }

    // 真实时间驱动：每次只推进 ≤ (wallDelta * SimSpeed) 的模拟时长，且不超过 maxSimSecondsSlice
    public void Advance(int maxEvents = 2000, double maxSimSecondsSlice = 0.25)
    {
        if (Completed) return;

        var wallNow = DateTime.UtcNow;
        var wallDelta = (wallNow - _lastAdvanceWallUtc).TotalSeconds;
        if (wallDelta <= 0.0005) return; // 本帧真实时间太短，跳过

        // 允许推进的模拟时长（受墙钟 + 上限限制）
        var allowedDelta = Math.Min(wallDelta * SimSpeed, Math.Max(0.001, maxSimSecondsSlice));

        // 切片结束时间 = 当前模拟时间 + 允许推进
        var sliceEnd = Math.Min(TargetDurationSeconds, Clock.CurrentTime + allowedDelta);
        int safety = 0;

        while (Scheduler.Count > 0 && safety++ < maxEvents)
        {
            // 当前时刻先推进周期效果与急速聚合
            Context.Buffs.Tick(Clock.CurrentTime);
            SyncTrackHaste(Context);

            var ev = Scheduler.PopNext();
            if (ev is null) break;

            // 超出本帧切片：不执行，requeue，并 idle 前进到 sliceEnd
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
                break; // 结束本帧
            }

            // 超过总目标时长：结束
            if (ev.ExecuteAt > TargetDurationSeconds)
            {
                Battle.Finish(Clock.CurrentTime);
                break;
            }

            // 执行事件
            Clock.AdvanceTo(ev.ExecuteAt);
            ev.Execute(Context);
            Collector.Tick(Clock.CurrentTime);

            if (Collector.ShouldFlush(Clock.CurrentTime))
                Segments.Add(Collector.Flush(Clock.CurrentTime));

            // 击杀终止
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

        // 收尾：达到总时长或队列空 → 完成
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

        // 更新墙钟基准
        _lastAdvanceWallUtc = wallNow;
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