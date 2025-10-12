using System.ComponentModel.DataAnnotations;

namespace BlazorIdle.Shared.Models.Shop;

/// <summary>
/// 购买请求 DTO
/// </summary>
public class PurchaseRequest
{
    /// <summary>
    /// 角色 ID
    /// </summary>
    [Required]
    public Guid CharacterId { get; set; }

    /// <summary>
    /// 商店 ID
    /// </summary>
    [Required]
    public string ShopId { get; set; } = "";

    /// <summary>
    /// 商品 ID
    /// </summary>
    [Required]
    public Guid ItemId { get; set; }

    /// <summary>
    /// 购买数量
    /// </summary>
    [Range(1, 999)]
    public int Quantity { get; set; } = 1;

    /// <summary>
    /// 幂等性键（可选，用于防重复）
    /// </summary>
    public string? IdempotencyKey { get; set; }
}
