namespace BlazorIdle.Server.Domain.Combat.Buffs;

public class BuffDefinition
{
    public string Id { get; }
    public string Name { get; }
    public double DurationSeconds { get; }
    public int MaxStacks { get; }
    public BuffStackPolicy StackPolicy { get; }

    // 属性修改（已存在）
    public double AdditiveHaste { get; }          // 直接加
    public double MultiplicativeHaste { get; }    // 乘性 (最终 HasteFactor *= (1 + value))

    // 周期（已存在）
    public BuffPeriodicType PeriodicType { get; }
    public double? PeriodicInterval { get; }
    public int PeriodicValue { get; }
    public string? PeriodicResourceId { get; }

    // 新增：伤害加成与穿透（总是“加法累积”，在计算时统一乘到最终伤害或作用到护甲/魔抗）
    // 约定：DamageMultiplierX = 0.1 表示最终伤害 * (1 + 0.1)
    public double DamageMultiplierPhysical { get; }
    public double DamageMultiplierMagic { get; }
    public double DamageMultiplierTrue { get; }

    // 穿透：Flat 为直接减值；Pct 为按百分比降低剩余护甲/魔抗（0..1）
    public double ArmorPenFlat { get; }
    public double ArmorPenPct { get; }
    public double MagicPenFlat { get; }
    public double MagicPenPct { get; }

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
        // 新增参数（均有默认值，保持向后兼容）
        double damageMultiplierPhysical = 0,
        double damageMultiplierMagic = 0,
        double damageMultiplierTrue = 0,
        double armorPenFlat = 0,
        double armorPenPct = 0,
        double magicPenFlat = 0,
        double magicPenPct = 0
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
    }
}