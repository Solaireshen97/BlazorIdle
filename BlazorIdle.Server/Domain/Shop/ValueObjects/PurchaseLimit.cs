using System.ComponentModel.DataAnnotations;

namespace BlazorIdle.Server.Domain.Shop.ValueObjects;

/// <summary>
/// 购买限制值对象
/// </summary>
public class PurchaseLimit
{
    /// <summary>
    /// 限制类型
    /// </summary>
    [Required]
    public LimitType Type { get; set; } = LimitType.Unlimited;

    /// <summary>
    /// 最大购买次数
    /// </summary>
    public int MaxPurchases { get; set; }

    /// <summary>
    /// 重置周期（秒）
    /// </summary>
    public int? ResetPeriodSeconds { get; set; }

    /// <summary>
    /// 检查是否无限制
    /// </summary>
    public bool IsUnlimited() => Type == LimitType.Unlimited;

    /// <summary>
    /// 验证限制是否有效
    /// </summary>
    public bool IsValid()
    {
        if (Type == LimitType.Unlimited) return true;
        if (MaxPurchases <= 0) return false;
        return true;
    }
}

/// <summary>
/// 限制类型枚举
/// </summary>
public enum LimitType
{
    /// <summary>
    /// 无限制
    /// </summary>
    Unlimited = 0,

    /// <summary>
    /// 每个角色限购（终生）
    /// </summary>
    PerCharacter = 1,

    /// <summary>
    /// 每日限购
    /// </summary>
    Daily = 2,

    /// <summary>
    /// 每周限购
    /// </summary>
    Weekly = 3,

    /// <summary>
    /// 自定义周期限购
    /// </summary>
    CustomPeriod = 4
}
