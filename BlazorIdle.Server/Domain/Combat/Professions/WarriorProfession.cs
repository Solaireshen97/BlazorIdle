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
        context.Buffs.RegisterDefinition(BuffDefinitionsRegistry.WarriorPrecision);
    }

    public void OnBattleStart(BattleContext context)
    {
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
        // 主要伤害技能（示例）
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

        // OffGCD 编织技能：Battle Shout（瞬发、可在施法中释放，提供 Precision）
        engine.AddSkill(new SkillDefinition(
            id: "battle_shout",
            name: "Battle Shout",
            costResourceId: null,
            costAmount: 0,
            cooldownSeconds: 20.0,  // 自身冷却
            priority: 3,            // 比伤害技能更高的优先级，便于及时编织
            baseDamage: 0,          // 无直接伤害
            offGcd: true,           // 不受 GCD 限制
            allowDuringCastingForOffGcd: true // 允许在施法期间释放
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
            // 释放即获得暴击率增益（演示 OffGCD 编织的实效玩法）
            context.Buffs.Apply("warrior_precision", context.Clock.CurrentTime);
            context.SegmentCollector.OnTag("shout_buff_applied", 1);
        }
    }
}