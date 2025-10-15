using System;
using System.Collections.Generic;
using BlazorIdle.Server.Domain.Combat.Damage;
using BlazorIdle.Server.Domain.Combat.Enemies;

namespace BlazorIdle.Server.Domain.Combat;

/// <summary>
/// 伤害计算器 - 战斗系统核心伤害计算引擎
/// </summary>
/// <remarks>
/// <para><strong>设计理念</strong>：</para>
/// <list type="bullet">
/// <item>三种伤害类型：物理、魔法、真实伤害，各有独立的减伤公式</item>
/// <item>穿透系统：支持固定值穿透和百分比穿透，分别来自属性和Buff</item>
/// <item>易伤机制：敌人可以有易伤属性，增加受到的伤害</item>
/// <item>伤害增幅：Buff可以提供伤害加成</item>
/// </list>
/// 
/// <para><strong>核心职责</strong>：</para>
/// <list type="number">
/// <item>计算最终伤害：基础伤害 → 穿透计算 → 减伤公式 → 易伤和增幅 → 最终伤害</item>
/// <item>应用伤害到目标：更新目标血量并记录到战斗段</item>
/// <item>发送战斗通知：通过SignalR实时推送伤害事件（可选）</item>
/// <item>支持多目标：群体技能可同时对多个目标造成伤害</item>
/// </list>
/// 
/// <para><strong>物理伤害公式</strong>：</para>
/// <code>
/// 有效护甲 = max(0, (护甲 - 固定穿透) × (1 - 百分比穿透))
/// 减伤系数 = 有效护甲 / (有效护甲 + K × 敌人等级 + C)
/// 最终伤害 = 基础伤害 × (1 - 减伤系数) × (1 + 易伤) × (1 + 伤害增幅)
/// 
/// 其中 K=50.0, C=400.0 是平衡常量（可配置化）
/// </code>
/// 
/// <para><strong>魔法伤害公式</strong>：</para>
/// <code>
/// 有效魔抗 = clamp(max(0, (魔抗 - 固定穿透) × (1 - 百分比穿透)), 0, 1)
/// 最终伤害 = 基础伤害 × (1 - 有效魔抗) × (1 + 易伤) × (1 + 伤害增幅)
/// </code>
/// 
/// <para><strong>真实伤害</strong>：</para>
/// <code>
/// 最终伤害 = 基础伤害 × (1 + 易伤) × (1 + 伤害增幅)
/// 不受护甲和魔抗影响
/// </code>
/// </remarks>
public static class DamageCalculator
{
    /// <summary>伤害减免系数K - 用于物理伤害计算公式（将来可配置化）</summary>
    private const double K = 50.0;
    
    /// <summary>伤害减免常量C - 用于物理伤害计算公式（将来可配置化）</summary>
    private const double C = 400.0;

    /// <summary>
    /// 应用伤害到当前目标
    /// </summary>
    /// <param name="context">战斗上下文</param>
    /// <param name="sourceId">伤害来源ID（技能ID或"attack"）</param>
    /// <param name="baseDamage">基础伤害值</param>
    /// <param name="type">伤害类型（物理/魔法/真实）</param>
    /// <returns>实际造成的伤害值</returns>
    /// <remarks>
    /// 如果当前没有目标（context.Encounter为null），则直接记录伤害不计算减伤。
    /// 否则调用 ApplyDamageToTarget 对当前目标造成伤害。
    /// </remarks>
    public static int ApplyDamage(BattleContext context, string sourceId, int baseDamage, DamageType type)
    {
        if (context.Encounter is null)
        {
            int dealtStat = Math.Max(0, (int)baseDamage);
            context.SegmentCollector.OnDamage(sourceId, dealtStat, type);
            return dealtStat;
        }

        return ApplyDamageToTarget(context, context.Encounter, sourceId, baseDamage, type);
    }

    /// <summary>
    /// 应用伤害到指定目标
    /// </summary>
    /// <param name="context">战斗上下文</param>
    /// <param name="target">目标敌人</param>
    /// <param name="sourceId">伤害来源ID</param>
    /// <param name="baseDamage">基础伤害值</param>
    /// <param name="type">伤害类型</param>
    /// <param name="isCrit">是否暴击（用于战斗日志显示）</param>
    /// <returns>实际造成的伤害值（可能因目标剩余血量而减少）</returns>
    /// <remarks>
    /// <para><strong>计算流程</strong>：</para>
    /// <list type="number">
    /// <item>通过 ComputeDealt 计算考虑减伤、穿透、易伤后的伤害</item>
    /// <item>应用伤害到目标，更新目标血量</item>
    /// <item>记录伤害到战斗段（用于统计和回放）</item>
    /// <item>如果启用了SignalR通知，发送伤害事件到前端</item>
    /// </list>
    /// 
    /// <para><strong>SignalR通知</strong>：</para>
    /// 通知包含攻击者名称、目标名称、伤害值、暴击标记、目标当前/最大血量等信息，
    /// 用于前端实时显示战斗日志和血条更新。
    /// </remarks>
    public static int ApplyDamageToTarget(BattleContext context, Encounter target, string sourceId, int baseDamage, DamageType type, bool isCrit = false)
    {
        var agg = context.Buffs.Aggregate;
        int dealt = ComputeDealt(baseDamage, type, target.Enemy, agg, context);
        var applied = target.ApplyDamage(dealt, context.Clock.CurrentTime);
        context.SegmentCollector.OnDamage(sourceId, applied, type);
        
        // 发送伤害应用事件（用于显示战斗日志）
        if (context.NotificationService?.IsAvailable == true && 
            context.MessageFormatter?.IsDamageDealtEnabled == true)
        {
            var attackerName = context.MessageFormatter.GetPlayerName();
            var targetName = target.Enemy.Name;
            var message = context.MessageFormatter.FormatDamageDealt(attackerName, targetName, applied, isCrit);
            
            var damageEvent = new BlazorIdle.Shared.Models.DamageAppliedEventDto
            {
                BattleId = context.Battle.Id,
                EventTime = context.Clock.CurrentTime,
                EventType = "DamageApplied",
                Source = sourceId,
                Damage = applied,
                IsCrit = isCrit,
                TargetCurrentHp = target.CurrentHp,
                TargetMaxHp = target.Enemy.MaxHp,
                AttackerName = attackerName,
                TargetName = targetName,
                Message = message
            };
            _ = context.NotificationService.NotifyEventAsync(context.Battle.Id, damageEvent);
        }
        
        return applied;
    }

