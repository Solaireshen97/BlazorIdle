using System.ComponentModel.DataAnnotations;

namespace BlazorIdle.Server.Domain.Shop;

/// <summary>
/// 购买计数器实体（用于限制检查）
/// </summary>
public class PurchaseCounter
{
    /// <summary>
    /// 复合主键：CharacterId + ShopItemId + PeriodKey
    /// </summary>
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// 角色 ID
    /// </summary>
    [Required]
    public Guid CharacterId { get; set; }

    /// <summary>
    /// 商品 ID
    /// </summary>
    [Required]
    public Guid ShopItemId { get; set; }

    /// <summary>
    /// 周期键（格式示例：daily_20251012, weekly_202541, total）
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string PeriodKey { get; set; } = "";

    /// <summary>
    /// 当前周期已购买数量
    /// </summary>
    public int PurchaseCount { get; set; } = 0;

    /// <summary>
    /// 上次购买时间
    /// </summary>
    public DateTime LastPurchaseAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 周期过期时间（用于清理）
    /// </summary>
    public DateTime? ExpiresAt { get; set; }
}
