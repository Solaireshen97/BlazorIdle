using System;
using System.Collections.Generic;
using BlazorIdle.Server.Domain.Combat.Damage;

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

    // 命中触发：由普攻与技能伤害调用
    public void OnDirectHit(BattleContext context, string sourceId, DamageType dmgType, bool isCrit, bool isDot, DirectSourceKind sourceKind, double now)
    {
        foreach (var pr in _procs)
        {
            var def = pr.Definition;

            if (def.Trigger == ProcTriggerType.Rppm)
                continue; // RPPM 不在 hit 时机评估

            if (pr.InIcd(now))
                continue;

            // 来源过滤
            if (def.SourceFilter == ProcSourceFilter.BasicAttackOnly && sourceKind != DirectSourceKind.BasicAttack)
                continue;
            if (def.SourceFilter == ProcSourceFilter.SkillOnly && sourceKind != DirectSourceKind.Skill)
                continue;

            // DoT 过滤
            if (isDot && !def.AllowFromDot)
                continue;

            // 伤害类型过滤
            if (def.DamageTypeFilter.HasValue && def.DamageTypeFilter.Value != dmgType)
                continue;

            // 触发类型：OnHit / OnCrit
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

    // RPPM 评估：每个 ProcPulseEvent（默认每秒一次）调用
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

            // 基础 RPPM 概率：PPM * Δt / 60
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

        // 动作执行
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
                    // 单体：默认打主目标
                    DamageCalculator.ApplyDamage(context, "proc:" + def.Id, val, def.ActionDamageType);
                    context.SegmentCollector.OnTag("proc:" + def.Id, 1);
                }
                break;
        }

        pr.MarkProc(now);
    }
}