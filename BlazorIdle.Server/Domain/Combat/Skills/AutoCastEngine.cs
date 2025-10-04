using System;
using System.Collections.Generic;
using BlazorIdle.Server.Domain.Combat.Damage;
using BlazorIdle.Server.Domain.Combat.Resources;

namespace BlazorIdle.Server.Domain.Combat.Skills;

public class AutoCastEngine
{
    private readonly List<SkillSlot> _slots = new();

    public IReadOnlyList<SkillSlot> Slots => _slots;

    public void AddSkill(SkillDefinition def)
    {
        _slots.Add(new SkillSlot(def));
        _slots.Sort((a, b) => a.Runtime.Definition.Priority.CompareTo(b.Runtime.Definition.Priority));
    }

    public bool TryAutoCast(BattleContext context, double now)
    {
        foreach (var slot in _slots)
        {
            var def = slot.Runtime.Definition;
            if (!slot.Runtime.IsReady(now))
                continue;

            if (def.CostResourceId is not null && def.CostAmount > 0)
            {
                if (!context.Resources.TryGet(def.CostResourceId, out var bucket))
                    continue;
                if (bucket.Current < def.CostAmount)
                    continue;

                var result = bucket.Add(-def.CostAmount);
                if (result.AppliedDelta != 0)
                    context.SegmentCollector.OnResourceChange(def.CostResourceId, result.AppliedDelta);
            }

            var dmgSource = "skill:" + def.Id;
            int dmg = def.BaseDamage;
            double chance = def.CritChance ?? context.Crit.Chance;
            double mult = def.CritMultiplier ?? context.Crit.Multiplier;

            bool isCrit = context.Rng.NextBool(chance);
            if (isCrit)
            {
                dmg = (int)Math.Round(dmg * mult);
                context.SegmentCollector.OnTag("crit:" + dmgSource, 1);
            }

            // 使用 DamageCalculator，技能默认按物理，可将来在定义中扩展 DamageType
            var type = DamageType.Physical;
            if (def is SkillDefinitionExt ext) type = ext.DamageType;

            DamageCalculator.ApplyDamage(context, dmgSource, dmg, type);
            context.SegmentCollector.OnTag("skill_cast:" + def.Id, 1);

            context.ProfessionModule.OnSkillCast(context, def);

            slot.Runtime.MarkCast(now);
            return true;
        }
        return false;
    }
}

// 可选：扩展版技能定义带 DamageType
public class SkillDefinitionExt : SkillDefinition
{
    public DamageType DamageType { get; }

    public SkillDefinitionExt(
        string id, string name,
        string? costResourceId, int costAmount,
        double cooldownSeconds, int priority, int baseDamage,
        DamageType damageType,
        double? critChance = null, double? critMultiplier = null)
        : base(id, name, costResourceId, costAmount, cooldownSeconds, priority, baseDamage, critChance, critMultiplier)
    {
        DamageType = damageType;
    }
}