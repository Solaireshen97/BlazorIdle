using BlazorIdle.Server.Domain.Combat.Damage;
using BlazorWebGame.Domain.Combat;
using System;

namespace BlazorIdle.Server.Domain.Combat.Skills;

public record SkillCastCompleteEvent(double ExecuteAt, SkillSlot Slot, long CastId) : IGameEvent
{
    public string EventType => "SkillCastComplete";

    public void Execute(BattleContext context)
    {
        if (!context.AutoCaster.IsCasting || context.AutoCaster.CurrentCastId is null || context.AutoCaster.CurrentCastId != CastId)
            return;

        var def = Slot.Runtime.Definition;

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
                context.AutoCaster.ClearCasting();
                return;
            }
        }

        // 充能/冷却：完成时消耗（或单充能进入冷却）
        if (def.MaxCharges > 1 && !def.ConsumeChargeOnCast)
        {
            var haste = context.Buffs.Aggregate.ApplyToBaseHaste(1.0);
            var effRecharge = def.RechargeAffectedByHaste ? Math.Max(0.01, def.RechargeSeconds / haste) : def.RechargeSeconds;
            Slot.Runtime.ConsumeAtComplete(ExecuteAt, effRecharge);
        }
        else if (def.MaxCharges <= 1)
        {
            Slot.Runtime.MarkCast(ExecuteAt);
        }

        // 伤害 + AoE
        var engine = context.AutoCaster;
        // 复用引擎的伤害逻辑以保持一致标签/暴击处理
        // 简化：复制与引擎一致的计算（避免交叉依赖）
        var (chance, mult) = context.Crit.ResolveWith(context.Buffs.Aggregate, def.CritChance, def.CritMultiplier);
        bool isCrit = context.Rng.NextBool(chance);
        int baseDmg = isCrit ? (int)Math.Round(def.BaseDamage * mult) : def.BaseDamage;
        if (isCrit) context.SegmentCollector.OnTag("crit:skill:" + def.Id, 1);

        var type = Damage.DamageType.Physical;
        if (def is SkillDefinitionExt ext) type = ext.DamageType;

        if (def.AoEMode != AoEMode.None && def.MaxTargets > 1 && context.EncounterGroup is not null)
        {
            var targets = context.EncounterGroup.SelectAlive(def.MaxTargets, includePrimary: def.IncludePrimaryTarget);
            if (targets.Count == 0 && context.Encounter != null)
                targets.Add(context.Encounter);

            if (targets.Count <= 1)
            {
                DamageCalculator.ApplyDamage(context, "skill:" + def.Id, baseDmg, type);
            }
            else
            {
                switch (def.AoEMode)
                {
                    case AoEMode.CleaveFull:
                        foreach (var t in targets)
                            DamageCalculator.ApplyDamageToTarget(context, t, "skill:" + def.Id, baseDmg, type);
                        break;

                    case AoEMode.SplitEven:
                        int n = targets.Count;
                        int share = Math.Max(1, baseDmg / n);
                        int remainder = Math.Max(0, baseDmg - share * n);
                        for (int i = 0; i < n; i++)
                        {
                            int dmg = share;
                            if (remainder > 0 && ((def.SplitRemainderToPrimary && i == 0) || (!def.SplitRemainderToPrimary && i < remainder)))
                                dmg += 1;
                            DamageCalculator.ApplyDamageToTarget(context, targets[i], "skill:" + def.Id, dmg, type);
                        }
                        break;
                }
            }
        }
        else
        {
            DamageCalculator.ApplyDamage(context, "skill:" + def.Id, baseDmg, type);
        }

        context.SegmentCollector.OnTag("skill_cast:" + def.Id, 1);
        context.ProfessionModule.OnSkillCast(context, def);

        context.AutoCaster.ClearCasting();
        context.AutoCaster.TryAutoCast(context, ExecuteAt);
    }
}