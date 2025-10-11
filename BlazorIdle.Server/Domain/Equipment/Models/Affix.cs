namespace BlazorIdle.Server.Domain.Equipment.Models;

/// <summary>
/// 词条定义实体 - 装备可以Roll出的随机词条配置
/// </summary>
public class Affix
{
    /// <summary>词条ID</summary>
    public string Id { get; set; } = "";
    
    /// <summary>词条名称（如"迅捷"）</summary>
    public string Name { get; set; } = "";
    
    /// <summary>影响的属性类型</summary>
    public StatType StatType { get; set; }
    
    /// <summary>修正类型</summary>
    public ModifierType ModifierType { get; set; }
    
    /// <summary>数值范围 - 最小值</summary>
    public double ValueMin { get; set; }
    
    /// <summary>数值范围 - 最大值</summary>
    public double ValueMax { get; set; }
    
    /// <summary>稀有度权重（影响出现概率）</summary>
    public double RarityWeight { get; set; } = 1.0;
    
    /// <summary>允许的装备槽位（null表示所有槽位）</summary>
    public List<EquipmentSlot>? AllowedSlots { get; set; }
    
    /// <summary>创建时间</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>更新时间</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
