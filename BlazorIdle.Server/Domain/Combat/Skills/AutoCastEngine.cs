using System;
using System.Collections.Generic;
using BlazorIdle.Server.Domain.Combat.Damage;
using BlazorIdle.Server.Domain.Combat.Resources;

namespace BlazorIdle.Server.Domain.Combat.Skills;

public class AutoCastEngine
{
    private readonly List<SkillSlot> _slots = new();

    public IReadOnlyList<SkillSlot> Slots => _slots;

    // 全局冷却 & 施法状态
    public double GlobalCooldownUntil { get; private set; } = 0;
    public bool IsCasting { get; private set; }
    public double CastingUntil { get; private set; }
    public bool CastingSkillLocksAttack { get; private set; }
    private SkillSlot? _castingSlot;

    public void AddSkill(SkillDefinition def)
    {
        _slots.Add(new SkillSlot(def));
        _slots.Sort((a, b) => a.Runtime.Definition.Priority.CompareTo(b.Runtime.Definition.Priority));
    }

    public bool TryAutoCast(BattleContext context, double now)
    {
        // 施法进行中：首版不支持编织，直接返回
        if (IsCasting) return false;

        foreach (var slot in _slots)
        {
            var def = slot.Runtime.Definition;

            // GCD 限制（OffGcd 技能可绕过）
            if (!def.OffGcd && now < GlobalCooldownUntil)
                continue;

            // 冷却判断（单充能版）
            if (!slot.Runtime.IsReady(now))
                continue;

            // 资源判定
            if (def.CostResourceId is not null && def.CostAmount > 0)
            {
                if (!context.Resources.TryGet(def.CostResourceId, out var bucket))
                    continue;
                if (bucket.Current < def.CostAmount)
                    continue;

                if (def.SpendCostOnCast)
                {
                    var result = bucket.Add(-def.CostAmount);
                    if (result.AppliedDelta != 0)
                        context.SegmentCollector.OnResourceChange(def.CostResourceId, result.AppliedDelta);
                }
            }

            if (def.CastTimeSeconds > 0)
            {
                // 施法技能：开始施法，GCD/Cast 受 Haste 缩放
                StartCasting(slot, context, now);
                return true;
            }
            else
            {
                // 即时技能：GCD 受 Haste 缩放
                CastInstant(slot, context, now);
                return true;
            }
        }

        return false;
    }

    private void StartCasting(SkillSlot slot, BattleContext context, double now)
    {
        var def = slot.Runtime.Definition;

        // 读取当前 Haste 因子并缩放 GCD/Cast
        var haste = ResolveHasteFactor(context);
        var effCast = Math.Max(0.01, def.CastTimeSeconds / haste);
        var effGcd = def.OffGcd ? 0.0 : Math.Max(0.01, def.GcdSeconds / haste);

        if (!def.OffGcd)
            GlobalCooldownUntil = now + effGcd;

        IsCasting = true;
        _castingSlot = slot;
        CastingUntil = now + effCast;
        CastingSkillLocksAttack = def.LockAttackDuringCast;

        // 调度施法完成事件
        context.Scheduler.Schedule(new SkillCastCompleteEvent(CastingUntil, slot));
        // 可选：记录开始施法标签
        context.SegmentCollector.OnTag("skill_cast_start:" + def.Id, 1);
    }

    internal void ClearCasting()
    {
        IsCasting = false;
        CastingUntil = 0;
        CastingSkillLocksAttack = false;
        _castingSlot = null;
    }

    private void CastInstant(SkillSlot slot, BattleContext context, double now)
    {
        var def = slot.Runtime.Definition;

        // 即时技能的 GCD 受 Haste 缩放
        if (!def.OffGcd)
        {
            var haste = ResolveHasteFactor(context);
            var effGcd = Math.Max(0.01, def.GcdSeconds / haste);
            GlobalCooldownUntil = now + effGcd;
        }

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
                // 资源不足则取消施放
                return;
            }
        }

        // 伤害 + 暴击
        var (chance, mult) = context.Crit.ResolveWith(context.Buffs.Aggregate, def.CritChance, def.CritMultiplier);
        bool isCrit = context.Rng.NextBool(chance);
        int dmg = isCrit ? (int)Math.Round(def.BaseDamage * mult) : def.BaseDamage;
        if (isCrit) context.SegmentCollector.OnTag("crit:skill:" + def.Id, 1);

        var type = DamageType.Physical;
        if (def is SkillDefinitionExt ext) type = ext.DamageType;

        DamageCalculator.ApplyDamage(context, "skill:" + def.Id, dmg, type);
        context.SegmentCollector.OnTag("skill_cast:" + def.Id, 1);

        // 职业钩子
        context.ProfessionModule.OnSkillCast(context, def);

        // 进入冷却
        slot.Runtime.MarkCast(now);
    }

    private static double ResolveHasteFactor(BattleContext context)
    {
        // 复用 BuffAggregate 的 Haste 聚合，1.0 为无加速
        // ApplyToBaseHaste 已含下限保护（>=0.1）
        return context.Buffs.Aggregate.ApplyToBaseHaste(1.0);
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