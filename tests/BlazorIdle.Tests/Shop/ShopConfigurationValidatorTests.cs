using BlazorIdle.Server.Domain.Shop.Configuration;
using BlazorIdle.Server.Infrastructure.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace BlazorIdle.Tests.Shop;

/// <summary>
/// 商店配置验证器测试
/// </summary>
public class ShopConfigurationValidatorTests
{
    private readonly ShopConfigurationValidator _validator;
    private readonly ShopOptions _options;

    public ShopConfigurationValidatorTests()
    {
        _options = new ShopOptions
        {
            MaxShopNameLength = 50,
            MaxShopDescriptionLength = 200,
            MaxItemNameLength = 100,
            MaxItemDescriptionLength = 500,
            MinPriceAmount = 1,
            MaxPriceAmount = 1000000,
            MinLevelRequirement = 1,
            MaxLevelRequirement = 100
        };

        var serviceProvider = new ServiceCollection()
            .AddLogging()
            .BuildServiceProvider();

        var logger = serviceProvider.GetRequiredService<ILogger<ShopConfigurationValidator>>();
        var optionsWrapper = Options.Create(_options);
        
        _validator = new ShopConfigurationValidator(optionsWrapper, logger);
    }

    #region ShopDefinitions 验证测试

    [Fact]
    public void ValidateShopDefinitions_WithValidConfig_ShouldPass()
    {
        // Arrange
        var config = new ShopDefinitionsConfig
        {
            Shops = new List<ShopDefinitionData>
            {
                new ShopDefinitionData
                {
                    Id = "shop_1",
                    Name = "Test Shop",
                    Type = "General",
                    Icon = "🏪",
                    Description = "A test shop",
                    IsEnabled = true
                }
            }
        };

        // Act
        var (isValid, errors) = _validator.ValidateShopDefinitions(config);

        // Assert
        Assert.True(isValid);
        Assert.Empty(errors);
    }

    [Fact]
    public void ValidateShopDefinitions_WithNullConfig_ShouldFail()
    {
        // Act
        var (isValid, errors) = _validator.ValidateShopDefinitions(null);

        // Assert
        Assert.False(isValid);
        Assert.Contains(errors, e => e.Contains("配置对象不能为空"));
    }

    [Fact]
    public void ValidateShopDefinitions_WithEmptyShops_ShouldFail()
    {
        // Arrange
        var config = new ShopDefinitionsConfig { Shops = new List<ShopDefinitionData>() };

        // Act
        var (isValid, errors) = _validator.ValidateShopDefinitions(config);

        // Assert
        Assert.False(isValid);
        Assert.Contains(errors, e => e.Contains("至少需要定义一个商店"));
    }

    [Fact]
    public void ValidateShopDefinitions_WithDuplicateIds_ShouldFail()
    {
        // Arrange
        var config = new ShopDefinitionsConfig
        {
            Shops = new List<ShopDefinitionData>
            {
                new ShopDefinitionData { Id = "shop_1", Name = "Shop 1", Type = "General" },
                new ShopDefinitionData { Id = "shop_1", Name = "Shop 2", Type = "General" }
            }
        };

        // Act
        var (isValid, errors) = _validator.ValidateShopDefinitions(config);

        // Assert
        Assert.False(isValid);
        Assert.Contains(errors, e => e.Contains("ID重复"));
    }

    [Fact]
    public void ValidateShopDefinitions_WithEmptyName_ShouldFail()
    {
        // Arrange
        var config = new ShopDefinitionsConfig
        {
            Shops = new List<ShopDefinitionData>
            {
                new ShopDefinitionData { Id = "shop_1", Name = "", Type = "General" }
            }
        };

        // Act
        var (isValid, errors) = _validator.ValidateShopDefinitions(config);

        // Assert
        Assert.False(isValid);
        Assert.Contains(errors, e => e.Contains("名称不能为空"));
    }

    [Fact]
    public void ValidateShopDefinitions_WithNameTooLong_ShouldFail()
    {
        // Arrange
        var config = new ShopDefinitionsConfig
        {
            Shops = new List<ShopDefinitionData>
            {
                new ShopDefinitionData 
                { 
                    Id = "shop_1", 
                    Name = new string('A', _options.MaxShopNameLength + 1), 
                    Type = "General" 
                }
            }
        };

        // Act
        var (isValid, errors) = _validator.ValidateShopDefinitions(config);

        // Assert
        Assert.False(isValid);
        Assert.Contains(errors, e => e.Contains("名称超过最大长度"));
    }

