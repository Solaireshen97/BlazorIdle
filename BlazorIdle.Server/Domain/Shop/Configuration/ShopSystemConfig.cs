namespace BlazorIdle.Server.Domain.Shop.Configuration;

/// <summary>
/// 商店系统配置常量
/// 注意: 这些是默认的后备值，实际配置应从 appsettings.json 的 Shop 节读取
/// 优先级: appsettings.json > ShopOptions > ShopSystemConfig (fallback)
/// </summary>
public static class ShopSystemConfig
{
    /// <summary>
    /// 商店配置
    /// </summary>
    public static class ShopConfig
    {
        /// <summary>默认刷新间隔（秒）</summary>
        public const int DefaultRefreshIntervalSeconds = 3600;
        
        /// <summary>商店名称最大长度</summary>
        public const int MaxShopNameLength = 50;
        
        /// <summary>商店描述最大长度</summary>
        public const int MaxShopDescriptionLength = 200;
    }
    
    /// <summary>
    /// 商品配置
    /// </summary>
    public static class ItemConfig
    {
        /// <summary>商品名称最大长度</summary>
        public const int MaxItemNameLength = 100;
        
        /// <summary>商品描述最大长度</summary>
        public const int MaxItemDescriptionLength = 500;
        
        /// <summary>无限库存标识</summary>
        public const int UnlimitedStock = -1;
    }
    
    /// <summary>
    /// 购买限制配置
    /// </summary>
    public static class PurchaseLimitConfig
    {
        /// <summary>每日重置周期（秒）</summary>
        public const int DailyResetSeconds = 86400;
        
        /// <summary>每周重置周期（秒）</summary>
        public const int WeeklyResetSeconds = 604800;
        
        /// <summary>默认每日限购数量</summary>
        public const int DefaultDailyLimit = 10;
        
        /// <summary>默认每周限购数量</summary>
        public const int DefaultWeeklyLimit = 5;
    }
    
    /// <summary>
    /// 价格配置
    /// </summary>
    public static class PriceConfig
    {
        /// <summary>最小价格金额</summary>
        public const int MinPriceAmount = 1;
        
        /// <summary>最大价格金额</summary>
        public const int MaxPriceAmount = 1000000;
    }
    
    /// <summary>
    /// 购买验证配置
    /// </summary>
    public static class ValidationConfig
    {
        /// <summary>最小等级要求</summary>
        public const int MinLevelRequirement = 1;
        
        /// <summary>最大等级要求</summary>
        public const int MaxLevelRequirement = 100;
        
        /// <summary>最小购买数量</summary>
        public const int MinPurchaseQuantity = 1;
        
        /// <summary>最大购买数量（单次）</summary>
        public const int MaxPurchaseQuantity = 999;
    }
    
    /// <summary>
    /// 缓存配置
    /// </summary>
    public static class CacheConfig
    {
        /// <summary>商店定义缓存过期时间（分钟）</summary>
        public const int ShopDefinitionCacheMinutes = 60;
        
        /// <summary>商品列表缓存过期时间（分钟）</summary>
        public const int ShopItemsCacheMinutes = 30;
        
        /// <summary>是否启用缓存</summary>
        public const bool EnableCaching = true;
    }
    
    /// <summary>
    /// 查询配置
    /// </summary>
    public static class QueryConfig
    {
        /// <summary>默认分页大小</summary>
        public const int DefaultPageSize = 20;
        
        /// <summary>最大分页大小</summary>
        public const int MaxPageSize = 100;
        
        /// <summary>购买历史默认查询天数</summary>
        public const int PurchaseHistoryDefaultDays = 30;
    }
}
