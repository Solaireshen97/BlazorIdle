using System;
using System.ComponentModel.DataAnnotations;

namespace BlazorIdle.Server.Domain.Shop;

/// <summary>
/// 购买计数器实体
/// 用于追踪角色对特定商品的购买次数（支持周期性重置）
/// </summary>
public class PurchaseCounter
{
    /// <summary>
    /// 计数器唯一标识
    /// </summary>
    [Key]
    [MaxLength(200)]
    public string Id { get; set; } = "";

    /// <summary>
    /// 角色 ID
    /// </summary>
    [Required]
    public Guid CharacterId { get; set; }

    /// <summary>
    /// 商品 ID
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string ShopItemId { get; set; } = "";

    /// <summary>
    /// 当前周期内的购买次数
    /// </summary>
    public int PurchaseCount { get; set; } = 0;

    /// <summary>
    /// 周期开始时间
    /// </summary>
    public DateTime PeriodStartAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 上次购买时间
    /// </summary>
    public DateTime LastPurchasedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 生成复合键
    /// </summary>
    public static string GenerateId(Guid characterId, string shopItemId)
    {
        return $"{characterId}_{shopItemId}";
    }

    /// <summary>
    /// 检查是否需要重置计数器
    /// </summary>
    public bool ShouldReset(int periodSeconds)
    {
        var elapsed = (DateTime.UtcNow - PeriodStartAt).TotalSeconds;
        return elapsed >= periodSeconds;
    }

    /// <summary>
    /// 重置计数器
    /// </summary>
    public void Reset()
    {
        PurchaseCount = 0;
        PeriodStartAt = DateTime.UtcNow;
    }

    /// <summary>
    /// 增加购买计数
    /// </summary>
    public void IncrementCount(int quantity = 1)
    {
        PurchaseCount += quantity;
        LastPurchasedAt = DateTime.UtcNow;
    }
}
