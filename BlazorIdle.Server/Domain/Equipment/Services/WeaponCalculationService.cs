using BlazorIdle.Server.Domain.Equipment.Models;

namespace BlazorIdle.Server.Domain.Equipment.Services;

/// <summary>
/// 武器计算服务
/// 负责计算武器攻击速度、伤害倍率等
/// </summary>
public class WeaponCalculationService
{
    /// <summary>
    /// 获取武器类型的基础攻击速度
    /// 攻击速度：每秒攻击次数
    /// </summary>
    /// <param name="weaponType">武器类型</param>
    /// <returns>基础攻击速度</returns>
    public double GetBaseAttackSpeed(WeaponType weaponType)
    {
        return weaponType switch
        {
            // 快速武器 (>= 2.0 APS)
            WeaponType.Dagger => 2.5,       // 匕首：最快
            WeaponType.Fist => 2.3,         // 拳套：非常快
            WeaponType.Wand => 2.0,         // 法杖：快速
            
            // 中速武器 (1.5 - 2.0 APS)
            WeaponType.Sword => 1.8,        // 单手剑：中快
            WeaponType.Axe => 1.7,          // 单手斧：中等
            WeaponType.Mace => 1.6,         // 单手锤：中慢
            WeaponType.Staff => 1.5,        // 双手法杖：中等
            
            // 慢速武器 (1.0 - 1.5 APS)
            WeaponType.TwoHandSword => 1.3, // 双手剑：较慢
            WeaponType.TwoHandAxe => 1.2,   // 双手斧：慢
            WeaponType.TwoHandMace => 1.1,  // 双手锤：很慢
            WeaponType.Polearm => 1.4,      // 长柄武器：较慢
            
            // 远程武器
            WeaponType.Bow => 1.6,          // 弓：中等
            WeaponType.Crossbow => 1.3,     // 弩：较慢但伤害高
            WeaponType.Gun => 1.5,          // 枪：中等
            
            // 特殊
            WeaponType.Shield => 0.0,       // 盾牌不攻击
            WeaponType.None => 1.0,         // 默认
            _ => 1.0
        };
    }

    /// <summary>
    /// 获取武器类型的伤害倍率
    /// 用于平衡不同攻速武器的DPS
    /// 慢速武器伤害高，快速武器伤害低
    /// </summary>
    /// <param name="weaponType">武器类型</param>
    /// <returns>伤害倍率</returns>
    public double GetDamageMultiplier(WeaponType weaponType)
    {
        return weaponType switch
        {
            // 快速武器：低伤害倍率
            WeaponType.Dagger => 0.35,       // 匕首：最低单次伤害
            WeaponType.Fist => 0.38,         // 拳套
            WeaponType.Wand => 0.40,         // 法杖
            
            // 中速单手武器：中等倍率
            WeaponType.Sword => 0.42,        // 单手剑
            WeaponType.Axe => 0.44,          // 单手斧
            WeaponType.Mace => 0.46,         // 单手锤
            
            // 双手武器：高倍率
            WeaponType.TwoHandSword => 0.55, // 双手剑
            WeaponType.TwoHandAxe => 0.58,   // 双手斧：最高单次伤害
            WeaponType.TwoHandMace => 0.60,  // 双手锤：超高单次伤害
            WeaponType.Polearm => 0.52,      // 长柄武器
            WeaponType.Staff => 0.50,        // 双手法杖
            
            // 远程武器
            WeaponType.Bow => 0.46,          // 弓
            WeaponType.Crossbow => 0.55,     // 弩：高爆发
            WeaponType.Gun => 0.48,          // 枪
            
            // 特殊
            WeaponType.Shield => 0.0,        // 盾牌不攻击
            WeaponType.None => 0.40,         // 默认
            _ => 0.40
        };
    }

    /// <summary>
    /// 计算武器的理论DPS系数
    /// DPS系数 = 攻击速度 * 伤害倍率
    /// 用于验证武器平衡性
    /// </summary>
    /// <param name="weaponType">武器类型</param>
    /// <returns>DPS系数</returns>
    public double CalculateDpsCoefficient(WeaponType weaponType)
    {
        var attackSpeed = GetBaseAttackSpeed(weaponType);
        var damageMultiplier = GetDamageMultiplier(weaponType);
        return attackSpeed * damageMultiplier;
    }

