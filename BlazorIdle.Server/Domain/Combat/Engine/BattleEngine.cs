using BlazorIdle.Server.Application.Abstractions;
using BlazorIdle.Server.Domain.Characters;
using BlazorIdle.Server.Domain.Combat.Buffs;
using BlazorIdle.Server.Domain.Combat.Combatants;
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

    // 暴露 provider 信息（由 RunningBattle/Runner 透传）
    public int WaveIndex => _provider?.CurrentWaveIndex ?? 1;
    public int RunCount => _provider?.CompletedRunCount ?? 0;

    private readonly IEncounterProvider? _provider;

    // 刷新/击杀标记
    private EncounterGroup? _pendingNextGroup;
    private double? _pendingSpawnAt;
    private bool _waitingSpawn;
    private readonly HashSet<Encounter> _markedDead = new();

    private void ClearDeathMarks() => _markedDead.Clear();

    public BattleEngine(
        Guid battleId,
        Guid characterId,
        Profession profession,
        CharacterStats stats,
        RngContext rng,
        EnemyDefinition enemyDef,
        int enemyCount,
        IProfessionModule? module = null,
        BattleMeta? meta = null,
        IBattleNotificationService? notificationService = null,       // SignalR Phase 2
        Services.BattleMessageFormatter? messageFormatter = null)
        : this(battleId, characterId, profession, stats, rng,
               provider: null,
               initialGroup: new EncounterGroup(Enumerable.Range(0, Math.Max(1, enemyCount)).Select(_ => enemyDef).ToList()),
               module: module,
               meta: meta,
               notificationService: notificationService,
               messageFormatter: messageFormatter)
    {
    }

    public BattleEngine(
        Guid battleId,
        Guid characterId,
        Profession profession,
        CharacterStats stats,
        RngContext rng,
        IEncounterProvider provider,
        IProfessionModule? module = null,
        BattleMeta? meta = null,
        IBattleNotificationService? notificationService = null,       // SignalR Phase 2
        Services.BattleMessageFormatter? messageFormatter = null)
        : this(battleId, characterId, profession, stats, rng,
               provider: provider,
               initialGroup: provider.CurrentGroup,
               module: module,
               meta: meta,
               notificationService: notificationService,
               messageFormatter: messageFormatter)
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
        IProfessionModule? module,
        BattleMeta? meta,
        IBattleNotificationService? notificationService,              // SignalR Phase 2
        Services.BattleMessageFormatter? messageFormatter)
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

        // Phase 6: 从 provider 提取副本定义
        DungeonDefinition? dungeonDef = null;
        if (provider is DungeonEncounterProvider dungeonProvider)
        {
            dungeonDef = dungeonProvider.Dungeon;
        }
        
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
            stats: stats,
            dungeon: dungeonDef,
            notificationService: notificationService,
            messageFormatter: messageFormatter
        );

        SeedIndexStart = rng.Index;

        professionModule.RegisterBuffDefinitions(Context);
        professionModule.OnBattleStart(Context);
        professionModule.BuildSkills(Context, Context.AutoCaster);

        // 攻击轨道从完整间隔开始，避免战斗开始时立即攻击
        // 这样玩家会有"读秒-攻击"的直观感受
        var attackTrack = new TrackState(TrackType.Attack, Battle.AttackIntervalSeconds, Battle.AttackIntervalSeconds);
        
        // 特殊轨道根据职业配置决定初始延迟
        var specialInitialDelay = professionModule.SpecialStartsImmediately 
            ? 0.0 
            : Battle.SpecialIntervalSeconds;
        var specialTrack = new TrackState(TrackType.Special, Battle.SpecialIntervalSeconds, specialInitialDelay);
        
        Context.Tracks.Add(attackTrack);
        Context.Tracks.Add(specialTrack);
        
        // Phase 5: 初始同步急速到攻击轨道（确保装备的急速属性在战斗开始时就生效）
        SyncTrackHaste(Context);

        Scheduler.Schedule(new AttackTickEvent(attackTrack.NextTriggerAt, attackTrack));
        Scheduler.Schedule(new SpecialPulseEvent(specialTrack.NextTriggerAt, specialTrack));
        Scheduler.Schedule(new ProcPulseEvent(Clock.CurrentTime + 1.0, 1.0));

        // Phase 4: 初始化怪物攻击轨道
        InitializeEnemyAttacks(initialGroup);
        
        // Phase 5: 初始化怪物技能系统
        InitializeEnemySkills(initialGroup);

        // 统一：应用上下文标签（ctx.*）
        ApplyMetaTags(meta);
    }

    private void ApplyMetaTags(BattleMeta? meta)
    {
        if (meta is null) return;

        // 统一小写写入
        var modeTag = string.IsNullOrWhiteSpace(meta.ModeTag) ? "duration" : meta.ModeTag.Trim().ToLowerInvariant();
        Collector.OnTag($"ctx.mode.{modeTag}", 1);
        Collector.OnTag($"ctx.enemyId.{(string.IsNullOrWhiteSpace(meta.EnemyId) ? "dummy" : meta.EnemyId)}", 1);
        Collector.OnTag($"ctx.enemyCount.{Math.Max(1, meta.EnemyCount)}", 1);
        if (!string.IsNullOrWhiteSpace(meta.DungeonId))
            Collector.OnTag($"ctx.dungeonId.{meta.DungeonId}", 1);

        if (meta.ExtraTags is not null)
        {
            foreach (var (tag, val) in meta.ExtraTags)
            {
                if (!string.IsNullOrWhiteSpace(tag))
                    Collector.OnTag(tag, val);
            }
        }
    }

    // 波是否清空：整波全部死亡才算清场
    private bool IsWaveCleared()
    {
        var grp = Context.EncounterGroup;
        if (grp is null)
            return Context.Encounter?.IsDead == true;
        return grp.All.All(e => e.IsDead);
    }

    // 重置攻击进度（切换目标或等待刷新时使用）
    private void ResetAttackProgress()
    {
        var attackTrack = Context.Tracks.FirstOrDefault(t => t.TrackType == TrackType.Attack);
        if (attackTrack is not null)
        {
            // 将下次攻击时间设置为当前时间 + 完整的攻击间隔
            attackTrack.NextTriggerAt = Clock.CurrentTime + attackTrack.CurrentInterval;
            Collector.OnTag("attack_progress_reset", 1);
        }
    }

    /// <summary>
    /// 暂停玩家轨道（类似玩家死亡的机制）
    /// 用于刷新等待期间暂停攻击和特殊轨道
    /// </summary>
    /// <param name="reason">暂停原因，用于日志记录</param>
    private void PausePlayerTracks(string reason)
    {
        const double FAR_FUTURE = 1e10;
        
        // 如果刷新延迟为0，跳过暂停（因为会立即恢复）
        if (_pendingSpawnAt.HasValue && 
            Math.Abs(_pendingSpawnAt.Value - Clock.CurrentTime) < 1e-6)
        {
            Collector.OnTag("pause_skipped:immediate_spawn", 1);
            return;
        }
        
        foreach (var track in Context.Tracks)
        {
            bool shouldPause = false;
            
            // 攻击轨道总是暂停
            if (track.TrackType == TrackType.Attack)
            {
                shouldPause = true;
            }
            // 特殊轨道根据职业配置决定
            else if (track.TrackType == TrackType.Special)
            {
                shouldPause = Context.ProfessionModule.PauseSpecialWhenNoEnemies;
            }
            
            if (shouldPause && track.NextTriggerAt < FAR_FUTURE)
            {
                track.NextTriggerAt = FAR_FUTURE;
                Collector.OnTag($"track_paused:{track.TrackType}", 1);
            }
        }
        
        Collector.OnTag($"tracks_paused:{reason}", 1);
    }

    /// <summary>
    /// 恢复玩家轨道（类似玩家复活的机制）
    /// 用于新怪物出现后恢复攻击和特殊轨道
    /// </summary>
    private void ResumePlayerTracks()
    {
        const double FAR_FUTURE = 1e10;
        double resumeTime = Clock.CurrentTime;
        
        foreach (var track in Context.Tracks)
        {
            // 检查轨道是否处于暂停状态
            if (track.NextTriggerAt > FAR_FUTURE / 2)
            {
                // 根据轨道类型和职业配置决定恢复策略
                double resumeDelay;
                
                if (track.TrackType == TrackType.Special && 
                    Context.ProfessionModule.SpecialStartsImmediately)
                {
                    // 特殊轨道如果配置为立即触发，恢复时也立即触发
                    resumeDelay = 0.0;
                }
                else
                {
                    // 默认从完整间隔开始（符合"从0开始计算进度"的需求）
                    resumeDelay = track.CurrentInterval;
                }
                
                track.NextTriggerAt = resumeTime + resumeDelay;
                
                // 重新调度事件
                if (track.TrackType == TrackType.Attack)
                {
                    Scheduler.Schedule(new AttackTickEvent(track.NextTriggerAt, track));
                }
                else if (track.TrackType == TrackType.Special)
                {
                    Scheduler.Schedule(new SpecialPulseEvent(track.NextTriggerAt, track));
                }
                
                Collector.OnTag($"track_resumed:{track.TrackType}", 1);
            }
        }
        
        Collector.OnTag("tracks_resumed:spawn_complete", 1);
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
                ResetAttackProgress(); // 切换目标时重置攻击进度
                Collector.OnTag("retarget_primary", 1);
                
                // SignalR Phase 2: 发送目标切换通知
                if (Context.NotificationService?.IsAvailable == true)
                {
                    _ = Context.NotificationService.NotifyStateChangeAsync(Battle.Id, "TargetSwitched");
                }
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

            // 进入刷新等待状态时重置攻击进度
            ResetAttackProgress();

            // 暂停玩家轨道（攻击和特殊轨道）
            PausePlayerTracks("spawn_wait");

            if (runCompleted) Collector.OnTag("dungeon_run_complete", 1);
            Collector.OnTag("spawn_scheduled", 1);
        }
        else
        {
            // 地城非循环：整轮完成 → 结束
            FinalizeNowFromLastKill();
        }
    }

    // 捕获新死亡并打 tag：kill.{enemyId} = 1
    private void CaptureNewDeaths()
    {
        var grp = Context.EncounterGroup;
        if (grp is not null)
        {
            foreach (var e in grp.All)
            {
                if (e.IsDead && !_markedDead.Contains(e))
                {
                    Collector.OnTag($"kill.{e.Enemy.Id}", 1);
                    _markedDead.Add(e);
                    
                    // SignalR Phase 2: 发送怪物死亡通知
                    if (Context.NotificationService?.IsAvailable == true)
                    {
                        _ = Context.NotificationService.NotifyStateChangeAsync(Battle.Id, "EnemyKilled");
                    }
                }
            }
        }
        else
        {
            if (Context.Encounter is { IsDead: true } enc && !_markedDead.Contains(enc))
            {
                Collector.OnTag($"kill.{enc.Enemy.Id}", 1);
                
                // SignalR Phase 2: 发送怪物死亡通知
                if (Context.NotificationService?.IsAvailable == true)
                {
                    _ = Context.NotificationService.NotifyStateChangeAsync(Battle.Id, "EnemyKilled");
                }
                _markedDead.Add(enc);
            }
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

            // 新一波开始：清理死亡标记，避免与新实例混淆
            ClearDeathMarks();

            // Bug Fix: 清理旧波次的敌人战斗单位，并为新波次重新初始化
            // 这确保新波次的怪物可以正常攻击玩家
            var oldEnemyCount = Context.EnemyCombatants.Count;
            Context.EnemyCombatants.Clear();
            Collector.OnTag("wave_transition_enemy_cleared", oldEnemyCount);
            
            // 重新初始化新波次的怪物攻击系统（使用当前时间）
            InitializeEnemyAttacks(Context.EncounterGroup!, Clock.CurrentTime);
            
            // 重新初始化新波次的怪物技能系统（内部使用 Clock.CurrentTime）
            InitializeEnemySkills(Context.EncounterGroup!);

            // 恢复玩家轨道
            ResumePlayerTracks();

            Collector.OnTag("spawn_performed", 1);
            Collector.OnTag("wave_transition_enemy_reinitialized", Context.EnemyCombatants.Count);
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

            // 新增：事件执行后捕获新死亡
            CaptureNewDeaths();

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

    /// <summary>
    /// 捕获当前战斗状态（用于离线/在线无缝切换）
    /// </summary>
    public Application.Battles.Offline.BattleState CaptureBattleState()
    {
        var enemies = new List<Application.Battles.Offline.EnemyHealthState>();
        
        if (Context.EncounterGroup != null)
        {
            foreach (var enc in Context.EncounterGroup.All)
            {
                enemies.Add(new Application.Battles.Offline.EnemyHealthState
                {
                    EnemyId = enc.Enemy.Id,
                    CurrentHp = enc.CurrentHp,
                    MaxHp = enc.Enemy.MaxHp,
                    IsDead = enc.IsDead,
                    KillTime = enc.KillTime,
                    Overkill = enc.Overkill
                });
            }
        }
        else if (Context.Encounter != null)
        {
            enemies.Add(new Application.Battles.Offline.EnemyHealthState
            {
                EnemyId = Context.Encounter.Enemy.Id,
                CurrentHp = Context.Encounter.CurrentHp,
                MaxHp = Context.Encounter.Enemy.MaxHp,
                IsDead = Context.Encounter.IsDead,
                KillTime = Context.Encounter.KillTime,
                Overkill = Context.Encounter.Overkill
            });
        }

        return new Application.Battles.Offline.BattleState
        {
            Enemies = enemies,
            WaveIndex = WaveIndex,
            RunCount = RunCount,
            SnapshotAtSeconds = Clock.CurrentTime
        };
    }

    /// <summary>
    /// 从战斗状态恢复（在创建后立即调用，用于继承离线/在线的进度）
    /// </summary>
    public void RestoreBattleState(Application.Battles.Offline.BattleState? state)
    {
        if (state == null || state.Enemies.Count == 0)
            return;

        // 恢复敌人血量
        if (Context.EncounterGroup != null)
        {
            var encounters = Context.EncounterGroup.All;
            for (int i = 0; i < Math.Min(encounters.Count, state.Enemies.Count); i++)
            {
                var enc = encounters[i];
                var healthState = state.Enemies[i];
                
                // 使用反射或直接访问设置当前血量
                // 由于 Encounter.CurrentHp 是私有 setter，我们需要通过 ApplyDamage 来调整
                var hpDiff = enc.CurrentHp - healthState.CurrentHp;
                if (hpDiff > 0 && !healthState.IsDead)
                {
                    enc.ApplyDamage(hpDiff, healthState.KillTime ?? Clock.CurrentTime);
                }
            }
        }
        else if (Context.Encounter != null && state.Enemies.Count > 0)
        {
            var healthState = state.Enemies[0];
            var hpDiff = Context.Encounter.CurrentHp - healthState.CurrentHp;
            if (hpDiff > 0 && !healthState.IsDead)
            {
                Context.Encounter.ApplyDamage(hpDiff, healthState.KillTime ?? Clock.CurrentTime);
            }
        }

        // Note: WaveIndex and RunCount are managed by the provider, 
        // so we don't restore them here to avoid breaking the provider's state machine
    }

    /// <summary>
    /// Phase 4: 初始化怪物攻击轨道
    /// 为每个怪物创建攻击轨道并调度第一个攻击事件
    /// </summary>
    /// <param name="encounterGroup">怪物组</param>
    /// <param name="spawnTime">生成时间（默认使用当前时间）。用于波次切换时正确调度攻击</param>
    private void InitializeEnemyAttacks(EncounterGroup encounterGroup, double? spawnTime = null)
    {
        if (encounterGroup == null || encounterGroup.All.Count == 0)
            return;

        // 使用提供的生成时间，或默认使用当前时间
        double now = spawnTime ?? Clock.CurrentTime;

        int enemyIndex = 0;
        foreach (var encounter in encounterGroup.All)
        {
            // 只为配置了攻击能力的怪物创建攻击轨道
            if (encounter.Enemy.BaseDamage > 0 && encounter.Enemy.AttackIntervalSeconds > 0)
            {
                var enemyId = $"enemy_{enemyIndex}";
                var enemyCombatant = new EnemyCombatant(enemyId, encounter);
                
                // 创建攻击轨道（类似玩家的攻击轨道）
                var attackInterval = encounter.Enemy.AttackIntervalSeconds;
                // 对于战斗开始（now = 0），使用 attackInterval 作为初始延迟
                // 对于波次切换（now > 0），也使用 attackInterval，但要基于当前时间
                var firstAttackTime = now + attackInterval;
                var attackTrack = new TrackState(TrackType.Attack, attackInterval, firstAttackTime);
                enemyCombatant.AttackTrack = attackTrack;
                
                // 存储到 Context 以便后续访问（例如玩家复活时重新激活）
                Context.EnemyCombatants.Add(enemyCombatant);
                
                // 调度第一个攻击事件
                Scheduler.Schedule(new EnemyAttackEvent(attackTrack.NextTriggerAt, enemyCombatant));
                
                Collector.OnTag("enemy_attack_initialized", 1);
            }
            
            enemyIndex++;
        }
    }

    /// <summary>
    /// Phase 5: 初始化怪物技能系统和 Buff 系统
    /// 为配置了技能的怪物创建技能管理器、Buff 管理器并调度技能检查事件
    /// </summary>
    private void InitializeEnemySkills(EncounterGroup encounterGroup)
    {
        if (encounterGroup == null || encounterGroup.All.Count == 0)
            return;

        double combatStartTime = Clock.CurrentTime;
        bool hasAnySkills = false;

        // 注册敌人 Buff 定义
        var enemyBuffDefs = EnemyBuffDefinitionsRegistry.GetAll();

        // 遍历已创建的 EnemyCombatants
        foreach (var enemyCombatant in Context.EnemyCombatants)
        {
            var enemy = enemyCombatant.Encounter.Enemy;
            
            // 为每个怪物创建 BuffManager（支持 Buff 效果）
            // 先创建一个占位对象，解决循环引用问题
            BuffManager? buffManagerRef = null;
            var buffManager = new BuffManager(
                tagRecorder: (tag, count) => Collector.OnTag(tag, count),
                resourceRecorder: (res, delta) => 
                {
                    // 处理资源恢复（例如治疗效果）
                    if (res == "health" && delta > 0)
                    {
                        enemyCombatant.Encounter.ApplyHealing(delta);
                        Collector.OnTag($"enemy_buff_heal:{enemyCombatant.Id}", delta);
                    }
                },
                damageApplier: null,  // 敌人不会对自己造成伤害
                resolveHasteFactor: () => buffManagerRef?.Aggregate.ApplyToBaseHaste(1.0) ?? 1.0,
                resolveApsp: () => (0.0, 0.0),  // 敌人暂时不使用 AP/SP 系统
                onDotDirectHit: null
            );
            buffManagerRef = buffManager;  // 设置引用以便回调使用
            
            // 注册所有敌人 Buff 定义
            foreach (var buffDef in enemyBuffDefs)
            {
                buffManager.RegisterDefinition(buffDef);
            }
            
            enemyCombatant.Buffs = buffManager;
            Collector.OnTag("enemy_buff_manager_initialized", 1);
            
            // 只为配置了技能的怪物创建技能管理器
            if (enemy.Skills != null && enemy.Skills.Count > 0)
            {
                // 为每个怪物创建独立的 RNG 子流（用于技能触发概率）
                var skillRng = Context.Rng.Split((ulong)enemyCombatant.Id.GetHashCode());
                
                // 创建技能管理器
                var skillManager = new Enemies.EnemySkillManager(
                    enemyCombatant,
                    enemy.Skills,
                    combatStartTime,
                    skillRng
                );
                
                enemyCombatant.SkillManager = skillManager;
                hasAnySkills = true;
                
                Collector.OnTag("enemy_skill_manager_initialized", 1);
            }
        }

        // 如果有任何怪物配置了技能，调度定期技能检查事件
        if (hasAnySkills)
        {
            // 每 0.5 秒检查一次技能触发条件（平衡性能和响应速度）
            const double SKILL_CHECK_INTERVAL = 0.5;
            Scheduler.Schedule(new Enemies.EnemySkillCheckEvent(
                Clock.CurrentTime + SKILL_CHECK_INTERVAL,
                SKILL_CHECK_INTERVAL
            ));
        }
        
        // 调度定期 Buff Tick 事件（处理 DoT/HoT 等周期效果）
        // 只在有技能的怪物时才调度 buff tick，以减少事件数量
        // 使用 1.0 秒间隔以减少事件数量，大多数 Buff DoT/HoT 间隔都是 2 秒以上
        if (hasAnySkills && Context.EnemyCombatants.Count > 0)
        {
            const double BUFF_TICK_INTERVAL = 1.0;  // 每 1.0 秒 tick 一次 buff
            Scheduler.Schedule(new Enemies.EnemyBuffTickEvent(
                Clock.CurrentTime + BUFF_TICK_INTERVAL,
                BUFF_TICK_INTERVAL
            ));
        }
    }
}