using System;
using System.ComponentModel.DataAnnotations;

namespace BlazorIdle.Server.Domain.Shop;

/// <summary>
/// 购买记录实体
/// 记录玩家的所有购买历史
/// </summary>
public class PurchaseRecord
{
    /// <summary>
    /// 记录唯一标识
    /// </summary>
    [Key]
    [MaxLength(100)]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// 角色 ID
    /// </summary>
    [Required]
    public Guid CharacterId { get; set; }

    /// <summary>
    /// 商店 ID
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string ShopId { get; set; } = "";

    /// <summary>
    /// 商品 ID
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string ShopItemId { get; set; } = "";

    /// <summary>
    /// 物品定义 ID
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string ItemDefinitionId { get; set; } = "";

    /// <summary>
    /// 购买数量
    /// </summary>
    public int Quantity { get; set; } = 1;

    /// <summary>
    /// 实际支付的价格（JSON 序列化）
    /// </summary>
    [Required]
    public string PriceJson { get; set; } = "";

    /// <summary>
    /// 购买时间
    /// </summary>
    public DateTime PurchasedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 关联的经济事件 ID（可选）
    /// </summary>
    [MaxLength(100)]
    public string? EconomyEventId { get; set; }
}
