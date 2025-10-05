using BlazorIdle.Server.Domain.Characters;
using BlazorIdle.Server.Domain.Combat.Buffs;
using BlazorIdle.Server.Domain.Combat.Damage;
using BlazorIdle.Server.Domain.Combat.Enemies;
using BlazorIdle.Server.Domain.Combat.Procs;
using BlazorIdle.Server.Domain.Combat.Professions;
using BlazorIdle.Server.Domain.Combat.Rng;
using BlazorIdle.Server.Domain.Combat.Skills;
using BlazorIdle.Shared.Models;
using BlazorWebGame.Domain.Combat;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BlazorIdle.Server.Domain.Combat.Engine;

/// <summary>
/// 统一的战斗引擎核心：事件队列驱动 + 段聚合 + RNG 区间记录 + 终止判定。
/// 同步与异步 Step 均调用此引擎，避免战斗逻辑重复。
/// </summary>
public sealed class BattleEngine
{
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

    public long SeedIndexStart { get; }
    public long SeedIndexEnd => Context.Rng.Index;

    public BattleEngine(
        Guid battleId,
        Guid characterId,
        Profession profession,
        CharacterStats stats,
        RngContext rng,
        EnemyDefinition enemyDef,
        int enemyCount,
        IProfessionModule? module = null)
    {
        var professionModule = module ?? ProfessionRegistry.Resolve(profession);

        Battle = new Battle
        {
            Id = battleId,
            CharacterId = characterId,
            AttackIntervalSeconds = professionModule.BaseAttackInterval,
            SpecialIntervalSeconds = professionModule.BaseSpecialInterval,
            StartedAt = 0
        };

        Clock = new GameClock();
        Scheduler = new EventScheduler();
        Collector = new SegmentCollector();

        // 构造 EncounterGroup
        enemyCount = Math.Max(1, enemyCount);
        var groupDefs = Enumerable.Range(0, enemyCount).Select(_ => enemyDef).ToList();
        var encounterGroup = new EncounterGroup(groupDefs);

        Context = new BattleContext(
            battle: Battle,
            clock: Clock,
            scheduler: Scheduler,
            collector: Collector,
            professionModule: professionModule,
            profession: profession,
            rng: rng,
            encounter: null,
            encounterGroup: encounterGroup,
            stats: stats
        );

        SeedIndexStart = rng.Index;

        // 职业钩子 + 技能
        professionModule.RegisterBuffDefinitions(Context);
        professionModule.OnBattleStart(Context);
        professionModule.BuildSkills(Context, Context.AutoCaster);

        // 轨道与初始事件
        var attackTrack = new TrackState(TrackType.Attack, Battle.AttackIntervalSeconds, 0);
        var specialTrack = new TrackState(TrackType.Special, Battle.SpecialIntervalSeconds, Battle.SpecialIntervalSeconds);
        Context.Tracks.Add(attackTrack);
        Context.Tracks.Add(specialTrack);

        Scheduler.Schedule(new AttackTickEvent(attackTrack.NextTriggerAt, attackTrack));
        Scheduler.Schedule(new SpecialPulseEvent(specialTrack.NextTriggerAt, specialTrack));
        Scheduler.Schedule(new ProcPulseEvent(Clock.CurrentTime + 1.0, 1.0));
    }

    /// <summary>
    /// 推进到指定模拟时间上限（sliceEnd），或达到 maxEvents 限制为止。
    /// 在事件执行前后统一记录 RNG Index 到段聚合。
    /// </summary>
    public void AdvanceTo(double sliceEnd, int maxEvents)
    {
        if (Completed) return;

        int safety = 0;
        sliceEnd = Math.Max(Clock.CurrentTime, sliceEnd);

        while (Scheduler.Count > 0 && safety++ < maxEvents)
        {
            // 周期效果与急速同步
            Context.Buffs.Tick(Clock.CurrentTime);
            SyncTrackHaste(Context);

            var ev = Scheduler.PopNext();
            if (ev is null) break;

            // 下一事件超出切片上限：回灌并推进时间到切片边界（不执行事件）
            if (ev.ExecuteAt > sliceEnd)
            {
                Scheduler.Schedule(ev);
                if (sliceEnd > Clock.CurrentTime)
                {
                    Clock.AdvanceTo(sliceEnd);
                    Collector.Tick(Clock.CurrentTime);
                    TryFlushSegment();
                }
                return;
            }

            // 推进时间到事件时刻并执行
            Clock.AdvanceTo(ev.ExecuteAt);

            // 段级 RNG 区间：执行前/后各记录一次（与同步保持一致）
            Collector.OnRngIndex(Context.Rng.Index);
            ev.Execute(Context);
            Collector.OnRngIndex(Context.Rng.Index);

            Collector.Tick(Clock.CurrentTime);
            TryFlushSegment();

            // 目标死亡 → 封盘
            if (Context.Encounter?.IsDead == true)
            {
                FinalizeNow();
                return;
            }
        }
    }

    /// <summary>
    /// 一次性推进直至 targetTime 或事件耗尽/死亡。
    /// </summary>
    public void AdvanceUntil(double targetTime, int maxEventsPerSlice = 5000, double maxSliceSeconds = 5.0)
    {
        if (Completed) return;

        targetTime = Math.Max(Clock.CurrentTime, targetTime);
        while (!Completed && Clock.CurrentTime + 1e-9 < targetTime && Scheduler.Count > 0)
        {
            var sliceEnd = Math.Min(targetTime, Clock.CurrentTime + maxSliceSeconds);
            AdvanceTo(sliceEnd, maxEventsPerSlice);
        }

        // 时间达标或事件耗尽 → 最终落段并封盘
        if (!Completed && (Clock.CurrentTime + 1e-9 >= targetTime || Scheduler.Count == 0))
        {
            TryFlushSegment(force: true);
            FinalizeNow();
        }
    }

    public void FinalizeNow()
    {
        if (Completed) return;

        // 最终段 flush
        TryFlushSegment(force: true);

        Killed = Context.Encounter?.IsDead ?? false;
        KillTime = Context.Encounter?.KillTime;
        Overkill = Context.Encounter?.Overkill ?? 0;

        Completed = true;
        Battle.Finish(Clock.CurrentTime);
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
            // 如需：Special 轨道也可在未来开放急速影响
        }
    }
}