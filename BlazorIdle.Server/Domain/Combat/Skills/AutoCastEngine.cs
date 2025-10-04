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

    // 技能队列（仅 1 格，保存优先级最高候选）
    private SkillSlot? _queuedSlot;

    // 内部施法跟踪
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
        foreach (var s in _slots)
            s.Runtime.TickRecharge(now);

        // 若有排队技能，先尝试消费（可避免空转）
        if (TryConsumeQueued(context, now))
            return true;

        // 施法进行中：首版不支持编织，但仍可评估并记录队列候选，随后返回
        if (IsCasting)
        {
            ConsiderQueueCandidates(context, now, castingBlocked: true);
            return false;
        }

        var haste = ResolveHasteFactor(context);

        // 扫描技能：可立即施放的直接施放；被 GCD 阻塞但其他条件满足的，进入队列
        foreach (var slot in _slots)
        {
            var def = slot.Runtime.Definition;

            // 可用性（冷却/充能）
            if (!slot.Runtime.IsReady(now))
                continue;

            // 资源检查（不扣除，仅校验；扣除在开始施法或瞬发时机）
            if (!HasSufficientResourceForStart(context, def))
                continue;

            bool gcdBlocked = (!def.OffGcd && now < GlobalCooldownUntil);
            if (gcdBlocked)
            {
                ConsiderQueue(slot, context);
                continue;
            }

            // 可立即施放
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

        // 没有可施放技能；若因为 GCD 或施法阻塞，队列里可能已有候选
        return false;
    }

    // 施法开始
    private void StartCasting(SkillSlot slot, BattleContext context, double now, double haste, bool deductCostNow)
    {
        var def = slot.Runtime.Definition;

        // GCD/Cast 受 Haste 缩放
        var effCast = Math.Max(0.01, def.CastTimeSeconds / haste);
        var effGcd = def.OffGcd ? 0.0 : Math.Max(0.01, def.GcdSeconds / haste);

        if (!def.OffGcd)
        {
            GlobalCooldownUntil = now + effGcd;
            // 调度 GCD 结束事件以及时尝试释放队列
            context.Scheduler.Schedule(new GcdReadyEvent(GlobalCooldownUntil));
        }

        // 资源扣除（如果配置在开始扣）
        if (deductCostNow && def.CostResourceId is not null && def.CostAmount > 0)
        {
            if (!TryDeductResource(context, def.CostResourceId, def.CostAmount))
            {
                // 扣除失败则取消施法
                return;
            }
        }

        // 充能：如配置为开始时消耗
        _consumedChargeForCurrentCast = false;
        if (def.MaxCharges > 1 && def.ConsumeChargeOnCast)
        {
            var effRecharge = def.RechargeAffectedByHaste ? Math.Max(0.01, def.RechargeSeconds / haste) : def.RechargeSeconds;
            slot.Runtime.ConsumeAtStart(now, effRecharge);
            _consumedChargeForCurrentCast = true;
        }

        // 设置施法状态
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

    // 瞬发施放
    private void CastInstant(SkillSlot slot, BattleContext context, double now, double haste, bool deductCostNow)
    {
        var def = slot.Runtime.Definition;

        // GCD 缩放
        if (!def.OffGcd)
        {
            var effGcd = Math.Max(0.01, def.GcdSeconds / haste);
            GlobalCooldownUntil = now + effGcd;
            // 调度 GCD 结束事件
            context.Scheduler.Schedule(new GcdReadyEvent(GlobalCooldownUntil));
        }

        // 资源扣除（如果配置在开始扣）
        if (deductCostNow && def.CostResourceId is not null && def.CostAmount > 0)
        {
            if (!TryDeductResource(context, def.CostResourceId, def.CostAmount))
                return;
        }

        // 充能/冷却处理
        if (def.MaxCharges > 1)
        {
            var effRecharge = def.RechargeAffectedByHaste ? Math.Max(0.01, def.RechargeSeconds / haste) : def.RechargeSeconds;
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

        // 若本次施放命中了队列项，则清空队列
        if (_queuedSlot == slot) _queuedSlot = null;
    }

    private static double ResolveHasteFactor(BattleContext context)
    {
        return context.Buffs.Aggregate.ApplyToBaseHaste(1.0);
    }

    // 队列：在 GCD 或施法阻塞时，收集优先级最高的候选
    private void ConsiderQueueCandidates(BattleContext context, double now, bool castingBlocked)
    {
        foreach (var slot in _slots)
        {
            var def = slot.Runtime.Definition;

            if (!slot.Runtime.IsReady(now))
                continue;

            if (!HasSufficientResourceForStart(context, def))
                continue;

            // 施法阻塞时，任何技能都不能立即施放（含 OffGcd），可进入队列
            if (castingBlocked)
            {
                ConsiderQueue(slot, context);
                continue;
            }

            // 非施法阻塞但 GCD 阻塞的情况
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

    // 在合适时机尝试释放队列（GCD 结束、施法完成、任意事件触发时机）
    private bool TryConsumeQueued(BattleContext context, double now)
    {
        if (_queuedSlot == null || IsCasting)
            return false;

        var slot = _queuedSlot;
        var def = slot.Runtime.Definition;

        // 再次校验 GCD、冷却/充能、资源
        if (!slot.Runtime.IsReady(now))
        {
            _queuedSlot = null; // 队列项已失效
            return false;
        }

        bool gcdBlocked = (!def.OffGcd && now < GlobalCooldownUntil);
        if (gcdBlocked)
            return false;

        if (!HasSufficientResourceForStart(context, def))
        {
            _queuedSlot = null; // 资源不足，清队列
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

    // 尝试在某时刻发起“打断当前施法”的事件（保持既有接口）
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