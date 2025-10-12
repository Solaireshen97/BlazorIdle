using BlazorIdle.Server.Domain.Characters;
using BlazorIdle.Server.Domain.Equipment.Models;
using BlazorIdle.Shared.Models;

namespace BlazorIdle.Server.Domain.Equipment.Services;

/// <summary>
/// 装备属性集成服务
/// 负责将装备属性集成到角色战斗属性中
/// 
/// 【设计理念】
/// 本服务采用预计算策略，将装备相关的计算在战斗开始前完成，避免战斗循环中的重复查询和计算。
/// 
/// 【性能优化】
/// 1. 武器伤害倍率预计算（Phase 5）：在BuildStatsWithEquipmentAsync中计算武器伤害倍率，
///    将其直接应用到AttackPower中，战斗循环中无需再查询装备表
/// 2. 属性聚合一次完成：所有装备属性（基础、词条、套装）在一次调用中完成聚合
/// 3. 格挡率预计算：如果装备盾牌，格挡率在战斗开始时计算完成
/// 
/// 【集成点】
/// - 战斗系统：通过BuildStatsWithEquipmentAsync获取包含装备加成的完整属性
/// - 属性显示：通过GetEquipmentArmorAsync等方法获取装备相关数值
/// </summary>
public class EquipmentStatsIntegration
{
    private readonly StatsAggregationService _statsAggregationService;
    
    // 评级转换常数：4000评级 = 100%属性提升（1.0）
    // 这个值可以在未来根据数值平衡需求调整
    private const double RATING_CONVERSION_DIVISOR = 4000.0;

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
        
        // 5. 获取格挡率（Phase 4）
        var blockChance = await _statsAggregationService.CalculateBlockChanceAsync(
            characterId, 
            primaryAttrs.Strength);
        
        // 6. 获取武器伤害倍率（Phase 5）
        var weaponDamageMultiplier = await CalculateWeaponDamageMultiplierAsync(characterId);
        
        // 7. 将装备属性应用到战斗属性中（包括护甲、格挡和武器伤害）
        var finalStats = ApplyEquipmentStats(combinedStats, equipmentStats, blockChance, weaponDamageMultiplier);
        
