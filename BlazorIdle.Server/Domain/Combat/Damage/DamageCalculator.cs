using System;
using BlazorIdle.Server.Domain.Combat.Damage;

namespace BlazorIdle.Server.Domain.Combat;

public static class DamageCalculator
{
    // 护甲减伤公式参数（可后续配置化）
    private const double K = 50.0;
    private const double C = 400.0;

    public static int ApplyDamage(BattleContext context, string sourceId, int baseDamage, DamageType type)
    {
        int dealt;
        double factor = 1.0;

        var agg = context.Buffs.Aggregate;

        if (context.Encounter is not null)
        {
            var enemy = context.Encounter.Enemy;

            switch (type)
            {
                case DamageType.Physical:
                    {
                        // 有效护甲：先减固定值，再按比例降低
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
                        // 有效魔抗 %：先减固定，再按比例降低；值域 0..1
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

            dealt = Math.Max(0, (int)Math.Round(baseDamage * factor));

            var applied = context.Encounter.ApplyDamage(dealt, context.Clock.CurrentTime);
            context.SegmentCollector.OnDamage(sourceId, applied, type);
            return applied;
        }

        // 无目标：仅统计
        dealt = Math.Max(0, (int)Math.Round(baseDamage * factor));
        context.SegmentCollector.OnDamage(sourceId, dealt, type);
        return dealt;
    }

    private static double Clamp01(double v) => v < 0 ? 0 : (v > 1 ? 1 : v);
}