namespace BlazorIdle.Server.Domain.Combat.Buffs;

public class BuffAggregate
{
    // 急速
    public double AdditiveHaste { get; set; }
    public double MultiplicativeHasteFactor { get; set; } = 1.0;

    // 伤害加成（最终乘数：1 + 值）
    public double DamageMultiplierPhysical { get; set; }
    public double DamageMultiplierMagic { get; set; }
    public double DamageMultiplierTrue { get; set; }

    // 穿透（多个来源累加；Pct 合并为和并后在计算中 clamp）
    public double ArmorPenFlat { get; set; }
    public double ArmorPenPct { get; set; }
    public double MagicPenFlat { get; set; }
    public double MagicPenPct { get; set; }

    public double ApplyToBaseHaste(double baseFactor)
    {
        var result = (baseFactor + AdditiveHaste) * MultiplicativeHasteFactor;
        if (result < 0.1) result = 0.1;
        return result;
    }
}