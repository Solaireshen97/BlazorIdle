using BlazorWebGame.Domain.Combat;

namespace BlazorIdle.Server.Domain.Combat;

public record AttackTickEvent(double ExecuteAt, TrackState Track) : IGameEvent
{
    public string EventType => "AttackTick";

    public void Execute(BattleContext context)
    {
        const int dmg = 10; // 之后可抽离成公式
        context.SegmentCollector.OnDamage("basic_attack", dmg);

        // 调度下一次
        Track.NextTriggerAt = ExecuteAt + Track.CurrentInterval;
        context.Scheduler.Schedule(new AttackTickEvent(Track.NextTriggerAt, Track));
    }
}