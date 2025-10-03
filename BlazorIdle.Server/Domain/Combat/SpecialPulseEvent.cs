using BlazorWebGame.Domain.Combat;

namespace BlazorIdle.Server.Domain.Combat;

public record SpecialPulseEvent(double ExecuteAt, TrackState Track) : IGameEvent
{
    public string EventType => "SpecialPulse";

    public void Execute(BattleContext context)
    {
        // 先记录一个 tag，后面可以在这里加资源、Buff、职业特效
        context.SegmentCollector.OnTag("special_pulse", 1);

        // 调度下一次
        Track.NextTriggerAt = ExecuteAt + Track.CurrentInterval;
        context.Scheduler.Schedule(new SpecialPulseEvent(Track.NextTriggerAt, Track));
    }
}