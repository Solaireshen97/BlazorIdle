using BlazorIdle.Shared.Models;

namespace BlazorIdle.Client.Services;

/// <summary>
/// DPS（每秒伤害）计算服务
/// 用于前端显示装备的理论DPS
/// </summary>
public class DPSCalculatorService
{
    // 武器类型基础攻击速度（秒）- 与后端AttackSpeedCalculator保持一致
    private static readonly Dictionary<string, double> WeaponBaseAttackSpeed = new()
    {
        { "None", 1.0 },
        // 单手武器
        { "Dagger", 1.8 },
        { "Fist", 2.0 },
        { "Wand", 2.0 },
        { "Sword", 2.4 },
        { "Axe", 2.6 },
        { "Mace", 2.8 },
        { "Shield", 0.0 },
        // 双手武器
        { "Staff", 3.0 },
        { "TwoHandSword", 3.4 },
        { "TwoHandAxe", 3.6 },
        { "TwoHandMace", 3.8 },
        { "Polearm", 3.2 },
        // 远程武器
        { "Bow", 2.8 },
        { "Crossbow", 3.2 },
        { "Gun", 2.6 }
    };

    // 武器类型DPS系数（用于平衡不同攻击速度）- 与后端保持一致
    private static readonly Dictionary<string, double> WeaponDPSCoefficient = new()
    {
        { "None", 1.0 },
        // 单手武器 - 系数约0.40
        { "Dagger", 0.40 },
        { "Fist", 0.40 },
        { "Wand", 0.42 },
        { "Sword", 0.42 },
        { "Axe", 0.41 },
        { "Mace", 0.40 },
        { "Shield", 0.0 },
        // 双手武器 - 系数约0.50
        { "Staff", 0.52 },
        { "TwoHandSword", 0.50 },
        { "TwoHandAxe", 0.49 },
        { "TwoHandMace", 0.48 },
        { "Polearm", 0.51 },
        // 远程武器 - 系数约0.45
        { "Bow", 0.45 },
        { "Crossbow", 0.44 },
        { "Gun", 0.46 }
    };

    /// <summary>
    /// 计算理论DPS
    /// </summary>
    /// <param name="attackPower">攻击强度</param>
    /// <param name="mainHandWeapon">主手武器类型</param>
    /// <param name="offHandWeapon">副手武器类型（可选）</param>
    /// <param name="hastePercent">急速百分比（如0.10表示10%急速）</param>
    /// <param name="critChance">暴击率（如0.20表示20%暴击）</param>
    /// <param name="critMultiplier">暴击倍率（默认2.0）</param>
    /// <returns>理论DPS</returns>
    public double CalculateDPS(
        double attackPower,
        string mainHandWeapon,
        string? offHandWeapon = null,
        double hastePercent = 0,
        double critChance = 0,
        double critMultiplier = 2.0)
    {
        if (string.IsNullOrEmpty(mainHandWeapon) || mainHandWeapon == "None")
        {
            return 0;
        }

        bool isDualWielding = !string.IsNullOrEmpty(offHandWeapon) && 
                              offHandWeapon != "None" && 
                              offHandWeapon != "Shield" &&
                              CanDualWield(mainHandWeapon) && 
                              CanDualWield(offHandWeapon);

        // 计算基础伤害
        double baseDamage = attackPower;
        
        if (!isDualWielding)
        {
            // 单手或双手武器
            double weaponMultiplier = GetWeaponDamageMultiplier(mainHandWeapon);
            double damagePerHit = baseDamage * weaponMultiplier;
            
            // 考虑暴击：平均伤害 = 基础伤害 * (1 - 暴击率 + 暴击率 * 暴击倍率)
            double avgDamagePerHit = damagePerHit * (1 - critChance + critChance * critMultiplier);
            
            // 计算攻击速度（考虑急速）
            double attackSpeed = CalculateAttackSpeed(mainHandWeapon, hastePercent);
            
            // DPS = 平均伤害 / 攻击间隔
            return attackSpeed > 0 ? avgDamagePerHit / attackSpeed : 0;
        }
        else
        {
            // 双持武器
            double mainHandMultiplier = GetWeaponDamageMultiplier(mainHandWeapon);
            double offHandMultiplier = GetWeaponDamageMultiplier(offHandWeapon!);
            
            const double offHandDamageCoefficient = 0.85; // 副手伤害系数
            const double offHandHitChance = 0.81; // 副手命中率（1 - 19%未命中惩罚）
            
            // 主手伤害
            double mainHandDamage = baseDamage * mainHandMultiplier;
            double mainHandAvgDamage = mainHandDamage * (1 - critChance + critChance * critMultiplier);
            
            // 副手伤害（考虑命中惩罚）
            double offHandDamage = baseDamage * offHandMultiplier * offHandDamageCoefficient;
            double offHandAvgDamage = offHandDamage * (1 - critChance + critChance * critMultiplier) * offHandHitChance;
            
            // 双持时使用主手攻击速度
            double attackSpeed = CalculateAttackSpeed(mainHandWeapon, hastePercent);
            
            // DPS = (主手平均伤害 + 副手平均伤害) / 攻击间隔
            return attackSpeed > 0 ? (mainHandAvgDamage + offHandAvgDamage) / attackSpeed : 0;
        }
    }

