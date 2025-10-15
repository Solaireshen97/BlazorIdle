using BlazorIdle.Server.Domain.Combat.Damage;
using BlazorIdle.Server.Domain.Combat.Enemies;
using BlazorIdle.Server.Domain.Combat.Procs;
using BlazorIdle.Server.Domain.Combat.Resources;
using System;
using System.Collections.Generic;

namespace BlazorIdle.Server.Domain.Combat.Skills;

/// <summary>
/// 技能自动施放引擎 - 管理技能槽位、冷却、施放条件和优先级
/// </summary>
/// <remarks>
/// <para><strong>设计理念</strong>：</para>
/// <list type="bullet">
/// <item>优先级系统：技能按Priority值排序，值越小优先级越高</item>
/// <item>资源检查：自动检查技能消耗的资源是否足够</item>
/// <item>GCD机制：普通技能触发全局冷却（GCD），脱离GCD的技能可以在任何时候使用</item>
/// <item>施法时间：支持瞬发和引导技能，引导期间可以使用特定的脱离GCD技能</item>
/// <item>队列系统：当技能准备好但GCD未结束时，可以加入队列等待GCD结束</item>
/// </list>
/// 
/// <para><strong>核心职责</strong>：</para>
/// <list type="number">
/// <item>管理技能槽位：添加、排序技能</item>
/// <item>自动施放：根据优先级、冷却、资源条件自动选择技能</item>
/// <item>施法管理：处理引导技能的开始和完成</item>
/// <item>GCD控制：管理全局冷却时间</item>
/// <item>队列机制：管理等待施放的技能</item>
/// </list>
/// 
/// <para><strong>技能施放流程</strong>：</para>
/// <list type="number">
/// <item>更新所有技能的冷却状态</item>
/// <item>检查队列中的技能是否可以施放</item>
/// <item>如果正在施法，检查是否可以使用脱离GCD的瞬发技能</item>
/// <item>按优先级遍历技能槽位，找到第一个满足条件的技能</item>
/// <item>检查冷却、资源、GCD等条件</item>
/// <item>根据技能类型（瞬发/引导）执行相应的施放逻辑</item>
/// </list>
/// 
/// <para><strong>关键特性</strong>：</para>
/// <list type="bullet">
/// <item>急速影响：施法时间和GCD受到急速属性影响</item>
/// <item>成本选项：技能可以在施法开始或结束时消耗资源</item>
/// <item>充能系统：支持技能充能机制（如3层充能）</item>
/// <item>触发机制：技能执行可以触发装备和Buff的Proc效果</item>
/// </list>
/// </remarks>
public class AutoCastEngine
{
    private readonly List<SkillSlot> _slots = new();

    /// <summary>技能槽位列表（按优先级排序）</summary>
    public IReadOnlyList<SkillSlot> Slots => _slots;

    /// <summary>全局冷却结束时间（游戏时间，单位：秒）</summary>
    public double GlobalCooldownUntil { get; private set; } = 0;
    
    /// <summary>是否正在施法（引导技能）</summary>
    public bool IsCasting { get; private set; }
    
    /// <summary>施法结束时间（游戏时间，单位：秒）</summary>
    public double CastingUntil { get; private set; }
    
    /// <summary>当前施法的技能是否锁定普通攻击</summary>
    public bool CastingSkillLocksAttack { get; private set; }
    
    /// <summary>当前施法的唯一ID（用于追踪施法过程）</summary>
    public long? CurrentCastId => _currentCastId;

    /// <summary>队列中等待施放的技能槽位</summary>
    private SkillSlot? _queuedSlot;

    /// <summary>当前正在施法的技能槽位</summary>
    private SkillSlot? _castingSlot;
    
    /// <summary>施法序列号（用于生成唯一的施法ID）</summary>
    private long _castSeq = 0;
    
    /// <summary>当前施法的ID</summary>
    private long? _currentCastId;
    
    /// <summary>是否已为当前施法消耗了充能</summary>
    private bool _consumedChargeForCurrentCast;

    /// <summary>
    /// 添加技能到槽位
    /// </summary>
    /// <param name="def">技能定义</param>
    /// <remarks>
    /// 添加后会自动按优先级排序，优先级值越小越先施放。
    /// 例如：Priority=1的技能会在Priority=2的技能之前检查和施放。
    /// </remarks>
    public void AddSkill(SkillDefinition def)
    {
        _slots.Add(new SkillSlot(def));
        _slots.Sort((a, b) => a.Runtime.Definition.Priority.CompareTo(b.Runtime.Definition.Priority));
    }

