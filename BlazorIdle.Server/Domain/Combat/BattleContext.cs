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
    
    /// <summary>目标选取管理器（Phase 2 目标选取系统）</summary>
    public TargetSelector TargetSelector { get; }

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
        string? characterName = null)
    {
        Battle = battle;
        Clock = clock;
        Scheduler = scheduler;
        SegmentCollector = collector;
        ProfessionModule = professionModule;
        Profession = profession;
        Rng = rng;
        Stats = stats ?? new CharacterStats();

        EncounterGroup = encounterGroup ?? (encounter != null ? Enemies.EncounterGroup.FromSingle(encounter) : null);
        Encounter = EncounterGroup?.PrimaryAlive() ?? encounter;
        
        // Phase 1: 初始化玩家战斗单位
        Player = new PlayerCombatant(
            id: characterId ?? battle?.CharacterId.ToString() ?? "unknown",
            name: characterName ?? "Player",
            stats: Stats,
            stamina: stamina
        );
        
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
    
    /// <summary>
    /// 获取所有敌人战斗单位（Phase 2 目标选取系统）
    /// </summary>
    /// <returns>敌人战斗单位列表</returns>
    public List<EnemyCombatant> GetAllEnemyCombatants()
    {
        var result = new List<EnemyCombatant>();
        if (EncounterGroup != null)
        {
            int index = 0;
            foreach (var encounter in EncounterGroup.All)
            {
                var combatant = new EnemyCombatant($"enemy_{index}", encounter);
                result.Add(combatant);
                index++;
            }
        }
        return result;
    }
}