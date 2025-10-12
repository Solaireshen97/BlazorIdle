using BlazorIdle.Client.Services;
using BlazorIdle.Shared.Models;

namespace BlazorIdle.Client.Services;

/// <summary>
/// DPS（每秒伤害）计算服务
/// 用于估算装备组合的战斗输出能力
/// </summary>
public class DpsCalculatorService
{
    /// <summary>
    /// DPS计算结果
    /// </summary>
    public class DpsResult
    {
        /// <summary>基础DPS（攻击力 / 攻击间隔）</summary>
        public double BaseDps { get; set; }
        
        /// <summary>暴击期望DPS</summary>
        public double CritDps { get; set; }
        
        /// <summary>总DPS（考虑所有加成）</summary>
        public double TotalDps { get; set; }
        
        /// <summary>攻击间隔（秒）</summary>
        public double AttackInterval { get; set; }
        
        /// <summary>单次攻击伤害</summary>
        public double DamagePerHit { get; set; }
        
        /// <summary>武器类型</summary>
        public string WeaponType { get; set; } = "None";
        
        /// <summary>是否双持</summary>
        public bool IsDualWielding { get; set; }
    }

    /// <summary>
    /// 计算角色的DPS
    /// </summary>
    /// <param name="stats">角色总属性</param>
    /// <param name="weaponInfo">武器信息（可选）</param>
    /// <returns>DPS计算结果</returns>
    public DpsResult CalculateDps(
        Dictionary<string, double> stats,
        string? weaponInfo = null)
    {
        var result = new DpsResult();
        
        // 获取基础属性
        var attackPower = stats.GetValueOrDefault("AttackPower", 0);
        var critChance = stats.GetValueOrDefault("CritChance", 0);
        var hastePercent = stats.GetValueOrDefault("HastePercent", 0);
        
        // 解析武器信息（如果有）
        ParseWeaponInfo(weaponInfo, out string weaponType, out bool isDualWielding);
        result.WeaponType = weaponType;
        result.IsDualWielding = isDualWielding;
        
        // 计算攻击间隔
        double baseInterval = GetBaseAttackInterval(weaponType);
        result.AttackInterval = baseInterval / (1 + hastePercent);
        
        // 计算单次攻击伤害
        double weaponDamageMultiplier = GetWeaponDamageMultiplier(weaponType, isDualWielding);
        result.DamagePerHit = attackPower * weaponDamageMultiplier;
        
        // 计算基础DPS
        if (result.AttackInterval > 0)
        {
            result.BaseDps = result.DamagePerHit / result.AttackInterval;
        }
        
        // 计算暴击期望DPS（暴击伤害为200%）
        double critMultiplier = 1 + (critChance * 1.0); // 暴击增加100%伤害
        result.CritDps = result.BaseDps * critMultiplier;
        
        // 总DPS等于暴击期望DPS
        result.TotalDps = result.CritDps;
        
        return result;
    }
    
    /// <summary>
    /// 比较两套装备的DPS差异
    /// </summary>
    /// <param name="currentStats">当前属性</param>
    /// <param name="newStats">新属性</param>
    /// <param name="currentWeaponInfo">当前武器信息</param>
    /// <param name="newWeaponInfo">新武器信息</param>
    /// <returns>DPS差异（正数表示提升）</returns>
    public double CompareDps(
        Dictionary<string, double> currentStats,
        Dictionary<string, double> newStats,
        string? currentWeaponInfo = null,
        string? newWeaponInfo = null)
    {
        var currentDps = CalculateDps(currentStats, currentWeaponInfo);
        var newDps = CalculateDps(newStats, newWeaponInfo);
        
        return newDps.TotalDps - currentDps.TotalDps;
    }
    
