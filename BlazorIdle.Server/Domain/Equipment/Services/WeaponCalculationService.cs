using BlazorIdle.Server.Domain.Equipment.Models;

namespace BlazorIdle.Server.Domain.Equipment.Services;

/// <summary>
/// 武器计算服务
/// 负责计算武器类型相关的战斗机制
/// </summary>
public class WeaponCalculationService
{
    /// <summary>
    /// 获取武器类型的攻击速度系数
    /// </summary>
    /// <param name="weaponType">武器类型</param>
    /// <returns>攻击速度系数（基准1.0）</returns>
    public double GetAttackSpeedMultiplier(WeaponType weaponType)
    {
        return weaponType switch
        {
            // 快速武器
            WeaponType.Dagger => 1.3,      // 匕首：攻击速度+30%
            WeaponType.Fist => 1.25,       // 拳套：攻击速度+25%
            WeaponType.Wand => 1.2,        // 魔杖：攻击速度+20%
            
            // 标准速度单手武器
            WeaponType.Sword => 1.0,       // 单手剑：标准速度
            WeaponType.Axe => 1.0,         // 单手斧：标准速度
            WeaponType.Mace => 1.0,        // 单手锤：标准速度
            
            // 慢速双手武器
            WeaponType.TwoHandSword => 0.8,  // 双手剑：攻击速度-20%
            WeaponType.TwoHandAxe => 0.75,   // 双手斧：攻击速度-25%
            WeaponType.TwoHandMace => 0.75,  // 双手锤：攻击速度-25%
            WeaponType.Polearm => 0.85,      // 长柄武器：攻击速度-15%
            WeaponType.Staff => 0.9,         // 法杖：攻击速度-10%
            
            // 远程武器
            WeaponType.Bow => 0.9,           // 弓：攻击速度-10%
            WeaponType.Crossbow => 0.7,      // 弩：攻击速度-30%（慢但高伤害）
            WeaponType.Gun => 0.85,          // 枪：攻击速度-15%
            
            // 副手特殊
            WeaponType.Shield => 0.0,        // 盾牌不能攻击
            
            WeaponType.None => 1.0,
            _ => 1.0
        };
    }

    /// <summary>
    /// 获取武器类型的伤害系数
    /// 慢速武器提供更高的单次伤害
    /// </summary>
    /// <param name="weaponType">武器类型</param>
    /// <returns>伤害系数（基准1.0）</returns>
    public double GetDamageMultiplier(WeaponType weaponType)
    {
        return weaponType switch
        {
            // 快速武器：低伤害
            WeaponType.Dagger => 0.85,      // 匕首：伤害-15%
            WeaponType.Fist => 0.9,         // 拳套：伤害-10%
            WeaponType.Wand => 0.95,        // 魔杖：伤害-5%
            
            // 标准单手武器
            WeaponType.Sword => 1.0,
            WeaponType.Axe => 1.0,
            WeaponType.Mace => 1.0,
            
            // 双手武器：高伤害
            WeaponType.TwoHandSword => 1.4,   // 双手剑：伤害+40%
            WeaponType.TwoHandAxe => 1.5,     // 双手斧：伤害+50%
            WeaponType.TwoHandMace => 1.5,    // 双手锤：伤害+50%
            WeaponType.Polearm => 1.35,       // 长柄武器：伤害+35%
            WeaponType.Staff => 1.3,          // 法杖：伤害+30%
            
            // 远程武器
            WeaponType.Bow => 1.2,            // 弓：伤害+20%
            WeaponType.Crossbow => 1.6,       // 弩：伤害+60%（慢但高伤害）
            WeaponType.Gun => 1.3,            // 枪：伤害+30%
            
            WeaponType.Shield => 0.0,
            WeaponType.None => 1.0,
            _ => 1.0
        };
    }

    /// <summary>
    /// 判断武器是否为双手武器
    /// </summary>
    /// <param name="weaponType">武器类型</param>
    /// <returns>是否为双手武器</returns>
    public bool IsTwoHanded(WeaponType weaponType)
    {
        return weaponType switch
        {
            WeaponType.TwoHandSword => true,
            WeaponType.TwoHandAxe => true,
            WeaponType.TwoHandMace => true,
            WeaponType.Staff => true,
            WeaponType.Polearm => true,
            WeaponType.Bow => true,
            WeaponType.Crossbow => true,
            WeaponType.Gun => true,
            _ => false
        };
    }

