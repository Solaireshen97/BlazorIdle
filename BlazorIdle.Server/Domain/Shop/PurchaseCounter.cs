using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace BlazorIdle.Server.Domain.Shop;

/// <summary>
/// 购买计数器实体（用于限购管理）
/// </summary>
[Index(nameof(CharacterId), nameof(ShopItemId), nameof(PeriodStart))]
public class PurchaseCounter
{
    /// <summary>
    /// 计数器ID
    /// </summary>
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// 角色ID
    /// </summary>
    [Required]
    public Guid CharacterId { get; set; }

    /// <summary>
    /// 商品ID
    /// </summary>
    [Required]
    public Guid ShopItemId { get; set; }

    /// <summary>
    /// 周期开始时间
    /// </summary>
    public DateTime PeriodStart { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 周期结束时间（可选）
    /// </summary>
    public DateTime? PeriodEnd { get; set; }

    /// <summary>
    /// 已购买次数
    /// </summary>
    public int PurchaseCount { get; set; } = 0;

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 更新时间
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
