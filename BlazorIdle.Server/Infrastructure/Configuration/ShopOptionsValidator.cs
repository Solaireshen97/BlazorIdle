using Microsoft.Extensions.Options;

namespace BlazorIdle.Server.Infrastructure.Configuration;

/// <summary>
/// 商店配置选项验证器
/// 在应用启动时验证配置的有效性，防止非法配置导致运行时错误
/// </summary>
public class ShopOptionsValidator : IValidateOptions<ShopOptions>
{
    /// <summary>
    /// 验证商店配置选项
    /// </summary>
    /// <param name="name">配置名称（通常为 null）</param>
    /// <param name="options">待验证的配置选项</param>
    /// <returns>验证结果，包含是否成功和错误信息列表</returns>
    public ValidateOptionsResult Validate(string? name, ShopOptions options)
    {
        var errors = new List<string>();

        // ═══════════════════════════════════
        // 缓存配置验证
        // ═══════════════════════════════════
        if (options.ShopDefinitionCacheMinutes < 0)
        {
            errors.Add($"ShopDefinitionCacheMinutes 不能为负数，当前值: {options.ShopDefinitionCacheMinutes}");
        }
        
        if (options.ShopItemsCacheMinutes < 0)
        {
            errors.Add($"ShopItemsCacheMinutes 不能为负数，当前值: {options.ShopItemsCacheMinutes}");
        }

        // ═══════════════════════════════════
        // 文件路径配置验证
        // ═══════════════════════════════════
        if (string.IsNullOrWhiteSpace(options.ConfigPath))
        {
            errors.Add("ConfigPath 不能为空");
        }
        
        if (string.IsNullOrWhiteSpace(options.ShopDefinitionsFile))
        {
            errors.Add("ShopDefinitionsFile 不能为空");
        }
        
        if (string.IsNullOrWhiteSpace(options.ShopItemsFile))
        {
            errors.Add("ShopItemsFile 不能为空");
        }

        // ═══════════════════════════════════
        // 商店配置验证
        // ═══════════════════════════════════
        if (options.DefaultRefreshIntervalSeconds < 0)
        {
            errors.Add($"DefaultRefreshIntervalSeconds 不能为负数，当前值: {options.DefaultRefreshIntervalSeconds}");
        }
        
        if (options.MaxShopNameLength <= 0)
        {
            errors.Add($"MaxShopNameLength 必须为正数，当前值: {options.MaxShopNameLength}");
        }
        
        if (options.MaxShopDescriptionLength <= 0)
        {
            errors.Add($"MaxShopDescriptionLength 必须为正数，当前值: {options.MaxShopDescriptionLength}");
        }

        // ═══════════════════════════════════
        // 商品配置验证
        // ═══════════════════════════════════
        if (options.MaxItemNameLength <= 0)
        {
            errors.Add($"MaxItemNameLength 必须为正数，当前值: {options.MaxItemNameLength}");
        }
        
        if (options.MaxItemDescriptionLength <= 0)
        {
            errors.Add($"MaxItemDescriptionLength 必须为正数，当前值: {options.MaxItemDescriptionLength}");
        }

        // ═══════════════════════════════════
        // 购买限制配置验证
        // ═══════════════════════════════════
        if (options.DailyResetSeconds < 0)
        {
            errors.Add($"DailyResetSeconds 不能为负数，当前值: {options.DailyResetSeconds}");
        }
        
        if (options.WeeklyResetSeconds < 0)
        {
            errors.Add($"WeeklyResetSeconds 不能为负数，当前值: {options.WeeklyResetSeconds}");
        }
        
        if (options.DefaultDailyLimit < 0)
        {
            errors.Add($"DefaultDailyLimit 不能为负数，当前值: {options.DefaultDailyLimit}");
        }
        
        if (options.DefaultWeeklyLimit < 0)
        {
            errors.Add($"DefaultWeeklyLimit 不能为负数，当前值: {options.DefaultWeeklyLimit}");
        }

        // ═══════════════════════════════════
        // 价格配置验证
        // ═══════════════════════════════════
        if (options.MinPriceAmount < 0)
        {
            errors.Add($"MinPriceAmount 不能为负数，当前值: {options.MinPriceAmount}");
        }
        
        if (options.MaxPriceAmount < 0)
        {
            errors.Add($"MaxPriceAmount 不能为负数，当前值: {options.MaxPriceAmount}");
        }
        
        if (options.MinPriceAmount > options.MaxPriceAmount)
        {
            errors.Add($"MinPriceAmount ({options.MinPriceAmount}) 不能大于 MaxPriceAmount ({options.MaxPriceAmount})");
        }

        // ═══════════════════════════════════
        // 验证配置
        // ═══════════════════════════════════
        if (options.MinLevelRequirement < 1)
        {
            errors.Add($"MinLevelRequirement 必须至少为 1，当前值: {options.MinLevelRequirement}");
        }
        
        if (options.MaxLevelRequirement < 1)
        {
            errors.Add($"MaxLevelRequirement 必须至少为 1，当前值: {options.MaxLevelRequirement}");
        }
        
        if (options.MinLevelRequirement > options.MaxLevelRequirement)
        {
            errors.Add($"MinLevelRequirement ({options.MinLevelRequirement}) 不能大于 MaxLevelRequirement ({options.MaxLevelRequirement})");
        }
        
        if (options.MinPurchaseQuantity < 1)
        {
            errors.Add($"MinPurchaseQuantity 必须至少为 1，当前值: {options.MinPurchaseQuantity}");
        }
        
        if (options.MaxPurchaseQuantity < 1)
        {
            errors.Add($"MaxPurchaseQuantity 必须至少为 1，当前值: {options.MaxPurchaseQuantity}");
        }
        
        if (options.MinPurchaseQuantity > options.MaxPurchaseQuantity)
        {
            errors.Add($"MinPurchaseQuantity ({options.MinPurchaseQuantity}) 不能大于 MaxPurchaseQuantity ({options.MaxPurchaseQuantity})");
        }

        // ═══════════════════════════════════
        // 查询配置验证
        // ═══════════════════════════════════
        if (options.DefaultPageSize < 1)
        {
            errors.Add($"DefaultPageSize 必须至少为 1，当前值: {options.DefaultPageSize}");
        }
        
        if (options.MaxPageSize < 1)
        {
            errors.Add($"MaxPageSize 必须至少为 1，当前值: {options.MaxPageSize}");
        }
        
        if (options.DefaultPageSize > options.MaxPageSize)
        {
            errors.Add($"DefaultPageSize ({options.DefaultPageSize}) 不能大于 MaxPageSize ({options.MaxPageSize})");
        }
        
        if (options.PurchaseHistoryDefaultDays < 1)
        {
            errors.Add($"PurchaseHistoryDefaultDays 必须至少为 1，当前值: {options.PurchaseHistoryDefaultDays}");
        }

        // ═══════════════════════════════════
        // 稀有度权重配置验证
        // ═══════════════════════════════════
        if (options.RarityOrderWeights == null)
        {
            errors.Add("RarityOrderWeights 不能为 null");
        }
        else if (options.RarityOrderWeights.Count == 0)
        {
            errors.Add("RarityOrderWeights 不能为空，至少需要配置一个稀有度权重");
        }
        else
        {
            // 验证权重值不能为负数
            var negativeWeights = options.RarityOrderWeights
                .Where(kv => kv.Value < 0)
                .ToList();
            
            if (negativeWeights.Any())
            {
                var negativeKeys = string.Join(", ", negativeWeights.Select(kv => $"{kv.Key}={kv.Value}"));
                errors.Add($"稀有度权重不能为负数，当前非法配置: {negativeKeys}");
            }
        }

        // 返回验证结果
        return errors.Any() 
            ? ValidateOptionsResult.Fail(errors)
            : ValidateOptionsResult.Success;
    }
}
