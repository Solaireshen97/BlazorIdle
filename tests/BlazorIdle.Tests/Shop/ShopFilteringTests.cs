using BlazorIdle.Server.Application.Shop;
using BlazorIdle.Server.Domain.Characters;
using BlazorIdle.Server.Domain.Shop;
using BlazorIdle.Server.Domain.Shop.ValueObjects;
using BlazorIdle.Server.Infrastructure.Configuration;
using BlazorIdle.Server.Infrastructure.Persistence;
using BlazorIdle.Shared.Models.Shop;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace BlazorIdle.Tests.Shop;

/// <summary>
/// 商店过滤功能测试
/// </summary>
public class ShopFilteringTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly GameDbContext _context;
    private readonly ShopService _shopService;
    private readonly Character _testCharacter;

    public ShopFilteringTests()
    {
        // 设置 SQLite 内存数据库
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<GameDbContext>()
            .UseSqlite(_connection)
            .Options;

        _context = new GameDbContext(options);
        _context.Database.EnsureCreated();

        // 创建测试角色
        _testCharacter = new Character
        {
            Id = Guid.NewGuid(),
            Name = "TestHero",
            Level = 15,
            Gold = 10000
        };
        _context.Characters.Add(_testCharacter);

        // 创建测试商店
        var testShop = new ShopDefinition
        {
            Id = "test_shop",
            Name = "测试商店",
            Type = ShopType.General,
            Icon = "🏪",
            Description = "用于测试过滤功能",
            IsEnabled = true,
            SortOrder = 1
        };
        _context.ShopDefinitions.Add(testShop);

        // 创建多样化的测试商品
        var testItems = new List<ShopItem>
        {
            CreateTestItem("item_1", "test_shop", "小型生命药水", 50, 1, "Consumable", "Common"),
            CreateTestItem("item_2", "test_shop", "中型生命药水", 150, 5, "Consumable", "Uncommon"),
            CreateTestItem("item_3", "test_shop", "铁剑", 500, 1, "Equipment", "Common"),
            CreateTestItem("item_4", "test_shop", "钢剑", 1500, 10, "Equipment", "Uncommon"),
            CreateTestItem("item_5", "test_shop", "龙鳞", 5000, 20, "Material", "Epic"),
            CreateTestItem("item_6", "test_shop", "传送卷轴", 1000, 10, "Special", "Rare"),
            CreateTestItem("item_7", "test_shop", "高级生命药水", 300, 10, "Consumable", "Rare"),
            CreateTestItem("item_8", "test_shop", "木盾", 300, 1, "Equipment", "Common"),
        };

        _context.ShopItems.AddRange(testItems);
        _context.SaveChanges();

        // 创建缓存服务
        var serviceProvider = new ServiceCollection()
            .AddMemoryCache()
            .AddLogging()
            .BuildServiceProvider();
            
        var cache = serviceProvider.GetRequiredService<IMemoryCache>();
        var logger = serviceProvider.GetRequiredService<ILogger<ShopCacheService>>();
        
        // 创建配置（禁用缓存以便测试）
        var configBuilder = new Microsoft.Extensions.Configuration.ConfigurationBuilder();
        configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
        {
            { "Shop:EnableCaching", "false" }
        });
        var configuration = configBuilder.Build();
        
        var cacheService = new ShopCacheService(cache, logger, configuration);

        // 创建 ShopOptions 用于测试
        var shopOptions = Options.Create(new ShopOptions
        {
            EnableCaching = false,
            DailyResetSeconds = 86400,
            WeeklyResetSeconds = 604800,
            MinPurchaseQuantity = 1,
            MaxPurchaseQuantity = 999,
            DefaultPageSize = 20,
            MaxPageSize = 100
        });

        // 创建库存服务
        var inventoryLogger = serviceProvider.GetRequiredService<ILogger<BlazorIdle.Server.Application.Inventory.InventoryService>>();
        var inventoryService = new BlazorIdle.Server.Application.Inventory.InventoryService(_context, inventoryLogger);
        
        // 创建验证器（需要库存服务和logger）
        var validatorLogger = serviceProvider.GetRequiredService<ILogger<PurchaseValidator>>();
        var validator = new PurchaseValidator(_context, shopOptions, inventoryService, validatorLogger);

        // 创建商店服务（需要logger）
        var shopServiceLogger = serviceProvider.GetRequiredService<ILogger<ShopService>>();
        _shopService = new ShopService(_context, validator, cacheService, inventoryService, shopOptions, shopServiceLogger);
    }

    private ShopItem CreateTestItem(
        string id, 
        string shopId, 
        string name, 
        int price, 
        int minLevel,
        string category,
        string rarity)
    {
        var priceObj = new Price
        {
            CurrencyType = CurrencyType.Gold,
            Amount = price
        };

        var limit = new PurchaseLimit
        {
            Type = LimitType.Unlimited
        };

        return new ShopItem
        {
            Id = id,
            ShopId = shopId,
            ItemDefinitionId = id,
            ItemName = name,
            ItemIcon = "🧪",
            PriceJson = JsonSerializer.Serialize(priceObj),
            PurchaseLimitJson = JsonSerializer.Serialize(limit),
            StockQuantity = -1,
            MinLevel = minLevel,
            ItemCategory = category,
            Rarity = rarity,
            IsEnabled = true,
            SortOrder = 1
        };
    }

    [Fact]
    public async Task GetShopItemsWithFilter_ByCategory_ReturnsOnlyMatchingItems()
    {
        // Arrange
        var filter = new ShopItemFilterRequest
        {
            ShopId = "test_shop",
            ItemCategory = "Consumable"
        };

        // Act
        var response = await _shopService.GetShopItemsWithFilterAsync(_testCharacter.Id.ToString(), filter);

        // Assert
        Assert.NotNull(response);
        Assert.NotEmpty(response.Items);
        Assert.All(response.Items, item => Assert.Equal("Consumable", item.ItemCategory));
        Assert.Equal(3, response.Items.Count); // 3 consumables
    }

    [Fact]
    public async Task GetShopItemsWithFilter_ByRarity_ReturnsOnlyMatchingItems()
    {
        // Arrange
        var filter = new ShopItemFilterRequest
        {
            ShopId = "test_shop",
            Rarity = "Common"
        };

        // Act
        var response = await _shopService.GetShopItemsWithFilterAsync(_testCharacter.Id.ToString(), filter);

        // Assert
        Assert.NotNull(response);
        Assert.NotEmpty(response.Items);
        Assert.All(response.Items, item => Assert.Equal("Common", item.Rarity));
        Assert.Equal(3, response.Items.Count); // 3 common items
    }

    [Fact]
    public async Task GetShopItemsWithFilter_ByPriceRange_ReturnsItemsInRange()
    {
        // Arrange
        var filter = new ShopItemFilterRequest
        {
            ShopId = "test_shop",
            MinPrice = 100,
            MaxPrice = 1000
        };

        // Act
        var response = await _shopService.GetShopItemsWithFilterAsync(_testCharacter.Id.ToString(), filter);

        // Assert
        Assert.NotNull(response);
        Assert.NotEmpty(response.Items);
        Assert.All(response.Items, item => 
        {
            Assert.True(item.Price.Amount >= 100);
            Assert.True(item.Price.Amount <= 1000);
        });
        Assert.Equal(5, response.Items.Count); // Items with prices: 150, 300, 300, 500, 1000
    }

    [Fact]
    public async Task GetShopItemsWithFilter_ByLevelRange_ReturnsItemsInRange()
    {
        // Arrange
        var filter = new ShopItemFilterRequest
        {
            ShopId = "test_shop",
            MinLevel = 5,
            MaxLevel = 15
        };

        // Act
        var response = await _shopService.GetShopItemsWithFilterAsync(_testCharacter.Id.ToString(), filter);

        // Assert
        Assert.NotNull(response);
        Assert.NotEmpty(response.Items);
        Assert.All(response.Items, item => 
        {
            Assert.True(item.MinLevel >= 5);
            Assert.True(item.MinLevel <= 15);
        });
        Assert.Equal(4, response.Items.Count); // Items with levels: 5, 10, 10, 10
    }

    [Fact]
    public async Task GetShopItemsWithFilter_CombinedFilters_ReturnsCorrectItems()
    {
        // Arrange
        var filter = new ShopItemFilterRequest
        {
            ShopId = "test_shop",
            ItemCategory = "Equipment",
            MinPrice = 1000
        };

        // Act
        var response = await _shopService.GetShopItemsWithFilterAsync(_testCharacter.Id.ToString(), filter);

        // Assert
        Assert.NotNull(response);
        Assert.NotEmpty(response.Items);
        Assert.All(response.Items, item => 
        {
            Assert.Equal("Equipment", item.ItemCategory);
            Assert.True(item.Price.Amount >= 1000);
        });
        Assert.Single(response.Items); // Only steel sword (1500 gold, Equipment)
    }

    [Fact]
    public async Task GetShopItemsWithFilter_SortByPrice_Ascending_ReturnsCorrectOrder()
    {
        // Arrange
        var filter = new ShopItemFilterRequest
        {
            ShopId = "test_shop",
            SortBy = "Price",
            SortDirection = "Asc"
        };

        // Act
        var response = await _shopService.GetShopItemsWithFilterAsync(_testCharacter.Id.ToString(), filter);

        // Assert
        Assert.NotNull(response);
        Assert.NotEmpty(response.Items);
        
        // Check if sorted ascending by price
        for (int i = 0; i < response.Items.Count - 1; i++)
        {
            Assert.True(response.Items[i].Price.Amount <= response.Items[i + 1].Price.Amount);
        }
    }

    [Fact]
    public async Task GetShopItemsWithFilter_SortByPrice_Descending_ReturnsCorrectOrder()
    {
        // Arrange
        var filter = new ShopItemFilterRequest
        {
            ShopId = "test_shop",
            SortBy = "Price",
            SortDirection = "Desc"
        };

        // Act
        var response = await _shopService.GetShopItemsWithFilterAsync(_testCharacter.Id.ToString(), filter);

        // Assert
        Assert.NotNull(response);
        Assert.NotEmpty(response.Items);
        
        // Check if sorted descending by price
        for (int i = 0; i < response.Items.Count - 1; i++)
        {
            Assert.True(response.Items[i].Price.Amount >= response.Items[i + 1].Price.Amount);
        }
    }

    [Fact]
    public async Task GetShopItemsWithFilter_SortByLevel_ReturnsCorrectOrder()
    {
        // Arrange
        var filter = new ShopItemFilterRequest
        {
            ShopId = "test_shop",
            SortBy = "Level",
            SortDirection = "Asc"
        };

        // Act
        var response = await _shopService.GetShopItemsWithFilterAsync(_testCharacter.Id.ToString(), filter);

        // Assert
        Assert.NotNull(response);
        Assert.NotEmpty(response.Items);
        
        // Check if sorted ascending by level
        for (int i = 0; i < response.Items.Count - 1; i++)
        {
            Assert.True(response.Items[i].MinLevel <= response.Items[i + 1].MinLevel);
        }
    }

    [Fact]
    public async Task GetShopItemsWithFilter_SortByName_ReturnsCorrectOrder()
    {
        // Arrange
        var filter = new ShopItemFilterRequest
        {
            ShopId = "test_shop",
            SortBy = "Name",
            SortDirection = "Asc"
        };

        // Act
        var response = await _shopService.GetShopItemsWithFilterAsync(_testCharacter.Id.ToString(), filter);

        // Assert
        Assert.NotNull(response);
        Assert.NotEmpty(response.Items);
        
        // Check if sorted alphabetically by name
        for (int i = 0; i < response.Items.Count - 1; i++)
        {
            Assert.True(string.Compare(response.Items[i].ItemName, response.Items[i + 1].ItemName, StringComparison.Ordinal) <= 0);
        }
    }

    [Fact]
    public async Task GetShopItemsWithFilter_SortByRarity_ReturnsCorrectOrder()
    {
        // Arrange
        var filter = new ShopItemFilterRequest
        {
            ShopId = "test_shop",
            SortBy = "Rarity",
            SortDirection = "Asc"
        };

        // Act
        var response = await _shopService.GetShopItemsWithFilterAsync(_testCharacter.Id.ToString(), filter);

        // Assert
        Assert.NotNull(response);
        Assert.NotEmpty(response.Items);
        
        // Verify rarities are in ascending order (Common < Uncommon < Rare < Epic)
        var rarityOrder = new Dictionary<string, int>
        {
            { "Common", 1 },
            { "Uncommon", 2 },
            { "Rare", 3 },
            { "Epic", 4 },
            { "Legendary", 5 }
        };
        
        for (int i = 0; i < response.Items.Count - 1; i++)
        {
            var currentOrder = rarityOrder.GetValueOrDefault(response.Items[i].Rarity ?? "Common", 0);
            var nextOrder = rarityOrder.GetValueOrDefault(response.Items[i + 1].Rarity ?? "Common", 0);
            Assert.True(currentOrder <= nextOrder);
        }
    }

    [Fact]
    public async Task GetShopItemsWithFilter_NoFilters_ReturnsAllItems()
    {
        // Arrange
        var filter = new ShopItemFilterRequest
        {
            ShopId = "test_shop"
        };

        // Act
        var response = await _shopService.GetShopItemsWithFilterAsync(_testCharacter.Id.ToString(), filter);

        // Assert
        Assert.NotNull(response);
        Assert.Equal(8, response.Items.Count); // All test items
    }

    [Fact]
    public async Task GetShopItemsWithFilter_NonExistentCategory_ReturnsEmpty()
    {
        // Arrange
        var filter = new ShopItemFilterRequest
        {
            ShopId = "test_shop",
            ItemCategory = "NonExistent"
        };

        // Act
        var response = await _shopService.GetShopItemsWithFilterAsync(_testCharacter.Id.ToString(), filter);

        // Assert
        Assert.NotNull(response);
        Assert.Empty(response.Items);
    }

    [Fact]
    public async Task GetShopItemsWithFilter_SearchByName_ReturnsMatchingItems()
    {
        // Arrange
        var filter = new ShopItemFilterRequest
        {
            ShopId = "test_shop",
            SearchText = "药水"  // 搜索包含"药水"的商品
        };

        // Act
        var response = await _shopService.GetShopItemsWithFilterAsync(_testCharacter.Id.ToString(), filter);

        // Assert
        Assert.NotNull(response);
        Assert.NotEmpty(response.Items);
        Assert.All(response.Items, item =>
        {
            Assert.Contains("药水", item.ItemName.ToLower() + item.ItemDefinitionId.ToLower());
        });
    }

    [Fact]
    public async Task GetShopItemsWithFilter_SearchNonExistent_ReturnsEmpty()
    {
        // Arrange
        var filter = new ShopItemFilterRequest
        {
            ShopId = "test_shop",
            SearchText = "不存在的商品xyz123"
        };

        // Act
        var response = await _shopService.GetShopItemsWithFilterAsync(_testCharacter.Id.ToString(), filter);

        // Assert
        Assert.NotNull(response);
        Assert.Empty(response.Items);
    }

    [Fact]
    public async Task GetShopItemsWithFilter_SearchWithOtherFilters_ReturnsCombinedResults()
    {
        // Arrange - 组合搜索和其他过滤条件
        var filter = new ShopItemFilterRequest
        {
            ShopId = "test_shop",
            SearchText = "potion",
            ItemCategory = "Consumable",
            MaxPrice = 200
        };

        // Act
        var response = await _shopService.GetShopItemsWithFilterAsync(_testCharacter.Id.ToString(), filter);

        // Assert
        Assert.NotNull(response);
        // 所有结果应该同时满足搜索词、类别和价格条件
        Assert.All(response.Items, item =>
        {
            Assert.Equal("Consumable", item.ItemCategory);
            Assert.True(item.Price.Amount <= 200);
        });
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }
}
