namespace BlazorWebGame.Domain.Combat;

public class BattleContext
{
    public Battle Battle { get; }
    public IGameClock Clock { get; }
    public IEventScheduler Scheduler { get; }
    public SegmentCollector SegmentCollector { get; }

    public BattleContext(Battle battle, IGameClock clock, IEventScheduler scheduler, SegmentCollector collector)
    {
        Battle = battle;
        Clock = clock;
        Scheduler = scheduler;
        SegmentCollector = collector;
    }
}