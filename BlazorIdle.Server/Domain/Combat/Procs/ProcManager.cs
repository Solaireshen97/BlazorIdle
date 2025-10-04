using System;
using System.Collections.Generic;
using BlazorIdle.Server.Domain.Combat.Damage;
using BlazorIdle.Server.Domain.Combat.Enemies;
using BlazorIdle.Server.Domain.Combat.Skills;

namespace BlazorIdle.Server.Domain.Combat.Procs;

public enum DirectSourceKind { BasicAttack, Skill, Dot }

public sealed class ProcManager
{
    private readonly List<ProcRuntime> _procs = new();

    public void RegisterDefinition(ProcDefinition def)
    {
        if (_procs.Exists(p => p.Definition.Id == def.Id)) return;
        _procs.Add(new ProcRuntime(def));
    }

    public void OnDirectHit(BattleContext context, string sourceId, DamageType dmgType, bool isCrit, bool isDot, DirectSourceKind sourceKind, double now)
    {
        foreach (var pr in _procs)
        {
            var def = pr.Definition;

            if (def.Trigger == ProcTriggerType.Rppm)
                continue;

            if (pr.InIcd(now))
                continue;

            if (def.SourceFilter == ProcSourceFilter.BasicAttackOnly && sourceKind != DirectSourceKind.BasicAttack)
                continue;
            if (def.SourceFilter == ProcSourceFilter.SkillOnly && sourceKind != DirectSourceKind.Skill)
                continue;

            if (isDot && !def.AllowFromDot)
                continue;

            if (def.DamageTypeFilter.HasValue && def.DamageTypeFilter.Value != dmgType)
                continue;

            if (def.Trigger == ProcTriggerType.OnCrit && !isCrit)
                continue;

            if (def.Trigger == ProcTriggerType.OnHit || def.Trigger == ProcTriggerType.OnCrit)
            {
                var roll = context.Rng.NextDouble();
                if (roll > Math.Clamp(def.Chance, 0, 1))
                    continue;

                Fire(context, pr, now);
            }
        }
    }

    public void EvaluateRppm(BattleContext context, double now, double intervalSeconds)
    {
        foreach (var pr in _procs)
        {
            var def = pr.Definition;
            if (def.Trigger != ProcTriggerType.Rppm)
                continue;

            if (pr.InIcd(now))
                continue;

            if (def.Rppm <= 0)
                continue;

            double p = def.Rppm * (intervalSeconds / 60.0);
            p = Math.Clamp(p, 0, 1);

            var roll = context.Rng.NextDouble();
            if (roll <= p)
            {
                Fire(context, pr, now);
            }
        }
    }

    private void Fire(BattleContext context, ProcRuntime pr, double now)
    {
        var def = pr.Definition;

        switch (def.Action)
        {
            case ProcActionType.ApplyBuff:
                if (!string.IsNullOrEmpty(def.ActionBuffId))
                {
                    context.Buffs.Apply(def.ActionBuffId, now);
                    context.SegmentCollector.OnTag("proc:" + def.Id, 1);
                }
                break;

            case ProcActionType.DealDamage:
                {
                    int val = Math.Max(0, def.ActionDamageValue);

                    // AoE：当配置了多目标并且存在 EncounterGroup 时，对多目标逐个结算
                    if (def.MaxTargets > 1 && def.AoEMode != AoEMode.None && context.EncounterGroup is not null)
                    {
                        var targets = context.EncounterGroup.SelectAlive(def.MaxTargets, includePrimary: def.IncludePrimaryTarget);
                        if (targets.Count == 0 && context.Encounter != null)
                            targets.Add(context.Encounter);

                        if (targets.Count <= 1)
                        {
                            DamageCalculator.ApplyDamage(context, "proc:" + def.Id, val, def.ActionDamageType);
                        }
                        else
                        {
                            switch (def.AoEMode)
                            {
                                case AoEMode.CleaveFull:
                                    foreach (var t in targets)
                                        DamageCalculator.ApplyDamageToTarget(context, t, "proc:" + def.Id, val, def.ActionDamageType);
                                    break;

                                case AoEMode.SplitEven:
                                    int n = targets.Count;
                                    int share = Math.Max(1, val / n);
                                    int remainder = Math.Max(0, val - share * n);
                                    for (int i = 0; i < n; i++)
                                    {
                                        int dmg = share;
                                        bool giveRemainder = def.SplitRemainderToPrimary ? (i == 0) : (i < remainder);
                                        if (remainder > 0 && giveRemainder)
                                        {
                                            dmg += 1;
                                            remainder -= def.SplitRemainderToPrimary ? remainder : 1;
                                        }
                                        DamageCalculator.ApplyDamageToTarget(context, targets[i], "proc:" + def.Id, dmg, def.ActionDamageType);
                                    }
                                    break;
                            }
                        }
                    }
                    else
                    {
                        // 单体默认打主目标
                        DamageCalculator.ApplyDamage(context, "proc:" + def.Id, val, def.ActionDamageType);
                    }

                    context.SegmentCollector.OnTag("proc:" + def.Id, 1);
                    break;
                }
        }

        pr.MarkProc(now);
    }
}