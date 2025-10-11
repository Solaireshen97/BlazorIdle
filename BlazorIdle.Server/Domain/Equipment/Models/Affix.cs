namespace BlazorIdle.Server.Domain.Equipment.Models;

/// <summary>
/// 词条定义（配置实体）
/// </summary>
public class Affix
{
    /// <summary>
    /// 唯一标识
    /// </summary>
    public string Id { get; set; } = "";
    
    /// <summary>
    /// 显示名称
    /// </summary>
    public string Name { get; set; } = "";
    
    /// <summary>
    /// 属性类型
    /// </summary>
    public StatType StatType { get; set; }
    
    /// <summary>
    /// 修饰符类型
    /// </summary>
    public ModifierType ModifierType { get; set; }
    
    /// <summary>
    /// 最小值
    /// </summary>
    public double ValueMin { get; set; }
    
    /// <summary>
    /// 最大值
    /// </summary>
    public double ValueMax { get; set; }
    
    /// <summary>
    /// 稀有度权重（影响出现概率）
    /// </summary>
    public double RarityWeight { get; set; } = 1.0;
    
    /// <summary>
    /// 允许的槽位列表（null表示所有槽位都允许）
    /// </summary>
    public List<EquipmentSlot>? AllowedSlots { get; set; }
    
    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// 更新时间
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
