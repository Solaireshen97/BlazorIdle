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

    // 穿透
    public double ArmorPenFlat { get; set; }
    public double ArmorPenPct { get; set; }
    public double MagicPenFlat { get; set; }
    public double MagicPenPct { get; set; }

    // 新增：暴击加成
    // CritChanceBonus 为“加法”，0.2 表示 +20% 绝对概率
    // CritMultiplierBonus 为“乘法加成”，0.5 表示最终倍数 *= 1.5
    public double CritChanceBonus { get; set; }
    public double CritMultiplierBonus { get; set; }

    public double ApplyToBaseHaste(double baseFactor)
    {
        var result = (baseFactor + AdditiveHaste) * MultiplicativeHasteFactor;
        if (result < 0.1) result = 0.1;
        return result;
    }
}