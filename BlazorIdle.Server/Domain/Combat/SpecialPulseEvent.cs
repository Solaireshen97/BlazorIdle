using BlazorWebGame.Domain.Combat;

namespace BlazorIdle.Server.Domain.Combat;

public record SpecialPulseEvent(double ExecuteAt, TrackState Track) : IGameEvent
{
    public string EventType => "SpecialPulse";

    public void Execute(BattleContext context)
    {
        // 基础 Tag（维持以前统计）
        context.SegmentCollector.OnTag("special_pulse", 1);

        // 职业钩子
        context.ProfessionModule.OnSpecialPulse(context, this);
        context.AutoCaster.TryAutoCast(context, ExecuteAt);

        Track.NextTriggerAt = ExecuteAt + Track.CurrentInterval;
        context.Scheduler.Schedule(new SpecialPulseEvent(Track.NextTriggerAt, Track));
    }
}