namespace BlazorIdle.Server.Domain.Shop.ValueObjects;

/// <summary>
/// 购买限制值对象
/// </summary>
public class PurchaseLimit
{
    /// <summary>
    /// 限制类型
    /// </summary>
    public PurchaseLimitType LimitType { get; set; } = PurchaseLimitType.None;

    /// <summary>
    /// 最大购买次数
    /// </summary>
    public int MaxPurchases { get; set; }

    /// <summary>
    /// 重置时间配置（UTC 小时，仅用于 Daily/Weekly）
    /// 示例: 0 表示每天 UTC 00:00 重置
    /// </summary>
    public int ResetHour { get; set; } = 0;
}

/// <summary>
/// 购买限制类型枚举
/// </summary>
public enum PurchaseLimitType
{
    /// <summary>
    /// 无限制
    /// </summary>
    None = 0,

    /// <summary>
    /// 每日限制
    /// </summary>
    Daily = 1,

    /// <summary>
    /// 每周限制（周一重置）
    /// </summary>
    Weekly = 2,

    /// <summary>
    /// 总计限制（永久）
    /// </summary>
    Total = 3
}
