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
using BlazorIdle.Server.Infrastructure.Configuration;
using BlazorIdle.Shared.Models;
using BlazorWebGame.Domain.Combat;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BlazorIdle.Server.Domain.Combat.Engine;

/// <summary>
/// 战斗引擎 - 核心战斗循环处理器
/// </summary>
/// <remarks>
/// <para><strong>设计理念</strong>：</para>
/// <list type="bullet">
/// <item>事件队列驱动：所有战斗行为（攻击、技能、Buff）都是事件</item>
/// <item>支持多怪物/波次：可处理单波战斗或多波次地下城</item>
/// <item>刷新机制：支持怪物死亡后的延迟刷新</item>
/// <item>RNG段记录：记录随机数使用，用于战斗重放</item>
/// </list>
/// 
/// <para><strong>核心职责</strong>：</para>
/// <list type="number">
/// <item>驱动事件调度器推进战斗时间</item>
/// <item>处理攻击、技能、Buff等战斗事件</item>
/// <item>管理敌人生成和波次切换</item>
/// <item>收集战斗数据和统计</item>
/// <item>支持步进战斗和同步战斗两种模式</item>
/// </list>
/// 
/// <para><strong>使用场景</strong>：</para>
/// <list type="bullet">
/// <item>普通战斗：单波敌人，击杀后结束</item>
/// <item>地下城战斗：多波敌人，需要Provider管理波次</item>
/// <item>持续战斗：无限刷新，用于挂机</item>
/// <item>步进战斗：前端控制推进速度，用于实时显示</item>
/// <item>离线战斗：一次性模拟到结束，用于离线结算</item>
/// </list>
/// 
/// <para><strong>技术特性</strong>：</para>
/// <list type="bullet">
/// <item>可序列化：支持保存/恢复战斗状态</item>
/// <item>可重放：通过RNG种子可精确重现战斗过程</item>
/// <item>实时通知：可选的SignalR通知支持</item>
/// <item>性能优化：分段处理，避免长时间阻塞</item>
/// </list>
/// </remarks>
public sealed class BattleEngine
{
    /// <summary>战斗实例 - 包含基本信息（ID、角色ID、开始时间等）</summary>
    public Battle Battle { get; }
    
    /// <summary>游戏时钟 - 管理当前战斗时间</summary>
    public IGameClock Clock { get; }
    
    /// <summary>事件调度器 - 管理所有待执行的战斗事件队列</summary>
    public IEventScheduler Scheduler { get; }
    
    /// <summary>段收集器 - 收集战斗数据并生成段（用于前端显示）</summary>
    public SegmentCollector Collector { get; }
    
    /// <summary>战斗上下文 - 包含所有战斗状态（角色、敌人、Buff、技能等）</summary>
    public BattleContext Context { get; }
    
    /// <summary>战斗段列表 - 已生成的历史段，用于回放和统计</summary>
    public List<CombatSegment> Segments { get; } = new();

    /// <summary>战斗是否已完成</summary>
    public bool Completed { get; private set; }
    
    /// <summary>是否击杀了敌人（单波模式或地下城最后一波）</summary>
    public bool Killed { get; private set; }
    
    /// <summary>击杀时间（游戏时间，单位：秒）</summary>
    public double? KillTime { get; private set; }
    
    /// <summary>溢出伤害（最后一击超出敌人剩余血量的部分）</summary>
    public int Overkill { get; private set; }

    /// <summary>战斗开始时的RNG索引（用于重放）</summary>
    public long SeedIndexStart { get; }
    
    /// <summary>当前RNG索引（战斗结束时用于记录RNG使用范围）</summary>
    public long SeedIndexEnd => Context.Rng.Index;

    /// <summary>当前波次索引（从Provider获取，无Provider时默认为1）</summary>
    public int WaveIndex => _provider?.CurrentWaveIndex ?? 1;
    
