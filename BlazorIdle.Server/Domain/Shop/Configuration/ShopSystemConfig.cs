namespace BlazorIdle.Server.Domain.Shop.Configuration;

/// <summary>
/// 商店系统配置
/// 集中管理商店系统的常量和配置参数
/// 注意：这些值现在从 appsettings.json 的 Shop 配置节读取
/// 此类保留用于向后兼容，提供默认值
/// </summary>
public static class ShopSystemConfig
{
    /// <summary>
    /// 商店配置
    /// </summary>
    public static class ShopConfig
    {
        /// <summary>默认刷新间隔（秒）- 默认值，实际值从配置读取</summary>
        public const int DefaultRefreshIntervalSeconds = 3600;
        
        /// <summary>商店名称最大长度 - 默认值，实际值从配置读取</summary>
        public const int MaxShopNameLength = 50;
        
        /// <summary>商店描述最大长度 - 默认值，实际值从配置读取</summary>
        public const int MaxShopDescriptionLength = 200;
    }
    
    /// <summary>
    /// 商品配置
    /// </summary>
    public static class ItemConfig
    {
        /// <summary>商品名称最大长度 - 默认值，实际值从配置读取</summary>
        public const int MaxItemNameLength = 100;
        
        /// <summary>商品描述最大长度 - 默认值，实际值从配置读取</summary>
        public const int MaxItemDescriptionLength = 500;
        
        /// <summary>无限库存标识 - 默认值，实际值从配置读取</summary>
        public const int UnlimitedStock = -1;
    }
    
    /// <summary>
    /// 购买限制配置
    /// </summary>
    public static class PurchaseLimitConfig
    {
        /// <summary>每日重置周期（秒）- 默认值，实际值从配置读取</summary>
        public const int DailyResetSeconds = 86400;
        
        /// <summary>每周重置周期（秒）- 默认值，实际值从配置读取</summary>
        public const int WeeklyResetSeconds = 604800;
        
        /// <summary>默认每日限购数量 - 默认值，实际值从配置读取</summary>
        public const int DefaultDailyLimit = 10;
        
        /// <summary>默认每周限购数量 - 默认值，实际值从配置读取</summary>
        public const int DefaultWeeklyLimit = 5;
    }
    
    /// <summary>
    /// 价格配置
    /// </summary>
    public static class PriceConfig
    {
        /// <summary>最小价格金额 - 默认值，实际值从配置读取</summary>
        public const int MinPriceAmount = 1;
        
        /// <summary>最大价格金额 - 默认值，实际值从配置读取</summary>
        public const int MaxPriceAmount = 1000000;
    }
    
    /// <summary>
    /// 购买验证配置
    /// </summary>
    public static class ValidationConfig
    {
        /// <summary>最小等级要求 - 默认值，实际值从配置读取</summary>
        public const int MinLevelRequirement = 1;
        
        /// <summary>最大等级要求 - 默认值，实际值从配置读取</summary>
        public const int MaxLevelRequirement = 100;
        
        /// <summary>最小购买数量 - 默认值，实际值从配置读取</summary>
        public const int MinPurchaseQuantity = 1;
        
        /// <summary>最大购买数量（单次）- 默认值，实际值从配置读取</summary>
        public const int MaxPurchaseQuantity = 999;
    }
    
    /// <summary>
    /// 缓存配置
    /// </summary>
    public static class CacheConfig
    {
        /// <summary>商店定义缓存过期时间（分钟）- 默认值，实际值从配置读取</summary>
        public const int ShopDefinitionCacheMinutes = 60;
        
        /// <summary>商品列表缓存过期时间（分钟）- 默认值，实际值从配置读取</summary>
        public const int ShopItemsCacheMinutes = 30;
        
        /// <summary>是否启用缓存 - 默认值，实际值从配置读取</summary>
        public const bool EnableCaching = true;
    }
    
    /// <summary>
    /// 查询配置
    /// </summary>
    public static class QueryConfig
    {
        /// <summary>默认分页大小 - 默认值，实际值从配置读取</summary>
        public const int DefaultPageSize = 20;
        
        /// <summary>最大分页大小 - 默认值，实际值从配置读取</summary>
        public const int MaxPageSize = 100;
        
        /// <summary>购买历史默认查询天数 - 默认值，实际值从配置读取</summary>
        public const int PurchaseHistoryDefaultDays = 30;
    }
}
