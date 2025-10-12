namespace BlazorIdle.Server.Domain.Shop;

/// <summary>
/// 商店类型枚举
/// </summary>
public enum ShopType
{
    /// <summary>
    /// 通用商店（所有玩家可见）
    /// </summary>
    General = 0,

    /// <summary>
    /// 特殊商店（需要解锁）
    /// </summary>
    Special = 1,

    /// <summary>
    /// 限时商店（限定时间开放）
    /// </summary>
    Limited = 2,

    /// <summary>
    /// 个人商店（角色专属）
    /// </summary>
    Personal = 3
}
