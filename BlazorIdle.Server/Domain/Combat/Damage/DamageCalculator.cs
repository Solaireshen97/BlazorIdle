using System;
using BlazorIdle.Server.Domain.Combat.Damage;

namespace BlazorIdle.Server.Domain.Combat;

public static class DamageCalculator
{
    // 护甲减伤公式参数（简化，可后续配置化）
    // reduction = armor / (armor + K * level + C)
    private const double K = 50.0;
    private const double C = 400.0;

    public static int ApplyDamage(BattleContext context, string sourceId, int baseDamage, DamageType type)
    {
        int dealt = baseDamage;

        if (context.Encounter is not null)
        {
            var enemy = context.Encounter.Enemy;

            double multiplier = 1.0;
            switch (type)
            {
                case DamageType.Physical:
                    var denom = enemy.Armor + (K * enemy.Level + C);
                    var reduction = denom <= 0 ? 0 : enemy.Armor / denom;
                    multiplier *= Math.Clamp(1.0 - reduction, 0.0, 1.0);
                    multiplier *= 1.0 + enemy.VulnerabilityPhysical;
                    break;
                case DamageType.Magic:
                    multiplier *= Math.Clamp(1.0 - enemy.MagicResist, 0.0, 1.0);
                    multiplier *= 1.0 + enemy.VulnerabilityMagic;
                    break;
                case DamageType.True:
                    multiplier *= 1.0 + enemy.VulnerabilityTrue;
                    break;
            }

            dealt = (int)Math.Round(baseDamage * multiplier);
            if (dealt < 0) dealt = 0;

            // 结算至目标
            var applied = context.Encounter.ApplyDamage(dealt, context.Clock.CurrentTime);
            // 记录到段
            context.SegmentCollector.OnDamage(sourceId, applied, type);

            return applied;
        }

        // 没有目标时，作为纯统计
        context.SegmentCollector.OnDamage(sourceId, baseDamage, type);
        return baseDamage;
    }
}