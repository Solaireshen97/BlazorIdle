using BlazorIdle.Server.Domain.Combat.Resources;
using BlazorWebGame.Domain.Combat;

namespace BlazorIdle.Server.Domain.Combat;

public record AttackTickEvent(double ExecuteAt, TrackState Track) : IGameEvent
{
    public string EventType => "AttackTick";

    public void Execute(BattleContext context)
    {
        const int dmg = 10; // 之后可抽离成公式
        context.SegmentCollector.OnDamage("basic_attack", dmg);
        // 确保 Rage 桶存在：max=10, 溢出 Convert，每 2 转化 1 次
        var rage = context.Resources.Ensure(
            id: "rage",
            max: 10,
            initial: 0,
            policy: OverflowPolicy.Convert,
            convertUnit: 2,
            conversionTag: "rage_overflow_proc"
        );
        var result = rage.Add(1); // 每次普攻 +1
        if (result.AppliedDelta != 0)
            context.SegmentCollector.OnResourceChange("rage", result.AppliedDelta);
        if (result.ConversionCount > 0)
            context.SegmentCollector.OnTag("rage_overflow_proc", result.ConversionCount);
        if (rage.Current == rage.Max)
            context.SegmentCollector.OnTag("rage_cap_hit", 1);
        // 调度下一次
        Track.NextTriggerAt = ExecuteAt + Track.CurrentInterval;
        context.Scheduler.Schedule(new AttackTickEvent(Track.NextTriggerAt, Track));
    }
}