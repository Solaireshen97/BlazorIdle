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
    public long? CurrentCastId => _currentCastId;

    private SkillSlot? _castingSlot;
    private long _castSeq = 0;
    private long? _currentCastId;
    private bool _consumedChargeForCurrentCast; // 本次施法是否在开始时消耗了充能

    public void AddSkill(SkillDefinition def)
    {
        _slots.Add(new SkillSlot(def));
        _slots.Sort((a, b) => a.Runtime.Definition.Priority.CompareTo(b.Runtime.Definition.Priority));
    }

    public bool TryAutoCast(BattleContext context, double now)
    {
        // 先推进所有技能的充能恢复
        var haste = ResolveHasteFactor(context);
        foreach (var s in _slots)
        {
            var def = s.Runtime.Definition;
            if (def.MaxCharges > 1)
            {
                // 按配置决定是否受 Haste 影响；受影响时，effRecharge 使用开始恢复时的快照（在 runtime 内部保存）
                s.Runtime.TickRecharge(now);
            }
        }

        // 施法进行中：首版不支持编织
        if (IsCasting) return false;

        foreach (var slot in _slots)
        {
            var def = slot.Runtime.Definition;

            // GCD 限制（OffGcd 技能可绕过）
            if (!def.OffGcd && now < GlobalCooldownUntil)
                continue;

            // 冷却/充能判断
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
                // 施法技能：开始施法（GCD/Cast 受 Haste 缩放）
                StartCasting(slot, context, now, haste);
                return true;
            }
            else
            {
                // 即时技能
                CastInstant(slot, context, now, haste);
                return true;
            }
        }

        return false;
    }

    private void StartCasting(SkillSlot slot, BattleContext context, double now, double haste)
    {
        var def = slot.Runtime.Definition;

        var effCast = Math.Max(0.01, def.CastTimeSeconds / haste);
        var effGcd = def.OffGcd ? 0.0 : Math.Max(0.01, def.GcdSeconds / haste);

        if (!def.OffGcd)
            GlobalCooldownUntil = now + effGcd;

        // 充能：如配置为开始时消耗
        _consumedChargeForCurrentCast = false;
        if (def.MaxCharges > 1 && def.ConsumeChargeOnCast)
        {
            var effRecharge = def.RechargeAffectedByHaste ? Math.Max(0.01, def.RechargeSeconds / haste) : def.RechargeSeconds;
            slot.Runtime.ConsumeAtStart(now, effRecharge);
            _consumedChargeForCurrentCast = true;
        }
        else if (def.MaxCharges <= 1)
        {
            // 单充能：开始施法时不进入冷却，完成时进入（保持与现有完成时 Mark 一致）
        }

        IsCasting = true;
        _castingSlot = slot;
        CastingUntil = now + effCast;
        CastingSkillLocksAttack = def.LockAttackDuringCast;

        // 分配 CastId（用于安全打断）
        _currentCastId = ++_castSeq;

        // 调度施法完成事件
        context.Scheduler.Schedule(new SkillCastCompleteEvent(CastingUntil, slot, _currentCastId.Value));
        context.SegmentCollector.OnTag("skill_cast_start:" + def.Id, 1);
    }

    internal void ClearCasting()
    {
        IsCasting = false;
        CastingUntil = 0;
        CastingSkillLocksAttack = false;
        _castingSlot = null;
        _currentCastId = null;
        _consumedChargeForCurrentCast = false;
    }

    private void CastInstant(SkillSlot slot, BattleContext context, double now, double haste)
    {
        var def = slot.Runtime.Definition;

        // 即时技能的 GCD 受 Haste 缩放
        if (!def.OffGcd)
        {
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
                return;
            }
        }

        // 充能/冷却处理
        if (def.MaxCharges > 1)
        {
            var effRecharge = def.RechargeAffectedByHaste ? Math.Max(0.01, def.RechargeSeconds / haste) : def.RechargeSeconds;
            // 即时技能：无完成时机，直接消耗
            slot.Runtime.ConsumeAtStart(now, effRecharge);
        }
        else
        {
            // 单充能：即时释放后立即进入冷却
            slot.Runtime.MarkCast(now);
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

        context.ProfessionModule.OnSkillCast(context, def);
    }

    private static double ResolveHasteFactor(BattleContext context)
    {
        return context.Buffs.Aggregate.ApplyToBaseHaste(1.0);
    }

    // 对外：请求在某时刻发起“打断当前施法”的事件（立即或未来）
    public bool RequestInterrupt(BattleContext context, double atTime, InterruptReason reason = InterruptReason.Other)
    {
        if (!IsCasting || _currentCastId is null) return false;
        context.Scheduler.Schedule(new SkillCastInterruptEvent(atTime, _currentCastId.Value, reason));
        return true;
    }

    // 事件执行：打断当前施法
    internal void InterruptCasting(BattleContext context, double now, InterruptReason reason)
    {
        if (!IsCasting || _castingSlot is null || _currentCastId is null)
            return;

        var def = _castingSlot.Runtime.Definition;

        // 不可打断则忽略
        if (!def.Interruptible)
            return;

        // 资源返还
        if (def.SpendCostOnCast && def.RefundCostOnInterrupt && def.CostResourceId is not null && def.CostAmount > 0)
        {
            if (context.Resources.TryGet(def.CostResourceId, out var bucket))
            {
                var refund = (int)Math.Round(def.CostAmount * Math.Clamp(def.RefundRatioOnInterrupt, 0, 1));
                if (refund > 0)
                {
                    var result = bucket.Add(refund);
                    if (result.AppliedDelta != 0)
                        context.SegmentCollector.OnResourceChange(def.CostResourceId, result.AppliedDelta);
                }
            }
        }

        // 充能返还（仅当本次施法在开始时消耗过充能且允许返还）
        if (_consumedChargeForCurrentCast && def.MaxCharges > 1 && def.RefundChargeOnInterrupt)
        {
            var haste = ResolveHasteFactor(context);
            var effRecharge = def.RechargeAffectedByHaste ? Math.Max(0.01, def.RechargeSeconds / haste) : def.RechargeSeconds;
            _castingSlot.Runtime.RefundOnInterrupt(now, effRecharge);
        }

        // 标签
        context.SegmentCollector.OnTag("skill_cast_interrupt:" + def.Id, 1);
        context.SegmentCollector.OnTag("interrupt_reason:" + reason.ToString().ToLowerInvariant(), 1);

        // 清空施法状态
        ClearCasting();
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