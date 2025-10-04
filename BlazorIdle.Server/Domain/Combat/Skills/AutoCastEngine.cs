using BlazorIdle.Server.Domain.Combat.Damage;
using BlazorIdle.Server.Domain.Combat.Enemies;
using BlazorIdle.Server.Domain.Combat.Procs;
using BlazorIdle.Server.Domain.Combat.Resources;
using System;
using System.Collections.Generic;

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

    // 单槽队列
    private SkillSlot? _queuedSlot;

    // 施法跟踪
    private SkillSlot? _castingSlot;
    private long _castSeq = 0;
    private long? _currentCastId;
    private bool _consumedChargeForCurrentCast;

    public void AddSkill(SkillDefinition def)
    {
        _slots.Add(new SkillSlot(def));
        _slots.Sort((a, b) => a.Runtime.Definition.Priority.CompareTo(b.Runtime.Definition.Priority));
    }

    public bool TryAutoCast(BattleContext context, double now)
    {
        // 推进充能
        foreach (var s in _slots)
            s.Runtime.TickRecharge(now);

        // 先尝试消费队列（若可）
        if (TryConsumeQueued(context, now))
            return true;

        // 施法期间：允许“OffGCD 且允许编织且瞬发”的技能穿插释放
        if (IsCasting)
        {
            var hasteDuring = ResolveHasteFactor(context); // 用于充能/冷却的时长缩放
            foreach (var slot in _slots)
            {
                var def = slot.Runtime.Definition;

                // 仅允许：OffGCD + 允许编织 + 瞬发（CastTime=0）
                if (!(def.OffGcd && def.AllowDuringCastingForOffGcd && def.CastTimeSeconds <= 0))
                    continue;

                if (!slot.Runtime.IsReady(now))
                    continue;

                if (!HasSufficientResourceForStart(context, def))
                    continue;

                // 直接使用瞬发路径；不改变施法状态，不影响“暂停普攻”规则
                CastInstant(slot, context, now, hasteDuring, deductCostNow: def.SpendCostOnCast);
                return true;
            }

            // 未能编织，记录队列候选（含 OffGCD）以便施法完成后抢占
            ConsiderQueueCandidates(context, now, castingBlocked: true);
            return false;
        }

        var haste = ResolveHasteFactor(context);

        // 常规扫描：可立即施放就施放；被 GCD 阻塞则入队
        foreach (var slot in _slots)
        {
            var def = slot.Runtime.Definition;

            if (!slot.Runtime.IsReady(now))
                continue;

            if (!HasSufficientResourceForStart(context, def))
                continue;

            bool gcdBlocked = (!def.OffGcd && now < GlobalCooldownUntil);
            if (gcdBlocked)
            {
                ConsiderQueue(slot, context);
                continue;
            }

            if (def.CastTimeSeconds > 0)
            {
                StartCasting(slot, context, now, haste, deductCostNow: def.SpendCostOnCast);
                return true;
            }
            else
            {
                CastInstant(slot, context, now, haste, deductCostNow: def.SpendCostOnCast);
                return true;
            }
        }

        return false;
    }

    private void StartCasting(SkillSlot slot, BattleContext context, double now, double haste, bool deductCostNow)
    {
        var def = slot.Runtime.Definition;

        var effCast = Math.Max(0.01, def.CastTimeSeconds / haste);
        var effGcd = def.OffGcd ? 0.0 : Math.Max(0.01, def.GcdSeconds / haste);

        if (!def.OffGcd)
        {
            GlobalCooldownUntil = now + effGcd;
            context.Scheduler.Schedule(new GcdReadyEvent(GlobalCooldownUntil));
        }

        if (deductCostNow && def.CostResourceId is not null && def.CostAmount > 0)
        {
            if (!TryDeductResource(context, def.CostResourceId, def.CostAmount))
                return;
        }

        _consumedChargeForCurrentCast = false;
        if (def.MaxCharges > 1 && def.ConsumeChargeOnCast)
        {
            var effRecharge = def.RechargeAffectedByHaste ? Math.Max(0.01, def.RechargeSeconds / haste) : def.RechargeSeconds;
            slot.Runtime.ConsumeAtStart(now, effRecharge);
            _consumedChargeForCurrentCast = true;
        }

        IsCasting = true;
        _castingSlot = slot;
        CastingUntil = now + effCast;
        CastingSkillLocksAttack = def.LockAttackDuringCast;
        _currentCastId = ++_castSeq;

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

    private void CastInstant(SkillSlot slot, BattleContext context, double now, double haste, bool deductCostNow)
    {
        var def = slot.Runtime.Definition;

        // OffGCD 不进入 GCD；OnGCD 则按 Haste 缩放 GCD
        if (!def.OffGcd)
        {
            var effGcd = Math.Max(0.01, def.GcdSeconds / haste);
            GlobalCooldownUntil = now + effGcd;
            context.Scheduler.Schedule(new GcdReadyEvent(GlobalCooldownUntil));
        }

        if (deductCostNow && def.CostResourceId is not null && def.CostAmount > 0)
        {
            if (!TryDeductResource(context, def.CostResourceId, def.CostAmount))
                return;
        }

        // 充能/冷却
        if (def.MaxCharges > 1)
        {
            var effRecharge = def.RechargeAffectedByHaste ? Math.Max(0.01, def.RechargeSeconds / haste) : def.RechargeSeconds;
            slot.Runtime.ConsumeAtStart(now, effRecharge);
        }
        else
        {
            slot.Runtime.MarkCast(now);
        }

        // 伤害 + AoE
        DoSkillDamage(slot, context, def, now);

        if (_queuedSlot == slot) _queuedSlot = null;
    }

    private void DoSkillDamage(SkillSlot slot, BattleContext context, SkillDefinition def,double now)
    {
        var (chance, mult) = context.Crit.ResolveWith(context.Buffs.Aggregate, def.CritChance, def.CritMultiplier);
        bool isCrit = context.Rng.NextBool(chance);
        int baseDmg = isCrit ? (int)Math.Round(def.BaseDamage * mult) : def.BaseDamage;
        if (isCrit) context.SegmentCollector.OnTag("crit:skill:" + def.Id, 1);

        var type = DamageType.Physical;
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

        // Proc：OnHit/OnCrit（非 DoT），来源为技能
        context.Procs.OnDirectHit(context, "skill:" + def.Id, type, isCrit, isDot: false, DirectSourceKind.Skill, now);
    }

    private static double ResolveHasteFactor(BattleContext context)
        => context.Buffs.Aggregate.ApplyToBaseHaste(1.0);

    private void ConsiderQueueCandidates(BattleContext context, double now, bool castingBlocked)
    {
        foreach (var slot in _slots)
        {
            var def = slot.Runtime.Definition;

            if (!slot.Runtime.IsReady(now))
                continue;

            if (!HasSufficientResourceForStart(context, def))
                continue;

            if (castingBlocked)
            {
                ConsiderQueue(slot, context);
                continue;
            }

            bool gcdBlocked = (!def.OffGcd && now < GlobalCooldownUntil);
            if (gcdBlocked)
            {
                ConsiderQueue(slot, context);
            }
        }
    }

    private void ConsiderQueue(SkillSlot slot, BattleContext context)
    {
        if (_queuedSlot == null ||
            slot.Runtime.Definition.Priority < _queuedSlot.Runtime.Definition.Priority)
        {
            _queuedSlot = slot;
            context.SegmentCollector.OnTag("skill_queued:" + slot.Runtime.Definition.Id, 1);
        }
    }

    private bool TryConsumeQueued(BattleContext context, double now)
    {
        if (_queuedSlot == null || IsCasting)
            return false;

        var slot = _queuedSlot;
        var def = slot.Runtime.Definition;

        if (!slot.Runtime.IsReady(now))
        {
            _queuedSlot = null;
            return false;
        }

        bool gcdBlocked = (!def.OffGcd && now < GlobalCooldownUntil);
        if (gcdBlocked)
            return false;

        if (!HasSufficientResourceForStart(context, def))
        {
            _queuedSlot = null;
            return false;
        }

        var haste = ResolveHasteFactor(context);

        if (def.CastTimeSeconds > 0)
        {
            StartCasting(slot, context, now, haste, deductCostNow: def.SpendCostOnCast);
            _queuedSlot = null;
            return true;
        }
        else
        {
            CastInstant(slot, context, now, haste, deductCostNow: def.SpendCostOnCast);
            _queuedSlot = null;
            return true;
        }
    }

    public bool RequestInterrupt(BattleContext context, double atTime, InterruptReason reason = InterruptReason.Other)
    {
        if (!IsCasting || _currentCastId is null) return false;
        context.Scheduler.Schedule(new SkillCastInterruptEvent(atTime, _currentCastId.Value, reason));
        return true;
    }

    internal void InterruptCasting(BattleContext context, double now, InterruptReason reason)
    {
        if (!IsCasting || _castingSlot is null || _currentCastId is null)
            return;

        var def = _castingSlot.Runtime.Definition;

        if (!def.Interruptible)
            return;

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

        if (_consumedChargeForCurrentCast && def.MaxCharges > 1 && def.RefundChargeOnInterrupt)
        {
            var haste = ResolveHasteFactor(context);
            var effRecharge = def.RechargeAffectedByHaste ? Math.Max(0.01, def.RechargeSeconds / haste) : def.RechargeSeconds;
            _castingSlot.Runtime.RefundOnInterrupt(now, effRecharge);
        }

        context.SegmentCollector.OnTag("skill_cast_interrupt:" + def.Id, 1);
        context.SegmentCollector.OnTag("interrupt_reason:" + reason.ToString().ToLowerInvariant(), 1);

        ClearCasting();
    }

    private static bool HasSufficientResourceForStart(BattleContext context, SkillDefinition def)
    {
        if (def.CostResourceId is null || def.CostAmount <= 0) return true;
        if (!context.Resources.TryGet(def.CostResourceId, out var bucket)) return false;
        return bucket.Current >= def.CostAmount;
    }

    private static bool TryDeductResource(BattleContext context, string resourceId, int amount)
    {
        if (!context.Resources.TryGet(resourceId, out var bucket)) return false;
        if (bucket.Current < amount) return false;
        var result = bucket.Add(-amount);
        if (result.AppliedDelta != 0)
            context.SegmentCollector.OnResourceChange(resourceId, result.AppliedDelta);
        return true;
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