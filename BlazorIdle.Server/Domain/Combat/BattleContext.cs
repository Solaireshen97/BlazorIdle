using BlazorIdle.Server.Domain.Characters;
using BlazorIdle.Server.Domain.Combat.Professions;
using BlazorIdle.Server.Domain.Combat.Resources;
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
    public IProfessionModule ProfessionModule { get; } // 新增
    public Profession Profession { get; }              // 记录职业（可选）

    public BattleContext(
        Battle battle,
        IGameClock clock,
        IEventScheduler scheduler,
        SegmentCollector collector,
        IProfessionModule professionModule,
        Profession profession)
    {
        Battle = battle;
        Clock = clock;
        Scheduler = scheduler;
        SegmentCollector = collector;
        ProfessionModule = professionModule;
        Profession = profession;
    }
}