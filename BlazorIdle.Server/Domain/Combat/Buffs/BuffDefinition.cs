namespace BlazorIdle.Server.Domain.Combat.Buffs;
using BlazorIdle.Server.Domain.Combat.Damage;

public class BuffDefinition
{
    public string Id { get; }
    public string Name { get; }
    public double DurationSeconds { get; }
    public int MaxStacks { get; }
    public BuffStackPolicy StackPolicy { get; }

    // 急速
    public double AdditiveHaste { get; }
    public double MultiplicativeHaste { get; }

    // 周期效果
    public BuffPeriodicType PeriodicType { get; }
    public double? PeriodicInterval { get; }
    public int PeriodicValue { get; }
    public string? PeriodicResourceId { get; }

    // 新增：周期伤害类型（当 PeriodicType=Damage 时生效）
    public DamageType PeriodicDamageType { get; }

    // 新增：DoT/HoT 强化
    // - 周期是否受 Haste 影响（true: effInterval = base / hasteSnapshot）
    // - Pandemic 刷新窗口比率（0.3 表示最多把 30% 基础时长的剩余时间结转）
    public bool PeriodicHasteAffected { get; }
    public double PandemicRatio { get; }

    // 伤害乘区与穿透
    public double DamageMultiplierPhysical { get; }
    public double DamageMultiplierMagic { get; }
    public double DamageMultiplierTrue { get; }

    public double ArmorPenFlat { get; }
    public double ArmorPenPct { get; }
    public double MagicPenFlat { get; }
    public double MagicPenPct { get; }

    // 暴击加成
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
        DamageType periodicDamageType = DamageType.Physical,
        // DoT/HoT 强化（新增）
        bool periodicHasteAffected = true,
        double pandemicRatio = 0.30,
        // 乘区/穿透
        double damageMultiplierPhysical = 0,
        double damageMultiplierMagic = 0,
        double damageMultiplierTrue = 0,
        double armorPenFlat = 0,
        double armorPenPct = 0,
        double magicPenFlat = 0,
        double magicPenPct = 0,
        // 暴击加成
        double critChanceBonus = 0,
        double critMultiplierBonus = 0
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
        PeriodicDamageType = periodicDamageType;

        PeriodicHasteAffected = periodicHasteAffected;
        PandemicRatio = pandemicRatio;

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