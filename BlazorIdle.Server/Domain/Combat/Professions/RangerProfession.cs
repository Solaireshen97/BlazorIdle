using BlazorIdle.Server.Domain.Combat.Buffs;
using BlazorIdle.Server.Domain.Combat.Resources;
using BlazorIdle.Server.Domain.Combat.Skills;

namespace BlazorIdle.Server.Domain.Combat.Professions;

public class RangerProfession : IProfessionModule
{
    public string Id => "ranger";
    public double BaseAttackInterval => 1.4;
    public double BaseSpecialInterval => 4.0;

    public void RegisterBuffDefinitions(BattleContext context)
    {
        context.Buffs.RegisterDefinition(BuffDefinitionsRegistry.RangerBleed);
        context.Buffs.RegisterDefinition(BuffDefinitionsRegistry.FocusFlow);
        context.Buffs.RegisterDefinition(BuffDefinitionsRegistry.RangerHuntersMark);
        context.Buffs.RegisterDefinition(BuffDefinitionsRegistry.RangerSharpsight); // 新
    }

    public void OnBattleStart(BattleContext context)
    {
        context.Resources.Ensure(
            id: "focus",
            max: 100,
            initial: 0,
            policy: Resources.OverflowPolicy.Convert,
            convertUnit: 25,
            conversionTag: "focus_overflow_proc"
        );
    }

    public void OnAttackTick(BattleContext context, AttackTickEvent evt)
    {
        var focus = context.Resources.Get("focus");
        var result = focus.Add(1);
        if (result.AppliedDelta != 0)
            context.SegmentCollector.OnResourceChange("focus", result.AppliedDelta);
        if (result.ConversionCount > 0)
            context.SegmentCollector.OnTag("focus_overflow_proc", result.ConversionCount);
    }

    public void OnSpecialPulse(BattleContext context, SpecialPulseEvent evt)
    {
        var focus = context.Resources.Get("focus");
        var result = focus.Add(5);
        if (result.AppliedDelta != 0)
            context.SegmentCollector.OnResourceChange("focus", result.AppliedDelta);
        if (result.ConversionCount > 0)
            context.SegmentCollector.OnTag("focus_overflow_proc", result.ConversionCount);
        if (focus.Current == focus.Max)
            context.SegmentCollector.OnTag("focus_cap_hit", 1);

        context.Buffs.Apply("focus_flow", context.Clock.CurrentTime);
    }

    public void BuildSkills(BattleContext context, AutoCastEngine engine)
    {
        engine.AddSkill(new SkillDefinition(
            id: "power_shot",
            name: "Power Shot",
            costResourceId: "focus",
            costAmount: 40,
            cooldownSeconds: 5.0,
            priority: 5,
            baseDamage: 70
        ));
        engine.AddSkill(new SkillDefinition(
            id: "quick_shot",
            name: "Quick Shot",
            costResourceId: "focus",
            costAmount: 10,
            cooldownSeconds: 1.5,
            priority: 15,
            baseDamage: 25
        ));
    }

    public void OnSkillCast(BattleContext context, SkillDefinition def)
    {
        if (def.Id == "power_shot")
        {
            context.Buffs.Apply("ranger_hunters_mark", context.Clock.CurrentTime); // 物理易伤
            context.Buffs.Apply("ranger_sharpsight", context.Clock.CurrentTime);   // 暴击倍数+
        }
    }
}