using BlazorIdle.Server.Domain.Equipment.Models;

namespace BlazorIdle.Server.Domain.Equipment.Services;

/// <summary>
/// 攻击速度计算服务
/// 根据武器类型计算攻击速度和伤害倍率
/// </summary>
public class AttackSpeedCalculator
{
    // 武器类型基础攻击速度（秒）
    private static readonly Dictionary<WeaponType, double> WeaponBaseAttackSpeed = new()
    {
        { WeaponType.None, 1.0 },
        // 单手武器
        { WeaponType.Dagger, 1.8 },        // 最快
        { WeaponType.Fist, 2.0 },
        { WeaponType.Wand, 2.0 },
        { WeaponType.Sword, 2.4 },
        { WeaponType.Axe, 2.6 },
        { WeaponType.Mace, 2.8 },          // 最慢单手
        { WeaponType.Shield, 0.0 },        // 盾牌不攻击
        // 双手武器
        { WeaponType.Staff, 3.0 },
        { WeaponType.TwoHandSword, 3.4 },
        { WeaponType.TwoHandAxe, 3.6 },
        { WeaponType.TwoHandMace, 3.8 },
        { WeaponType.Polearm, 3.2 },
        // 远程武器
        { WeaponType.Bow, 2.8 },
        { WeaponType.Crossbow, 3.2 },
        { WeaponType.Gun, 2.6 }
    };

    // 武器类型DPS系数（用于平衡不同攻击速度）
    private static readonly Dictionary<WeaponType, double> WeaponDPSCoefficient = new()
    {
        { WeaponType.None, 1.0 },
        // 单手武器 - 系数约0.40
        { WeaponType.Dagger, 0.40 },
        { WeaponType.Fist, 0.40 },
        { WeaponType.Wand, 0.42 },
        { WeaponType.Sword, 0.42 },
        { WeaponType.Axe, 0.41 },
        { WeaponType.Mace, 0.40 },
        { WeaponType.Shield, 0.0 },
        // 双手武器 - 系数约0.50（更高DPS补偿单手）
        { WeaponType.Staff, 0.52 },
        { WeaponType.TwoHandSword, 0.50 },
        { WeaponType.TwoHandAxe, 0.49 },
        { WeaponType.TwoHandMace, 0.48 },
        { WeaponType.Polearm, 0.51 },
        // 远程武器 - 系数约0.45
        { WeaponType.Bow, 0.45 },
        { WeaponType.Crossbow, 0.44 },
        { WeaponType.Gun, 0.46 }
    };

    /// <summary>
    /// 获取武器基础攻击速度
    /// </summary>
    /// <param name="weaponType">武器类型</param>
    /// <returns>攻击间隔（秒）</returns>
    public double GetBaseAttackSpeed(WeaponType weaponType)
    {
        return WeaponBaseAttackSpeed.TryGetValue(weaponType, out var speed) ? speed : 2.5;
    }

    /// <summary>
    /// 计算实际攻击速度（考虑急速）
    /// </summary>
    /// <param name="weaponType">武器类型</param>
    /// <param name="hastePercent">急速百分比（0.10 = 10%急速）</param>
    /// <returns>实际攻击间隔（秒）</returns>
    public double CalculateAttackSpeed(WeaponType weaponType, double hastePercent)
    {
        double baseSpeed = GetBaseAttackSpeed(weaponType);
        
        // 急速降低攻击间隔：实际速度 = 基础速度 / (1 + 急速%)
        // 限制急速最低-50%（即最多2倍攻击速度）
        double hasteMultiplier = 1.0 / (1.0 + Math.Max(hastePercent, -0.5));
        
        return baseSpeed * hasteMultiplier;
    }

    /// <summary>
    /// 获取武器DPS系数（用于伤害计算）
    /// </summary>
    /// <param name="weaponType">武器类型</param>
    /// <returns>DPS系数</returns>
    public double GetWeaponDPSCoefficient(WeaponType weaponType)
    {
        return WeaponDPSCoefficient.TryGetValue(weaponType, out var coeff) ? coeff : 0.42;
    }

    /// <summary>
    /// 计算武器伤害倍率（攻击速度 * DPS系数）
    /// </summary>
    /// <param name="weaponType">武器类型</param>
    /// <returns>伤害倍率</returns>
    public double CalculateWeaponDamageMultiplier(WeaponType weaponType)
    {
        double baseSpeed = GetBaseAttackSpeed(weaponType);
        double dpsCoeff = GetWeaponDPSCoefficient(weaponType);
        
        return baseSpeed * dpsCoeff;
    }

    /// <summary>
    /// 检查武器类型是否为双手武器
    /// </summary>
    public static bool IsTwoHandedWeapon(WeaponType weaponType)
    {
        return weaponType switch
        {
            WeaponType.TwoHandSword or
            WeaponType.TwoHandAxe or
            WeaponType.TwoHandMace or
            WeaponType.Staff or
            WeaponType.Polearm or
            WeaponType.Bow or
            WeaponType.Crossbow or
            WeaponType.Gun => true,
            _ => false
        };
    }

    /// <summary>
    /// 检查武器类型是否可以双持
    /// </summary>
    public static bool CanDualWield(WeaponType weaponType)
    {
        return weaponType switch
        {
            WeaponType.Dagger or
            WeaponType.Fist or
            WeaponType.Sword or
            WeaponType.Axe or
            WeaponType.Mace => true,
            _ => false
        };
    }

    /// <summary>
    /// 计算双持惩罚（副手命中率降低）
    /// </summary>
    public static double GetDualWieldHitPenalty()
    {
        return 0.19; // 副手19%未命中惩罚
    }

    /// <summary>
    /// 计算双持DPS提升（理论值）
    /// 考虑副手未命中惩罚后，双持约提升37% DPS
    /// </summary>
    public static double GetDualWieldDPSBonus()
    {
        // 主手100% + 副手81% = 181% / 单手100% = 1.81倍
        // 但副手通常伤害稍低，实际约1.37倍
        return 0.37;
    }

    /// <summary>
    /// 获取武器类型显示名称
    /// </summary>
    public static string GetWeaponTypeName(WeaponType weaponType)
    {
        return weaponType switch
        {
            WeaponType.None => "空手",
            WeaponType.Sword => "单手剑",
            WeaponType.Dagger => "匕首",
            WeaponType.Axe => "单手斧",
            WeaponType.Mace => "单手锤",
            WeaponType.Fist => "拳套",
            WeaponType.Wand => "魔杖",
            WeaponType.TwoHandSword => "双手剑",
            WeaponType.TwoHandAxe => "双手斧",
            WeaponType.TwoHandMace => "双手锤",
            WeaponType.Staff => "法杖",
            WeaponType.Polearm => "长柄武器",
            WeaponType.Bow => "弓",
            WeaponType.Crossbow => "弩",
            WeaponType.Gun => "枪械",
            WeaponType.Shield => "盾牌",
            _ => "未知"
        };
    }
}
