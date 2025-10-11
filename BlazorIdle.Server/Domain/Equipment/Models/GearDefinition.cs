using BlazorIdle.Server.Domain.Equipment.ValueObjects;

namespace BlazorIdle.Server.Domain.Equipment.Models;

/// <summary>
/// 装备定义实体 - 存储在数据库的装备模板配置
/// </summary>
public class GearDefinition
{
    /// <summary>唯一标识（如"sword_iron"）</summary>
    public string Id { get; set; } = "";
    
    /// <summary>显示名称</summary>
    public string Name { get; set; } = "";
    
    /// <summary>图标（emoji或URL）</summary>
    public string Icon { get; set; } = "⚔️";
    
    /// <summary>装备槽位</summary>
    public EquipmentSlot Slot { get; set; }
    
    /// <summary>护甲类型</summary>
    public ArmorType ArmorType { get; set; } = ArmorType.None;
    
    /// <summary>武器类型</summary>
    public WeaponType WeaponType { get; set; } = WeaponType.None;
    
    /// <summary>需求等级</summary>
    public int RequiredLevel { get; set; } = 1;
    
    /// <summary>基础护甲值（仅护甲类装备）</summary>
    public double BaseArmor { get; set; } = 0;
    
    /// <summary>基础攻击速度（仅武器）</summary>
    public double BaseAttackSpeed { get; set; } = 0;
    
    /// <summary>基础伤害范围（仅武器）</summary>
    public double BaseDamageMin { get; set; } = 0;
    public double BaseDamageMax { get; set; } = 0;
    
    /// <summary>基础属性范围（JSON序列化存储）</summary>
    public Dictionary<StatType, StatRange> BaseStats { get; set; } = new();
    
    /// <summary>允许的词条池（词条ID列表）</summary>
    public List<string> AllowedAffixPool { get; set; } = new();
    
    /// <summary>稀有度权重（用于生成装备时的稀有度概率）</summary>
    public Dictionary<Rarity, double> RarityWeights { get; set; } = new();
    
    /// <summary>套装ID（如果属于套装）</summary>
    public string? SetId { get; set; }
    
    /// <summary>创建时间</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>更新时间</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
