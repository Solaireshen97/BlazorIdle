using System.Text.Json.Serialization;

namespace BlazorIdle.Server.Domain.Shop.ValueObjects;

/// <summary>
/// 价格值对象
/// </summary>
public class Price
{
    /// <summary>
    /// 货币类型
    /// </summary>
    [JsonPropertyName("currencyType")]
    public CurrencyType CurrencyType { get; set; } = CurrencyType.Gold;

    /// <summary>
    /// 数量
    /// </summary>
    [JsonPropertyName("amount")]
    public int Amount { get; set; }

    /// <summary>
    /// 物品ID（用于物品交换）
    /// </summary>
    [JsonPropertyName("itemId")]
    public string? ItemId { get; set; }

    /// <summary>
    /// 物品数量（用于物品交换）
    /// </summary>
    [JsonPropertyName("itemQuantity")]
    public int ItemQuantity { get; set; }
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
    /// 特殊货币（如声望点、荣誉点等）
    /// </summary>
    SpecialCurrency = 1,

    /// <summary>
    /// 物品兑换
    /// </summary>
    ItemExchange = 2
}
