using System.ComponentModel.DataAnnotations;

namespace BlazorIdle.Server.Domain.Shop;

/// <summary>
/// 购买记录实体
/// </summary>
public class PurchaseRecord
{
    /// <summary>
    /// 记录ID
    /// </summary>
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// 角色ID
    /// </summary>
    [Required]
    public Guid CharacterId { get; set; }

    /// <summary>
    /// 商店ID
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string ShopId { get; set; } = "";

    /// <summary>
    /// 商品ID
    /// </summary>
    [Required]
    public Guid ShopItemId { get; set; }

    /// <summary>
    /// 物品定义ID
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string ItemDefinitionId { get; set; } = "";

    /// <summary>
    /// 购买数量
    /// </summary>
    public int Quantity { get; set; } = 1;

    /// <summary>
    /// 支付的金币数量
    /// </summary>
    public int GoldPaid { get; set; }

    /// <summary>
    /// 支付的物品ID（物品交换时使用）
    /// </summary>
    [MaxLength(100)]
    public string? ItemPaidId { get; set; }

    /// <summary>
    /// 支付的物品数量
    /// </summary>
    public int ItemPaidQuantity { get; set; }

    /// <summary>
    /// 购买时间
    /// </summary>
    public DateTime PurchasedAt { get; set; } = DateTime.UtcNow;
}
