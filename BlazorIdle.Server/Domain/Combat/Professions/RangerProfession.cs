using BlazorIdle.Server.Domain.Combat.Buffs;
using BlazorIdle.Server.Domain.Combat.Procs;
using BlazorIdle.Server.Domain.Combat.Resources;
using BlazorIdle.Server.Domain.Combat.Skills;

namespace BlazorIdle.Server.Domain.Combat.Professions;

public class RangerProfession : IProfessionModule
{
    public string Id => "ranger";
    public double BaseAttackInterval => 1.4;
    public double BaseSpecialInterval => 4.0;
    
    /// <summary>
    /// 游侠的特殊轨道在无怪物时暂停（默认行为）
    /// </summary>
    public virtual bool PauseSpecialWhenNoEnemies => true;
    
    /// <summary>
    /// 游侠的特殊轨道等待间隔后触发（默认行为）
    /// </summary>
    public virtual bool SpecialStartsImmediately => false;

    public virtual void RegisterBuffDefinitions(BattleContext context)
    {
        context.Buffs.RegisterDefinition(BuffDefinitionsRegistry.RangerBleed);
        context.Buffs.RegisterDefinition(BuffDefinitionsRegistry.FocusFlow);
        context.Buffs.RegisterDefinition(BuffDefinitionsRegistry.RangerHuntersMark);
        context.Buffs.RegisterDefinition(BuffDefinitionsRegistry.RangerSharpsight);
    }

    public virtual void OnBattleStart(BattleContext context)
    {
        // 可选注册：Proc 示例（按需启用）
        // context.Procs.RegisterDefinition(ProcDefinitionsRegistry.RangerExplosiveArrow);
        // context.Procs.RegisterDefinition(ProcDefinitionsRegistry.RangerExplosiveBurst);

        context.Resources.Ensure(
            id: "focus",
            max: 100,
            initial: 0,
            policy: Resources.OverflowPolicy.Convert,
            convertUnit: 25,
            conversionTag: "focus_overflow_proc"
        );
    }

    public virtual void OnAttackTick(BattleContext context, AttackTickEvent evt)
    {
        var focus = context.Resources.Get("focus");
        var result = focus.Add(1);
        if (result.AppliedDelta != 0)
            context.SegmentCollector.OnResourceChange("focus", result.AppliedDelta);
        if (result.ConversionCount > 0)
            context.SegmentCollector.OnTag("focus_overflow_proc", result.ConversionCount);
    }

    public virtual void OnSpecialPulse(BattleContext context, SpecialPulseEvent evt)
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

    public virtual void BuildSkills(BattleContext context, AutoCastEngine engine)
    {
        engine.AddSkill(new SkillDefinition(
            id: "power_shot",
            name: "Power Shot",
            costResourceId: "focus",
            costAmount: 40,
            cooldownSeconds: 5.0,
            priority: 5,
            baseDamage: 70,
            critChance: 0.25,
            critMultiplier: 2.4
        ));

        engine.AddSkill(new SkillDefinition(
            id: "bleed_shot",
            name: "Bleed Shot",
            costResourceId: "focus",
            costAmount: 25,
            cooldownSeconds: 6.0,
            priority: 12,
            baseDamage: 30,
            critChance: 0.10,
            critMultiplier: 2.0
        ));

        engine.AddSkill(new SkillDefinition(
            id: "quick_shot",
            name: "Quick Shot",
            costResourceId: "focus",
            costAmount: 10,
            cooldownSeconds: 1.5,
            priority: 15,
            baseDamage: 25,
            critChance: 0.05,
            critMultiplier: 1.8
        ));
    }

    public virtual void OnSkillCast(BattleContext context, SkillDefinition def)
    {
        if (def.Id == "power_shot")
        {
            context.Buffs.Apply("ranger_hunters_mark", context.Clock.CurrentTime);
            context.Buffs.Apply("ranger_sharpsight", context.Clock.CurrentTime);
        }
        else if (def.Id == "bleed_shot")
        {
            context.Buffs.Apply("ranger_bleed", context.Clock.CurrentTime);
        }
    }
}