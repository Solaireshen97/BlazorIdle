using BlazorIdle.Server.Domain.Equipment.ValueObjects;

namespace BlazorIdle.Server.Domain.Equipment.Models;

/// <summary>
/// 装备定义（配置实体）
/// 存储在数据库或JSON配置文件中
/// </summary>
public class GearDefinition
{
    /// <summary>
    /// 唯一标识，如 "sword_iron"
    /// </summary>
    public string Id { get; set; } = "";
    
    /// <summary>
    /// 显示名称
    /// </summary>
    public string Name { get; set; } = "";
    
    /// <summary>
    /// 图标emoji或URL
    /// </summary>
    public string Icon { get; set; } = "⚔️";
    
    /// <summary>
    /// 装备槽位
    /// </summary>
    public EquipmentSlot Slot { get; set; }
    
    /// <summary>
    /// 护甲类型
    /// </summary>
    public ArmorType ArmorType { get; set; }
    
    /// <summary>
    /// 武器类型
    /// </summary>
    public WeaponType WeaponType { get; set; }
    
    /// <summary>
    /// 需求等级
    /// </summary>
    public int RequiredLevel { get; set; }
    
    /// <summary>
    /// 基础属性范围（属性类型 -> 最小-最大值）
    /// </summary>
    public Dictionary<StatType, StatRange> BaseStats { get; set; } = new();
    
    /// <summary>
    /// 允许的词条池（词条ID列表）
    /// </summary>
    public List<string> AllowedAffixPool { get; set; } = new();
    
    /// <summary>
    /// 稀有度权重（稀有度 -> 权重）
    /// </summary>
    public Dictionary<Rarity, double> RarityWeights { get; set; } = new();
    
    /// <summary>
    /// 套装ID（可选）
    /// </summary>
    public string? SetId { get; set; }
    
    /// <summary>
    /// 品级系数（T1/T2/T3）
    /// </summary>
    public Dictionary<int, double> TierMultipliers { get; set; } = new()
    {
        { 1, 0.8 },
        { 2, 1.0 },
        { 3, 1.2 }
    };
    
    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// 更新时间
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
