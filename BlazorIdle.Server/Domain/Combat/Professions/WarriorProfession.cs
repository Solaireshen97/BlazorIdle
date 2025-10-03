using BlazorIdle.Server.Domain.Combat.Resources;
using BlazorIdle.Server.Domain.Combat.Skills;

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
            max: 100,
            initial: 0,
            policy: OverflowPolicy.Convert,
            convertUnit: 20,
            conversionTag: "rage_overflow_proc"
        );
    }

    public void OnAttackTick(BattleContext context, AttackTickEvent evt)
    {
        var rage = context.Resources.Get("rage");
        var result = rage.Add(10);
        if (result.AppliedDelta != 0)
            context.SegmentCollector.OnResourceChange("rage", result.AppliedDelta);
        if (result.ConversionCount > 0)
            context.SegmentCollector.OnTag("rage_overflow_proc", result.ConversionCount);
        if (rage.Current == rage.Max)
            context.SegmentCollector.OnTag("rage_cap_hit", 1);
    }

    public void OnSpecialPulse(BattleContext context, SpecialPulseEvent evt)
    {
        // 预留：可在这里添加脉冲增益
    }

    public void BuildSkills(BattleContext context, AutoCastEngine engine)
    {
        // 优先级：数值越小越先尝试
        // HeroicStrike（高伤耗资源）
        engine.AddSkill(new SkillDefinition(
            id: "heroic_strike",
            name: "Heroic Strike",
            costResourceId: "rage",
            costAmount: 30,
            cooldownSeconds: 3.0,
            priority: 10,
            baseDamage: 50
        ));
        // 未来可以追加更多战士技能
    }

    public void OnSkillCast(BattleContext context, SkillDefinition def)
    {
        // 预留：例如施放 HeroicStrike 时加一个短 Buff
    }
}