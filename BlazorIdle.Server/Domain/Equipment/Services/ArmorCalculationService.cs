using BlazorIdle.Server.Domain.Equipment.Models;

namespace BlazorIdle.Server.Domain.Equipment.Services;

/// <summary>
/// 护甲计算服务
/// 负责计算护甲值和护甲减伤
/// </summary>
public class ArmorCalculationService
{
    // 护甲减伤公式常数
    private const double K = 50.0;   // 等级系数
    private const double C = 400.0;  // 基础常数
    private const double MAX_ARMOR_REDUCTION = 0.75; // 最大减伤75%

    /// <summary>
    /// 计算护甲减伤百分比
    /// 公式: reduction = armor / (armor + K * level + C)
    /// 最大减伤: 75%
    /// </summary>
    /// <param name="armor">护甲值</param>
    /// <param name="attackerLevel">攻击者等级</param>
    /// <returns>减伤百分比 (0-0.75)</returns>
    public double CalculateArmorReduction(double armor, int attackerLevel)
    {
        if (armor <= 0)
        {
            return 0;
        }

        var denominator = armor + (K * attackerLevel + C);
        if (denominator <= 0)
        {
            return 0;
        }

        var reduction = armor / denominator;
        
        // 限制最大减伤
        return Math.Min(reduction, MAX_ARMOR_REDUCTION);
    }

    /// <summary>
    /// 根据护甲类型获取护甲系数
    /// 布甲 < 皮甲 < 锁甲 < 板甲
    /// </summary>
    /// <param name="armorType">护甲类型</param>
    /// <returns>护甲系数</returns>
    public double GetArmorTypeMultiplier(ArmorType armorType)
    {
        return armorType switch
        {
            ArmorType.Cloth => 0.5,    // 布甲：50%护甲值
            ArmorType.Leather => 1.0,  // 皮甲：100%护甲值（基准）
            ArmorType.Mail => 1.5,     // 锁甲：150%护甲值
            ArmorType.Plate => 2.0,    // 板甲：200%护甲值
            _ => 1.0
        };
    }

    /// <summary>
    /// 计算单件装备的护甲贡献
    /// </summary>
    /// <param name="gear">装备实例</param>
    /// <returns>护甲值</returns>
    public double CalculateGearArmorContribution(GearInstance gear)
    {
        if (gear.Definition == null || gear.Definition.ArmorType == ArmorType.None)
        {
            return 0;
        }

        // 从基础属性中获取护甲值
        if (!gear.RolledStats.TryGetValue(StatType.Armor, out var baseArmor))
        {
            return 0;
        }

        // 应用护甲类型系数
        var multiplier = GetArmorTypeMultiplier(gear.Definition.ArmorType);
        return baseArmor * multiplier;
    }

    /// <summary>
    /// 计算装备提供的总护甲值
    /// </summary>
    /// <param name="equippedGear">已装备的装备列表</param>
    /// <returns>总护甲值</returns>
    public double CalculateTotalArmor(List<GearInstance> equippedGear)
    {
        double totalArmor = 0;

        foreach (var gear in equippedGear)
        {
            totalArmor += CalculateGearArmorContribution(gear);
        }

        return totalArmor;
    }

    /// <summary>
    /// 计算实际减免的伤害
    /// </summary>
    /// <param name="incomingDamage">incoming伤害</param>
    /// <param name="armor">护甲值</param>
    /// <param name="attackerLevel">攻击者等级</param>
    /// <returns>减免后的伤害</returns>
    public int CalculateMitigatedDamage(int incomingDamage, double armor, int attackerLevel)
    {
        var reduction = CalculateArmorReduction(armor, attackerLevel);
        var mitigated = incomingDamage * (1.0 - reduction);
        return Math.Max(0, (int)Math.Round(mitigated));
    }

    /// <summary>
    /// 获取护甲减伤详情（用于UI显示）
    /// </summary>
    /// <param name="armor">护甲值</param>
    /// <param name="characterLevel">角色等级</param>
    /// <returns>护甲详情</returns>
    public ArmorDetails GetArmorDetails(double armor, int characterLevel)
    {
        var reduction = CalculateArmorReduction(armor, characterLevel);
        var reductionPercent = reduction * 100;

        return new ArmorDetails
        {
            TotalArmor = armor,
            ReductionPercent = reductionPercent,
            EffectiveAgainstLevel = characterLevel
        };
    }
}

/// <summary>
/// 护甲详情
/// </summary>
public class ArmorDetails
{
    /// <summary>
    /// 总护甲值
    /// </summary>
    public double TotalArmor { get; set; }

    /// <summary>
    /// 减伤百分比
    /// </summary>
    public double ReductionPercent { get; set; }

    /// <summary>
    /// 对该等级敌人的有效性
    /// </summary>
    public int EffectiveAgainstLevel { get; set; }
}
