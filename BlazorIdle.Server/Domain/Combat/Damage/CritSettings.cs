namespace BlazorIdle.Server.Domain.Combat.Damage;

public class CritSettings
{
    // 暴击几率（0..1）
    public double Chance { get; private set; }
    // 暴击倍数（>= 1）
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
}