using System;
using System.Collections.Generic;
using BlazorIdle.Server.Domain.Combat.Damage;
using BlazorIdle.Server.Domain.Combat.Enemies;

namespace BlazorIdle.Server.Domain.Combat;

public static class DamageCalculator
{
    // 护甲减伤公式参数（可后续配置化）
    private const double K = 50.0;
    private const double C = 400.0;

    // 兼容旧 API：按 context.Encounter 结算单体
    public static int ApplyDamage(BattleContext context, string sourceId, int baseDamage, DamageType type)
    {
        if (context.Encounter is null)
        {
            // 无目标：仅统计
            int dealtStat = Math.Max(0, (int)baseDamage);
            context.SegmentCollector.OnDamage(sourceId, dealtStat, type);
            return dealtStat;
        }

        return ApplyDamageToTarget(context, context.Encounter, sourceId, baseDamage, type);
    }

    // 新：对指定目标结算（用于 AoE 逐个目标）
    public static int ApplyDamageToTarget(BattleContext context, Encounter target, string sourceId, int baseDamage, DamageType type)
    {
        var agg = context.Buffs.Aggregate;
        int dealt = ComputeDealt(baseDamage, type, target.Enemy, agg);
        var applied = target.ApplyDamage(dealt, context.Clock.CurrentTime);
        context.SegmentCollector.OnDamage(sourceId, applied, type);
        return applied;
    }

    // 新：对多个目标结算（返回总实际伤害）
    public static int ApplyDamageToTargets(BattleContext context, IEnumerable<(Encounter target, int damage)> plan, string sourceId, DamageType type)
    {
        int total = 0;
        foreach (var (t, dmg) in plan)
            total += ApplyDamageToTarget(context, t, sourceId, dmg, type);
        return total;
    }

    private static int ComputeDealt(int baseDamage, DamageType type, EnemyDefinition enemy, Buffs.BuffAggregate agg)
    {
        double factor = 1.0;

        switch (type)
        {
            case DamageType.Physical:
                {
                    var armorEff = Math.Max(0, enemy.Armor - Math.Max(0, agg.ArmorPenFlat));
                    armorEff *= (1 - Clamp01(agg.ArmorPenPct));

                    var denom = armorEff + (K * enemy.Level + C);
                    var reduction = denom <= 0 ? 0 : armorEff / denom; // 0..1
                    factor *= Clamp01(1.0 - reduction);
                    factor *= 1.0 + enemy.VulnerabilityPhysical;
                    factor *= 1.0 + agg.DamageMultiplierPhysical;
                    break;
                }
            case DamageType.Magic:
                {
                    var mrEff = Math.Max(0.0, enemy.MagicResist - Math.Max(0, agg.MagicPenFlat));
                    mrEff *= (1 - Clamp01(agg.MagicPenPct));
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

    private static double Clamp01(double v) => v < 0 ? 0 : (v > 1 ? 1 : v);
}