using System;
using System.Collections.Generic;
using BlazorIdle.Server.Domain.Combat.Damage;
using BlazorIdle.Server.Domain.Combat.Enemies;

namespace BlazorIdle.Server.Domain.Combat;

public static class DamageCalculator
{
    private const double K = 50.0;
    private const double C = 400.0;

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

    public static int ApplyDamageToTarget(BattleContext context, Encounter target, string sourceId, int baseDamage, DamageType type)
    {
        var agg = context.Buffs.Aggregate;
        int dealt = ComputeDealt(baseDamage, type, target.Enemy, agg, context);
        var applied = target.ApplyDamage(dealt, context.Clock.CurrentTime);
        context.SegmentCollector.OnDamage(sourceId, applied, type);
        return applied;
    }

    public static int ApplyDamageToTargets(BattleContext context, IEnumerable<(Encounter target, int damage)> plan, string sourceId, DamageType type)
    {
        int total = 0;
        foreach (var (t, dmg) in plan)
            total += ApplyDamageToTarget(context, t, sourceId, dmg, type);
        return total;
    }

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

    private static double Clamp01(double v) => v < 0 ? 0 : (v > 1 ? 1 : v);
}