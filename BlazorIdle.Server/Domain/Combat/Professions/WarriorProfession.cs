using BlazorIdle.Server.Domain.Combat.Resources;
using BlazorWebGame.Domain.Combat;

namespace BlazorIdle.Server.Domain.Combat.Professions;

public class WarriorProfession : IProfessionModule
{
    public string Id => "warrior";
    public double BaseAttackInterval => 1.5;
    public double BaseSpecialInterval => 5.0;

    public void OnBattleStart(BattleContext context)
    {
        context.Resources.Ensure(
            id: "rage",
            max: 5,
            initial: 0,
            policy: OverflowPolicy.Convert,
            convertUnit: 1,
            conversionTag: "rage_overflow_proc"
        );
    }

    public void OnAttackTick(BattleContext context, AttackTickEvent evt)
    {
        var rage = context.Resources.Get("rage");
        var result = rage.Add(1);

        if (result.AppliedDelta != 0)
            context.SegmentCollector.OnResourceChange("rage", result.AppliedDelta);

        if (result.ConversionCount > 0)
            context.SegmentCollector.OnTag("rage_overflow_proc", result.ConversionCount);

        if (rage.Current == rage.Max)
            context.SegmentCollector.OnTag("rage_cap_hit", 1);
    }

    public void OnSpecialPulse(BattleContext context, SpecialPulseEvent evt)
    {
        // 初版战士脉冲不额外给 rage，可扩展：脉冲期间 buff
    }
}