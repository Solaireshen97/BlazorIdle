using BlazorIdle.Server.Domain.Characters;
using BlazorIdle.Server.Domain.Combat.Buffs;
using BlazorIdle.Server.Domain.Combat.Damage;
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
    public IProfessionModule ProfessionModule { get; }
    public Profession Profession { get; }
    public AutoCastEngine AutoCaster { get; } = new(); // 新增
    public BuffManager Buffs { get; }  // 新增
    public RngContext Rng { get; } // 新增：可重放 RNG
    public CritSettings Crit { get; } = new(); // 新增：全局暴击配置（可被职业/Buff调整）
    public Encounter? Encounter { get; } // 新增：遭遇目标

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
    damageRecorder: (src, dmg) => SegmentCollector.OnDamage(src, dmg)
);
    }
}