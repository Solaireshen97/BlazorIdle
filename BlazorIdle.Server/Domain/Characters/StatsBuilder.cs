using System;
using BlazorIdle.Shared.Models;

namespace BlazorIdle.Server.Domain.Characters;

public static class StatsBuilder
{
    // Phase 1：线性映射 + 基础值组合，不考虑 Rating/等级曲线
    public static CharacterStats Build(Profession profession, PrimaryAttributes attrs)
    {
        var w = AttributeWeightsRegistry.Resolve(profession);

        double ap = attrs.Strength * w.StrToAP + attrs.Agility * w.AgiToAP;
        double sp = attrs.Intellect * w.IntToSP;

        // 暴击基础：仅由主属性贡献（全局基础值保持在 CritSettings/技能处）
        double crit = Clamp01(attrs.Strength * w.StrToCrit
                            + attrs.Agility * w.AgiToCrit
                            + attrs.Intellect * w.IntToCrit);

        // 急速：默认不开放主属性来源（保持 0）
        double haste = Clamp01(attrs.Agility * w.AgiToHaste + attrs.Intellect * w.IntToHaste);

        return new CharacterStats
        {
            AttackPower = ap,
            SpellPower = sp,
            CritChance = crit,
            CritMultiplier = 2.0,     // Phase 1 固定；Buff/技能可覆盖
            HastePercent = haste,   // 目前基本为 0；未来可启用
            ArmorPenFlat = 0.0,
            ArmorPenPct = 0.0,
            MagicPenFlat = 0.0,
            MagicPenPct = 0.0
        };
    }

    private static double Clamp01(double v) => v < 0 ? 0 : (v > 1 ? 1 : v);
}