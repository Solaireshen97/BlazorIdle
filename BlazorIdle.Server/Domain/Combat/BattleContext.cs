using BlazorIdle.Server.Domain.Characters;
using BlazorIdle.Server.Domain.Combat.Buffs;
using BlazorIdle.Server.Domain.Combat.Damage;
using BlazorIdle.Server.Domain.Combat.Enemies;
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
    public IProfessionModule ProfessionModule { get; }
    public Profession Profession { get; }
    public AutoCastEngine AutoCaster { get; } = new();
    public RngContext Rng { get; }
    public Damage.CritSettings Crit { get; } = new();

    public Encounter? Encounter { get; }

    public BattleContext(
        Battle battle,
        IGameClock clock,
        IEventScheduler scheduler,
        SegmentCollector collector,
        IProfessionModule professionModule,
        Profession profession,
        RngContext rng,
        Encounter? encounter = null)
    {
        Battle = battle;
        Clock = clock;
        Scheduler = scheduler;
        SegmentCollector = collector;
        ProfessionModule = professionModule;
        Profession = profession;
        Rng = rng;
        Encounter = encounter;

        Buffs = new BuffManager(
            tagRecorder: (tag, count) => SegmentCollector.OnTag(tag, count),
            resourceRecorder: (res, delta) => SegmentCollector.OnResourceChange(res, delta),
            damageApplier: (src, amount, type) => DamageCalculator.ApplyDamage(this, src, amount, type)
        );
    }

    // 快捷：在当前时间发起“立即打断施法”的事件（如果正在施法）
    public bool TryInterruptCasting(InterruptReason reason = InterruptReason.Other)
    {
        var now = Clock.CurrentTime;
        return AutoCaster.RequestInterrupt(this, now, reason);
    }
}