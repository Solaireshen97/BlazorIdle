using BlazorIdle.Shared.Models;

namespace BlazorIdle.Server.Domain.Characters;

// 职业基础面板（Phase 1）：不包含任何来自主属性/装备的转换，仅定义职业“起点”。
// 说明：HastePercent 保持为 0，急速仅由 Buff/专门属性影响。
public static class ProfessionBaseStatsRegistry
{
    public static CharacterStats Resolve(Profession p) =>
        p switch
        {
            // 物理近战：较高 AP，基础暴击中等
            Profession.Warrior => new CharacterStats
            {
                AttackPower = 30,
                SpellPower = 0,
                CritChance = 0.05,   // 5%
                CritMultiplier = 2.0,
                HastePercent = 0.0,    // 急速仅由 Buff/专门属性来源
                ArmorPenFlat = 0.0,
                ArmorPenPct = 0.0,
                MagicPenFlat = 0.0,
                MagicPenPct = 0.0
            },

            // 敏捷射手：AP 略低于战士，但更高基础暴击
            Profession.Ranger => new CharacterStats
            {
                AttackPower = 25,
                SpellPower = 0,
                CritChance = 0.10,   // 10%
                CritMultiplier = 2.0,
                HastePercent = 0.0,
                ArmorPenFlat = 0.0,
                ArmorPenPct = 0.0,
                MagicPenFlat = 0.0,
                MagicPenPct = 0.0
            },

            // 默认兜底（当前仅两职业，保持与 Warrior 接近）
            _ => new CharacterStats
            {
                AttackPower = 25,
                SpellPower = 0,
                CritChance = 0.05,
                CritMultiplier = 2.0,
                HastePercent = 0.0,
                ArmorPenFlat = 0.0,
                ArmorPenPct = 0.0,
                MagicPenFlat = 0.0,
                MagicPenPct = 0.0
            }
        };
}