    /// <summary>
    /// 从总属性字典计算DPS
    /// </summary>
    /// <param name="stats">总属性字典</param>
    /// <param name="mainHandWeapon">主手武器类型</param>
    /// <param name="offHandWeapon">副手武器类型</param>
    /// <returns>理论DPS</returns>
    public double CalculateDPSFromStats(
        Dictionary<string, double> stats,
        string mainHandWeapon,
        string? offHandWeapon = null)
    {
        double attackPower = stats.TryGetValue("AttackPower", out var ap) ? ap : 0;
        double hastePercent = stats.TryGetValue("HastePercent", out var haste) ? haste : 0;
        double critChance = stats.TryGetValue("CritChance", out var crit) ? crit : 0;

        return CalculateDPS(attackPower, mainHandWeapon, offHandWeapon, hastePercent, critChance);
    }

    /// <summary>
    /// 获取武器基础攻击速度
    /// </summary>
    private double GetBaseAttackSpeed(string weaponType)
    {
        return WeaponBaseAttackSpeed.TryGetValue(weaponType, out var speed) ? speed : 2.5;
    }

    /// <summary>
    /// 计算实际攻击速度（考虑急速）
    /// </summary>
    private double CalculateAttackSpeed(string weaponType, double hastePercent)
    {
        double baseSpeed = GetBaseAttackSpeed(weaponType);
        
        // 急速降低攻击间隔：实际速度 = 基础速度 / (1 + 急速%)
        // 限制急速最低-50%（即最多2倍攻击速度）
        double hasteMultiplier = 1.0 / (1.0 + Math.Max(hastePercent, -0.5));
        
        return baseSpeed * hasteMultiplier;
    }

    /// <summary>
    /// 获取武器DPS系数
    /// </summary>
    private double GetWeaponDPSCoefficient(string weaponType)
    {
        return WeaponDPSCoefficient.TryGetValue(weaponType, out var coeff) ? coeff : 0.42;
    }

    /// <summary>
    /// 计算武器伤害倍率
    /// </summary>
    private double GetWeaponDamageMultiplier(string weaponType)
    {
        double baseSpeed = GetBaseAttackSpeed(weaponType);
        double dpsCoeff = GetWeaponDPSCoefficient(weaponType);
        
        return baseSpeed * dpsCoeff;
    }

    /// <summary>
    /// 检查武器类型是否可以双持
    /// </summary>
    private bool CanDualWield(string weaponType)
    {
        return weaponType switch
        {
            "Dagger" or "Fist" or "Sword" or "Axe" or "Mace" => true,
            _ => false
        };
    }

    /// <summary>
    /// 检查武器类型是否为双手武器
    /// </summary>
    public bool IsTwoHandedWeapon(string weaponType)
    {
        return weaponType switch
        {
            "TwoHandSword" or "TwoHandAxe" or "TwoHandMace" or
            "Staff" or "Polearm" or "Bow" or "Crossbow" or "Gun" => true,
            _ => false
        };
    }

    /// <summary>
    /// 格式化DPS显示（带单位）
    /// </summary>
    /// <param name="dps">DPS数值</param>
    /// <returns>格式化的字符串</returns>
    public string FormatDPS(double dps)
    {
        if (dps >= 1000000)
        {
            return $"{dps / 1000000:F2}M";
        }
        else if (dps >= 1000)
        {
            return $"{dps / 1000:F2}K";
        }
        else
        {
            return $"{dps:F1}";
        }
    }

    /// <summary>
    /// 获取攻击速度显示文本
    /// </summary>
    /// <param name="weaponType">武器类型</param>
    /// <param name="hastePercent">急速百分比</param>
    /// <returns>攻击速度文本（如："2.4s -> 2.0s (20%急速)"）</returns>
    public string GetAttackSpeedText(string weaponType, double hastePercent = 0)
    {
        double baseSpeed = GetBaseAttackSpeed(weaponType);
        
        if (baseSpeed == 0)
        {
            return "无攻击";
        }
        
        if (hastePercent > 0)
        {
            double actualSpeed = CalculateAttackSpeed(weaponType, hastePercent);
            return $"{baseSpeed:F1}s → {actualSpeed:F2}s ({hastePercent * 100:F0}%急速)";
        }
        
        return $"{baseSpeed:F1}s";
    }
}
