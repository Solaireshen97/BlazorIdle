using BlazorIdle.Shared.Models;
using BlazorIdle.Server.Domain.Equipment.Models;

namespace BlazorIdle.Server.Domain.Characters;

public static class StatsBuilder
{
    // 由主属性线性构建“增量面板”（不含职业基础）
    public static CharacterStats BuildDerived(Profession profession, PrimaryAttributes attrs)
    {
        var w = AttributeWeightsRegistry.Resolve(profession);

        double ap = attrs.Strength * w.StrToAP + attrs.Agility * w.AgiToAP;
        double sp = attrs.Intellect * w.IntToSP;

        double crit = Clamp01(attrs.Strength * w.StrToCrit
                            + attrs.Agility * w.AgiToCrit
                            + attrs.Intellect * w.IntToCrit);

        // 急速来自专门属性/BUFF，这里保持为 0
        return new CharacterStats
        {
            AttackPower = ap,
            SpellPower = sp,
            CritChance = crit,
            // 下面这些在 derived 中不生效或保持 0，由 base 或 Buff 提供
            CritMultiplier = 0.0,
            HastePercent = 0.0,
            ArmorPenFlat = 0.0,
            ArmorPenPct = 0.0,
            MagicPenFlat = 0.0,
            MagicPenPct = 0.0
        };
    }

    // 合并：base（职业基础） + derived（主属性转换） + equipment（装备加成，可选）
    public static CharacterStats Combine(CharacterStats @base, CharacterStats derived, CharacterStats? equipment = null)
    {
        var eq = equipment ?? new CharacterStats();
        
        return new CharacterStats
        {
            AttackPower = @base.AttackPower + derived.AttackPower + eq.AttackPower,
            SpellPower = @base.SpellPower + derived.SpellPower + eq.SpellPower,
            CritChance = Clamp01(@base.CritChance + derived.CritChance + eq.CritChance),
            CritMultiplier = @base.CritMultiplier,            // 保持职业基础倍数
            HastePercent = @base.HastePercent + derived.HastePercent + eq.HastePercent,
            ArmorPenFlat = @base.ArmorPenFlat + derived.ArmorPenFlat + eq.ArmorPenFlat,
            ArmorPenPct = Clamp01(@base.ArmorPenPct + derived.ArmorPenPct + eq.ArmorPenPct),
            MagicPenFlat = @base.MagicPenFlat + derived.MagicPenFlat + eq.MagicPenFlat,
            MagicPenPct = Clamp01(@base.MagicPenPct + derived.MagicPenPct + eq.MagicPenPct)
        };
    }

    // 将装备系统的属性字典转换为CharacterStats
    public static CharacterStats FromEquipmentStats(Dictionary<StatType, double> equipmentStats)
    {
        if (equipmentStats == null || equipmentStats.Count == 0)
        {
            return new CharacterStats();
        }

        return new CharacterStats
        {
            AttackPower = equipmentStats.GetValueOrDefault(StatType.AttackPower, 0),
            SpellPower = equipmentStats.GetValueOrDefault(StatType.SpellPower, 0),
            CritChance = Clamp01(equipmentStats.GetValueOrDefault(StatType.CritChance, 0)),
            HastePercent = equipmentStats.GetValueOrDefault(StatType.HastePercent, 0),
            // 装备暂不提供这些穿透属性，预留为0
            ArmorPenFlat = 0,
            ArmorPenPct = 0,
            MagicPenFlat = 0,
            MagicPenPct = 0,
            CritMultiplier = 0  // 装备不影响暴击倍率
        };
    }

    private static double Clamp01(double v) => v < 0 ? 0 : (v > 1 ? 1 : v);
}