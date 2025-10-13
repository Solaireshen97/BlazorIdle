using BlazorIdle.Server.Application.Abstractions;
using BlazorIdle.Server.Domain.Characters;
using BlazorIdle.Server.Domain.Combat.Buffs;
using BlazorIdle.Server.Domain.Combat.Combatants;
using BlazorIdle.Server.Domain.Combat.Damage;
using BlazorIdle.Server.Domain.Combat.Enemies;
using BlazorIdle.Server.Domain.Combat.Procs;
using BlazorIdle.Server.Domain.Combat.Professions;
using BlazorIdle.Server.Domain.Combat.Resources;
using BlazorIdle.Server.Domain.Combat.Rng;
using BlazorIdle.Server.Domain.Combat.Skills;
using BlazorIdle.Shared.Models;
using BlazorWebGame.Domain.Combat;
using System.Collections.Generic;

namespace BlazorIdle.Server.Domain.Combat;

public class BattleContext
{
    public Battle Battle { get; }
    public IGameClock Clock { get; }
    public IEventScheduler Scheduler { get; }
    public SegmentCollector SegmentCollector { get; }
    public List<TrackState> Tracks { get; } = new();
    public ResourceSet Resources { get; } = new();
    public BuffManager Buffs { get; }
    public ProcManager Procs { get; } = new();
    public IProfessionModule ProfessionModule { get; }
    public Profession Profession { get; }
    public AutoCastEngine AutoCaster { get; } = new();
    public RngContext Rng { get; }
    public Damage.CritSettings Crit { get; } = new();

    public Encounter? Encounter { get; private set; }
    public EncounterGroup? EncounterGroup { get; private set; }

    public CharacterStats Stats { get; }
    
    /// <summary>玩家战斗单位（Phase 1 基础架构）</summary>
    public PlayerCombatant Player { get; private set; }
    
    /// <summary>目标选取管理器（Phase 2: 基于权重的随机目标选择）</summary>
    public TargetSelector TargetSelector { get; private set; }
    
    /// <summary>Phase 4: 当前战斗中的敌人战斗单位列表（用于怪物攻击）</summary>
    public List<EnemyCombatant> EnemyCombatants { get; } = new();
    
    /// <summary>Phase 6: 当前副本定义（如果有）</summary>
    public DungeonDefinition? CurrentDungeon { get; private set; }
    
    /// <summary>SignalR Phase 2: 战斗通知服务（可选，用于实时通知前端）</summary>
    public IBattleNotificationService? NotificationService { get; private set; }

    public BattleContext(
        Battle battle,
        IGameClock clock,
        IEventScheduler scheduler,
        SegmentCollector collector,
        IProfessionModule professionModule,
        Profession profession,
        RngContext rng,
        Encounter? encounter = null,
        EncounterGroup? encounterGroup = null,
        CharacterStats? stats = null,
        int stamina = 10,
        string? characterId = null,
        string? characterName = null,
        DungeonDefinition? dungeon = null,
        IBattleNotificationService? notificationService = null)
    {
        Battle = battle;
        Clock = clock;
        Scheduler = scheduler;
        SegmentCollector = collector;
        ProfessionModule = professionModule;
        Profession = profession;
        Rng = rng;
        Stats = stats ?? new CharacterStats();
        NotificationService = notificationService;

        EncounterGroup = encounterGroup ?? (encounter != null ? Enemies.EncounterGroup.FromSingle(encounter) : null);
        Encounter = EncounterGroup?.PrimaryAlive() ?? encounter;
        
        // Phase 6: 设置副本定义
        CurrentDungeon = dungeon;
        
        // Phase 1 & Phase 4: 初始化玩家战斗单位（含护甲和格挡）
        var armorCalculator = new Equipment.Services.ArmorCalculator();
        var blockCalculator = new Equipment.Services.BlockCalculator();
        Player = new PlayerCombatant(
            id: characterId ?? battle?.CharacterId.ToString() ?? "unknown",
            name: characterName ?? "Player",
            stats: Stats,
            stamina: stamina,
            armorCalculator: armorCalculator,
            blockCalculator: blockCalculator
        )
        {
            TotalArmor = Stats.Armor,
            BlockChance = Stats.BlockChance
        };
        
        // Phase 6: 根据副本配置设置玩家自动复活
        if (dungeon != null)
        {
            Player.AutoReviveEnabled = dungeon.AllowAutoRevive;
        }
        
        // Phase 2: 初始化目标选取管理器
        TargetSelector = new TargetSelector(rng);

        // DoT：Haste/AP/SP 快照委托 + DoT 命中回调到 Procs（isDot=true）
        Buffs = new BuffManager(
            tagRecorder: (tag, count) => SegmentCollector.OnTag(tag, count),
            resourceRecorder: (res, delta) => SegmentCollector.OnResourceChange(res, delta),
            damageApplier: (src, amount, type) => DamageCalculator.ApplyDamage(this, src, amount, type),
            resolveHasteFactor: () => Buffs.Aggregate.ApplyToBaseHaste(1.0 + Stats.HastePercent),
            resolveApsp: () => (Stats.AttackPower, Stats.SpellPower),
            onDotDirectHit: (src, type, now) => Procs.OnDirectHit(this, src, type, isCrit: false, isDot: true, DirectSourceKind.Dot, now)
        );
    }

    // 新增：切换当前 EncounterGroup（用于连续模式重生/地城切波）
    public void ResetEncounterGroup(EncounterGroup group)
    {
        EncounterGroup = group;
        Encounter = EncounterGroup.PrimaryAlive();
    }

    internal void RefreshPrimaryEncounter()
    {
        Encounter = EncounterGroup?.PrimaryAlive() ?? Encounter;
    }
}