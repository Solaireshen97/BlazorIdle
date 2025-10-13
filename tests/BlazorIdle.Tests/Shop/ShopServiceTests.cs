using BlazorIdle.Server.Application.Shop;
using BlazorIdle.Server.Domain.Characters;
using BlazorIdle.Server.Domain.Shop;
using BlazorIdle.Server.Domain.Shop.ValueObjects;
using BlazorIdle.Server.Infrastructure.Configuration;
using BlazorIdle.Server.Infrastructure.Persistence;
using BlazorIdle.Shared.Models;
using BlazorIdle.Shared.Models.Shop;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;
using Xunit;

namespace BlazorIdle.Tests.Shop;

/// <summary>
/// 商店服务集成测试
/// </summary>
public class ShopServiceTests : IDisposable
{
    private readonly GameDbContext _context;
    private readonly ShopService _shopService;
    private readonly PurchaseValidator _validator;
    private readonly IShopCacheService _cacheService;
    private readonly Guid _testCharacterId;

    public ShopServiceTests()
    {
        // 使用内存数据库进行测试
        var options = new DbContextOptionsBuilder<GameDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new GameDbContext(options);
        
        // 创建 ShopOptions 用于测试
        var shopOptions = Options.Create(new ShopOptions
        {
            EnableCaching = false, // 测试时禁用缓存
            DailyResetSeconds = 86400,
            WeeklyResetSeconds = 604800,
            MinPurchaseQuantity = 1,
            MaxPurchaseQuantity = 999,
            DefaultPageSize = 20,
            MaxPageSize = 100
        });
        
        // 创建服务提供者（测试时禁用缓存以确保测试数据新鲜）
        var serviceProvider = new ServiceCollection()
            .AddMemoryCache()
            .AddLogging()
            .BuildServiceProvider();
        
        // 创建库存服务
        var inventoryLogger = serviceProvider.GetRequiredService<ILogger<BlazorIdle.Server.Application.Inventory.InventoryService>>();
        var inventoryService = new BlazorIdle.Server.Application.Inventory.InventoryService(_context, inventoryLogger);
        
        // 创建验证器（需要库存服务）
        _validator = new PurchaseValidator(_context, shopOptions, inventoryService);
            
        var cache = serviceProvider.GetRequiredService<IMemoryCache>();
        var logger = serviceProvider.GetRequiredService<ILogger<ShopCacheService>>();
        
        // 创建配置（禁用缓存以便测试）
        var configBuilder = new ConfigurationBuilder();
        configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
        {
            { "Shop:EnableCaching", "false" }
        });
        var configuration = configBuilder.Build();
        
        _cacheService = new ShopCacheService(cache, logger, configuration);
        
        _shopService = new ShopService(_context, _validator, _cacheService, inventoryService, shopOptions);

