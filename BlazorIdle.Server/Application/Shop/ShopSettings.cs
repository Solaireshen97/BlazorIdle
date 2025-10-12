namespace BlazorIdle.Server.Application.Shop;

/// <summary>
/// 商店系统配置
/// </summary>
public class ShopSettings
{
    /// <summary>
    /// 配置节名称
    /// </summary>
    public const string SectionName = "Shop";

    /// <summary>
    /// 每日购买限制重置周期（秒）
    /// 默认：86400 秒（24小时）
    /// </summary>
    public int DailyResetPeriodSeconds { get; set; } = 86400;

    /// <summary>
    /// 每周购买限制重置周期（秒）
    /// 默认：604800 秒（7天）
    /// </summary>
    public int WeeklyResetPeriodSeconds { get; set; } = 604800;

    /// <summary>
    /// 购买历史默认每页数量
    /// 默认：20
    /// </summary>
    public int DefaultPageSize { get; set; } = 20;

    /// <summary>
    /// 购买历史最大每页数量
    /// 默认：100
    /// </summary>
    public int MaxPageSize { get; set; } = 100;

    /// <summary>
    /// 商店定义缓存时间（分钟）
    /// 默认：5 分钟
    /// </summary>
    public int ShopDefinitionCacheMinutes { get; set; } = 5;

    /// <summary>
    /// 商品列表缓存时间（分钟）
    /// 默认：2 分钟
    /// </summary>
    public int ShopItemsCacheMinutes { get; set; } = 2;

    /// <summary>
    /// 是否启用购买限制
    /// 默认：true
    /// </summary>
    public bool EnablePurchaseLimit { get; set; } = true;

    /// <summary>
    /// 是否启用缓存
    /// 默认：true
    /// </summary>
    public bool EnableCache { get; set; } = true;

    /// <summary>
    /// 购买计数器清理计划（Cron 表达式）
    /// 默认："0 0 * * *"（每天凌晨）
    /// </summary>
    public string CleanupSchedule { get; set; } = "0 0 * * *";
}
