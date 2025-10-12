using System.ComponentModel.DataAnnotations;

namespace BlazorIdle.Server.Domain.Shop.ValueObjects;

/// <summary>
/// 价格值对象
/// 支持多种货币类型（金币、物品等）
/// </summary>
public class Price
{
    /// <summary>
    /// 货币类型
    /// </summary>
    [Required]
    public CurrencyType CurrencyType { get; set; } = CurrencyType.Gold;

    /// <summary>
    /// 货币 ID（当 CurrencyType=Item 时使用）
    /// </summary>
    [MaxLength(100)]
    public string? CurrencyId { get; set; }

    /// <summary>
    /// 价格数量
    /// </summary>
    public int Amount { get; set; }

    /// <summary>
    /// 验证价格是否有效
    /// </summary>
    public bool IsValid()
    {
        if (Amount <= 0) return false;
        if (CurrencyType == CurrencyType.Item && string.IsNullOrWhiteSpace(CurrencyId))
            return false;
        return true;
    }
}

/// <summary>
/// 货币类型枚举
/// </summary>
public enum CurrencyType
{
    /// <summary>
    /// 金币
    /// </summary>
    Gold = 0,

    /// <summary>
    /// 特殊物品（用于以物易物）
    /// </summary>
    Item = 2
}