    [Fact]
    public void ValidateShopDefinitions_WithInvalidType_ShouldFail()
    {
        // Arrange
        var config = new ShopDefinitionsConfig
        {
            Shops = new List<ShopDefinitionData>
            {
                new ShopDefinitionData { Id = "shop_1", Name = "Shop", Type = "InvalidType" }
            }
        };

        // Act
        var (isValid, errors) = _validator.ValidateShopDefinitions(config);

        // Assert
        Assert.False(isValid);
        Assert.Contains(errors, e => e.Contains("类型") && e.Contains("无效"));
    }

    #endregion

    #region ShopItems 验证测试

    [Fact]
    public void ValidateShopItems_WithValidConfig_ShouldPass()
    {
        // Arrange
        var shopDefinitions = new ShopDefinitionsConfig
        {
            Shops = new List<ShopDefinitionData>
            {
                new ShopDefinitionData { Id = "shop_1", Name = "Shop", Type = "General" }
            }
        };

        var config = new ShopItemsConfig
        {
            Items = new List<ShopItemData>
            {
                new ShopItemData
                {
                    Id = "item_1",
                    ShopId = "shop_1",
                    ItemDefinitionId = "potion",
                    ItemName = "Potion",
                    ItemIcon = "🧪",
                    Price = new PriceData { CurrencyType = "Gold", Amount = 100 },
                    PurchaseLimit = new PurchaseLimitData { Type = "Unlimited" },
                    MinLevel = 1,
                    ItemCategory = "Consumable",
                    Rarity = "Common",
                    IsEnabled = true
                }
            }
        };

        // Act
        var (isValid, errors) = _validator.ValidateShopItems(config, shopDefinitions);

        // Assert
        Assert.True(isValid);
        Assert.Empty(errors);
    }

    [Fact]
    public void ValidateShopItems_WithDuplicateIds_ShouldFail()
    {
        // Arrange
        var shopDefinitions = new ShopDefinitionsConfig
        {
            Shops = new List<ShopDefinitionData>
            {
                new ShopDefinitionData { Id = "shop_1", Name = "Shop", Type = "General" }
            }
        };

        var config = new ShopItemsConfig
        {
            Items = new List<ShopItemData>
            {
                new ShopItemData 
                { 
                    Id = "item_1", 
                    ShopId = "shop_1", 
                    ItemDefinitionId = "potion1",
                    ItemName = "Potion 1",
                    Price = new PriceData { CurrencyType = "Gold", Amount = 100 },
                    PurchaseLimit = new PurchaseLimitData { Type = "Unlimited" }
                },
                new ShopItemData 
                { 
                    Id = "item_1", 
                    ShopId = "shop_1", 
                    ItemDefinitionId = "potion2",
                    ItemName = "Potion 2",
                    Price = new PriceData { CurrencyType = "Gold", Amount = 100 },
                    PurchaseLimit = new PurchaseLimitData { Type = "Unlimited" }
                }
            }
        };

        // Act
        var (isValid, errors) = _validator.ValidateShopItems(config, shopDefinitions);

        // Assert
        Assert.False(isValid);
        Assert.Contains(errors, e => e.Contains("ID重复"));
    }

    [Fact]
    public void ValidateShopItems_WithInvalidShopReference_ShouldFail()
    {
        // Arrange
        var shopDefinitions = new ShopDefinitionsConfig
        {
            Shops = new List<ShopDefinitionData>
            {
                new ShopDefinitionData { Id = "shop_1", Name = "Shop", Type = "General" }
            }
        };

        var config = new ShopItemsConfig
        {
            Items = new List<ShopItemData>
            {
                new ShopItemData 
                { 
                    Id = "item_1", 
                    ShopId = "shop_999", // Non-existent shop
                    ItemDefinitionId = "potion",
                    ItemName = "Potion",
                    Price = new PriceData { CurrencyType = "Gold", Amount = 100 },
                    PurchaseLimit = new PurchaseLimitData { Type = "Unlimited" }
                }
            }
        };

        // Act
        var (isValid, errors) = _validator.ValidateShopItems(config, shopDefinitions);

        // Assert
        Assert.False(isValid);
        Assert.Contains(errors, e => e.Contains("引用的商店") && e.Contains("不存在"));
    }

    [Fact]
    public void ValidateShopItems_WithInvalidPrice_ShouldFail()
    {
        // Arrange
        var shopDefinitions = new ShopDefinitionsConfig
        {
            Shops = new List<ShopDefinitionData>
            {
                new ShopDefinitionData { Id = "shop_1", Name = "Shop", Type = "General" }
            }
        };

        var config = new ShopItemsConfig
        {
            Items = new List<ShopItemData>
            {
                new ShopItemData 
                { 
                    Id = "item_1", 
                    ShopId = "shop_1",
                    ItemDefinitionId = "potion",
                    ItemName = "Potion",
                    Price = new PriceData { CurrencyType = "Gold", Amount = 0 }, // Invalid amount
                    PurchaseLimit = new PurchaseLimitData { Type = "Unlimited" }
                }
            }
        };

        // Act
        var (isValid, errors) = _validator.ValidateShopItems(config, shopDefinitions);

        // Assert
        Assert.False(isValid);
        Assert.Contains(errors, e => e.Contains("价格") && e.Contains("低于最小值"));
    }