    /// <summary>
    /// 应用伤害到多个目标（用于群体技能）
    /// </summary>
    /// <param name="context">战斗上下文</param>
    /// <param name="plan">目标和伤害对列表（目标, 伤害值）</param>
    /// <param name="sourceId">伤害来源ID</param>
    /// <param name="type">伤害类型</param>
    /// <returns">总共造成的伤害值</returns>
    /// <remarks>
    /// 用于群体技能或范围效果，对每个目标分别计算伤害。
    /// 每个目标的减伤、易伤等属性独立计算。
    /// </remarks>
    public static int ApplyDamageToTargets(BattleContext context, IEnumerable<(Encounter target, int damage)> plan, string sourceId, DamageType type)
    {
        int total = 0;
        foreach (var (t, dmg) in plan)
            total += ApplyDamageToTarget(context, t, sourceId, dmg, type);
        return total;
    }

    /// <summary>
    /// 计算最终伤害（考虑减伤、穿透、易伤、增幅）
    /// </summary>
    /// <param name="baseDamage">基础伤害值</param>
    /// <param name="type">伤害类型</param>
    /// <param name="enemy">敌人定义（包含护甲、魔抗、易伤等属性）</param>
    /// <param name="agg">Buff聚合（提供穿透、伤害增幅等加成）</param>
    /// <param name="ctx">战斗上下文（提供角色属性）</param>
    /// <returns>计算后的最终伤害值</returns>
    /// <remarks>
    /// <para><strong>物理伤害</strong>：</para>
    /// <list type="number">
    /// <item>合并固定穿透：armorPenFlat = Buff穿透 + 属性穿透</item>
    /// <item>合并百分比穿透：armorPenPct = Buff穿透% + 属性穿透%（限制0-1）</item>
    /// <item>计算有效护甲：(护甲 - 固定穿透) × (1 - 百分比穿透)</item>
    /// <item>计算减伤：有效护甲 / (有效护甲 + K×等级 + C)</item>
    /// <item>应用易伤和增幅：(1 - 减伤) × (1 + 易伤) × (1 + 伤害增幅)</item>
    /// </list>
    /// 
    /// <para><strong>魔法伤害</strong>：</para>
    /// <list type="number">
    /// <item>计算有效魔抗（类似物理，但魔抗直接作为0-1的减伤系数）</item>
    /// <item>应用减伤、易伤和增幅</item>
    /// </list>
    /// 
    /// <para><strong>真实伤害</strong>：</para>
    /// 只受易伤和伤害增幅影响，无视护甲和魔抗。
    /// </remarks>
    private static int ComputeDealt(int baseDamage, DamageType type, EnemyDefinition enemy, Buffs.BuffAggregate agg, BattleContext ctx)
    {
        double factor = 1.0;

        switch (type)
        {
            case DamageType.Physical:
                {
                    // 合并 Stats 与 Buff 穿透
                    var armorPenFlat = Math.Max(0.0, agg.ArmorPenFlat + ctx.Stats.ArmorPenFlat);
                    var armorPenPct = Clamp01(agg.ArmorPenPct + ctx.Stats.ArmorPenPct);

                    var armorEff = Math.Max(0.0, enemy.Armor - armorPenFlat);
                    armorEff *= (1 - armorPenPct);

                    var denom = armorEff + (K * enemy.Level + C);
                    var reduction = denom <= 0 ? 0 : armorEff / denom; // 0..1
                    factor *= Clamp01(1.0 - reduction);
                    factor *= 1.0 + enemy.VulnerabilityPhysical;
                    factor *= 1.0 + agg.DamageMultiplierPhysical;
                    break;
                }
            case DamageType.Magic:
                {
                    var mrPenFlat = Math.Max(0.0, agg.MagicPenFlat + ctx.Stats.MagicPenFlat);
                    var mrPenPct = Clamp01(agg.MagicPenPct + ctx.Stats.MagicPenPct);

                    var mrEff = Math.Max(0.0, enemy.MagicResist - mrPenFlat);
                    mrEff *= (1 - mrPenPct);
                    mrEff = Clamp01(mrEff);

                    factor *= 1.0 - mrEff;
                    factor *= 1.0 + enemy.VulnerabilityMagic;
                    factor *= 1.0 + agg.DamageMultiplierMagic;
                    break;
                }
            case DamageType.True:
                {
                    factor *= 1.0 + enemy.VulnerabilityTrue;
                    factor *= 1.0 + agg.DamageMultiplierTrue;
                    break;
                }
        }

        int dealt = Math.Max(0, (int)Math.Round(baseDamage * factor));
        return dealt;
    }

    /// <summary>
    /// 将数值限制在 0-1 范围内
    /// </summary>
    /// <param name="v">输入值</param>
    /// <returns>限制后的值（0 ≤ 返回值 ≤ 1）</returns>
    private static double Clamp01(double v) => v < 0 ? 0 : (v > 1 ? 1 : v);
}