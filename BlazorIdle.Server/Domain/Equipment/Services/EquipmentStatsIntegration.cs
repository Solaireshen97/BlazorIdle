using BlazorIdle.Server.Domain.Characters;
using BlazorIdle.Server.Domain.Equipment.Models;
using BlazorIdle.Shared.Models;

namespace BlazorIdle.Server.Domain.Equipment.Services;

/// <summary>
/// 装备属性集成服务
/// 负责将装备属性集成到角色战斗属性中
/// </summary>
public class EquipmentStatsIntegration
{
    private readonly StatsAggregationService _statsAggregationService;

    public EquipmentStatsIntegration(StatsAggregationService statsAggregationService)
    {
        _statsAggregationService = statsAggregationService;
    }

    /// <summary>
    /// 构建包含装备加成的完整角色属性
    /// </summary>
    /// <param name="characterId">角色ID</param>
    /// <param name="profession">职业</param>
    /// <param name="primaryAttrs">主属性</param>
    /// <returns>完整的战斗属性</returns>
    public async Task<CharacterStats> BuildStatsWithEquipmentAsync(
        Guid characterId,
        Profession profession,
        PrimaryAttributes primaryAttrs)
    {
        // 1. 获取职业基础属性
        var baseStats = ProfessionBaseStatsRegistry.Resolve(profession);
        
        // 2. 从主属性派生属性
        var derivedStats = StatsBuilder.BuildDerived(profession, primaryAttrs);
        
        // 3. 合并基础和派生属性
        var combinedStats = StatsBuilder.Combine(baseStats, derivedStats);
        
        // 4. 获取装备属性
        var equipmentStats = await _statsAggregationService.CalculateEquipmentStatsAsync(characterId);
        
        // 5. 将装备属性应用到战斗属性中
        var finalStats = ApplyEquipmentStats(combinedStats, equipmentStats);
        
        return finalStats;
    }

    /// <summary>
    /// 将装备属性应用到角色战斗属性
    /// </summary>
    /// <param name="baseStats">基础战斗属性</param>
    /// <param name="equipmentStats">装备属性</param>
    /// <returns>应用装备加成后的战斗属性</returns>
    private CharacterStats ApplyEquipmentStats(
        CharacterStats baseStats,
        Dictionary<StatType, double> equipmentStats)
    {
        // 累加装备提供的属性
        double attackPowerBonus = 0;
        double spellPowerBonus = 0;
        double critChanceBonus = 0;
        double hasteBonus = 0;
        double armorPenFlatBonus = 0;
        double armorPenPctBonus = 0;
        double magicPenFlatBonus = 0;
        double magicPenPctBonus = 0;
        double armorBonus = 0;

        // 应用装备属性
        foreach (var (statType, value) in equipmentStats)
        {
            switch (statType)
            {
                case StatType.AttackPower:
                    attackPowerBonus += value;
                    break;
                
                case StatType.SpellPower:
                    spellPowerBonus += value;
                    break;
                
                case StatType.CritChance:
                    critChanceBonus += value;
                    break;
                
                case StatType.CritRating:
                    // 暴击评级转换为暴击率 (简化: 4000评级 = 1.0暴击率)
                    critChanceBonus += value / 4000.0;
                    break;
                
                case StatType.Haste:
                    hasteBonus += value;
                    break;
                
                case StatType.HastePercent:
                    hasteBonus += value;
                    break;
                
                case StatType.MasteryRating:
                case StatType.BlockRating:
                case StatType.DodgeRating:
                case StatType.ParryRating:
                case StatType.HitRating:
                    // 这些评级暂时不处理，预留扩展
                    break;
                
                case StatType.Armor:
                    armorBonus += value;
                    break;
                
                // 主属性通过装备增加
                case StatType.Strength:
                case StatType.Agility:
                case StatType.Intellect:
                case StatType.Stamina:
                    // 主属性的增加需要重新计算派生属性
                    // 这里暂时不处理，因为会导致循环依赖
                    // 实际应该在BuildStatsWithEquipmentAsync中先聚合主属性
                    break;
            }
        }

        // 创建新的CharacterStats（init-only properties）
        var result = new CharacterStats
        {
            AttackPower = baseStats.AttackPower + attackPowerBonus,
            SpellPower = baseStats.SpellPower + spellPowerBonus,
            CritChance = Clamp01(baseStats.CritChance + critChanceBonus),
            CritMultiplier = baseStats.CritMultiplier,
            HastePercent = baseStats.HastePercent + hasteBonus,
            ArmorPenFlat = baseStats.ArmorPenFlat + armorPenFlatBonus,
            ArmorPenPct = Clamp01(baseStats.ArmorPenPct + armorPenPctBonus),
            MagicPenFlat = baseStats.MagicPenFlat + magicPenFlatBonus,
            MagicPenPct = Clamp01(baseStats.MagicPenPct + magicPenPctBonus),
            Armor = baseStats.Armor + armorBonus
        };

        return result;
    }

    /// <summary>
    /// 获取装备提供的护甲值
    /// </summary>
    /// <param name="characterId">角色ID</param>
    /// <returns>总护甲值</returns>
    public async Task<double> GetEquipmentArmorAsync(Guid characterId)
    {
        var equipmentStats = await _statsAggregationService.CalculateEquipmentStatsAsync(characterId);
        
        if (equipmentStats.TryGetValue(StatType.Armor, out var armorValue))
        {
            return armorValue;
        }
        
        return 0;
    }

    /// <summary>
    /// 获取装备提供的格挡率（如果装备了盾牌）
    /// </summary>
    /// <param name="characterId">角色ID</param>
    /// <returns>格挡率（0-1）</returns>
    public Task<double> GetEquipmentBlockChanceAsync(Guid characterId)
    {
        // TODO: 实现盾牌格挡率计算
        // 需要检查副手槽位是否装备了盾牌
        return Task.FromResult(0.0);
    }

    private static double Clamp01(double value)
    {
        if (value < 0) return 0;
        if (value > 1) return 1;
        return value;
    }
}