        // 设置测试数据
        _testCharacterId = Guid.NewGuid();
        SetupTestData();
    }

    private void SetupTestData()
    {
        // 创建测试角色
        var character = new Character
        {
            Id = _testCharacterId,
            Name = "测试角色",
            Level = 15,
            Gold = 10000,
            Profession = Profession.Warrior
        };
        _context.Characters.Add(character);

        // 创建测试商店
        var shop = new ShopDefinition
        {
            Id = "test_shop",
            Name = "测试商店",
            Type = ShopType.General,
            Icon = "🏪",
            Description = "测试用商店",
            IsEnabled = true,
            SortOrder = 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.ShopDefinitions.Add(shop);

        // 创建测试商品
        var price1 = new Price { CurrencyType = CurrencyType.Gold, Amount = 100 };
        var limit1 = new PurchaseLimit { Type = LimitType.Unlimited };
        
        var item1 = new ShopItem
        {
            Id = "test_item_1",
            ShopId = "test_shop",
            ItemDefinitionId = "potion",
            ItemName = "测试药水",
            ItemIcon = "🧪",
            PriceJson = JsonSerializer.Serialize(price1),
            PurchaseLimitJson = JsonSerializer.Serialize(limit1),
            StockQuantity = -1,
            MinLevel = 1,
            IsEnabled = true,
            SortOrder = 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var price2 = new Price { CurrencyType = CurrencyType.Gold, Amount = 500 };
        var limit2 = new PurchaseLimit { Type = LimitType.Daily, MaxPurchases = 3 };
        
        var item2 = new ShopItem
        {
            Id = "test_item_2",
            ShopId = "test_shop",
            ItemDefinitionId = "elixir",
            ItemName = "测试药剂",
            ItemIcon = "💪",
            PriceJson = JsonSerializer.Serialize(price2),
            PurchaseLimitJson = JsonSerializer.Serialize(limit2),
            StockQuantity = 10,
            MinLevel = 10,
            IsEnabled = true,
            SortOrder = 2,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.ShopItems.AddRange(item1, item2);
        _context.SaveChanges();
    }

    [Fact]
    public async Task ListShopsAsync_ShouldReturnEnabledShops()
    {
        // Act
        var response = await _shopService.ListShopsAsync(_testCharacterId.ToString());

        // Assert
        Assert.NotNull(response);
        Assert.NotEmpty(response.Shops);
        Assert.Single(response.Shops);
        Assert.Equal("test_shop", response.Shops[0].Id);
        Assert.Equal("测试商店", response.Shops[0].Name);
        Assert.True(response.Shops[0].IsUnlocked);
    }

    [Fact]
    public async Task GetShopItemsAsync_ShouldReturnShopItems()
    {
        // Act
        var response = await _shopService.GetShopItemsAsync("test_shop", _testCharacterId.ToString());

        // Assert
        Assert.NotNull(response);
        Assert.NotEmpty(response.Items);
        Assert.Equal(2, response.Items.Count);
        
        var item1 = response.Items[0];
        Assert.Equal("test_item_1", item1.Id);
        Assert.Equal("测试药水", item1.ItemName);
        Assert.Equal("Gold", item1.Price.CurrencyType);
        Assert.Equal(100, item1.Price.Amount);
        Assert.True(item1.CanPurchase);
    }

    [Fact]
    public async Task GetShopItemsAsync_ShouldFilterByLevel()
    {
        // Arrange - 创建低等级角色
        var lowLevelCharId = Guid.NewGuid();
        var lowLevelChar = new Character
        {
            Id = lowLevelCharId,
            Name = "低级角色",
            Level = 5,
            Gold = 10000,
            Profession = Profession.Warrior
        };
        _context.Characters.Add(lowLevelChar);
        await _context.SaveChangesAsync();

        // Act
        var response = await _shopService.GetShopItemsAsync("test_shop", lowLevelCharId.ToString());

        // Assert
        Assert.NotNull(response);
        Assert.Equal(2, response.Items.Count);
        
        // 第一个商品应该可以购买（等级1）
        Assert.True(response.Items[0].CanPurchase);
        Assert.Null(response.Items[0].PurchaseBlockReason);
        
        // 第二个商品应该不能购买（等级10）
        Assert.False(response.Items[1].CanPurchase);
        Assert.Equal("需要等级 10", response.Items[1].PurchaseBlockReason);
    }

    [Fact]
    public async Task PurchaseItemAsync_WithValidRequest_ShouldSucceed()
    {
        // Arrange
        var request = new PurchaseRequest
        {
            ShopItemId = "test_item_1",
            Quantity = 2
        };

        // Act
        var response = await _shopService.PurchaseItemAsync(_testCharacterId.ToString(), request);

        // Assert
        Assert.True(response.Success);
        Assert.NotNull(response.Message);
        Assert.NotNull(response.Record);
        Assert.Equal(2, response.Record.Quantity);

        // 验证金币是否扣除
        var character = await _context.Characters.FindAsync(_testCharacterId);
        Assert.NotNull(character);
        Assert.Equal(9800, character.Gold); // 10000 - (100 * 2)

        // 验证购买记录
        var records = await _context.PurchaseRecords
            .Where(r => r.CharacterId == _testCharacterId)
            .ToListAsync();
        Assert.Single(records);
        Assert.Equal("test_item_1", records[0].ShopItemId);
        Assert.Equal(2, records[0].Quantity);
    }

    [Fact]
    public async Task PurchaseItemAsync_WithInsufficientGold_ShouldFail()
    {
        // Arrange
        var request = new PurchaseRequest
        {
            ShopItemId = "test_item_1",
            Quantity = 200 // 需要 20000 金币，但角色只有 10000
        };

        // Act
        var response = await _shopService.PurchaseItemAsync(_testCharacterId.ToString(), request);

        // Assert
        Assert.False(response.Success);
        Assert.Contains("金币不足", response.Message);
    }

    [Fact]
    public async Task PurchaseItemAsync_WithLevelRestriction_ShouldFail()
    {
        // Arrange - 创建低等级角色
        var lowLevelCharId = Guid.NewGuid();
        var lowLevelChar = new Character
        {
            Id = lowLevelCharId,
            Name = "低级角色",
            Level = 5,
            Gold = 10000,
            Profession = Profession.Warrior
        };
        _context.Characters.Add(lowLevelChar);
        await _context.SaveChangesAsync();

        var request = new PurchaseRequest
        {
            ShopItemId = "test_item_2", // 需要等级10
            Quantity = 1
        };

        // Act
        var response = await _shopService.PurchaseItemAsync(lowLevelCharId.ToString(), request);

        // Assert
        Assert.False(response.Success);
        Assert.Equal("需要等级 10", response.Message);
    }

    [Fact]
    public async Task PurchaseItemAsync_WithDailyLimit_ShouldEnforceLimit()
    {
        // Arrange
        var request = new PurchaseRequest
        {
            ShopItemId = "test_item_2", // 每日限购3次
            Quantity = 1
        };

        // Act - 购买3次应该成功
        var response1 = await _shopService.PurchaseItemAsync(_testCharacterId.ToString(), request);
        var response2 = await _shopService.PurchaseItemAsync(_testCharacterId.ToString(), request);
        var response3 = await _shopService.PurchaseItemAsync(_testCharacterId.ToString(), request);

        // Assert
        Assert.True(response1.Success);
        Assert.True(response2.Success);
        Assert.True(response3.Success);

        // Act - 第4次应该失败
        var response4 = await _shopService.PurchaseItemAsync(_testCharacterId.ToString(), request);

        // Assert
        Assert.False(response4.Success);
        Assert.Contains("超过购买限制", response4.Message);
    }

    [Fact]
    public async Task PurchaseItemAsync_WithStockLimit_ShouldReduceStock()
    {
        // Arrange
        var request = new PurchaseRequest
        {
            ShopItemId = "test_item_2", // 库存10个，每日限购3次
            Quantity = 2
        };

        // Act
        var response = await _shopService.PurchaseItemAsync(_testCharacterId.ToString(), request);

        // Assert
        Assert.True(response.Success, $"Purchase failed: {response.Message}");

        // 验证库存是否减少
        var item = await _context.ShopItems.FindAsync("test_item_2");
        Assert.NotNull(item);
        Assert.Equal(8, item.StockQuantity); // 10 - 2
    }

    [Fact]
    public async Task GetPurchaseHistoryAsync_ShouldReturnHistory()
    {
        // Arrange - 先购买一些商品
        var request1 = new PurchaseRequest { ShopItemId = "test_item_1", Quantity = 1 };
        var request2 = new PurchaseRequest { ShopItemId = "test_item_2", Quantity = 2 };
        
        await _shopService.PurchaseItemAsync(_testCharacterId.ToString(), request1);
        await _shopService.PurchaseItemAsync(_testCharacterId.ToString(), request2);

        // Act
        var response = await _shopService.GetPurchaseHistoryAsync(_testCharacterId.ToString(), page: 1, pageSize: 10);

        // Assert
        Assert.NotNull(response);
        Assert.Equal(2, response.TotalCount);
        Assert.Equal(2, response.Records.Count);
        
        // 验证记录按时间倒序
        Assert.Equal("test_item_2", response.Records[0].ShopItemId);
        Assert.Equal("test_item_1", response.Records[1].ShopItemId);
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
