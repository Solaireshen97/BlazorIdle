using BlazorIdle.Server.Domain.Equipment.Models;
using BlazorIdle.Server.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace BlazorIdle.Server.Domain.Equipment.Services;

/// <summary>
/// 护甲计算服务
/// 根据护甲类型和物品等级计算护甲值和减伤效果
/// </summary>
public class ArmorCalculator
{
    private readonly EquipmentSystemOptions _options;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="options">装备系统配置选项</param>
    public ArmorCalculator(IOptions<EquipmentSystemOptions>? options = null)
    {
        _options = options?.Value ?? new EquipmentSystemOptions();
    }

    // 护甲类型系数
    private static readonly Dictionary<ArmorType, double> ArmorTypeMultipliers = new()
    {
        { ArmorType.None, 0.0 },
        { ArmorType.Cloth, 0.5 },
        { ArmorType.Leather, 1.0 },
        { ArmorType.Mail, 1.5 },
        { ArmorType.Plate, 2.0 }
    };

    // 槽位护甲基础值系数
    private static readonly Dictionary<EquipmentSlot, double> SlotArmorMultipliers = new()
    {
        { EquipmentSlot.Head, 1.0 },
        { EquipmentSlot.Neck, 0.0 },      // 项链无护甲
        { EquipmentSlot.Shoulder, 0.8 },
        { EquipmentSlot.Back, 0.5 },      // 披风少量护甲
        { EquipmentSlot.Chest, 1.5 },     // 胸甲最多护甲
        { EquipmentSlot.Wrist, 0.6 },
        { EquipmentSlot.Hands, 0.8 },
        { EquipmentSlot.Waist, 0.8 },
        { EquipmentSlot.Legs, 1.3 },      // 腿部护甲较多
        { EquipmentSlot.Feet, 0.8 },
        { EquipmentSlot.Finger1, 0.0 },   // 戒指无护甲
        { EquipmentSlot.Finger2, 0.0 },
        { EquipmentSlot.Trinket1, 0.0 },  // 饰品无护甲
        { EquipmentSlot.Trinket2, 0.0 },
        { EquipmentSlot.MainHand, 0.0 },  // 武器无护甲
        { EquipmentSlot.OffHand, 0.0 },   // 副手武器无护甲（盾牌特殊处理）
        { EquipmentSlot.TwoHand, 0.0 }
    };

    /// <summary>
    /// 计算装备的护甲值
    /// </summary>
    /// <param name="armorType">护甲类型</param>
    /// <param name="slot">装备槽位</param>
    /// <param name="itemLevel">物品等级</param>
    /// <returns>护甲值</returns>
    public double CalculateArmorValue(ArmorType armorType, EquipmentSlot slot, int itemLevel)
    {
        if (!ArmorTypeMultipliers.TryGetValue(armorType, out var typeMultiplier))
        {
            typeMultiplier = 0.0;
        }

        if (!SlotArmorMultipliers.TryGetValue(slot, out var slotMultiplier))
        {
            slotMultiplier = 0.0;
        }

        // 基础护甲 = 物品等级 * 槽位系数 * 护甲类型系数
        return itemLevel * slotMultiplier * typeMultiplier;
    }

    /// <summary>
    /// 计算盾牌的护甲值（盾牌有额外护甲加成）
    /// </summary>
    /// <param name="itemLevel">物品等级</param>
    /// <returns>护甲值</returns>
    public double CalculateShieldArmorValue(int itemLevel)
    {
        // 盾牌提供较高的护甲值（相当于1.5倍板甲胸甲）
        return itemLevel * _options.ArmorCalculation.ShieldArmorMultiplier;
    }

    /// <summary>
    /// 计算护甲减伤百分比
    /// </summary>
    /// <param name="totalArmor">总护甲值</param>
    /// <param name="attackerLevel">攻击者等级（用于计算护甲效果）</param>
    /// <returns>减伤百分比（0-0.75）</returns>
    public double CalculateArmorReduction(double totalArmor, int attackerLevel)
    {
        if (totalArmor <= 0)
        {
            return 0.0;
        }

        // 护甲减伤公式：Armor / (Armor + K * AttackerLevel + C)
        // K = 50, C = 400 (与DamageCalculator一致)
        double K = 50.0;
        double denominator = totalArmor + (K * attackerLevel + _options.ArmorCalculation.ArmorConstant);
        double reduction = totalArmor / denominator;

        // 限制最大减伤
        return Math.Min(reduction, _options.ArmorCalculation.MaxArmorReduction);
    }

    /// <summary>
    /// 获取护甲类型显示名称
    /// </summary>
    public static string GetArmorTypeName(ArmorType armorType)
    {
        return armorType switch
        {
            ArmorType.None => "无",
            ArmorType.Cloth => "布甲",
            ArmorType.Leather => "皮甲",
            ArmorType.Mail => "锁甲",
            ArmorType.Plate => "板甲",
            _ => "未知"
        };
    }

    /// <summary>
    /// 获取护甲类型系数
    /// </summary>
    public static double GetArmorTypeMultiplier(ArmorType armorType)
    {
        return ArmorTypeMultipliers.TryGetValue(armorType, out var multiplier) ? multiplier : 0.0;
    }
}
