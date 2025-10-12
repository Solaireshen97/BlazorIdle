using BlazorIdle.Server.Application.Shop;
using BlazorIdle.Server.Domain.Shop;
using BlazorIdle.Server.Domain.Shop.ValueObjects;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace BlazorIdle.Tests.Shop;

/// <summary>
/// 商店缓存服务测试
/// </summary>
public class ShopCacheTests
{
    private readonly IShopCacheService _cacheService;
    private readonly IMemoryCache _cache;

    public ShopCacheTests()
    {
        var serviceProvider = new ServiceCollection()
            .AddMemoryCache()
            .AddLogging()
            .BuildServiceProvider();
            
        _cache = serviceProvider.GetRequiredService<IMemoryCache>();
        var logger = serviceProvider.GetRequiredService<ILogger<ShopCacheService>>();
        
        // 创建配置（启用缓存用于测试）
        var configBuilder = new ConfigurationBuilder();
        configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
        {
            { "Shop:EnableCaching", "true" },
            { "Shop:ShopDefinitionCacheMinutes", "60" },
            { "Shop:ShopItemsCacheMinutes", "30" }
        });
        var configuration = configBuilder.Build();
        
        _cacheService = new ShopCacheService(_cache, logger, configuration);
    }

    [Fact]
    public async Task GetShopsAsync_WhenCacheEmpty_ShouldReturnNull()
    {
        // Act
        var result = await _cacheService.GetShopsAsync();

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task SetShops_AndGetShops_ShouldReturnCachedData()
    {
        // Arrange
        var shops = new List<ShopDefinition>
        {
            new ShopDefinition
            {
                Id = "test_shop",
                Name = "测试商店",
                Type = ShopType.General,
                Icon = "🏪",
                Description = "测试",
                IsEnabled = true,
                SortOrder = 1
            }
        };

        // Act
        _cacheService.SetShops(shops);
        var result = await _cacheService.GetShopsAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal("test_shop", result[0].Id);
    }

    [Fact]
    public async Task GetShopItemsAsync_WhenCacheEmpty_ShouldReturnNull()
    {
        // Act
        var result = await _cacheService.GetShopItemsAsync("test_shop");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task SetShopItems_AndGetShopItems_ShouldReturnCachedData()
    {
        // Arrange
        var items = new List<ShopItem>
        {
            new ShopItem
            {
                Id = "test_item",
                ShopId = "test_shop",
                ItemDefinitionId = "item_def",
                ItemName = "测试物品",
                ItemIcon = "🎁",
                PriceJson = "{\"CurrencyType\":1,\"Amount\":100}",
                PurchaseLimitJson = "{\"Type\":0}",
                StockQuantity = -1,
                MinLevel = 1,
                IsEnabled = true,
                SortOrder = 1
            }
        };

        // Act
        _cacheService.SetShopItems("test_shop", items);
        var result = await _cacheService.GetShopItemsAsync("test_shop");

        // Assert
        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal("test_item", result[0].Id);
    }

    [Fact]
    public void ClearAllCache_ShouldRemoveShopsCache()
    {
        // Arrange
        var shops = new List<ShopDefinition>
        {
            new ShopDefinition
            {
                Id = "test_shop",
                Name = "测试商店",
                Type = ShopType.General,
                Icon = "🏪",
                Description = "测试",
                IsEnabled = true,
                SortOrder = 1
            }
        };
        _cacheService.SetShops(shops);

        // Act
        _cacheService.ClearAllCache();
        var result = _cacheService.GetShopsAsync().Result;

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void ClearShopItemsCache_ShouldRemoveSpecificShopCache()
    {
        // Arrange
        var items = new List<ShopItem>
        {
            new ShopItem
            {
                Id = "test_item",
                ShopId = "test_shop",
                ItemDefinitionId = "item_def",
                ItemName = "测试物品",
                ItemIcon = "🎁",
                PriceJson = "{\"CurrencyType\":1,\"Amount\":100}",
                PurchaseLimitJson = "{\"Type\":0}",
                StockQuantity = -1,
                MinLevel = 1,
                IsEnabled = true,
                SortOrder = 1
            }
        };
        _cacheService.SetShopItems("test_shop", items);

        // Act
        _cacheService.ClearShopItemsCache("test_shop");
        var result = _cacheService.GetShopItemsAsync("test_shop").Result;

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task MultipleShopItems_ShouldBeCachedSeparately()
    {
        // Arrange
        var items1 = new List<ShopItem>
        {
            new ShopItem
            {
                Id = "item1",
                ShopId = "shop1",
                ItemDefinitionId = "def1",
                ItemName = "物品1",
                ItemIcon = "🎁",
                PriceJson = "{\"CurrencyType\":1,\"Amount\":100}",
                PurchaseLimitJson = "{\"Type\":0}",
                StockQuantity = -1,
                MinLevel = 1,
                IsEnabled = true,
                SortOrder = 1
            }
        };

        var items2 = new List<ShopItem>
        {
            new ShopItem
            {
                Id = "item2",
                ShopId = "shop2",
                ItemDefinitionId = "def2",
                ItemName = "物品2",
                ItemIcon = "🎁",
                PriceJson = "{\"CurrencyType\":1,\"Amount\":200}",
                PurchaseLimitJson = "{\"Type\":0}",
                StockQuantity = -1,
                MinLevel = 1,
                IsEnabled = true,
                SortOrder = 1
            }
        };

        // Act
        _cacheService.SetShopItems("shop1", items1);
        _cacheService.SetShopItems("shop2", items2);
        var result1 = await _cacheService.GetShopItemsAsync("shop1");
        var result2 = await _cacheService.GetShopItemsAsync("shop2");

        // Assert
        Assert.NotNull(result1);
        Assert.NotNull(result2);
        Assert.Single(result1);
        Assert.Single(result2);
        Assert.Equal("item1", result1[0].Id);
        Assert.Equal("item2", result2[0].Id);
    }
}
