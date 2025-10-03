using System;
using System.Collections.Generic;
using System.Linq;
using BlazorIdle.Server.Domain.Combat.Resources;

namespace BlazorIdle.Server.Domain.Combat.Skills;

public class AutoCastEngine
{
    private readonly List<SkillSlot> _slots = new();

    public IReadOnlyList<SkillSlot> Slots => _slots;

    public void AddSkill(SkillDefinition def)
    {
        _slots.Add(new SkillSlot(def));
        // 保持按优先级排序（优先级数字小排前）
        _slots.Sort((a, b) => a.Runtime.Definition.Priority.CompareTo(b.Runtime.Definition.Priority));
    }

    public bool TryAutoCast(BattleContext context, double now)
    {
        foreach (var slot in _slots)
        {
            var def = slot.Runtime.Definition;
            if (!slot.Runtime.IsReady(now))
                continue;

            // 资源判定
            if (def.CostResourceId is not null && def.CostAmount > 0)
            {
                if (!context.Resources.TryGet(def.CostResourceId, out var bucket))
                    continue; // 没有该资源，跳过

                if (bucket.Current < def.CostAmount)
                    continue; // 资源不足

                // 扣资源
                var before = bucket.Current;
                var result = bucket.Add(-def.CostAmount);
                var delta = result.AppliedDelta; // 负数
                if (delta != 0)
                    context.SegmentCollector.OnResourceChange(def.CostResourceId, delta);
            }

            // 造成伤害
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

            context.SegmentCollector.OnDamage(dmgSource, dmg);
            context.SegmentCollector.OnTag("skill_cast:" + def.Id, 1);

            // 职业钩子（可做额外效果）
            context.ProfessionModule.OnSkillCast(context, def);

            // 进入冷却
            slot.Runtime.MarkCast(now);
            return true; // 每次只施放一个技能
        }
        return false;
    }
}