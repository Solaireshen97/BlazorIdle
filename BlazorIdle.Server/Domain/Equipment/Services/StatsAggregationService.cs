using BlazorIdle.Server.Domain.Equipment.Models;

namespace BlazorIdle.Server.Domain.Equipment.Services;

/// <summary>
/// 属性聚合服务
/// 负责计算装备总属性、套装效果等
/// </summary>
public class StatsAggregationService
{
    private readonly EquipmentService _equipmentService;
    private readonly ArmorCalculator _armorCalculator;
    private readonly BlockCalculator _blockCalculator;
    private readonly AttackSpeedCalculator _attackSpeedCalculator;

    public StatsAggregationService(
        EquipmentService equipmentService,
        ArmorCalculator armorCalculator,
        BlockCalculator blockCalculator,
        AttackSpeedCalculator attackSpeedCalculator)
    {
        _equipmentService = equipmentService;
        _armorCalculator = armorCalculator;
        _blockCalculator = blockCalculator;
        _attackSpeedCalculator = attackSpeedCalculator;
    }

    /// <summary>
    /// 计算角色装备总属性
    /// </summary>
    /// <param name="characterId">角色ID</param>
    /// <returns>属性字典（属性类型 -> 数值）</returns>
    public virtual async Task<Dictionary<StatType, double>> CalculateEquipmentStatsAsync(Guid characterId)
    {
        var stats = new Dictionary<StatType, double>();

        // 1. 获取所有已装备的装备
        var equippedGear = await _equipmentService.GetEquippedGearAsync(characterId);

        // 2. 聚合基础属性
        foreach (var gear in equippedGear)
        {
            // 2.1 聚合装备Roll的基础属性
            foreach (var (statType, value) in gear.RolledStats)
            {
                if (!stats.ContainsKey(statType))
                {
                    stats[statType] = 0;
                }
                stats[statType] += value;
            }

            // 2.2 聚合词条属性
            foreach (var affix in gear.Affixes)
            {
                if (affix.ModifierType == ModifierType.Flat || affix.ModifierType == ModifierType.Percent)
                {
                    if (!stats.ContainsKey(affix.StatType))
                    {
                        stats[affix.StatType] = 0;
                    }
                    stats[affix.StatType] += affix.RolledValue;
                }
            }

            // 2.3 护甲类型特殊处理
            if (gear.Definition != null && gear.Definition.ArmorType != ArmorType.None)
            {
                var armorValue = CalculateArmorValue(gear);
                if (armorValue > 0)
                {
                    if (!stats.ContainsKey(StatType.Armor))
                    {
                        stats[StatType.Armor] = 0;
                    }
                    stats[StatType.Armor] += armorValue;
                }
            }
        }

        // 3. 计算套装加成
        var setBonus = CalculateSetBonus(equippedGear);
        foreach (var (statType, value) in setBonus)
        {
            if (!stats.ContainsKey(statType))
            {
                stats[statType] = 0;
            }
            stats[statType] += value;
        }

        return stats;
    }

    /// <summary>
    /// 计算护甲值
    /// </summary>
    private double CalculateArmorValue(GearInstance gear)
    {
        if (gear.Definition == null)
        {
            return 0;
        }

        // 使用 ArmorCalculator 计算护甲值
        if (gear.Definition.ArmorType != ArmorType.None && gear.SlotType.HasValue)
        {
            return _armorCalculator.CalculateArmorValue(
                gear.Definition.ArmorType,
                gear.SlotType.Value,
                gear.ItemLevel
            );
        }

        // 盾牌特殊处理
        if (gear.Definition.WeaponType == WeaponType.Shield && gear.SlotType == EquipmentSlot.OffHand)
        {
            return _armorCalculator.CalculateShieldArmorValue(gear.ItemLevel);
        }

        return 0;
    }

