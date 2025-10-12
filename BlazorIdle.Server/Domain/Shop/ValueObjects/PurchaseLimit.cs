using System.Text.Json.Serialization;

namespace BlazorIdle.Server.Domain.Shop.ValueObjects;

/// <summary>
/// 购买限制值对象
/// </summary>
public class PurchaseLimit
{
    /// <summary>
    /// 限制类型
    /// </summary>
    [JsonPropertyName("limitType")]
    public LimitType LimitType { get; set; } = LimitType.None;

    /// <summary>
    /// 最大购买次数
    /// </summary>
    [JsonPropertyName("maxPurchases")]
    public int MaxPurchases { get; set; }

    /// <summary>
    /// 重置周期（秒）
    /// </summary>
    [JsonPropertyName("resetPeriodSeconds")]
    public int? ResetPeriodSeconds { get; set; }
}

/// <summary>
/// 限制类型枚举
/// </summary>
public enum LimitType
{
    /// <summary>
    /// 无限制
    /// </summary>
    None = 0,

    /// <summary>
    /// 总限制（永久）
    /// </summary>
    Total = 1,

    /// <summary>
    /// 日限制
    /// </summary>
    Daily = 2,

    /// <summary>
    /// 周限制
    /// </summary>
    Weekly = 3,

    /// <summary>
    /// 月限制
    /// </summary>
    Monthly = 4,

    /// <summary>
    /// 自定义周期限制
    /// </summary>
    Custom = 5
}
