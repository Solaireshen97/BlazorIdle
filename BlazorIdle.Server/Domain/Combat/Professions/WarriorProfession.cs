using BlazorIdle.Server.Domain.Combat.Buffs;
using BlazorIdle.Server.Domain.Combat.Resources;
using BlazorIdle.Server.Domain.Combat.Skills;

namespace BlazorIdle.Server.Domain.Combat.Professions;

public class WarriorProfession : IProfessionModule
{
    public string Id => "warrior";
    public double BaseAttackInterval => 1.5;
    public double BaseSpecialInterval => 5.0;

    public void RegisterBuffDefinitions(BattleContext context)
    {
        context.Buffs.RegisterDefinition(BuffDefinitionsRegistry.WarriorBerserk);
        context.Buffs.RegisterDefinition(BuffDefinitionsRegistry.WarriorExposeArmor);
    }

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
        // 预留
    }

    public void BuildSkills(BattleContext context, AutoCastEngine engine)
    {
        engine.AddSkill(new SkillDefinition(
            id: "heroic_strike",
            name: "Heroic Strike",
            costResourceId: "rage",
            costAmount: 30,
            cooldownSeconds: 3.0,
            priority: 10,
            baseDamage: 50
        ));
    }

    public void OnSkillCast(BattleContext context, SkillDefinition def)
    {
        if (def.Id == "heroic_strike")
        {
            // 施加爆发加速 or 护甲破甲，这里示范破甲
            context.Buffs.Apply("warrior_expose_armor", context.Clock.CurrentTime);
        }
    }
}