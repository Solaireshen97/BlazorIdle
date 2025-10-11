using BlazorIdle.Server.Domain.Equipment.ValueObjects;

namespace BlazorIdle.Server.Domain.Equipment.Models;

/// <summary>
/// 套装定义实体 - 定义套装及其加成效果
/// </summary>
public class GearSet
{
    /// <summary>套装ID</summary>
    public string Id { get; set; } = "";
    
    /// <summary>套装名称</summary>
    public string Name { get; set; } = "";
    
    /// <summary>套装描述</summary>
    public string Description { get; set; } = "";
    
    /// <summary>套装包含的装备定义ID列表</summary>
    public List<string> Pieces { get; set; } = new();
    
    /// <summary>套装加成（键=件数，值=该件数激活的加成列表）</summary>
    public Dictionary<int, List<StatModifier>> Bonuses { get; set; } = new();
    
    /// <summary>创建时间</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>更新时间</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