    /// <summary>
    /// 计算套装加成
    /// </summary>
    private Dictionary<StatType, double> CalculateSetBonus(List<GearInstance> equippedGear)
    {
        var setBonus = new Dictionary<StatType, double>();

        // 统计各套装的件数
        var setCounts = new Dictionary<string, int>();
        foreach (var gear in equippedGear)
        {
            if (!string.IsNullOrEmpty(gear.SetId))
            {
                if (!setCounts.ContainsKey(gear.SetId))
                {
                    setCounts[gear.SetId] = 0;
                }
                setCounts[gear.SetId]++;
            }
        }

        // 应用套装效果
        // 注意：这里简化处理，实际应该从GearSet定义表读取套装效果
        foreach (var (setId, count) in setCounts)
        {
            var bonus = GetSetBonus(setId, count);
            foreach (var (statType, value) in bonus)
            {
                if (!setBonus.ContainsKey(statType))
                {
                    setBonus[statType] = 0;
                }
                setBonus[statType] += value;
            }
        }

        return setBonus;
    }

    /// <summary>
    /// 获取套装加成（临时实现，实际应该从数据库读取）
    /// </summary>
    private Dictionary<StatType, double> GetSetBonus(string setId, int pieceCount)
    {
        var bonus = new Dictionary<StatType, double>();

        // 简化实现：根据件数给予固定加成
        if (pieceCount >= 2)
        {
            bonus[StatType.AttackPower] = 50;
        }
        if (pieceCount >= 4)
        {
            bonus[StatType.AttackPower] = 100;
            bonus[StatType.CritRating] = 50;
        }
        if (pieceCount >= 6)
        {
            bonus[StatType.AttackPower] = 200;
            bonus[StatType.CritRating] = 100;
            bonus[StatType.Haste] = 100;
        }

        return bonus;
    }

    /// <summary>
    /// 获取装备属性摘要（用于显示）
    /// </summary>
    public async Task<EquipmentStatsSummary> GetEquipmentStatsSummaryAsync(Guid characterId)
    {
        var stats = await CalculateEquipmentStatsAsync(characterId);
        var equippedGear = await _equipmentService.GetEquippedGearAsync(characterId);

        return new EquipmentStatsSummary
        {
            Stats = stats,
            EquippedCount = equippedGear.Count,
            TotalQualityScore = equippedGear.Sum(g => g.QualityScore)
        };
    }

    /// <summary>
    /// 计算格挡率（如果装备盾牌）
    /// </summary>
    /// <param name="characterId">角色ID</param>
    /// <param name="characterStrength">角色力量值（可选）</param>
    /// <returns>格挡率（0-0.5），无盾牌则返回0</returns>
    public virtual async Task<double> CalculateBlockChanceAsync(Guid characterId, double characterStrength = 0)
    {
        var equippedGear = await _equipmentService.GetEquippedGearAsync(characterId);
        
        // 查找副手盾牌
        var shield = equippedGear.FirstOrDefault(g => 
            g.SlotType == EquipmentSlot.OffHand && 
            g.Definition?.WeaponType == WeaponType.Shield);

        if (shield == null)
        {
            return 0;
        }

        return _blockCalculator.CalculateBlockChance(shield.ItemLevel, characterStrength);
    }

    /// <summary>
    /// 计算武器攻击速度（基于装备的主手武器）
    /// </summary>
    /// <param name="characterId">角色ID</param>
    /// <param name="hastePercent">角色急速百分比</param>
    /// <returns>攻击速度（秒），无武器返回职业默认值2.5</returns>
    public virtual async Task<double> CalculateAttackSpeedAsync(Guid characterId, double hastePercent = 0)
    {
        var equippedGear = await _equipmentService.GetEquippedGearAsync(characterId);
        
        // 查找主手武器
        var mainHandWeapon = equippedGear.FirstOrDefault(g => 
            g.SlotType == EquipmentSlot.MainHand);

        if (mainHandWeapon?.Definition?.WeaponType == null || 
            mainHandWeapon.Definition.WeaponType == WeaponType.None)
        {
            // 无武器，返回默认攻击速度
            return 2.5;
        }

        // 使用武器类型计算攻击速度（考虑急速）
        return _attackSpeedCalculator.CalculateAttackSpeed(
            mainHandWeapon.Definition.WeaponType, 
            hastePercent);
    }
}

/// <summary>
/// 装备属性摘要
/// </summary>
public class EquipmentStatsSummary
{
    /// <summary>
    /// 属性字典
    /// </summary>
    public Dictionary<StatType, double> Stats { get; set; } = new();

    /// <summary>
    /// 已装备数量
    /// </summary>
    public int EquippedCount { get; set; }

    /// <summary>
    /// 总评分
    /// </summary>
    public int TotalQualityScore { get; set; }
}
