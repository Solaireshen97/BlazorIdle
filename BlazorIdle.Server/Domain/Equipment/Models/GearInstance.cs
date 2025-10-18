using BlazorIdle.Server.Domain.Equipment.ValueObjects;
using BlazorIdle.Server.Infrastructure.DatabaseOptimization.Abstractions;

namespace BlazorIdle.Server.Domain.Equipment.Models;

/// <summary>
/// 装备实例（运行时实体）
/// Equipment instance (runtime entity)
/// 
/// 每个装备实例都是唯一的，包含Roll后的属性
/// Each equipment instance is unique with rolled attributes
/// </summary>
public class GearInstance : IEntity
{
    /// <summary>
    /// 实例ID
    /// Instance ID
    /// </summary>
    public Guid Id { get; set; }
    
    /// <summary>
    /// 关联的装备定义ID
    /// </summary>
    public string DefinitionId { get; set; } = "";
    
    /// <summary>
    /// 所属角色ID，null表示在背包或未分配
    /// </summary>
    public Guid? CharacterId { get; set; }
    
    /// <summary>
    /// 装备位置，null表示未装备（在背包中）
    /// </summary>
    public EquipmentSlot? SlotType { get; set; }
    
    /// <summary>
    /// 稀有度
    /// </summary>
    public Rarity Rarity { get; set; }
    
    /// <summary>
    /// 品级（1-3）
    /// </summary>
    public int TierLevel { get; set; } = 1;
    
    /// <summary>
    /// 物品等级
    /// </summary>
    public int ItemLevel { get; set; }
    
    /// <summary>
    /// 已Roll的基础属性（属性类型 -> 数值）
    /// </summary>
    public Dictionary<StatType, double> RolledStats { get; set; } = new();
    
    /// <summary>
    /// 词条列表
    /// </summary>
    public List<AffixInstance> Affixes { get; set; } = new();
    
    /// <summary>
    /// 装备评分
    /// </summary>
    public int QualityScore { get; set; }
    
    /// <summary>
    /// 套装ID（可选）
    /// </summary>
    public string? SetId { get; set; }
    
    /// <summary>
    /// 是否已装备
    /// </summary>
    public bool IsEquipped { get; set; }
    
    /// <summary>
    /// 是否绑定
    /// </summary>
    public bool IsBound { get; set; }
    
    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// 更新时间
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    // 导航属性
    public GearDefinition? Definition { get; set; }
}
