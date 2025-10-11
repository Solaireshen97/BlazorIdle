using BlazorIdle.Server.Domain.Characters;
using BlazorIdle.Server.Domain.Equipment.ValueObjects;

namespace BlazorIdle.Server.Domain.Equipment.Models;

/// <summary>
/// 装备实例实体 - 运行时实际装备对象，每个都是唯一的
/// </summary>
public class GearInstance
{
    /// <summary>实例唯一ID</summary>
    public Guid Id { get; set; }
    
    /// <summary>关联的装备定义ID</summary>
    public string DefinitionId { get; set; } = "";
    
    /// <summary>所属角色ID（null表示在背包或仓库）</summary>
    public Guid? CharacterId { get; set; }
    
    /// <summary>装备槽位（null表示未装备）</summary>
    public EquipmentSlot? SlotType { get; set; }
    
    /// <summary>稀有度</summary>
    public Rarity Rarity { get; set; } = Rarity.Common;
    
    /// <summary>品级（T1/T2/T3，影响属性系数）</summary>
    public int TierLevel { get; set; } = 1;
    
    /// <summary>物品等级（用于计算属性）</summary>
    public int ItemLevel { get; set; } = 1;
    
    /// <summary>Roll出的基础属性（JSON序列化存储）</summary>
    public Dictionary<StatType, double> RolledStats { get; set; } = new();
    
    /// <summary>词条列表（JSON序列化存储）</summary>
    public List<AffixInstance> Affixes { get; set; } = new();
    
    /// <summary>装备评分（用于比较装备优劣）</summary>
    public int QualityScore { get; set; } = 0;
    
    /// <summary>是否已装备</summary>
    public bool IsEquipped { get; set; } = false;
    
    /// <summary>是否绑定（绑定后不可交易）</summary>
    public bool IsBound { get; set; } = false;
    
    /// <summary>词条重置次数</summary>
    public int RerollCount { get; set; } = 0;
    
    /// <summary>创建时间</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>更新时间</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    // 导航属性
    /// <summary>所属角色</summary>
    public Character? Character { get; set; }
    
    /// <summary>装备定义（引用）</summary>
    public GearDefinition? Definition { get; set; }
}