    [Fact]
    public void ValidateShopItems_WithItemCurrencyButNoCurrencyId_ShouldFail()
    {
        // Arrange
        var shopDefinitions = new ShopDefinitionsConfig
        {
            Shops = new List<ShopDefinitionData>
            {
                new ShopDefinitionData { Id = "shop_1", Name = "Shop", Type = "General" }
            }
        };

        var config = new ShopItemsConfig
        {
            Items = new List<ShopItemData>
            {
                new ShopItemData 
                { 
                    Id = "item_1", 
                    ShopId = "shop_1",
                    ItemDefinitionId = "potion",
                    ItemName = "Potion",
                    Price = new PriceData { CurrencyType = "Item", Amount = 10, CurrencyId = null },
                    PurchaseLimit = new PurchaseLimitData { Type = "Unlimited" }
                }
            }
        };

        // Act
        var (isValid, errors) = _validator.ValidateShopItems(config, shopDefinitions);

        // Assert
        Assert.False(isValid);
        Assert.Contains(errors, e => e.Contains("物品货币") && e.Contains("CurrencyId"));
    }

    [Fact]
    public void ValidateShopItems_WithInvalidPurchaseLimit_ShouldFail()
    {
        // Arrange
        var shopDefinitions = new ShopDefinitionsConfig
        {
            Shops = new List<ShopDefinitionData>
            {
                new ShopDefinitionData { Id = "shop_1", Name = "Shop", Type = "General" }
            }
        };

        var config = new ShopItemsConfig
        {
            Items = new List<ShopItemData>
            {
                new ShopItemData 
                { 
                    Id = "item_1", 
                    ShopId = "shop_1",
                    ItemDefinitionId = "potion",
                    ItemName = "Potion",
                    Price = new PriceData { CurrencyType = "Gold", Amount = 100 },
                    PurchaseLimit = new PurchaseLimitData { Type = "Daily", MaxPurchases = null } // Missing MaxPurchases
                }
            }
        };

        // Act
        var (isValid, errors) = _validator.ValidateShopItems(config, shopDefinitions);

        // Assert
        Assert.False(isValid);
        Assert.Contains(errors, e => e.Contains("MaxPurchases") && e.Contains("必须大于"));
    }

    [Fact]
    public void ValidateShopItems_WithInvalidLevelRequirement_ShouldFail()
    {
        // Arrange
        var shopDefinitions = new ShopDefinitionsConfig
        {
            Shops = new List<ShopDefinitionData>
            {
                new ShopDefinitionData { Id = "shop_1", Name = "Shop", Type = "General" }
            }
        };

        var config = new ShopItemsConfig
        {
            Items = new List<ShopItemData>
            {
                new ShopItemData 
                { 
                    Id = "item_1", 
                    ShopId = "shop_1",
                    ItemDefinitionId = "potion",
                    ItemName = "Potion",
                    Price = new PriceData { CurrencyType = "Gold", Amount = 100 },
                    PurchaseLimit = new PurchaseLimitData { Type = "Unlimited" },
                    MinLevel = 999 // Invalid level
                }
            }
        };

        // Act
        var (isValid, errors) = _validator.ValidateShopItems(config, shopDefinitions);

        // Assert
        Assert.False(isValid);
        Assert.Contains(errors, e => e.Contains("最小等级") && e.Contains("高于"));
    }

    [Fact]
    public void ValidateShopItems_WithInvalidRarity_ShouldFail()
    {
        // Arrange
        var shopDefinitions = new ShopDefinitionsConfig
        {
            Shops = new List<ShopDefinitionData>
            {
                new ShopDefinitionData { Id = "shop_1", Name = "Shop", Type = "General" }
            }
        };

        var config = new ShopItemsConfig
        {
            Items = new List<ShopItemData>
            {
                new ShopItemData 
                { 
                    Id = "item_1", 
                    ShopId = "shop_1",
                    ItemDefinitionId = "potion",
                    ItemName = "Potion",
                    Price = new PriceData { CurrencyType = "Gold", Amount = 100 },
                    PurchaseLimit = new PurchaseLimitData { Type = "Unlimited" },
                    Rarity = "InvalidRarity"
                }
            }
        };

        // Act
        var (isValid, errors) = _validator.ValidateShopItems(config, shopDefinitions);

        // Assert
        Assert.False(isValid);
        Assert.Contains(errors, e => e.Contains("稀有度") && e.Contains("无效"));
    }

    #endregion
}
