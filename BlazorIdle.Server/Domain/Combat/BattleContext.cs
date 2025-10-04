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

    // 遭遇：单体主目标 + 目标组
    public Encounter? Encounter { get; }            // 兼容旧逻辑
    public EncounterGroup? EncounterGroup { get; }  // 新增

    public BattleContext(
        Battle battle,
        IGameClock clock,
        IEventScheduler scheduler,
        SegmentCollector collector,
        IProfessionModule professionModule,
        Profession profession,
        RngContext rng,
        Encounter? encounter = null,
        EncounterGroup? encounterGroup = null)
    {
        Battle = battle;
        Clock = clock;
        Scheduler = scheduler;
        SegmentCollector = collector;
        ProfessionModule = professionModule;
        Profession = profession;
        Rng = rng;

        // 优先采用传入的组；否则用单体构建单元素组
        EncounterGroup = encounterGroup ?? (encounter != null ? EncounterGroup.FromSingle(encounter) : null);
        Encounter = EncounterGroup?.PrimaryAlive() ?? encounter;

        Buffs = new BuffManager(
            tagRecorder: (tag, count) => SegmentCollector.OnTag(tag, count),
            resourceRecorder: (res, delta) => SegmentCollector.OnResourceChange(res, delta),
            damageApplier: (src, amount, type) => DamageCalculator.ApplyDamage(this, src, amount, type)
        );
    }
}