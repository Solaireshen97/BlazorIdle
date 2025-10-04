namespace BlazorIdle.Server.Domain.Combat.Damage;

public class CritSettings
{
    // 基础暴击几率（0..1）
    public double Chance { get; private set; }
    // 基础暴击倍数（>= 1）
    public double Multiplier { get; private set; }

    public CritSettings(double chance = 0.20, double multiplier = 2.0)
    {
        Set(chance, multiplier);
    }

    public void Set(double chance, double multiplier)
    {
        if (chance < 0) chance = 0;
        if (chance > 1) chance = 1;
        if (multiplier < 1) multiplier = 1;
        Chance = chance;
        Multiplier = multiplier;
    }

    // 结合 BuffAggregate 与可选的技能级覆盖，得到最终的暴击参数
    public (double chance, double multiplier) ResolveWith(
        BlazorIdle.Server.Domain.Combat.Buffs.BuffAggregate aggregate,
        double? overrideChance = null,
        double? overrideMultiplier = null)
    {
        double c = overrideChance ?? Chance;
        double m = overrideMultiplier ?? Multiplier;

        c = Clamp01(c + aggregate.CritChanceBonus);
        m = System.Math.Max(1.0, m * (1 + aggregate.CritMultiplierBonus));

        return (c, m);
    }

    private static double Clamp01(double v) => v < 0 ? 0 : (v > 1 ? 1 : v);
}