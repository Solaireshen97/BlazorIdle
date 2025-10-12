using System.ComponentModel.DataAnnotations;

namespace BlazorIdle.Server.Domain.Shop.ValueObjects;

/// <summary>
/// 价格值对象
/// </summary>
public class Price
{
    /// <summary>
    /// 货币类型
    /// </summary>
    [Required]
    public CurrencyType CurrencyType { get; set; } = CurrencyType.Gold;

    /// <summary>
    /// 金额/数量
    /// </summary>
    [Range(0, int.MaxValue)]
    public int Amount { get; set; }

    /// <summary>
    /// 货币/物品 ID（用于特殊货币和物品兑换）
    /// </summary>
    [MaxLength(100)]
    public string? CurrencyId { get; set; }
}

/// <summary>
/// 货币类型枚举
/// </summary>
public enum CurrencyType
{
    /// <summary>
    /// 金币（主货币）
    /// </summary>
    Gold = 0,

    /// <summary>
    /// 特殊货币（荣誉点、竞技场币等）
    /// </summary>
    SpecialCurrency = 1,

    /// <summary>
    /// 物品（以物易物）
    /// </summary>
    Item = 2
}