    /// <summary>
    /// 解析武器信息字符串
    /// </summary>
    private void ParseWeaponInfo(string? weaponInfo, out string weaponType, out bool isDualWielding)
    {
        weaponType = "None";
        isDualWielding = false;
        
        if (string.IsNullOrEmpty(weaponInfo))
        {
            return;
        }
        
        // 检查是否双持
        if (weaponInfo.Contains("双持") || weaponInfo.Contains("Dual"))
        {
            isDualWielding = true;
        }
        
        // 解析武器类型
        if (weaponInfo.Contains("单手剑") || weaponInfo.Contains("Sword"))
        {
            weaponType = "Sword";
        }
        else if (weaponInfo.Contains("双手剑") || weaponInfo.Contains("TwoHandSword"))
        {
            weaponType = "TwoHandSword";
        }
        else if (weaponInfo.Contains("匕首") || weaponInfo.Contains("Dagger"))
        {
            weaponType = "Dagger";
        }
        else if (weaponInfo.Contains("斧") || weaponInfo.Contains("Axe"))
        {
            weaponType = weaponInfo.Contains("双手") ? "TwoHandAxe" : "Axe";
        }
        else if (weaponInfo.Contains("锤") || weaponInfo.Contains("Mace"))
        {
            weaponType = weaponInfo.Contains("双手") ? "TwoHandMace" : "Mace";
        }
        else if (weaponInfo.Contains("拳套") || weaponInfo.Contains("Fist"))
        {
            weaponType = "Fist";
        }
        else if (weaponInfo.Contains("法杖") || weaponInfo.Contains("Staff"))
        {
            weaponType = "Staff";
        }
        else if (weaponInfo.Contains("魔杖") || weaponInfo.Contains("Wand"))
        {
            weaponType = "Wand";
        }
        else if (weaponInfo.Contains("弓") || weaponInfo.Contains("Bow"))
        {
            weaponType = "Bow";
        }
        else if (weaponInfo.Contains("弩") || weaponInfo.Contains("Crossbow"))
        {
            weaponType = "Crossbow";
        }
        else if (weaponInfo.Contains("枪械") || weaponInfo.Contains("Gun"))
        {
            weaponType = "Gun";
        }
        else if (weaponInfo.Contains("长柄") || weaponInfo.Contains("Polearm"))
        {
            weaponType = "Polearm";
        }
    }
    
    /// <summary>
    /// 获取武器基础攻击间隔（秒）
    /// </summary>
    private double GetBaseAttackInterval(string weaponType)
    {
        return weaponType switch
        {
            "Dagger" => 1.4,           // 匕首：快速
            "Fist" => 1.5,             // 拳套：快速
            "Wand" => 1.5,             // 魔杖：快速
            "Sword" => 2.0,            // 单手剑：中速
            "Axe" => 2.1,              // 单手斧：中速
            "Mace" => 2.0,             // 单手锤：中速
            "TwoHandSword" => 3.0,     // 双手剑：慢速
            "TwoHandAxe" => 3.2,       // 双手斧：慢速
            "TwoHandMace" => 3.0,      // 双手锤：慢速
            "Staff" => 2.8,            // 法杖：中慢速
            "Polearm" => 3.0,          // 长柄：慢速
            "Bow" => 2.5,              // 弓：中速
            "Crossbow" => 2.8,         // 弩：中慢速
            "Gun" => 2.3,              // 枪械：中速
            "None" => 2.5,             // 无武器：默认中速
            _ => 2.5
        };
    }
    
    /// <summary>
    /// 获取武器伤害倍率
    /// </summary>
    private double GetWeaponDamageMultiplier(string weaponType, bool isDualWielding)
    {
        // 基础倍率
        double multiplier = weaponType switch
        {
            // 快速武器：低倍率
            "Dagger" => 0.75,
            "Fist" => 0.80,
            "Wand" => 0.70,
            
            // 单手武器：标准倍率
            "Sword" => 1.0,
            "Axe" => 1.05,
            "Mace" => 1.0,
            
            // 双手武器：高倍率
            "TwoHandSword" => 1.8,
            "TwoHandAxe" => 1.9,
            "TwoHandMace" => 1.8,
            "Staff" => 1.6,
            "Polearm" => 1.75,
            
            // 远程武器：中等倍率
            "Bow" => 1.3,
            "Crossbow" => 1.4,
            "Gun" => 1.35,
            
            // 无武器
            "None" => 1.0,
            _ => 1.0
        };
        
        // 双持：主手100%，副手85%，总计185%
        if (isDualWielding)
        {
            multiplier *= 1.85;
        }
        
        return multiplier;
    }
    
    /// <summary>
    /// 格式化DPS显示
    /// </summary>
    /// <param name="dps">DPS值</param>
    /// <returns>格式化字符串</returns>
    public string FormatDps(double dps)
    {
        if (dps >= 1000)
        {
            return $"{dps / 1000:F2}K";
        }
        return $"{dps:F0}";
    }
    
    /// <summary>
    /// 格式化DPS差异显示
    /// </summary>
    /// <param name="dpsDifference">DPS差异</param>
    /// <param name="includePercent">是否包含百分比</param>
    /// <param name="currentDps">当前DPS（用于计算百分比）</param>
    /// <returns>格式化字符串</returns>
    public string FormatDpsDifference(double dpsDifference, bool includePercent = false, double currentDps = 0)
    {
        var sign = dpsDifference >= 0 ? "+" : "";
        var formatted = $"{sign}{FormatDps(dpsDifference)}";
        
        if (includePercent && currentDps > 0)
        {
            var percentChange = (dpsDifference / currentDps) * 100;
            formatted += $" ({sign}{percentChange:F1}%)";
        }
        
        return formatted;
    }
}
