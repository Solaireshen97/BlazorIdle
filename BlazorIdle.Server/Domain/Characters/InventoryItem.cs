using System;

namespace BlazorIdle.Server.Domain.Characters;

/// <summary>
/// 角色背包物品记录。
/// 每个物品 ID + 角色 ID 唯一，数量可累加。
/// </summary>
public class InventoryItem
{
    public Guid Id { get; set; }
    public Guid CharacterId { get; set; }
    public string ItemId { get; set; } = "";
    public int Quantity { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation property
    public Character Character { get; set; } = null!;
}
