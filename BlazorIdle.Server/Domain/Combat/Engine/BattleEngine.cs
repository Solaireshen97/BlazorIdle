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
/// 统一战斗引擎：事件队列驱动 + 多怪/波次 + 刷新等待 + RNG 段记录。
/// Step 与同步均调用本引擎推进时间。
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

    public int WaveIndex => _provider?.CurrentWaveIndex ?? 1;
    public int RunCount => _provider?.CompletedRunCount ?? 0;

    // 可选：用于持续/地城的提供器（为空则表示单波/单怪）
    private readonly IEncounterProvider? _provider;

    // 刷新等待状态
    private EncounterGroup? _pendingNextGroup;
    private double? _pendingSpawnAt;
    private bool _waitingSpawn;

    // 仅单波构造（兼容旧逻辑：清场即结束）
    public BattleEngine(
        Guid battleId,
        Guid characterId,
        Profession profession,
        CharacterStats stats,
        RngContext rng,
        EnemyDefinition enemyDef,
        int enemyCount,
        IProfessionModule? module = null)
        : this(battleId, characterId, profession, stats, rng,
               provider: null, initialGroup: new EncounterGroup(Enumerable.Range(0, Math.Max(1, enemyCount)).Select(_ => enemyDef).ToList()),
               module: module)
    {
    }

    // 新增：使用 EncounterProvider（持续/地城/循环），并从 provider.CurrentGroup 开始
    public BattleEngine(
        Guid battleId,
        Guid characterId,
        Profession profession,
        CharacterStats stats,
        RngContext rng,
        IEncounterProvider provider,
        IProfessionModule? module = null)
        : this(battleId, characterId, profession, stats, rng,
               provider: provider, initialGroup: provider.CurrentGroup, module: module)
    {
    }

    // 私有共享构造
    private BattleEngine(
        Guid battleId,
        Guid characterId,
        Profession profession,
        CharacterStats stats,
        RngContext rng,
        IEncounterProvider? provider,
        EncounterGroup initialGroup,
        IProfessionModule? module)
    {
        _provider = provider;

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

        Context = new BattleContext(
            battle: Battle,
            clock: Clock,
            scheduler: Scheduler,
            collector: Collector,
            professionModule: professionModule,
            profession: profession,
            rng: rng,
            encounter: null,
            encounterGroup: initialGroup,
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

    // 波是否清空：整波全部死亡才算清场
    private bool IsWaveCleared()
    {
        var grp = Context.EncounterGroup;
        if (grp is null)
            return Context.Encounter?.IsDead == true;
        return grp.All.All(e => e.IsDead);
    }

    // 若主目标已死而波未清空，立刻重选主目标
    private void TryRetargetPrimaryIfDead()
    {
        if (_waitingSpawn) return; // 已安排刷新则不再换目标
        var grp = Context.EncounterGroup;
        if (grp is null) return;

        if (Context.Encounter is null || Context.Encounter.IsDead)
        {
            var next = grp.PrimaryAlive();
            if (next is not null && !next.IsDead)
            {
                Context.RefreshPrimaryEncounter();
                Collector.OnTag("retarget_primary", 1);
            }
        }
    }

    private void TryScheduleNextWaveIfCleared()
    {
        if (!IsWaveCleared()) return;

        // 单波模式：清场即结束
        if (_provider is null)
        {
            FinalizeNowFromLastKill();
            return;
        }

        if (_waitingSpawn) return;

        if (_provider.TryAdvance(out var nextGroup, out var runCompleted) && nextGroup is not null)
        {
            var delay = Math.Max(0.0, _provider.GetRespawnDelaySeconds(runJustCompleted: runCompleted));
            _pendingNextGroup = nextGroup;
            _pendingSpawnAt = Clock.CurrentTime + delay;
            _waitingSpawn = true;

            if (runCompleted) Collector.OnTag("dungeon_run_complete", 1);
            Collector.OnTag("spawn_scheduled", 1);
        }
        else
        {
            // 地城非循环：整轮完成 → 结束
            FinalizeNowFromLastKill();
        }
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

    private void FinalizeNowFromLastKill()
    {
        // 取本波最后死亡目标的信息用于展示
        var last = Context.EncounterGroup?.All
            .OrderByDescending(e => e.KillTime ?? -1)
            .FirstOrDefault();

        Killed = last?.IsDead ?? (Context.Encounter?.IsDead ?? false);
        KillTime = last?.KillTime ?? Context.Encounter?.KillTime;
        Overkill = last?.Overkill ?? (Context.Encounter?.Overkill ?? 0);

        FinalizeNow();
    }

    /// <summary>
    /// 推进到指定模拟时间上限（sliceEnd），或达到 maxEvents 限制为止。
    /// 在事件执行前后统一记录 RNG Index 到段聚合，并处理多怪/刷新。
    /// </summary>
    public void AdvanceTo(double sliceEnd, int maxEvents)
    {
        if (Completed) return;

        int safety = 0;
        sliceEnd = Math.Max(Clock.CurrentTime, sliceEnd);

        // 入口补救：若错过了刷新点，立即执行刷新
        if (_waitingSpawn && _pendingSpawnAt.HasValue && Clock.CurrentTime + 1e-9 >= _pendingSpawnAt.Value)
            TryPerformPendingSpawn();

        // 切片上限：若在等待刷新，则卡到 spawnAt，但不回退
        double effectiveSliceEnd = sliceEnd;
        if (_waitingSpawn && _pendingSpawnAt.HasValue)
        {
            var spawnAt = Math.Max(_pendingSpawnAt.Value, Clock.CurrentTime);
            effectiveSliceEnd = Math.Min(sliceEnd, spawnAt);
        }

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
            // 若下一个事件超出切片上限：推进到边界并尝试 spawn
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
                return;
            }

            // 常规执行
            Context.Buffs.Tick(Clock.CurrentTime);
            SyncTrackHaste(Context);

            var ev = Scheduler.PopNext();
            if (ev is null) break;

            // 执行前：确保主目标是存活的
            TryRetargetPrimaryIfDead();

            // 推进并执行
            Clock.AdvanceTo(ev.ExecuteAt);
            Collector.OnRngIndex(Context.Rng.Index);
            ev.Execute(Context);
            Collector.OnRngIndex(Context.Rng.Index);

            Collector.Tick(Clock.CurrentTime);
            TryFlushSegment();

            // 执行后：若主目标刚死但波未清空，立刻 retarget；若整波清空，安排下一波/或结束
            if (!IsWaveCleared())
            {
                TryRetargetPrimaryIfDead();
            }
            else
            {
                TryScheduleNextWaveIfCleared();
            }

            // 到点刷新
            if (_waitingSpawn && _pendingSpawnAt.HasValue && Clock.CurrentTime + 1e-9 >= _pendingSpawnAt.Value)
            {
                TryPerformPendingSpawn();
            }

            // 切片边界
            if (Clock.CurrentTime + 1e-9 >= effectiveSliceEnd) return;
        }
    }

    /// <summary>
    /// 一次性推进直至 targetTime 或事件耗尽/（单波）清场。
    /// 注意：持续/地城模式下不会因“队列耗尽”自然结束。
    /// </summary>
    public void AdvanceUntil(double targetTime, int maxEventsPerSlice = 5000, double maxSliceSeconds = 5.0)
    {
        if (Completed) return;

        targetTime = Math.Max(Clock.CurrentTime, targetTime);
        while (!Completed && Clock.CurrentTime + 1e-9 < targetTime)
        {
            if (Scheduler.Count == 0)
            {
                // 持续/地城：如果在等待刷新，推进至刷新点；若无 provider 且队列空，则结束
                if (_provider is null)
                {
                    TryFlushSegment(force: true);
                    FinalizeNow();
                    break;
                }

                if (_waitingSpawn && _pendingSpawnAt.HasValue)
                {
                    var to = Math.Min(targetTime, Math.Max(_pendingSpawnAt.Value, Clock.CurrentTime));
                    if (to > Clock.CurrentTime + 1e-9)
                    {
                        Clock.AdvanceTo(to);
                        Collector.Tick(Clock.CurrentTime);
                        TryFlushSegment();
                    }
                    TryPerformPendingSpawn();
                }
            }

            var sliceEnd = Math.Min(targetTime, Clock.CurrentTime + maxSliceSeconds);
            AdvanceTo(sliceEnd, maxEventsPerSlice);
        }

        // 目标时间到达：最终 flush（不强制结束持续/地城）
        if (!Completed && Clock.CurrentTime + 1e-9 >= targetTime)
        {
            TryFlushSegment(force: true);
        }
    }

    public void FinalizeNow()
    {
        if (Completed) return;

        // 最终段 flush
        TryFlushSegment(force: true);

        // 单波模式下：根据当前 Encounter 填写；地城/持续由 FinalizeNowFromLastKill 负责
        Killed = Killed || (Context.Encounter?.IsDead ?? false);
        KillTime = KillTime ?? Context.Encounter?.KillTime;
        Overkill = Overkill != 0 ? Overkill : (Context.Encounter?.Overkill ?? 0);

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