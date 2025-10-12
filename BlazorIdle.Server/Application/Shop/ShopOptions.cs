namespace BlazorIdle.Server.Application.Shop;

/// <summary>
/// 商店系统配置选项
/// </summary>
public class ShopOptions
{
    /// <summary>
    /// 配置节名称
    /// </summary>
    public const string SectionName = "Shop";

    /// <summary>
    /// 是否启用购买限制功能
    /// </summary>
    public bool EnablePurchaseLimit { get; set; } = true;

    /// <summary>
    /// 每日重置周期（秒），默认 86400 秒（24小时）
    /// </summary>
    public int DailyResetSeconds { get; set; } = 86400;

    /// <summary>
    /// 每周重置周期（秒），默认 604800 秒（7天）
    /// </summary>
    public int WeeklyResetSeconds { get; set; } = 604800;

    /// <summary>
    /// 默认缓存时间（分钟），预留用于未来缓存优化
    /// </summary>
    public int DefaultCacheMinutes { get; set; } = 5;

    /// <summary>
    /// 清理计划 Cron 表达式，默认每天凌晨清理过期计数器
    /// 格式: "分 时 日 月 周" (Cron 表达式)
    /// </summary>
    public string CleanupSchedule { get; set; } = "0 0 * * *";

    /// <summary>
    /// 购买历史查询默认页大小
    /// </summary>
    public int DefaultHistoryPageSize { get; set; } = 20;

    /// <summary>
    /// 购买历史查询最大页大小
    /// </summary>
    public int MaxHistoryPageSize { get; set; } = 100;

    /// <summary>
    /// 验证配置有效性
    /// </summary>
    public bool IsValid()
    {
        return DailyResetSeconds > 0 
            && WeeklyResetSeconds > 0 
            && DefaultCacheMinutes >= 0
            && DefaultHistoryPageSize > 0
            && MaxHistoryPageSize >= DefaultHistoryPageSize;
    }
}
