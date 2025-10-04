using BlazorIdle.Server.Domain.Combat.Buffs;
using BlazorIdle.Server.Domain.Combat.Procs;
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
        context.Buffs.RegisterDefinition(BuffDefinitionsRegistry.WarriorPrecision);
    }

    public void OnBattleStart(BattleContext context)
    {
        // 注册战士示例 Proc
        context.Procs.RegisterDefinition(ProcDefinitionsRegistry.WarriorShredOnHit);

        context.Resources.Ensure(
            id: "rage",
            max: 100,
            initial: 0,
            policy: Resources.OverflowPolicy.Convert,
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

    public virtual void BuildSkills(BattleContext context, AutoCastEngine engine)
    {
        engine.AddSkill(new SkillDefinition(
            id: "heroic_strike",
            name: "Heroic Strike",
            costResourceId: "rage",
            costAmount: 30,
            cooldownSeconds: 3.0,
            priority: 10,
            baseDamage: 50,
            critChance: 0.15,
            critMultiplier: 2.2
        ));

        // OffGCD 示例供参考（如之前已加入）
        engine.AddSkill(new SkillDefinition(
            id: "battle_shout",
            name: "Battle Shout",
            costResourceId: null,
            costAmount: 0,
            cooldownSeconds: 20.0,
            priority: 3,
            baseDamage: 0,
            offGcd: true,
            allowDuringCastingForOffGcd: true
        ));
    }

    public virtual void OnSkillCast(BattleContext context, SkillDefinition def)
    {
        if (def.Id == "heroic_strike")
        {
            context.Buffs.Apply("warrior_expose_armor", context.Clock.CurrentTime);
            context.Buffs.Apply("warrior_precision", context.Clock.CurrentTime);
        }
        else if (def.Id == "battle_shout")
        {
            context.Buffs.Apply("warrior_precision", context.Clock.CurrentTime);
            context.SegmentCollector.OnTag("shout_buff_applied", 1);
        }
    }
}