    /// <summary>
    /// 判断是否为双手武器
    /// </summary>
    /// <param name="weaponType">武器类型</param>
    /// <returns>是否为双手武器</returns>
    public bool IsTwoHandedWeapon(WeaponType weaponType)
    {
        return weaponType switch
        {
            WeaponType.TwoHandSword => true,
            WeaponType.TwoHandAxe => true,
            WeaponType.TwoHandMace => true,
            WeaponType.Polearm => true,
            WeaponType.Staff => true,
            WeaponType.Bow => true,
            WeaponType.Crossbow => true,
            WeaponType.Gun => true,
            _ => false
        };
    }

    /// <summary>
    /// 判断武器是否可以双持
    /// </summary>
    /// <param name="weaponType">武器类型</param>
    /// <returns>是否可以双持</returns>
    public bool CanDualWield(WeaponType weaponType)
    {
        return weaponType switch
        {
            WeaponType.Dagger => true,
            WeaponType.Fist => true,
            WeaponType.Sword => true,
            WeaponType.Axe => true,
            WeaponType.Mace => true,
            _ => false
        };
    }

    /// <summary>
    /// 计算双持攻击速度惩罚后的实际攻击速度
    /// 双持时每只手独立攻击，但有命中惩罚
    /// </summary>
    /// <param name="mainHandSpeed">主手攻击速度</param>
    /// <param name="offHandSpeed">副手攻击速度</param>
    /// <param name="dualWieldPenalty">双持惩罚 (0-1)，默认0.1表示10%惩罚</param>
    /// <returns>实际总攻击速度</returns>
    public double CalculateDualWieldAttackSpeed(
        double mainHandSpeed, 
        double offHandSpeed, 
        double dualWieldPenalty = 0.1)
    {
        // 主手全速，副手有惩罚
        var effectiveOffHandSpeed = offHandSpeed * (1.0 - dualWieldPenalty);
        return mainHandSpeed + effectiveOffHandSpeed;
    }

    /// <summary>
    /// 获取盾牌格挡率
    /// </summary>
    /// <param name="shieldItemLevel">盾牌物品等级</param>
    /// <returns>格挡率 (0-0.5)</returns>
    public double GetShieldBlockChance(int shieldItemLevel)
    {
        // 基础格挡率 + 等级加成
        // 基础10%，每10级+2%，最高50%
        var baseBlockChance = 0.10;
        var levelBonus = (shieldItemLevel / 10) * 0.02;
        var totalBlockChance = baseBlockChance + levelBonus;
        
        // 上限50%
        return Math.Min(totalBlockChance, 0.50);
    }

    /// <summary>
    /// 计算格挡伤害减免
    /// </summary>
    /// <param name="incomingDamage">incoming伤害</param>
    /// <param name="blockChance">格挡率</param>
    /// <param name="blockAmount">格挡值（固定减免）</param>
    /// <returns>格挡后的伤害</returns>
    public int CalculateBlockedDamage(int incomingDamage, double blockChance, int blockAmount)
    {
        // 简化实现：不使用随机，而是按概率计算期望伤害
        // 实际战斗中应该使用RNG判定是否格挡
        var blockedDamage = Math.Max(0, incomingDamage - blockAmount);
        return (int)Math.Round(incomingDamage * (1.0 - blockChance) + blockedDamage * blockChance);
    }

    /// <summary>
    /// 获取武器详情（用于UI显示）
    /// </summary>
    /// <param name="weaponType">武器类型</param>
    /// <returns>武器详情</returns>
    public WeaponDetails GetWeaponDetails(WeaponType weaponType)
    {
        return new WeaponDetails
        {
            WeaponType = weaponType,
            BaseAttackSpeed = GetBaseAttackSpeed(weaponType),
            DamageMultiplier = GetDamageMultiplier(weaponType),
            DpsCoefficient = CalculateDpsCoefficient(weaponType),
            IsTwoHanded = IsTwoHandedWeapon(weaponType),
            CanDualWield = CanDualWield(weaponType)
        };
    }
}

/// <summary>
/// 武器详情
/// </summary>
public class WeaponDetails
{
    /// <summary>
    /// 武器类型
    /// </summary>
    public WeaponType WeaponType { get; set; }

    /// <summary>
    /// 基础攻击速度（每秒攻击次数）
    /// </summary>
    public double BaseAttackSpeed { get; set; }

    /// <summary>
    /// 伤害倍率
    /// </summary>
    public double DamageMultiplier { get; set; }

    /// <summary>
    /// DPS系数
    /// </summary>
    public double DpsCoefficient { get; set; }

    /// <summary>
    /// 是否为双手武器
    /// </summary>
    public bool IsTwoHanded { get; set; }

    /// <summary>
    /// 是否可以双持
    /// </summary>
    public bool CanDualWield { get; set; }
}
