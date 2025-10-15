namespace BlazorIdle.Server.Infrastructure.Configuration;

/// <summary>
/// 装备系统配置选项
/// 用于配置装备系统的核心参数，如护甲计算、格挡机制、武器伤害等
/// </summary>
public class EquipmentSystemOptions
{
    /// <summary>
    /// 护甲计算配置
    /// </summary>
    public ArmorCalculationOptions ArmorCalculation { get; set; } = new();
    
    /// <summary>
    /// 格挡计算配置
    /// </summary>
    public BlockCalculationOptions BlockCalculation { get; set; } = new();
    
    /// <summary>
    /// 武器伤害计算配置
    /// </summary>
    public WeaponDamageOptions WeaponDamage { get; set; } = new();
}

/// <summary>
/// 护甲计算配置选项
/// 用于配置护甲值计算和减伤效果
/// </summary>
public class ArmorCalculationOptions
{
    /// <summary>
    /// 护甲减伤常数
    /// 用于护甲减伤公式：reduction = armor / (armor + K * level + C)
    /// 默认值: 400.0（参考经典MMORPG设定）
    /// </summary>
    public double ArmorConstant { get; set; } = 400.0;
    
    /// <summary>
    /// 最大护甲减伤百分比
    /// 限制护甲提供的最大伤害减免
    /// 默认值: 0.75（75%减伤）
    /// </summary>
    public double MaxArmorReduction { get; set; } = 0.75;
    
    /// <summary>
    /// 盾牌护甲值系数
    /// 盾牌提供的护甲值 = 物品等级 * 盾牌系数
    /// 默认值: 2.25（相当于1.5倍板甲胸甲）
    /// </summary>
    public double ShieldArmorMultiplier { get; set; } = 2.25;
}

/// <summary>
/// 格挡计算配置选项
/// 用于配置盾牌格挡概率和格挡减伤
/// </summary>
public class BlockCalculationOptions
{
    /// <summary>
    /// 基础格挡率
    /// 装备盾牌的基础格挡概率
    /// 默认值: 0.05（5%）
    /// </summary>
    public double BaseBlockChance { get; set; } = 0.05;
    
    /// <summary>
    /// 格挡伤害减免
    /// 格挡成功时减免的伤害百分比
    /// 默认值: 0.30（30%）
    /// </summary>
    public double BlockDamageReduction { get; set; } = 0.30;
    
    /// <summary>
    /// 力量提供的格挡率
    /// 每点力量增加的格挡概率
    /// 默认值: 0.001（0.1%/点力量）
    /// </summary>
    public double BlockChancePerStrength { get; set; } = 0.001;
    
    /// <summary>
    /// 物品等级提供的格挡率
    /// 盾牌每点物品等级增加的格挡概率
    /// 默认值: 0.002（0.2%/点物品等级）
    /// </summary>
    public double BlockChancePerItemLevel { get; set; } = 0.002;
    
    /// <summary>
    /// 最大格挡率
    /// 限制格挡率的最大值
    /// 默认值: 0.50（50%）
    /// </summary>
    public double MaxBlockChance { get; set; } = 0.50;
}

/// <summary>
/// 武器伤害计算配置选项
/// 用于配置武器伤害计算，包括双持机制
/// </summary>
public class WeaponDamageOptions
{
    /// <summary>
    /// 副手武器伤害系数
    /// 双持时副手武器的伤害倍率
    /// 默认值: 0.85（副手造成85%伤害）
    /// </summary>
    public double OffHandDamageCoefficient { get; set; } = 0.85;
}
