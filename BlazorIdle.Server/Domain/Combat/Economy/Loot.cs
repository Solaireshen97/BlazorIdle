using System;
using System.Collections.Generic;

namespace BlazorIdle.Server.Domain.Economy;

/// <summary>
/// 物品类型枚举
/// </summary>
public enum ItemType
{
    /// <summary>
    /// 材料
    /// </summary>
    Material,
    
    /// <summary>
    /// 装备
    /// </summary>
    Gear,
    
    /// <summary>
    /// 消耗品（未来扩展）
    /// </summary>
    Consumable
}

public sealed class LootEntry
{
    // 概率（0..1），一次 roll 是否掉
    public double DropChance { get; init; } = 0.0;
    public string ItemId { get; init; } = "";
    public int QuantityMin { get; init; } = 1;
    public int QuantityMax { get; init; } = 1;

    // 新增：每只怪要进行的 roll 次数（默认 1）
    public int Rolls { get; init; } = 1;

    // 新增：物品类型（默认为材料，保持向后兼容）
    public ItemType ItemType { get; init; } = ItemType.Material;
    
    // 新增：装备定义ID（当 ItemType = Gear 时使用）
    public string? GearDefinitionId { get; init; } = null;

    // 预留：备注或来源（可选）
    public string? Note { get; init; }
}

public sealed class LootTable
{
    public string Id { get; }
    public IReadOnlyList<LootEntry> Entries { get; }

    public LootTable(string id, IReadOnlyList<LootEntry> entries)
    {
        Id = id;
        Entries = entries;
    }
}

public sealed class RewardSummary
{
    public long Gold { get; set; }
    public long Exp { get; set; }
    // 期望件数（或抽样件数），key=itemId
    public Dictionary<string, double> Items { get; } = new(StringComparer.Ordinal);
}