    /// <summary>已完成的轮数（地下城循环模式，无Provider时默认为0）</summary>
    public int RunCount => _provider?.CompletedRunCount ?? 0;

    /// <summary>敌人遭遇提供者 - 用于地下城/持续战斗的波次管理（可选）</summary>
    private readonly IEncounterProvider? _provider;

    /// <summary>待刷新的下一组敌人</summary>
    private EncounterGroup? _pendingNextGroup;
    
    /// <summary>计划的刷新时间点（游戏时间）</summary>
    private double? _pendingSpawnAt;
    
    /// <summary>是否正在等待刷新（标志位）</summary>
    private bool _waitingSpawn;
    
    /// <summary>已标记死亡的敌人集合（用于避免重复计数击杀）</summary>
    private readonly HashSet<Encounter> _markedDead = new();
    
    /// <summary>战斗循环配置选项（控制攻击/特殊轨道的行为）</summary>
    private readonly CombatLoopOptions _loopOptions;

    /// <summary>清除死亡标记（用于波次切换时重置）</summary>
    private void ClearDeathMarks() => _markedDead.Clear();

    /// <summary>
    /// 构造函数（单波战斗）- 用于普通战斗，击杀所有敌人后结束
    /// </summary>
    /// <param name="battleId">战斗唯一标识</param>
    /// <param name="characterId">角色ID</param>
    /// <param name="profession">职业类型</param>
    /// <param name="stats">角色属性（包含装备加成）</param>
    /// <param name="rng">随机数上下文（用于战斗重放）</param>
    /// <param name="enemyDef">敌人定义</param>
    /// <param name="enemyCount">敌人数量（至少为1）</param>
    /// <param name="module">职业模块（可选，默认从注册表解析）</param>
    /// <param name="meta">战斗元数据（可选，用于标签和统计）</param>
    /// <param name="notificationService">SignalR通知服务（可选，用于实时推送战斗事件）</param>
    /// <param name="messageFormatter">战斗消息格式化器（可选，用于生成战斗日志）</param>
    /// <param name="loopOptions">战斗循环配置（可选，控制轨道行为）</param>
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
        IBattleNotificationService? notificationService = null,
        Services.BattleMessageFormatter? messageFormatter = null,
        CombatLoopOptions? loopOptions = null)
        : this(battleId, characterId, profession, stats, rng,
               provider: null,
               initialGroup: new EncounterGroup(Enumerable.Range(0, Math.Max(1, enemyCount)).Select(_ => enemyDef).ToList()),
               module: module,
               meta: meta,
               notificationService: notificationService,
               messageFormatter: messageFormatter,
               loopOptions: loopOptions)
    {
    }

    /// <summary>
    /// 构造函数（多波战斗）- 用于地下城或持续战斗，由Provider管理波次
    /// </summary>
    /// <param name="battleId">战斗唯一标识</param>
    /// <param name="characterId">角色ID</param>
    /// <param name="profession">职业类型</param>
    /// <param name="stats">角色属性（包含装备加成）</param>
    /// <param name="rng">随机数上下文（用于战斗重放）</param>
    /// <param name="provider">敌人遭遇提供者（管理波次和刷新）</param>
    /// <param name="module">职业模块（可选，默认从注册表解析）</param>
    /// <param name="meta">战斗元数据（可选，用于标签和统计）</param>
    /// <param name="notificationService">SignalR通知服务（可选，用于实时推送战斗事件）</param>
    /// <param name="messageFormatter">战斗消息格式化器（可选，用于生成战斗日志）</param>
    /// <param name="loopOptions">战斗循环配置（可选，控制轨道行为）</param>
    public BattleEngine(
        Guid battleId,
        Guid characterId,
        Profession profession,
        CharacterStats stats,
        RngContext rng,
        IEncounterProvider provider,
        IProfessionModule? module = null,
        BattleMeta? meta = null,
        IBattleNotificationService? notificationService = null,
        Services.BattleMessageFormatter? messageFormatter = null,
        CombatLoopOptions? loopOptions = null)
        : this(battleId, characterId, profession, stats, rng,
               provider: provider,
               initialGroup: provider.CurrentGroup,
               module: module,
               meta: meta,
               notificationService: notificationService,
               messageFormatter: messageFormatter,
               loopOptions: loopOptions)
    {
    }

    /// <summary>
    /// 私有共享构造函数 - 初始化战斗引擎的所有组件
    /// </summary>
    /// <remarks>
    /// 初始化流程：
    /// 1. 创建战斗实例和核心组件（时钟、调度器、收集器）
    /// 2. 创建战斗上下文（包含角色、敌人、Buff、技能等）
    /// 3. 初始化职业模块（注册Buff定义、构建技能）
    /// 4. 创建攻击和特殊轨道，并调度初始事件
    /// 5. 初始化敌人攻击和技能系统
    /// 6. 应用战斗元数据标签
    /// </remarks>
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
        Services.BattleMessageFormatter? messageFormatter,
        CombatLoopOptions? loopOptions)
    {
        _provider = provider;
        _loopOptions = loopOptions ?? new CombatLoopOptions(); // 使用默认值如果未提供

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
            messageFormatter: messageFormatter,
            combatLoopOptions: _loopOptions
        );

        SeedIndexStart = rng.Index;

        professionModule.RegisterBuffDefinitions(Context);
        professionModule.OnBattleStart(Context);
        professionModule.BuildSkills(Context, Context.AutoCaster);

        // 战斗循环优化 Task 1.1: 根据配置决定攻击轨道的初始延迟
        // 如果配置为从完整间隔开始，则第一次攻击在战斗开始后 attackInterval 秒触发
        // 否则保持旧行为（立即触发）
        var attackStartDelay = _loopOptions.AttackStartsWithFullInterval 
            ? Battle.AttackIntervalSeconds 
            : 0;
        var attackTrack = new TrackState(TrackType.Attack, Battle.AttackIntervalSeconds, attackStartDelay);
        
        // 战斗循环优化 Task 2.3: 特殊轨道使用职业配置决定初始延迟
        // 优先级：职业配置 > 全局配置 > 默认值
        bool specialImmediateStart = GetProfessionSpecialStartsImmediately(professionModule);
        var specialStartDelay = specialImmediateStart 
            ? 0 
            : Battle.SpecialIntervalSeconds;
        var specialTrack = new TrackState(TrackType.Special, Battle.SpecialIntervalSeconds, specialStartDelay);
        
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
    /// 战斗循环优化 Task 1.2: 暂停玩家轨道（类似玩家死亡的机制）
    /// 用于刷新等待期间暂停攻击和特殊轨道
    /// </summary>
    /// <param name="reason">暂停原因，用于日志记录</param>
    private void PausePlayerTracks(string reason)
    {
        const double FAR_FUTURE = 1e10;
        var pausedTracks = new List<string>();
        
        // 如果刷新延迟极小（接近0），跳过暂停以避免状态抖动
        if (_pendingSpawnAt.HasValue && 
            Math.Abs(_pendingSpawnAt.Value - Clock.CurrentTime) < 1e-6)
        {
            Collector.OnTag("pause_skipped:immediate_spawn", 1);
            return;
        }
        
        foreach (var track in Context.Tracks)
        {
            bool shouldPause = false;
            
            // 攻击轨道：根据配置决定是否暂停
            if (track.TrackType == TrackType.Attack)
            {
                shouldPause = _loopOptions.PauseAttackWhenNoEnemies;
            }
            // 战斗循环优化 Task 2.3: 特殊轨道根据职业配置决定是否暂停
            // 优先级：职业配置 > 全局配置
            else if (track.TrackType == TrackType.Special)
            {
                shouldPause = GetProfessionPauseSpecialWhenNoEnemies();
            }
            
            if (shouldPause && track.NextTriggerAt < FAR_FUTURE)
            {
                track.NextTriggerAt = FAR_FUTURE;
                pausedTracks.Add(track.TrackType.ToString());
                Collector.OnTag($"track_paused:{track.TrackType}", 1);
            }
        }
        
        if (pausedTracks.Count > 0)
        {
            Collector.OnTag($"tracks_paused:{reason}", 1);
            
            // 战斗循环优化 Task 3.2: 发送轨道暂停事件通知前端
            if (Context.NotificationService?.IsAvailable == true)
            {
                var resetEvent = new BlazorIdle.Shared.Models.TrackProgressResetEventDto
                {
                    BattleId = Battle.Id,
                    EventTime = Clock.CurrentTime,
                    TrackTypes = pausedTracks,
                    ResetReason = reason
                };
                _ = Context.NotificationService.NotifyEventAsync(Battle.Id, resetEvent);
            }
        }
    }
    
    /// <summary>
    /// 战斗循环优化 Task 1.2 & 2.3: 恢复玩家轨道（类似玩家复活的机制）
    /// 用于新怪物出现后恢复攻击和特殊轨道
    /// 使用职业配置决定特殊轨道的恢复行为
    /// </summary>
    private void ResumePlayerTracks()
    {
        const double FAR_FUTURE = 1e10;
        double resumeTime = Clock.CurrentTime;
        var resumedTracks = new List<string>();
        
        foreach (var track in Context.Tracks)
        {
            // 检查轨道是否处于暂停状态（NextTriggerAt 被设置为 FAR_FUTURE）
            if (track.NextTriggerAt > FAR_FUTURE / 2)
            {
                // 根据配置决定恢复延迟
                // 攻击轨道：从完整间隔开始（符合"从0开始计算进度"的需求）
                // 战斗循环优化 Task 2.3: 特殊轨道根据职业配置决定是否立即触发
                double resumeDelay = track.CurrentInterval; // 默认从完整间隔开始
                
                if (track.TrackType == TrackType.Special)
                {
                    // 优先级：职业配置 > 全局配置
                    bool specialImmediateStart = GetProfessionSpecialStartsImmediately(Context.ProfessionModule);
                    resumeDelay = specialImmediateStart ? 0.0 : track.CurrentInterval;
                }
                
                track.NextTriggerAt = resumeTime + resumeDelay;
                resumedTracks.Add(track.TrackType.ToString());
                
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
        
        if (resumedTracks.Count > 0)
        {
            Collector.OnTag("tracks_resumed:spawn_complete", 1);
            
            // 战斗循环优化 Task 3.2: 发送轨道恢复事件通知前端
            if (Context.NotificationService?.IsAvailable == true)
            {
                // 收集新的触发时间
                var newTriggerTimes = new Dictionary<string, double>();
                foreach (var track in Context.Tracks)
                {
                    if (resumedTracks.Contains(track.TrackType.ToString()))
                    {
                        newTriggerTimes[track.TrackType.ToString()] = track.NextTriggerAt;
                    }
                }
                
                var resetEvent = new BlazorIdle.Shared.Models.TrackProgressResetEventDto
                {
                    BattleId = Battle.Id,
                    EventTime = resumeTime,
                    TrackTypes = resumedTracks,
                    ResetReason = "spawn_complete",
                    NewTriggerTimes = newTriggerTimes
                };
                _ = Context.NotificationService.NotifyEventAsync(Battle.Id, resetEvent);
            }
        }
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

            // 战斗循环优化 Task 1.2: 暂停玩家轨道（替代旧的 ResetAttackProgress）
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
            
            // 战斗循环优化 Task 1.2: 恢复玩家轨道
            // 只有在玩家存活时才恢复轨道（避免玩家死亡期间怪物刷新的边缘情况）
            if (Context.Player.CanAct())
            {
                ResumePlayerTracks();
            }
            else
            {
                // 玩家死亡中，标记刷新已完成但不恢复轨道
                // 等待 PlayerReviveEvent 恢复
                Collector.OnTag("spawn_completed_while_player_dead", 1);
            }

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
    /// 推进战斗到指定时间或事件数量限制（步进模式）
    /// </summary>
    /// <param name="sliceEnd">切片结束时间（游戏时间，单位：秒）</param>
    /// <param name="maxEvents">最多执行的事件数量（防止无限循环）</param>
    /// <remarks>
    /// <para><strong>执行流程</strong>：</para>
    /// <list type="number">
    /// <item>检查是否有待执行的刷新，如果时间已到则执行刷新</item>
    /// <item>如果正在等待刷新，将切片上限限制在刷新时间点</item>
    /// <item>循环执行事件队列中的事件，直到达到时间或事件数限制</item>
    /// <item>每个事件执行前后记录RNG索引（用于重放）</item>
    /// <item>事件执行后处理目标切换、波次清空、刷新调度等</item>
    /// <item>自动处理怪物死亡、目标重选、波次切换等逻辑</item>
    /// </list>
    /// 
    /// <para><strong>使用场景</strong>：</para>
    /// <list type="bullet">
    /// <item>步进战斗：前端每帧调用一次，推进固定时间</item>
    /// <item>实时战斗：配合SignalR实时推送战斗事件</item>
    /// </list>
    /// 
    /// <para><strong>注意事项</strong>：</para>
    /// <list type="bullet">
    /// <item>如果战斗已完成（Completed=true），直接返回</item>
    /// <item>maxEvents用于防止事件循环失控（通常设置为5000-10000）</item>
    /// <item>切片模式不会自动结束战斗，需要外部判断并调用FinalizeNow()</item>
    /// </list>
    /// </remarks>
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
    
    // ========================================
    // 战斗循环优化 - 中篇：职业配置辅助方法
    // ========================================
    
    /// <summary>
    /// 获取特殊轨道是否在无怪物时暂停
    /// 优先级：职业配置 > 全局配置
    /// </summary>
    private bool GetProfessionPauseSpecialWhenNoEnemies()
    {
        var professionConfig = Context.ProfessionModule.PauseSpecialWhenNoEnemies;
        
        // 如果职业提供了明确的配置，使用职业配置
        if (professionConfig.HasValue)
        {
            return professionConfig.Value;
        }
        
        // 否则使用全局默认配置
        return _loopOptions.PauseSpecialWhenNoEnemiesByDefault;
    }
    
    /// <summary>
    /// 获取特殊轨道是否立即触发（战斗开始/恢复时）
    /// 优先级：职业配置 > 全局配置（取反）
    /// </summary>
    private bool GetProfessionSpecialStartsImmediately(IProfessionModule professionModule)
    {
        var professionConfig = professionModule.SpecialStartsImmediately;
        
        // 如果职业提供了明确的配置，使用职业配置
        if (professionConfig.HasValue)
        {
            return professionConfig.Value;
        }
        
        // 否则使用全局配置的反向逻辑
        // SpecialStartsWithFullInterval = true 意味着 SpecialStartsImmediately = false
        return !_loopOptions.SpecialStartsWithFullInterval;
    }
    
    /// <summary>
    /// 获取特殊轨道在玩家复活后是否立即触发
    /// 优先级：职业配置 > 全局配置
    /// </summary>
    private bool GetProfessionSpecialStartsImmediatelyAfterRevive(IProfessionModule professionModule)
    {
        var professionConfig = professionModule.SpecialStartsImmediatelyAfterRevive;
        
        // 如果职业提供了明确的配置，使用职业配置
        if (professionConfig.HasValue)
        {
            return professionConfig.Value;
        }
        
        // 否则使用全局配置
        return _loopOptions.SpecialStartsImmediatelyAfterReviveByDefault;
    }
}