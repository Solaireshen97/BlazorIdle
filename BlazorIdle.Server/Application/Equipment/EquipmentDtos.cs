using System;
using System.Collections.Generic;

namespace BlazorIdle.Server.Application.Equipment;

/// <summary>
/// 装备槽位信息（Step 5: 装备系统UI预留）
/// </summary>
public sealed class EquipmentSlotDto
{
    /// <summary>槽位类型（head, weapon, chest, offhand, belt, legs, boots, trinket1, trinket2）</summary>
    public string SlotType { get; set; } = "";
    
    /// <summary>槽位中文名称</summary>
    public string SlotName { get; set; } = "";
    
    /// <summary>装备实例（null表示空槽）</summary>
    public GearInstanceDto? Item { get; set; }
    
    /// <summary>是否锁定</summary>
    public bool IsLocked { get; set; }
}

/// <summary>
/// 装备实例（具体装备物品）
/// </summary>
public sealed class GearInstanceDto
{
    /// <summary>装备实例ID</summary>
    public Guid Id { get; set; }
    
    /// <summary>装备定义ID（配置表ID）</summary>
    public string DefinitionId { get; set; } = "";
    
    /// <summary>装备名称</summary>
    public string Name { get; set; } = "";
    
    /// <summary>装备图标（emoji）</summary>
    public string Icon { get; set; } = "⚔️";
    
    /// <summary>品质（Common, Rare, Epic, Legendary）</summary>
    public string Rarity { get; set; } = "Common";
    
    /// <summary>装备等级（Tier 1-3）</summary>
    public int Tier { get; set; } = 1;
    
    /// <summary>物品等级</summary>
    public int ItemLevel { get; set; }
    
    /// <summary>装备评分</summary>
    public int QualityScore { get; set; }
    
    /// <summary>词条列表</summary>
    public List<AffixDto> Affixes { get; set; } = new();
    
    /// <summary>套装ID（null表示非套装）</summary>
    public string? SetId { get; set; }
    
    /// <summary>装备提供的属性（key: 属性名, value: 属性值）</summary>
    public Dictionary<string, double> Stats { get; set; } = new();
}

/// <summary>
/// 装备词条（随机属性）
/// </summary>
public sealed class AffixDto
{
    /// <summary>词条ID</summary>
    public string Id { get; set; } = "";
    
    /// <summary>词条名称</summary>
    public string Name { get; set; } = "";
    
    /// <summary>词条描述</summary>
    public string Description { get; set; } = "";
    
    /// <summary>词条数值</summary>
    public double Value { get; set; }
}

/// <summary>
/// 装备栏响应（包含所有装备槽位）
/// </summary>
public sealed class EquipmentResponse
{
    /// <summary>角色ID</summary>
    public Guid CharacterId { get; set; }
    
    /// <summary>角色名称</summary>
    public string CharacterName { get; set; } = "";
    
    /// <summary>装备槽位列表</summary>
    public List<EquipmentSlotDto> Slots { get; set; } = new();
    
    /// <summary>装备总属性</summary>
    public Dictionary<string, double> TotalStats { get; set; } = new();
    
    /// <summary>装备总评分</summary>
    public int TotalScore { get; set; }
}

/// <summary>
/// 装备操作请求
/// </summary>
public sealed class EquipItemRequest
{
    /// <summary>要装备的物品实例ID</summary>
    public Guid ItemId { get; set; }
}

/// <summary>
/// 装备操作响应
/// </summary>
public sealed class EquipmentOperationResponse
{
    /// <summary>操作是否成功</summary>
    public bool Success { get; set; }
    
    /// <summary>错误消息（如果失败）</summary>
    public string? ErrorMessage { get; set; }
    
    /// <summary>更新后的装备栏</summary>
    public EquipmentResponse? Equipment { get; set; }
}
