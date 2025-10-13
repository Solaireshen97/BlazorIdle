using BlazorIdle.Server.Domain.Shop;
using BlazorIdle.Server.Domain.Shop.Configuration;
using BlazorIdle.Server.Domain.Shop.ValueObjects;
using Microsoft.Extensions.Options;

namespace BlazorIdle.Server.Infrastructure.Configuration;

/// <summary>
/// 商店配置验证器实现
/// </summary>
public class ShopConfigurationValidator : IShopConfigurationValidator
{
    private readonly ShopOptions _options;
    private readonly ILogger<ShopConfigurationValidator> _logger;

    public ShopConfigurationValidator(
        IOptions<ShopOptions> options,
        ILogger<ShopConfigurationValidator> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public (bool isValid, List<string> errors) ValidateShopDefinitions(ShopDefinitionsConfig config)
    {
        var errors = new List<string>();

        if (config == null)
        {
            errors.Add("配置对象不能为空");
            return (false, errors);
        }

        if (config.Shops == null || config.Shops.Count == 0)
        {
            errors.Add("至少需要定义一个商店");
            return (false, errors);
        }

        var shopIds = new HashSet<string>();

        foreach (var shop in config.Shops)
        {
            // 验证ID
            if (string.IsNullOrWhiteSpace(shop.Id))
            {
                errors.Add("商店ID不能为空");
                continue;
            }

            if (!shopIds.Add(shop.Id))
            {
                errors.Add($"商店ID重复: {shop.Id}");
            }

            // 验证名称
            if (string.IsNullOrWhiteSpace(shop.Name))
            {
                errors.Add($"商店 '{shop.Id}' 的名称不能为空");
            }
            else if (shop.Name.Length > _options.MaxShopNameLength)
            {
                errors.Add($"商店 '{shop.Id}' 的名称超过最大长度 {_options.MaxShopNameLength}");
            }

            // 验证类型
            if (string.IsNullOrWhiteSpace(shop.Type))
            {
                errors.Add($"商店 '{shop.Id}' 的类型不能为空");
            }
            else if (!Enum.TryParse<ShopType>(shop.Type, out _))
            {
                errors.Add($"商店 '{shop.Id}' 的类型 '{shop.Type}' 无效。有效值: General, Special, Limited");
            }

            // 验证描述
            if (!string.IsNullOrWhiteSpace(shop.Description) && 
                shop.Description.Length > _options.MaxShopDescriptionLength)
            {
                errors.Add($"商店 '{shop.Id}' 的描述超过最大长度 {_options.MaxShopDescriptionLength}");
            }
        }

        var isValid = errors.Count == 0;
        if (!isValid)
        {
            _logger.LogWarning("商店定义配置验证失败: {ErrorCount} 个错误", errors.Count);
            foreach (var error in errors)
            {
                _logger.LogWarning("- {Error}", error);
            }
        }
        else
        {
            _logger.LogInformation("商店定义配置验证通过，共 {ShopCount} 个商店", config.Shops.Count);
        }

        return (isValid, errors);
    }

    public (bool isValid, List<string> errors) ValidateShopItems(
        ShopItemsConfig config, 
        ShopDefinitionsConfig shopDefinitions)
    {
        var errors = new List<string>();

        if (config == null)
        {
            errors.Add("配置对象不能为空");
            return (false, errors);
        }

        if (config.Items == null)
        {
            config.Items = new List<ShopItemData>();
            return (true, errors);
        }

        var shopIds = shopDefinitions.Shops.Select(s => s.Id).ToHashSet();
        var itemIds = new HashSet<string>();

        foreach (var item in config.Items)
        {
            // 验证ID
            if (string.IsNullOrWhiteSpace(item.Id))
            {
                errors.Add("商品ID不能为空");
                continue;
            }

            if (!itemIds.Add(item.Id))
            {
                errors.Add($"商品ID重复: {item.Id}");
            }

            // 验证商店引用
            if (string.IsNullOrWhiteSpace(item.ShopId))
            {
                errors.Add($"商品 '{item.Id}' 的商店ID不能为空");
            }
            else if (!shopIds.Contains(item.ShopId))
            {
                errors.Add($"商品 '{item.Id}' 引用的商店 '{item.ShopId}' 不存在");
            }

            // 验证物品定义ID
            if (string.IsNullOrWhiteSpace(item.ItemDefinitionId))
            {
                errors.Add($"商品 '{item.Id}' 的物品定义ID不能为空");
            }

            // 验证名称
            if (string.IsNullOrWhiteSpace(item.ItemName))
            {
                errors.Add($"商品 '{item.Id}' 的名称不能为空");
            }
            else if (item.ItemName.Length > _options.MaxItemNameLength)
            {
                errors.Add($"商品 '{item.Id}' 的名称超过最大长度 {_options.MaxItemNameLength}");
            }

            // 验证价格
            ValidatePrice(item.Id, item.Price, errors);

            // 验证购买限制
            ValidatePurchaseLimit(item.Id, item.PurchaseLimit, errors);

            // 验证等级要求
            if (item.MinLevel < _options.MinLevelRequirement)
            {
                errors.Add($"商品 '{item.Id}' 的最小等级 {item.MinLevel} 低于配置的最小值 {_options.MinLevelRequirement}");
            }

            if (item.MinLevel > _options.MaxLevelRequirement)
            {
                errors.Add($"商品 '{item.Id}' 的最小等级 {item.MinLevel} 高于配置的最大值 {_options.MaxLevelRequirement}");
            }

            // 验证稀有度
            if (!string.IsNullOrWhiteSpace(item.Rarity))
            {
                var validRarities = new[] { "Common", "Uncommon", "Rare", "Epic", "Legendary" };
                if (!validRarities.Contains(item.Rarity, StringComparer.OrdinalIgnoreCase))
                {
                    errors.Add($"商品 '{item.Id}' 的稀有度 '{item.Rarity}' 无效。有效值: Common, Uncommon, Rare, Epic, Legendary");
                }
            }
        }

        var isValid = errors.Count == 0;
        if (!isValid)
        {
            _logger.LogWarning("商品配置验证失败: {ErrorCount} 个错误", errors.Count);
            foreach (var error in errors)
            {
                _logger.LogWarning("- {Error}", error);
            }
        }
        else
        {
            _logger.LogInformation("商品配置验证通过，共 {ItemCount} 个商品", config.Items.Count);
        }

        return (isValid, errors);
    }

    private void ValidatePrice(string itemId, PriceData price, List<string> errors)
    {
        if (price == null)
        {
            errors.Add($"商品 '{itemId}' 的价格不能为空");
            return;
        }

        // 验证货币类型
        if (string.IsNullOrWhiteSpace(price.CurrencyType))
        {
            errors.Add($"商品 '{itemId}' 的货币类型不能为空");
        }
        else if (!Enum.TryParse<CurrencyType>(price.CurrencyType, out var currencyType))
        {
            errors.Add($"商品 '{itemId}' 的货币类型 '{price.CurrencyType}' 无效。有效值: Gold, Item");
        }
        else if (currencyType == CurrencyType.Item && string.IsNullOrWhiteSpace(price.CurrencyId))
        {
            errors.Add($"商品 '{itemId}' 使用物品货币时必须指定 CurrencyId");
        }

        // 验证金额
        if (price.Amount < _options.MinPriceAmount)
        {
            errors.Add($"商品 '{itemId}' 的价格 {price.Amount} 低于最小值 {_options.MinPriceAmount}");
        }

        if (price.Amount > _options.MaxPriceAmount)
        {
            errors.Add($"商品 '{itemId}' 的价格 {price.Amount} 超过最大值 {_options.MaxPriceAmount}");
        }
    }

    private void ValidatePurchaseLimit(string itemId, PurchaseLimitData limit, List<string> errors)
    {
        if (limit == null)
        {
            errors.Add($"商品 '{itemId}' 的购买限制不能为空");
            return;
        }

        // 验证限制类型
        if (string.IsNullOrWhiteSpace(limit.Type))
        {
            errors.Add($"商品 '{itemId}' 的限制类型不能为空");
        }
        else if (!Enum.TryParse<LimitType>(limit.Type, out var limitType))
        {
            errors.Add($"商品 '{itemId}' 的限制类型 '{limit.Type}' 无效。有效值: Unlimited, Daily, Weekly, PerCharacter");
        }
        else if (limitType != LimitType.Unlimited)
        {
            // 非无限制类型必须有最大购买次数
            if (!limit.MaxPurchases.HasValue || limit.MaxPurchases.Value <= 0)
            {
                errors.Add($"商品 '{itemId}' 的限制类型为 '{limit.Type}' 时，MaxPurchases 必须大于 0");
            }
        }
    }
}
