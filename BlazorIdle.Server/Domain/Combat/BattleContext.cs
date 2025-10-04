using BlazorIdle.Server.Domain.Characters;
using BlazorIdle.Server.Domain.Combat.Buffs;
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

    public Encounter? Encounter { get; }
    public EncounterGroup? EncounterGroup { get; }

    public CharacterStats Stats { get; }

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
        CharacterStats? stats = null)
    {
        Battle = battle;
        Clock = clock;
        Scheduler = scheduler;
        SegmentCollector = collector;
        ProfessionModule = professionModule;
        Profession = profession;
        Rng = rng;
        Stats = stats ?? new CharacterStats();

        EncounterGroup = encounterGroup ?? (encounter != null ? EncounterGroup.FromSingle(encounter) : null);
        Encounter = EncounterGroup?.PrimaryAlive() ?? encounter;

        // DoT：Haste 快照 + AP/SP 快照，均由委托提供
        Buffs = new BuffManager(
            tagRecorder: (tag, count) => SegmentCollector.OnTag(tag, count),
            resourceRecorder: (res, delta) => SegmentCollector.OnResourceChange(res, delta),
            damageApplier: (src, amount, type) => DamageCalculator.ApplyDamage(this, src, amount, type),
            resolveHasteFactor: () => Buffs.Aggregate.ApplyToBaseHaste(1.0 + Stats.HastePercent),
            resolveApsp: () => (Stats.AttackPower, Stats.SpellPower)
        );
    }
}