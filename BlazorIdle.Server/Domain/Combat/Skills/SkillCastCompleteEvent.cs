using BlazorIdle.Server.Domain.Combat.Damage;
using BlazorWebGame.Domain.Combat;
using System;

namespace BlazorIdle.Server.Domain.Combat.Skills;

public record SkillCastCompleteEvent(double ExecuteAt, SkillSlot Slot, long CastId) : IGameEvent
{
    public string EventType => "SkillCastComplete";

    public void Execute(BattleContext context)
    {
        // 若期间已被打断或换了施法，忽略（幂等保护）
        if (!context.AutoCaster.IsCasting || context.AutoCaster.CurrentCastId is null || context.AutoCaster.CurrentCastId != CastId)
            return;

        var def = Slot.Runtime.Definition;

        // 如未在开始扣资源，则在完成时扣
        if (!def.SpendCostOnCast && def.CostResourceId is not null && def.CostAmount > 0)
        {
            if (context.Resources.TryGet(def.CostResourceId, out var bucket) && bucket.Current >= def.CostAmount)
            {
                var result = bucket.Add(-def.CostAmount);
                if (result.AppliedDelta != 0)
                    context.SegmentCollector.OnResourceChange(def.CostResourceId, result.AppliedDelta);
            }
            else
            {
                // 资源不足，施法作废（不进入冷却）
                context.AutoCaster.ClearCasting();
                return;
            }
        }

        // 结算伤害
        var (chance, mult) = context.Crit.ResolveWith(context.Buffs.Aggregate, def.CritChance, def.CritMultiplier);
        bool isCrit = context.Rng.NextBool(chance);
        int dmg = isCrit ? (int)Math.Round(def.BaseDamage * mult) : def.BaseDamage;
        if (isCrit) context.SegmentCollector.OnTag("crit:skill:" + def.Id, 1);

        var type = Damage.DamageType.Physical;
        if (def is SkillDefinitionExt ext) type = ext.DamageType;

        DamageCalculator.ApplyDamage(context, "skill:" + def.Id, dmg, type);
        context.SegmentCollector.OnTag("skill_cast:" + def.Id, 1);

        // 职业钩子
        context.ProfessionModule.OnSkillCast(context, def);

        // 完成时进入冷却
        Slot.Runtime.MarkCast(ExecuteAt);

        // 清除施法状态
        context.AutoCaster.ClearCasting();

        // 完成后尝试继续自动施放
        context.AutoCaster.TryAutoCast(context, ExecuteAt);
    }
}