    /// <summary>
    /// 尝试自动施放技能
    /// </summary>
    /// <param name="context">战斗上下文</param>
    /// <param name="now">当前游戏时间（秒）</param>
    /// <returns>如果成功施放技能返回true，否则返回false</returns>
    /// <remarks>
    /// <para><strong>执行流程</strong>：</para>
    /// <list type="number">
    /// <item>更新所有技能的冷却状态（TickRecharge）</item>
    /// <item>检查队列中的技能是否可以施放</item>
    /// <item>如果正在施法，检查是否可以使用脱离GCD的瞬发技能</item>
    /// <item>按优先级遍历技能槽位，找到第一个可以施放的技能</item>
    /// </list>
    /// 
    /// <para><strong>施放条件</strong>：</para>
    /// <list type="bullet">
    /// <item>技能冷却已完成</item>
    /// <item>有足够的资源（法力、怒气等）</item>
    /// <item>GCD已结束（或技能脱离GCD）</item>
    /// <item>不在施法状态（或技能允许在施法期间使用）</item>
    /// </list>
    /// </remarks>
    public bool TryAutoCast(BattleContext context, double now)
    {
        foreach (var s in _slots)
            s.Runtime.TickRecharge(now);

        if (TryConsumeQueued(context, now))
            return true;

        if (IsCasting)
        {
            var hasteDuring = ResolveHasteFactor(context);
            foreach (var slot in _slots)
            {
                var def = slot.Runtime.Definition;

                if (!(def.OffGcd && def.AllowDuringCastingForOffGcd && def.CastTimeSeconds <= 0))
                    continue;

                if (!slot.Runtime.IsReady(now))
                    continue;

                if (!HasSufficientResourceForStart(context, def))
                    continue;

                CastInstant(slot, context, now, hasteDuring, deductCostNow: def.SpendCostOnCast);
                return true;
            }

            ConsiderQueueCandidates(context, now, castingBlocked: true);
            return false;
        }

        var haste = ResolveHasteFactor(context);

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

        if (def.MaxCharges > 1)
        {
            var effRecharge = def.RechargeAffectedByHaste ? Math.Max(0.01, def.RechargeSeconds / haste) : def.RechargeSeconds;
            slot.Runtime.ConsumeAtStart(now, effRecharge);
        }
        else
        {
            slot.Runtime.MarkCast(now);
        }

        DoSkillDamage(slot, context, def, now);

        if (_queuedSlot == slot) _queuedSlot = null;
    }

    private void DoSkillDamage(SkillSlot slot, BattleContext context, SkillDefinition def, double now)
    {
        // AP/SP 注入
        double preCrit = def.BaseDamage
            + context.Stats.AttackPower * def.AttackPowerCoef
            + context.Stats.SpellPower * def.SpellPowerCoef;

        // 暴击（用技能覆盖或用面板基础）
        var (chance, mult) = context.Crit.ResolveWith(
            context.Buffs.Aggregate,
            def.CritChance ?? context.Stats.CritChance,
            def.CritMultiplier ?? context.Stats.CritMultiplier
        );
        bool isCrit = context.Rng.NextBool(chance);
        int baseDmg = isCrit ? (int)Math.Round(preCrit * mult) : (int)Math.Round(preCrit);
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
            // Phase 2: 单体技能使用 TargetSelector 随机选择目标
            if (context.EncounterGroup != null)
            {
                var candidates = context.EncounterGroup.All
                    .Select((enc, idx) => new Combatants.EnemyCombatant($"enemy_{idx}", enc))
                    .ToList<Combatants.ICombatant>();
                
                var target = context.TargetSelector.SelectTarget(candidates);
                if (target is Combatants.EnemyCombatant enemyTarget)
                {
                    DamageCalculator.ApplyDamageToTarget(context, enemyTarget.Encounter, "skill:" + def.Id, baseDmg, type);
                }
                else
                {
                    // 无可用目标，跳过
                }
            }
            else
            {
                // 向后兼容：使用旧的 ApplyDamage 方法
                DamageCalculator.ApplyDamage(context, "skill:" + def.Id, baseDmg, type);
            }
        }

        context.SegmentCollector.OnTag("skill_cast:" + def.Id, 1);
        context.ProfessionModule.OnSkillCast(context, def);

        // Proc 来源为技能命中
        context.Procs.OnDirectHit(context, "skill:" + def.Id, type, isCrit, isDot: false, DirectSourceKind.Skill, now);
    }

    private static double ResolveHasteFactor(BattleContext context)
        => context.Buffs.Aggregate.ApplyToBaseHaste(1.0 + context.Stats.HastePercent);

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
        double? critChance = null, double? critMultiplier = null,
        double apCoef = 0.0, double spCoef = 0.0)
        : base(id, name, costResourceId, costAmount, cooldownSeconds, priority, baseDamage, critChance, critMultiplier, apCoef: apCoef, spCoef: spCoef)
    {
        DamageType = damageType;
    }
}