    /// <summary>
    /// 判断武器是否可以副手装备
    /// </summary>
    /// <param name="weaponType">武器类型</param>
    /// <returns>是否可以副手装备</returns>
    public bool CanEquipOffHand(WeaponType weaponType)
    {
        return weaponType switch
        {
            WeaponType.Dagger => true,
            WeaponType.Sword => true,
            WeaponType.Axe => true,
            WeaponType.Mace => true,
            WeaponType.Fist => true,
            WeaponType.Shield => true,
            _ => false
        };
    }

    /// <summary>
    /// 计算双持攻击速度加成
    /// 双持提供额外的攻击速度，但每把武器伤害降低
    /// </summary>
    /// <returns>双持攻击速度加成</returns>
    public double GetDualWieldSpeedBonus()
    {
        return 0.2; // 双持提供20%攻击速度加成
    }

    /// <summary>
    /// 计算双持伤害惩罚
    /// 副手武器伤害会有惩罚
    /// </summary>
    /// <param name="isOffHand">是否为副手</param>
    /// <returns>伤害系数</returns>
    public double GetDualWieldDamagePenalty(bool isOffHand)
    {
        if (isOffHand)
        {
            return 0.5; // 副手武器只造成50%伤害
        }
        return 1.0; // 主手武器造成100%伤害
    }

    /// <summary>
    /// 计算盾牌格挡率
    /// </summary>
    /// <param name="blockRating">格挡等级</param>
    /// <param name="hasShield">是否装备盾牌</param>
    /// <returns>格挡概率（0.0-0.75）</returns>
    public double CalculateBlockChance(double blockRating, bool hasShield)
    {
        if (!hasShield)
        {
            return 0.0;
        }

        // 基础格挡率5%（装备盾牌时）
        double baseBlockChance = 0.05;
        
        // 格挡等级转换为格挡率（100评级 = 1%）
        double ratingBlockChance = blockRating / 10000.0;
        
        double totalBlockChance = baseBlockChance + ratingBlockChance;
        
        // 格挡率上限75%
        return Math.Min(totalBlockChance, 0.75);
    }

    /// <summary>
    /// 计算格挡减伤值
    /// </summary>
    /// <param name="shieldArmor">盾牌提供的护甲值</param>
    /// <returns>格挡时的固定减伤值</returns>
    public int CalculateBlockValue(double shieldArmor)
    {
        // 格挡值 = 盾牌护甲值 × 0.5
        return (int)Math.Ceiling(shieldArmor * 0.5);
    }

    /// <summary>
    /// 应用格挡减伤
    /// </summary>
    /// <param name="rawDamage">原始伤害</param>
    /// <param name="blockValue">格挡值</param>
    /// <returns>格挡后的伤害</returns>
    public int ApplyBlockReduction(int rawDamage, int blockValue)
    {
        // 格挡至少减少70%伤害，最多减少100%
        int minDamage = (int)(rawDamage * 0.3);
        int reducedDamage = rawDamage - blockValue;
        
        return Math.Max(minDamage, Math.Max(0, reducedDamage));
    }

    /// <summary>
    /// 计算最终攻击间隔
    /// </summary>
    /// <param name="baseAttackInterval">基础攻击间隔（秒）</param>
    /// <param name="weaponSpeedMultiplier">武器速度系数</param>
    /// <param name="hastePercent">急速百分比</param>
    /// <param name="isDualWielding">是否双持</param>
    /// <returns>最终攻击间隔（秒）</returns>
    public double CalculateFinalAttackInterval(
        double baseAttackInterval,
        double weaponSpeedMultiplier,
        double hastePercent,
        bool isDualWielding)
    {
        // 应用武器速度系数
        double interval = baseAttackInterval / weaponSpeedMultiplier;
        
        // 应用急速
        interval = interval / (1.0 + hastePercent);
        
        // 应用双持加成
        if (isDualWielding)
        {
            interval = interval / (1.0 + GetDualWieldSpeedBonus());
        }
        
        // 攻击间隔不能低于0.5秒
        return Math.Max(interval, 0.5);
    }
}
