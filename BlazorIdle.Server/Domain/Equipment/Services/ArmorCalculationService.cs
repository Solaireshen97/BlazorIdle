using BlazorIdle.Server.Domain.Equipment.Models;

namespace BlazorIdle.Server.Domain.Equipment.Services;

/// <summary>
/// 护甲计算服务
/// 负责计算护甲减伤和相关机制
/// </summary>
public class ArmorCalculationService
{
    /// <summary>
    /// 护甲减伤常数K，用于控制护甲效果曲线（与DamageCalculator保持一致）
    /// </summary>
    private const double K = 50.0;
    
    /// <summary>
    /// 护甲减伤常数C（与DamageCalculator保持一致）
    /// </summary>
    private const double C = 400.0;
    
    /// <summary>
    /// 护甲减伤上限（75%）
    /// </summary>
    private const double MaxArmorReduction = 0.75;

    /// <summary>
    /// 计算护甲减伤百分比
    /// 公式: DamageReduction% = Armor / (Armor + K × AttackerLevel + C)
    /// 与DamageCalculator中的公式保持一致
    /// </summary>
    /// <param name="armor">总护甲值</param>
    /// <param name="attackerLevel">攻击者等级</param>
    /// <returns>护甲减伤百分比（0.0-0.75）</returns>
    public double CalculateArmorReduction(double armor, int attackerLevel)
    {
        if (armor <= 0) return 0;
        if (attackerLevel <= 0) attackerLevel = 1;
        
        double denom = armor + (K * attackerLevel + C);
        double reduction = denom <= 0 ? 0 : armor / denom;
        
        // 护甲减伤上限 75%
        return Math.Min(reduction, MaxArmorReduction);
    }

    /// <summary>
    /// 应用护甲减伤到伤害值
    /// </summary>
    /// <param name="rawDamage">原始伤害</param>
    /// <param name="armorReduction">护甲减伤百分比</param>
    /// <returns>减伤后的伤害值</returns>
    public int ApplyArmorReduction(int rawDamage, double armorReduction)
    {
        return (int)Math.Ceiling(rawDamage * (1.0 - armorReduction));
    }

    /// <summary>
    /// 计算有效护甲值（考虑护甲穿透）
    /// </summary>
    /// <param name="totalArmor">总护甲值</param>
    /// <param name="armorPenFlat">固定护甲穿透</param>
    /// <param name="armorPenPercent">百分比护甲穿透（0.0-1.0）</param>
    /// <returns>有效护甲值</returns>
    public double CalculateEffectiveArmor(
        double totalArmor, 
        double armorPenFlat, 
        double armorPenPercent)
    {
        // 有效护甲 = (总护甲 - 固定穿透) × (1 - 百分比穿透)
        double effectiveArmor = Math.Max(0, totalArmor - armorPenFlat);
        effectiveArmor *= (1 - Math.Clamp(armorPenPercent, 0, 1));
        
        return Math.Max(0, effectiveArmor);
    }

    /// <summary>
    /// 根据护甲类型计算护甲系数
    /// </summary>
    /// <param name="armorType">护甲类型</param>
    /// <returns>护甲系数</returns>
    public double GetArmorTypeMultiplier(ArmorType armorType)
    {
        return armorType switch
        {
            ArmorType.None => 0.0,
            ArmorType.Cloth => 0.5,
            ArmorType.Leather => 1.0,
            ArmorType.Mail => 1.5,
            ArmorType.Plate => 2.0,
            _ => 0.0
        };
    }

    /// <summary>
    /// 计算装备的护甲值
    /// 公式: 基础护甲值 × 护甲类型系数 × 物品等级系数 × 品级系数
    /// </summary>
    /// <param name="baseArmor">基础护甲值</param>
    /// <param name="armorType">护甲类型</param>
    /// <param name="itemLevel">物品等级</param>
    /// <param name="tierLevel">品级（1-3）</param>
    /// <returns>最终护甲值</returns>
    public double CalculateItemArmor(
        double baseArmor, 
        ArmorType armorType, 
        int itemLevel, 
        int tierLevel)
    {
        double armorTypeMultiplier = GetArmorTypeMultiplier(armorType);
        double itemLevelMultiplier = itemLevel / 10.0;
        double tierMultiplier = GetTierMultiplier(tierLevel);
        
        return baseArmor * armorTypeMultiplier * itemLevelMultiplier * tierMultiplier;
    }

    /// <summary>
    /// 获取品级系数
    /// </summary>
    /// <param name="tierLevel">品级（1-3）</param>
    /// <returns>品级系数</returns>
    private double GetTierMultiplier(int tierLevel)
    {
        return tierLevel switch
        {
            1 => 0.8,
            2 => 1.0,
            3 => 1.2,
            _ => 1.0
        };
    }

    /// <summary>
    /// 计算槽位护甲系数（某些槽位提供更多护甲）
    /// </summary>
    /// <param name="slot">装备槽位</param>
    /// <returns>槽位护甲系数</returns>
    public double GetSlotArmorMultiplier(EquipmentSlot slot)
    {
        return slot switch
        {
            EquipmentSlot.Chest => 1.5,      // 胸甲提供更多护甲
            EquipmentSlot.Legs => 1.3,       // 护腿次之
            EquipmentSlot.Shoulder => 1.2,   // 护肩略高
            EquipmentSlot.Head => 1.2,       // 头盔略高
            _ => 1.0                         // 其他部位
        };
    }
}
