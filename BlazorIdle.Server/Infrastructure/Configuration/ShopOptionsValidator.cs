using Microsoft.Extensions.Options;

namespace BlazorIdle.Server.Infrastructure.Configuration;

/// <summary>
/// 商店配置选项验证器
/// 在应用启动时验证配置参数的有效性
/// </summary>
public class ShopOptionsValidator : IValidateOptions<ShopOptions>
{
    public ValidateOptionsResult Validate(string? name, ShopOptions options)
    {
        var errors = new List<string>();

        // 验证缓存配置
        if (options.ShopDefinitionCacheMinutes < 0)
        {
            errors.Add($"{nameof(options.ShopDefinitionCacheMinutes)} 必须大于或等于 0");
        }
        
        if (options.ShopItemsCacheMinutes < 0)
        {
            errors.Add($"{nameof(options.ShopItemsCacheMinutes)} 必须大于或等于 0");
        }

        // 验证文件路径配置
        if (string.IsNullOrWhiteSpace(options.ConfigPath))
        {
            errors.Add($"{nameof(options.ConfigPath)} 不能为空");
        }
        
        if (string.IsNullOrWhiteSpace(options.ShopDefinitionsFile))
        {
            errors.Add($"{nameof(options.ShopDefinitionsFile)} 不能为空");
        }
        
        if (string.IsNullOrWhiteSpace(options.ShopItemsFile))
        {
            errors.Add($"{nameof(options.ShopItemsFile)} 不能为空");
        }

        // 验证商店配置
        if (options.DefaultRefreshIntervalSeconds < 60)
        {
            errors.Add($"{nameof(options.DefaultRefreshIntervalSeconds)} 必须大于或等于 60 秒");
        }
        
        if (options.MaxShopNameLength < 1 || options.MaxShopNameLength > 500)
        {
            errors.Add($"{nameof(options.MaxShopNameLength)} 必须在 1 到 500 之间");
        }
        
        if (options.MaxShopDescriptionLength < 1 || options.MaxShopDescriptionLength > 2000)
        {
            errors.Add($"{nameof(options.MaxShopDescriptionLength)} 必须在 1 到 2000 之间");
        }

        // 验证商品配置
        if (options.MaxItemNameLength < 1 || options.MaxItemNameLength > 500)
        {
            errors.Add($"{nameof(options.MaxItemNameLength)} 必须在 1 到 500 之间");
        }
        
        if (options.MaxItemDescriptionLength < 1 || options.MaxItemDescriptionLength > 5000)
        {
            errors.Add($"{nameof(options.MaxItemDescriptionLength)} 必须在 1 到 5000 之间");
        }

        // 验证购买限制配置
        if (options.DailyResetSeconds < 3600)
        {
            errors.Add($"{nameof(options.DailyResetSeconds)} 必须大于或等于 3600 秒（1 小时）");
        }
        
        if (options.WeeklyResetSeconds < 86400)
        {
            errors.Add($"{nameof(options.WeeklyResetSeconds)} 必须大于或等于 86400 秒（1 天）");
        }
        
        if (options.DefaultDailyLimit < 1)
        {
            errors.Add($"{nameof(options.DefaultDailyLimit)} 必须大于 0");
        }
        
        if (options.DefaultWeeklyLimit < 1)
        {
            errors.Add($"{nameof(options.DefaultWeeklyLimit)} 必须大于 0");
        }

        // 验证价格配置
        if (options.MinPriceAmount < 0)
        {
            errors.Add($"{nameof(options.MinPriceAmount)} 必须大于或等于 0");
        }
        
        if (options.MaxPriceAmount <= options.MinPriceAmount)
        {
            errors.Add($"{nameof(options.MaxPriceAmount)} 必须大于 {nameof(options.MinPriceAmount)}");
        }

        // 验证等级配置
        if (options.MinLevelRequirement < 1)
        {
            errors.Add($"{nameof(options.MinLevelRequirement)} 必须大于或等于 1");
        }
        
        if (options.MaxLevelRequirement <= options.MinLevelRequirement)
        {
            errors.Add($"{nameof(options.MaxLevelRequirement)} 必须大于 {nameof(options.MinLevelRequirement)}");
        }

        // 验证购买数量配置
        if (options.MinPurchaseQuantity < 1)
        {
            errors.Add($"{nameof(options.MinPurchaseQuantity)} 必须大于或等于 1");
        }
        
        if (options.MaxPurchaseQuantity <= options.MinPurchaseQuantity)
        {
            errors.Add($"{nameof(options.MaxPurchaseQuantity)} 必须大于 {nameof(options.MinPurchaseQuantity)}");
        }

        // 验证查询配置
        if (options.DefaultPageSize < 1 || options.DefaultPageSize > options.MaxPageSize)
        {
            errors.Add($"{nameof(options.DefaultPageSize)} 必须在 1 到 {nameof(options.MaxPageSize)} 之间");
        }
        
        if (options.MaxPageSize < 10 || options.MaxPageSize > 1000)
        {
            errors.Add($"{nameof(options.MaxPageSize)} 必须在 10 到 1000 之间");
        }
        
        if (options.PurchaseHistoryDefaultDays < 1)
        {
            errors.Add($"{nameof(options.PurchaseHistoryDefaultDays)} 必须大于 0");
        }

        if (errors.Any())
        {
            return ValidateOptionsResult.Fail(errors);
        }

        return ValidateOptionsResult.Success;
    }
}
