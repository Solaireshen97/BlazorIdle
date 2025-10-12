using System.ComponentModel.DataAnnotations;

namespace BlazorIdle.Server.Domain.Shop;

/// <summary>
/// 购买记录实体
/// </summary>
public class PurchaseRecord
{
    /// <summary>
    /// 记录唯一标识
    /// </summary>
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

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
    public Guid ShopItemId { get; set; }

    /// <summary>
    /// 物品定义 ID（冗余，便于查询）
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string ItemDefinitionId { get; set; } = "";

    /// <summary>
    /// 购买数量
    /// </summary>
    [Range(1, 999)]
    public int Quantity { get; set; } = 1;

    /// <summary>
    /// 实际支付价格（JSON 格式）
    /// </summary>
    [Required]
    public string PriceJson { get; set; } = "";

    /// <summary>
    /// 购买时间
    /// </summary>
    public DateTime PurchasedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 关联的经济事件记录 ID（如果涉及货币扣减）
    /// </summary>
    public Guid? EconomyEventId { get; set; }
}
