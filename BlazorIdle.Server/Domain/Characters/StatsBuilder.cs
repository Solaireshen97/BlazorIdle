using BlazorIdle.Shared.Models;

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

    // 合并：base（职业基础） + derived（主属性转换）
    public static CharacterStats Combine(CharacterStats @base, CharacterStats derived)
    {
        return new CharacterStats
        {
            AttackPower = @base.AttackPower + derived.AttackPower,
            SpellPower = @base.SpellPower + derived.SpellPower,
            CritChance = Clamp01(@base.CritChance + derived.CritChance),
            CritMultiplier = @base.CritMultiplier,            // 保持职业基础倍数
            HastePercent = @base.HastePercent + derived.HastePercent, // derived 为 0
            ArmorPenFlat = @base.ArmorPenFlat + derived.ArmorPenFlat,
            ArmorPenPct = Clamp01(@base.ArmorPenPct + derived.ArmorPenPct),
            MagicPenFlat = @base.MagicPenFlat + derived.MagicPenFlat,
            MagicPenPct = Clamp01(@base.MagicPenPct + derived.MagicPenPct)
        };
    }

    private static double Clamp01(double v) => v < 0 ? 0 : (v > 1 ? 1 : v);
}