        return finalStats;
    }

    /// <summary>
    /// 将装备属性应用到角色战斗属性
    /// </summary>
    /// <param name="baseStats">基础战斗属性</param>
    /// <param name="equipmentStats">装备属性</param>
    /// <param name="blockChance">格挡率</param>
    /// <param name="weaponDamageMultiplier">武器伤害倍率</param>
    /// <returns>应用装备加成后的战斗属性</returns>
    private CharacterStats ApplyEquipmentStats(
        CharacterStats baseStats,
        Dictionary<StatType, double> equipmentStats,
        double blockChance = 0,
        double weaponDamageMultiplier = 1.0)
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
        double armorBonus = 0; // Phase 4: 护甲值

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
                    // 暴击评级转换为暴击率
                    // 转换公式: 暴击率 = 暴击评级 / RATING_CONVERSION_DIVISOR
                    // 例如: 400评级 = 10%暴击率, 2000评级 = 50%暴击率
                    critChanceBonus += value / RATING_CONVERSION_DIVISOR;
                    break;
                
                case StatType.Haste:
                    // 急速评级转换为急速百分比
                    // 转换公式: 急速% = 急速评级 / RATING_CONVERSION_DIVISOR
                    // 例如: 400评级 = 10%急速, 2000评级 = 50%急速
                    // 急速影响: 攻击间隔 = 基础间隔 / (1 + 急速%)
                    hasteBonus += value / RATING_CONVERSION_DIVISOR;
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
                    // Phase 4: 护甲值
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

        // Phase 5: 应用武器伤害倍率到攻击强度
        // 这样在战斗循环中就不需要每次攻击都查询武器类型
        var effectiveAttackPower = (baseStats.AttackPower + attackPowerBonus) * weaponDamageMultiplier;

        // 创建新的CharacterStats（init-only properties）
        var result = new CharacterStats
        {
            AttackPower = effectiveAttackPower,
            SpellPower = baseStats.SpellPower + spellPowerBonus,
            CritChance = Clamp01(baseStats.CritChance + critChanceBonus),
            CritMultiplier = baseStats.CritMultiplier,
            HastePercent = baseStats.HastePercent + hasteBonus,
            ArmorPenFlat = baseStats.ArmorPenFlat + armorPenFlatBonus,
            ArmorPenPct = Clamp01(baseStats.ArmorPenPct + armorPenPctBonus),
            MagicPenFlat = baseStats.MagicPenFlat + magicPenFlatBonus,
            MagicPenPct = Clamp01(baseStats.MagicPenPct + magicPenPctBonus),
            // Phase 4: 防御属性
            Armor = armorBonus,
            BlockChance = Clamp01(blockChance)
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
    /// <param name="characterStrength">角色力量值（可选，用于增加格挡率）</param>
    /// <returns>格挡率（0-1）</returns>
    public async Task<double> GetEquipmentBlockChanceAsync(Guid characterId, double characterStrength = 0)
    {
        // 使用StatsAggregationService计算盾牌格挡率
        // 如果没有装备盾牌，返回0
        return await _statsAggregationService.CalculateBlockChanceAsync(characterId, characterStrength);
    }
    
    /// <summary>
    /// 计算基于装备武器的攻击间隔（Phase 5）
    /// </summary>
    /// <param name="characterId">角色ID</param>
    /// <param name="baseAttackInterval">职业基础攻击间隔</param>
    /// <returns>实际攻击间隔（考虑武器类型）</returns>
    public async Task<double> CalculateWeaponAttackIntervalAsync(Guid characterId, double baseAttackInterval)
    {
        var weaponType = await _statsAggregationService.GetMainHandWeaponTypeAsync(characterId);
        
        // 如果没有装备武器，使用职业基础攻击间隔
        if (weaponType == WeaponType.None)
        {
            return baseAttackInterval;
        }
        
        // 使用武器类型的基础攻击速度
        var attackSpeedCalculator = new AttackSpeedCalculator();
        return attackSpeedCalculator.GetBaseAttackSpeed(weaponType);
    }

    /// <summary>
    /// 获取武器信息用于战斗伤害计算（Phase 5）
    /// </summary>
    /// <param name="characterId">角色ID</param>
    /// <returns>包含主手、副手和双持状态的武器信息</returns>
    public async Task<WeaponInfo> GetWeaponInfoAsync(Guid characterId)
    {
        var mainHandType = await _statsAggregationService.GetMainHandWeaponTypeAsync(characterId);
        var offHandType = await _statsAggregationService.GetOffHandWeaponTypeAsync(characterId);
        var isDualWielding = await _statsAggregationService.IsDualWieldingAsync(characterId);
        
        return new WeaponInfo
        {
            MainHandWeaponType = mainHandType,
            OffHandWeaponType = offHandType,
            IsDualWielding = isDualWielding
        };
    }
    
    /// <summary>
    /// 计算武器伤害倍率（Phase 5）
    /// 将武器类型的伤害加成预先计算，避免战斗循环中的重复计算
    /// </summary>
    /// <param name="characterId">角色ID</param>
    /// <returns>武器伤害倍率（1.0 = 100%基础伤害）</returns>
    private async Task<double> CalculateWeaponDamageMultiplierAsync(Guid characterId)
    {
        var attackSpeedCalc = new AttackSpeedCalculator();
        var weaponDamageCalc = new WeaponDamageCalculator(attackSpeedCalc);
        
        var weaponInfo = await GetWeaponInfoAsync(characterId);
        
        // 如果没装备武器，使用空手倍率 (1.0)
        if (weaponInfo.MainHandWeaponType == WeaponType.None)
        {
            return 1.0;
        }
        
        // 使用 WeaponDamageCalculator 计算伤害倍率
        // 这里我们计算 (baseDamage=0, attackPower=1) 的结果来获取纯武器倍率
        var damageWith1AP = weaponDamageCalc.CalculateWeaponDamage(
            baseDamage: 0,
            attackPower: 1.0,
            mainHandWeapon: weaponInfo.MainHandWeaponType,
            offHandWeapon: weaponInfo.OffHandWeaponType,
            isDualWielding: weaponInfo.IsDualWielding
        );
        
        // 返回的值就是武器伤害倍率
        return damageWith1AP;
    }

    private static double Clamp01(double value)
    {
        if (value < 0) return 0;
        if (value > 1) return 1;
        return value;
    }
}

/// <summary>
/// 武器信息（用于战斗伤害计算）
/// </summary>
public class WeaponInfo
{
    /// <summary>
    /// 主手武器类型
    /// </summary>
    public WeaponType MainHandWeaponType { get; init; } = WeaponType.None;
    
    /// <summary>
    /// 副手武器类型
    /// </summary>
    public WeaponType OffHandWeaponType { get; init; } = WeaponType.None;
    
    /// <summary>
    /// 是否双持
    /// </summary>
    public bool IsDualWielding { get; init; }
}
