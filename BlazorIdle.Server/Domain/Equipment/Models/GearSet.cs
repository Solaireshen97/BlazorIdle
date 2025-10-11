using BlazorIdle.Server.Domain.Equipment.ValueObjects;

namespace BlazorIdle.Server.Domain.Equipment.Models;

/// <summary>
/// 装备套装定义
/// </summary>
public class GearSet
{
    /// <summary>
    /// 唯一标识
    /// </summary>
    public string Id { get; set; } = "";
    
    /// <summary>
    /// 套装名称
    /// </summary>
    public string Name { get; set; } = "";
    
    /// <summary>
    /// 套装件数（如：头、胸、腿、鞋 = 4件套）
    /// </summary>
    public List<string> Pieces { get; set; } = new();
    
    /// <summary>
    /// 套装加成（件数 -> 属性修饰符列表）
    /// 如：2件套加成、4件套加成
    /// </summary>
    public Dictionary<int, List<StatModifier>> Bonuses { get; set; } = new();
    
    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// 更新时间
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
