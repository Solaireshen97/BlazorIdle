using BlazorIdle.Server.Domain.Combat.Resources;
using BlazorWebGame.Domain.Combat;

namespace BlazorIdle.Server.Domain.Combat.Professions;

public class RangerProfession : IProfessionModule
{
    public string Id => "ranger";
    public double BaseAttackInterval => 1.4;   // 可稍快
    public double BaseSpecialInterval => 4.0;  // 脉冲更频繁

    public void OnBattleStart(BattleContext context)
    {
        context.Resources.Ensure(
            id: "focus",
            max: 100,
            initial: 0,
            policy: OverflowPolicy.Convert,
            convertUnit: 25,
            conversionTag: "focus_overflow_proc"
        );
    }

    public void OnAttackTick(BattleContext context, AttackTickEvent evt)
    {
        // 轻量积蓄：+1
        var bucket = context.Resources.Get("focus");
        var result = bucket.Add(1);
        if (result.AppliedDelta != 0)
            context.SegmentCollector.OnResourceChange("focus", result.AppliedDelta);
        if (result.ConversionCount > 0)
            context.SegmentCollector.OnTag("focus_overflow_proc", result.ConversionCount);
    }

    public void OnSpecialPulse(BattleContext context, SpecialPulseEvent evt)
    {
        // 脉冲获得额外 Focus +5
        var bucket = context.Resources.Get("focus");
        var result = bucket.Add(5);
        if (result.AppliedDelta != 0)
            context.SegmentCollector.OnResourceChange("focus", result.AppliedDelta);
        if (result.ConversionCount > 0)
            context.SegmentCollector.OnTag("focus_overflow_proc", result.ConversionCount);

        if (bucket.Current == bucket.Max)
            context.SegmentCollector.OnTag("focus_cap_hit", 1);
    }
}