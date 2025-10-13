using BlazorIdle.Server.Application.Inventory;
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
using Xunit;

namespace BlazorIdle.Tests.Shop;

/// <summary>
/// 商店与库存集成测试
/// </summary>
public class ShopInventoryIntegrationTests : IDisposable
{
    private readonly GameDbContext _context;
    private readonly ShopService _shopService;
    private readonly InventoryService _inventoryService;
    private readonly Guid _testCharacterId;

    public ShopInventoryIntegrationTests()
    {
        // 使用内存数据库进行测试
        var options = new DbContextOptionsBuilder<GameDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new GameDbContext(options);
        
        // 创建服务提供者
        var serviceProvider = new ServiceCollection()
            .AddMemoryCache()
            .AddLogging()
            .BuildServiceProvider();
        
        // 创建 ShopOptions
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
        
        // 创建缓存服务
        var cache = serviceProvider.GetRequiredService<IMemoryCache>();
        var cacheLogger = serviceProvider.GetRequiredService<ILogger<ShopCacheService>>();
        var configBuilder = new ConfigurationBuilder();
        configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
        {
            { "Shop:EnableCaching", "false" }
        });
        var configuration = configBuilder.Build();
        var cacheService = new ShopCacheService(cache, cacheLogger, configuration);
        
        // 创建库存服务
        var inventoryLogger = serviceProvider.GetRequiredService<ILogger<InventoryService>>();
        _inventoryService = new InventoryService(_context, inventoryLogger);
        
        // 创建验证器（需要库存服务）
        var validator = new PurchaseValidator(_context, shopOptions, _inventoryService);
        
        // 创建商店服务
        _shopService = new ShopService(_context, validator, cacheService, _inventoryService, shopOptions);

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
            SortOrder = 1
        };
        _context.ShopDefinitions.Add(shop);

        // 创建测试商品
        var price = new Price
        {
            CurrencyType = CurrencyType.Gold,
            CurrencyId = "gold",
            Amount = 100
        };

        var limit = new PurchaseLimit
        {
            Type = LimitType.Unlimited,
            MaxPurchases = -1,
            ResetPeriodSeconds = 0
        };

        var item = new ShopItem
        {
            Id = "test_item",
            ShopId = "test_shop",
            ItemDefinitionId = "potion_health_small",
            ItemName = "小型生命药水",
            ItemIcon = "🧪",
            StockQuantity = -1,
            MinLevel = 1,
            IsEnabled = true,
            SortOrder = 1
        };
        item.SetPrice(price);
        item.SetPurchaseLimit(limit);

        _context.ShopItems.Add(item);
        _context.SaveChanges();
    }

    [Fact]
    public async Task PurchaseItem_Should_AddItemToInventory()
    {
        // Arrange
        var request = new PurchaseRequest
        {
            ShopItemId = "test_item",
            Quantity = 5
        };

        // Act
        var response = await _shopService.PurchaseItemAsync(_testCharacterId.ToString(), request);

        // Assert
        Assert.True(response.Success);
        
        // 验证物品已添加到库存
        var inventoryItem = await _context.InventoryItems
            .FirstOrDefaultAsync(i => i.CharacterId == _testCharacterId && i.ItemId == "potion_health_small");
        
        Assert.NotNull(inventoryItem);
        Assert.Equal(5, inventoryItem.Quantity);
    }

    [Fact]
    public async Task PurchaseItem_MultipleTimes_Should_AccumulateInventory()
    {
        // Arrange
        var request1 = new PurchaseRequest
        {
            ShopItemId = "test_item",
            Quantity = 3
        };
        var request2 = new PurchaseRequest
        {
            ShopItemId = "test_item",
            Quantity = 7
        };

        // Act
        var response1 = await _shopService.PurchaseItemAsync(_testCharacterId.ToString(), request1);
        var response2 = await _shopService.PurchaseItemAsync(_testCharacterId.ToString(), request2);

        // Assert
        Assert.True(response1.Success);
        Assert.True(response2.Success);
        
        // 验证物品数量累加
        var inventoryItem = await _context.InventoryItems
            .FirstOrDefaultAsync(i => i.CharacterId == _testCharacterId && i.ItemId == "potion_health_small");
        
        Assert.NotNull(inventoryItem);
        Assert.Equal(10, inventoryItem.Quantity); // 3 + 7 = 10
    }

    [Fact]
    public async Task PurchaseItem_Should_DeductGold()
    {
        // Arrange
        var request = new PurchaseRequest
        {
            ShopItemId = "test_item",
            Quantity = 2
        };

        // Act
        var response = await _shopService.PurchaseItemAsync(_testCharacterId.ToString(), request);

        // Assert
        Assert.True(response.Success);
        
        // 验证金币已扣除 (100 * 2 = 200)
        var character = await _context.Characters.FindAsync(_testCharacterId);
        Assert.Equal(9800, character!.Gold); // 10000 - 200 = 9800
    }

    [Fact]
    public async Task PurchaseItem_InsufficientGold_Should_Fail()
    {
        // Arrange
        // 设置一个角色金币不足的场景
        var poorCharacterId = Guid.NewGuid();
        var poorCharacter = new Character
        {
            Id = poorCharacterId,
            Name = "穷人",
            Level = 15,
            Gold = 50, // 不够买2个物品 (需要200金币)
            Profession = Profession.Warrior
        };
        _context.Characters.Add(poorCharacter);
        await _context.SaveChangesAsync();

        var request = new PurchaseRequest
        {
            ShopItemId = "test_item",
            Quantity = 2
        };

        // Act
        var response = await _shopService.PurchaseItemAsync(poorCharacterId.ToString(), request);

        // Assert
        Assert.False(response.Success);
        Assert.Contains("金币不足", response.Message);
        
        // 验证没有物品添加到库存
        var inventoryItem = await _context.InventoryItems
            .FirstOrDefaultAsync(i => i.CharacterId == poorCharacterId);
        Assert.Null(inventoryItem);
    }

    [Fact]
    public async Task PurchaseItem_Should_CreatePurchaseRecord()
    {
        // Arrange
        var request = new PurchaseRequest
        {
            ShopItemId = "test_item",
            Quantity = 3
        };

        // Act
        var response = await _shopService.PurchaseItemAsync(_testCharacterId.ToString(), request);

        // Assert
        Assert.True(response.Success);
        
        // 验证购买记录已创建
        var purchaseRecord = await _context.PurchaseRecords
            .FirstOrDefaultAsync(r => r.CharacterId == _testCharacterId && r.ShopItemId == "test_item");
        
        Assert.NotNull(purchaseRecord);
        Assert.Equal(3, purchaseRecord.Quantity);
        Assert.Equal("test_shop", purchaseRecord.ShopId);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }
}
