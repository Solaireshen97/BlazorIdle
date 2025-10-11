using BlazorIdle.Server.Domain.Equipment.Models;
using BlazorIdle.Server.Domain.Equipment.Services;
using Microsoft.Extensions.Logging;

namespace BlazorIdle.Server.Domain.Characters;

/// <summary>
/// 角色属性构建器
/// 负责聚合角色的基础属性、装备加成等，生成最终的CharacterStats
/// </summary>
public class CharacterStatsBuilder
{
    private readonly StatsAggregationService _statsAggregationService;
    private readonly ILogger<CharacterStatsBuilder> _logger;

    public CharacterStatsBuilder(
        StatsAggregationService statsAggregationService,
        ILogger<CharacterStatsBuilder> logger)
    {
        _statsAggregationService = statsAggregationService;
        _logger = logger;
    }

    /// <summary>
    /// 构建角色的完整战斗属性
    /// </summary>
    /// <param name="character">角色实体</param>
    /// <param name="includeEquipment">是否包含装备加成（默认true）</param>
    /// <returns>完整的战斗属性</returns>
    public async Task<CharacterStats> BuildStatsAsync(
        Character character,
        bool includeEquipment = true)
    {
        // 1. 基础属性（从主属性派生）
        var baseStats = CalculateBaseStats(character);

        if (!includeEquipment)
        {
            return baseStats;
        }

        try
        {
            // 2. 获取装备属性加成
            var equipmentStats = await _statsAggregationService
                .CalculateEquipmentStatsAsync(character.Id);

            // 3. 合并属性
            return MergeStats(baseStats, equipmentStats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "Failed to calculate equipment stats for character {CharacterId}, using base stats only",
                character.Id);
            return baseStats;
        }
    }

    /// <summary>
    /// 从角色的主属性计算基础战斗属性
    /// </summary>
    private CharacterStats CalculateBaseStats(Character character)
    {
        // 基础属性转换规则：
        // - 力量 -> 物理攻击强度（1:1）
        // - 智力 -> 法术强度（1:1）
        // - 敏捷 -> 暴击等级（1:0.5）
        var attackPower = (double)character.Strength;
        var spellPower = (double)character.Intellect;
        var critRating = character.Agility * 0.5;

        // 暴击率转换：每100点暴击等级 = 1%暴击率
        var critChance = critRating / 100.0;

        return new CharacterStats
        {
            AttackPower = attackPower,
            SpellPower = spellPower,
            CritChance = Math.Min(1.0, critChance), // 最多100%
            CritMultiplier = 2.0,
            HastePercent = 0.0,
            ArmorPenFlat = 0.0,
            ArmorPenPct = 0.0,
            MagicPenFlat = 0.0,
            MagicPenPct = 0.0
        };
    }

    /// <summary>
    /// 合并基础属性和装备属性
    /// </summary>
    private CharacterStats MergeStats(
        CharacterStats baseStats,
        Dictionary<StatType, double> equipmentStats)
    {
        // 从装备属性中提取各项数值
        var equipAttackPower = GetEquipmentStat(equipmentStats, StatType.AttackPower);
        var equipSpellPower = GetEquipmentStat(equipmentStats, StatType.SpellPower);
        var equipStrength = GetEquipmentStat(equipmentStats, StatType.Strength);
        var equipIntellect = GetEquipmentStat(equipmentStats, StatType.Intellect);
        var equipAgility = GetEquipmentStat(equipmentStats, StatType.Agility);
        var equipCritRating = GetEquipmentStat(equipmentStats, StatType.CritRating);
        var equipHaste = GetEquipmentStat(equipmentStats, StatType.Haste);
        var equipHastePercent = GetEquipmentStat(equipmentStats, StatType.HastePercent);

        // 计算最终属性
        var totalAttackPower = baseStats.AttackPower + equipAttackPower + equipStrength;
        var totalSpellPower = baseStats.SpellPower + equipSpellPower + equipIntellect;
        var totalCritRating = (baseStats.CritChance * 100.0) + equipCritRating + (equipAgility * 0.5);
        var totalCritChance = Math.Min(1.0, totalCritRating / 100.0);

        // 急速：基础急速 + 装备急速等级/急速%
        // 假设：每100点急速等级 = 1%急速
        var totalHastePercent = baseStats.HastePercent + (equipHaste / 100.0) + equipHastePercent;

        return new CharacterStats
        {
            AttackPower = totalAttackPower,
            SpellPower = totalSpellPower,
            CritChance = totalCritChance,
            CritMultiplier = baseStats.CritMultiplier,
            HastePercent = totalHastePercent,
            ArmorPenFlat = baseStats.ArmorPenFlat,
            ArmorPenPct = baseStats.ArmorPenPct,
            MagicPenFlat = baseStats.MagicPenFlat,
            MagicPenPct = baseStats.MagicPenPct
        };
    }

    /// <summary>
    /// 从装备属性字典中安全获取指定属性的值
    /// </summary>
    private double GetEquipmentStat(Dictionary<StatType, double> equipmentStats, StatType statType)
    {
        return equipmentStats.TryGetValue(statType, out var value) ? value : 0.0;
    }
}
