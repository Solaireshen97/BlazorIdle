using BlazorIdle.Server.Domain.Combat.Damage;
using BlazorIdle.Server.Domain.Combat.Procs;
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

        if (def.MaxCharges > 1 && !def.ConsumeChargeOnCast)
        {
            var haste = context.Buffs.Aggregate.ApplyToBaseHaste(1.0 + context.Stats.HastePercent);
            var effRecharge = def.RechargeAffectedByHaste ? Math.Max(0.01, def.RechargeSeconds / haste) : def.RechargeSeconds;
            Slot.Runtime.ConsumeAtComplete(ExecuteAt, effRecharge);
        }
        else if (def.MaxCharges <= 1)
        {
            Slot.Runtime.MarkCast(ExecuteAt);
        }

        // AP/SP 注入 + 暴击
        double preCrit = def.BaseDamage
            + context.Stats.AttackPower * def.AttackPowerCoef
            + context.Stats.SpellPower * def.SpellPowerCoef;

        var (chance, mult) = context.Crit.ResolveWith(
            context.Buffs.Aggregate,
            def.CritChance ?? context.Stats.CritChance,
            def.CritMultiplier ?? context.Stats.CritMultiplier
        );
        bool isCrit = context.Rng.NextBool(chance);
        int baseDmg = isCrit ? (int)Math.Round(preCrit * mult) : (int)Math.Round(preCrit);
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
        context.Procs.OnDirectHit(context, "skill:" + def.Id, type, isCrit, isDot: false, DirectSourceKind.Skill, ExecuteAt);

        // SignalR: 发送技能施放完成轻量事件通知（用于前端进度条增量更新）
        if (context.NotificationService?.IsAvailable == true)
        {
            var eventDto = new BlazorIdle.Shared.Models.SkillCastCompleteEventDto
            {
                BattleId = context.Battle.Id,
                EventTime = ExecuteAt,
                EventType = "SkillCastComplete",
                SkillId = def.Id,
                CastCompleteAt = ExecuteAt
            };
            _ = context.NotificationService.NotifyEventAsync(context.Battle.Id, eventDto);
        }

        context.AutoCaster.ClearCasting();
        context.AutoCaster.TryAutoCast(context, ExecuteAt);
    }
}