namespace BlazorIdle.Server.Domain.Combat.Buffs;

public class BuffDefinition
{
    public string Id { get; }
    public string Name { get; }
    public double DurationSeconds { get; }
    public int MaxStacks { get; }
    public BuffStackPolicy StackPolicy { get; }

    // 已有
    public double AdditiveHaste { get; }
    public double MultiplicativeHaste { get; }

    public BuffPeriodicType PeriodicType { get; }
    public double? PeriodicInterval { get; }
    public int PeriodicValue { get; }
    public string? PeriodicResourceId { get; }

    // 伤害加成/穿透（已有于上一阶段）
    public double DamageMultiplierPhysical { get; }
    public double DamageMultiplierMagic { get; }
    public double DamageMultiplierTrue { get; }

    public double ArmorPenFlat { get; }
    public double ArmorPenPct { get; }
    public double MagicPenFlat { get; }
    public double MagicPenPct { get; }

    // 新增：暴击加成（聚合时叠加）
    public double CritChanceBonus { get; }
    public double CritMultiplierBonus { get; }

    public BuffDefinition(
        string id,
        string name,
        double durationSeconds,
        int maxStacks,
        BuffStackPolicy stackPolicy,
        double additiveHaste = 0,
        double multiplicativeHaste = 0,
        BuffPeriodicType periodicType = BuffPeriodicType.None,
        double? periodicInterval = null,
        int periodicValue = 0,
        string? periodicResourceId = null,
        double damageMultiplierPhysical = 0,
        double damageMultiplierMagic = 0,
        double damageMultiplierTrue = 0,
        double armorPenFlat = 0,
        double armorPenPct = 0,
        double magicPenFlat = 0,
        double magicPenPct = 0,
        double critChanceBonus = 0,         // 新增
        double critMultiplierBonus = 0      // 新增
    )
    {
        Id = id;
        Name = name;
        DurationSeconds = durationSeconds;
        MaxStacks = maxStacks;
        StackPolicy = stackPolicy;

        AdditiveHaste = additiveHaste;
        MultiplicativeHaste = multiplicativeHaste;

        PeriodicType = periodicType;
        PeriodicInterval = periodicInterval;
        PeriodicValue = periodicValue;
        PeriodicResourceId = periodicResourceId;

        DamageMultiplierPhysical = damageMultiplierPhysical;
        DamageMultiplierMagic = damageMultiplierMagic;
        DamageMultiplierTrue = damageMultiplierTrue;

        ArmorPenFlat = armorPenFlat;
        ArmorPenPct = armorPenPct;
        MagicPenFlat = magicPenFlat;
        MagicPenPct = magicPenPct;

        CritChanceBonus = critChanceBonus;
        CritMultiplierBonus = critMultiplierBonus;
    }
}