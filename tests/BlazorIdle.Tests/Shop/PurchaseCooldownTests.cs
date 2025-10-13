using BlazorIdle.Server.Application.Abstractions;
using BlazorIdle.Server.Application.Inventory;
using BlazorIdle.Server.Application.Shop;
using BlazorIdle.Server.Domain.Characters;
using BlazorIdle.Server.Domain.Shop;
using BlazorIdle.Server.Domain.Shop.ValueObjects;
using BlazorIdle.Server.Infrastructure.Configuration;
using BlazorIdle.Server.Infrastructure.Persistence;
using BlazorIdle.Shared.Models.Shop;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace BlazorIdle.Tests.Shop;

/// <summary>
/// 购买冷却系统测试（Phase 3 新增）
/// </summary>
public class PurchaseCooldownTests : IDisposable
{
    private readonly GameDbContext _context;
    private readonly ShopService _shopService;
    private readonly IInventoryService _inventoryService;
    private readonly ShopOptions _shopOptions;
    private readonly Character _testCharacter;
    private readonly ShopItem _testItem;

    public PurchaseCooldownTests()
    {
        // 创建内存数据库
        var options = new DbContextOptionsBuilder<GameDbContext>()
            .UseInMemoryDatabase(databaseName: $"CooldownTestDb_{Guid.NewGuid()}")
            .Options;

        _context = new GameDbContext(options);

        // 配置购买冷却
        _shopOptions = new ShopOptions
        {
            EnablePurchaseCooldown = true,
            GlobalPurchaseCooldownSeconds = 1,
            ItemPurchaseCooldownSeconds = 5,
            ExpensiveItemThreshold = 1000,
            ExpensiveItemCooldownSeconds = 10,
            MinPurchaseQuantity = 1,
            MaxPurchaseQuantity = 999
        };

        var shopOptionsWrapper = Options.Create(_shopOptions);

        // 创建服务
        var logger = new Microsoft.Extensions.Logging.Abstractions.NullLogger<InventoryService>();
        _inventoryService = new InventoryService(_context, logger);
        var validator = new PurchaseValidator(_context, shopOptionsWrapper, _inventoryService);
        var cacheService = new TestShopCacheService();

        _shopService = new ShopService(
            _context,
            validator,
            cacheService,
            _inventoryService,
            shopOptionsWrapper
        );

        // 创建测试数据
        _testCharacter = new Character
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Name = "CooldownTester",
            Level = 10,
            Gold = 10000,
            Experience = 0,
            CreatedAt = DateTime.UtcNow,
            Profession = Shared.Models.Profession.Warrior
        };

        var shop = new ShopDefinition
        {
            Id = "cooldown_test_shop",
            Name = "冷却测试商店",
            Type = ShopType.General,
            IsEnabled = true,
            SortOrder = 1
        };

        _testItem = new ShopItem
        {
            Id = "cooldown_test_item",
            ShopId = shop.Id,
            ItemDefinitionId = "test_potion",
            ItemName = "测试药水",
            ItemIcon = "🧪",
            PriceJson = "{\"currencyType\":1,\"amount\":100}",
            PurchaseLimitJson = "{\"type\":0}",
            StockQuantity = -1,
            MinLevel = 1,
            IsEnabled = true,
            SortOrder = 1,
            Shop = shop
        };

        _context.Characters.Add(_testCharacter);
        _context.ShopDefinitions.Add(shop);
        _context.ShopItems.Add(_testItem);
        _context.SaveChanges();
    }

    [Fact]
    public async Task PurchaseItem_WithCooldownDisabled_ShouldAllowImmediatePurchases()
    {
        // Arrange
        _shopOptions.EnablePurchaseCooldown = false;

        var request = new PurchaseRequest
        {
            ShopItemId = _testItem.Id,
            Quantity = 1
        };

        // Act - 连续购买两次
        var result1 = await _shopService.PurchaseItemAsync(_testCharacter.Id.ToString(), request);
        var result2 = await _shopService.PurchaseItemAsync(_testCharacter.Id.ToString(), request);

        // Assert
        Assert.True(result1.Success);
        Assert.True(result2.Success);
    }

    [Fact]
    public async Task PurchaseItem_WithCooldownEnabled_ShouldEnforceGlobalCooldown()
    {
        // Arrange
        var request = new PurchaseRequest
        {
            ShopItemId = _testItem.Id,
            Quantity = 1
        };

        // Act - 立即连续购买两次
        var result1 = await _shopService.PurchaseItemAsync(_testCharacter.Id.ToString(), request);
        var result2 = await _shopService.PurchaseItemAsync(_testCharacter.Id.ToString(), request);

        // Assert
        Assert.True(result1.Success);
        Assert.False(result2.Success);
        Assert.Contains("购买冷却中", result2.Message);
    }

    [Fact]
    public async Task PurchaseItem_AfterGlobalCooldownExpires_ShouldAllowPurchase()
    {
        // Arrange
        var request = new PurchaseRequest
        {
            ShopItemId = _testItem.Id,
            Quantity = 1
        };

        // Act
        var result1 = await _shopService.PurchaseItemAsync(_testCharacter.Id.ToString(), request);
        
        // 等待全局冷却过期（1秒）
        await Task.Delay(TimeSpan.FromSeconds(1.5));
        
        var result2 = await _shopService.PurchaseItemAsync(_testCharacter.Id.ToString(), request);

        // Assert
        Assert.True(result1.Success);
        Assert.True(result2.Success);
    }

    [Fact]
    public async Task PurchaseItem_WithItemCooldown_ShouldEnforceItemSpecificCooldown()
    {
        // Arrange
        var request = new PurchaseRequest
        {
            ShopItemId = _testItem.Id,
            Quantity = 1
        };

        // Act
        var result1 = await _shopService.PurchaseItemAsync(_testCharacter.Id.ToString(), request);
        
        // 等待全局冷却过期（1秒），但商品冷却（5秒）未过期
        await Task.Delay(TimeSpan.FromSeconds(2));
        
        var result2 = await _shopService.PurchaseItemAsync(_testCharacter.Id.ToString(), request);

        // Assert
        Assert.True(result1.Success);
        Assert.False(result2.Success);
        Assert.Contains("该物品购买冷却中", result2.Message);
    }

    [Fact]
    public async Task PurchaseItem_ExpensiveItem_ShouldUseLongerCooldown()
    {
        // Arrange - 创建昂贵物品
        var expensiveItem = new ShopItem
        {
            Id = "expensive_item",
            ShopId = _testItem.ShopId,
            ItemDefinitionId = "expensive_sword",
            ItemName = "昂贵的剑",
            ItemIcon = "⚔️",
            PriceJson = "{\"currencyType\":1,\"amount\":1500}", // 超过阈值 1000
            PurchaseLimitJson = "{\"type\":0}",
            StockQuantity = -1,
            MinLevel = 1,
            IsEnabled = true,
            SortOrder = 2,
            Shop = _testItem.Shop
        };
        _context.ShopItems.Add(expensiveItem);
        await _context.SaveChangesAsync();

        var request = new PurchaseRequest
        {
            ShopItemId = expensiveItem.Id,
            Quantity = 1
        };

        // Act
        var result1 = await _shopService.PurchaseItemAsync(_testCharacter.Id.ToString(), request);
        
        // 等待标准商品冷却时间（5秒），但昂贵物品冷却（10秒）未过期
        await Task.Delay(TimeSpan.FromSeconds(6));
        
        var result2 = await _shopService.PurchaseItemAsync(_testCharacter.Id.ToString(), request);

        // Assert
        Assert.True(result1.Success);
        Assert.False(result2.Success);
        Assert.Contains("该物品购买冷却中", result2.Message);
    }

    [Fact]
    public async Task PurchaseItem_DifferentItems_ShouldHaveIndependentCooldowns()
    {
        // Arrange - 创建第二个商品
        var item2 = new ShopItem
        {
            Id = "cooldown_test_item_2",
            ShopId = _testItem.ShopId,
            ItemDefinitionId = "test_bread",
            ItemName = "测试面包",
            ItemIcon = "🍞",
            PriceJson = "{\"currencyType\":1,\"amount\":50}",
            PurchaseLimitJson = "{\"type\":0}",
            StockQuantity = -1,
            MinLevel = 1,
            IsEnabled = true,
            SortOrder = 2,
            Shop = _testItem.Shop
        };
        _context.ShopItems.Add(item2);
        await _context.SaveChangesAsync();

        var request1 = new PurchaseRequest
        {
            ShopItemId = _testItem.Id,
            Quantity = 1
        };

        var request2 = new PurchaseRequest
        {
            ShopItemId = item2.Id,
            Quantity = 1
        };

        // Act
        var result1 = await _shopService.PurchaseItemAsync(_testCharacter.Id.ToString(), request1);
        
        // 等待全局冷却过期
        await Task.Delay(TimeSpan.FromSeconds(1.5));
        
        // 购买第二个商品应该成功（不受第一个商品冷却影响）
        var result2 = await _shopService.PurchaseItemAsync(_testCharacter.Id.ToString(), request2);

        // Assert
        Assert.True(result1.Success);
        Assert.True(result2.Success);
    }

    [Fact]
    public void PurchaseCooldown_GetRemainingSeconds_ShouldReturnCorrectValue()
    {
        // Arrange
        var cooldown = new PurchaseCooldown
        {
            Id = PurchaseCooldown.GenerateId(_testCharacter.Id.ToString()),
            CharacterId = _testCharacter.Id.ToString(),
            ShopItemId = null,
            CooldownUntil = DateTime.UtcNow.AddSeconds(5),
            CreatedAt = DateTime.UtcNow
        };

        // Act
        var remainingSeconds = cooldown.GetRemainingSeconds();

        // Assert
        Assert.True(remainingSeconds > 4 && remainingSeconds <= 5);
    }

    [Fact]
    public void PurchaseCooldown_IsExpired_WhenPastCooldownTime_ShouldReturnTrue()
    {
        // Arrange
        var cooldown = new PurchaseCooldown
        {
            Id = PurchaseCooldown.GenerateId(_testCharacter.Id.ToString()),
            CharacterId = _testCharacter.Id.ToString(),
            ShopItemId = null,
            CooldownUntil = DateTime.UtcNow.AddSeconds(-1),
            CreatedAt = DateTime.UtcNow.AddSeconds(-10)
        };

        // Act
        var isExpired = cooldown.IsExpired();

        // Assert
        Assert.True(isExpired);
    }

    [Fact]
    public void PurchaseCooldown_GenerateId_ShouldCreateCorrectFormat()
    {
        // Arrange
        var characterId = _testCharacter.Id.ToString();
        var itemId = "test_item";

        // Act
        var globalId = PurchaseCooldown.GenerateId(characterId);
        var itemId_result = PurchaseCooldown.GenerateId(characterId, itemId);

        // Assert
        Assert.Equal($"{characterId}_global", globalId);
        Assert.Equal($"{characterId}_{itemId}", itemId_result);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    // 测试用的简单缓存服务
    private class TestShopCacheService : IShopCacheService
    {
        public Task<List<ShopDefinition>?> GetShopsAsync() => Task.FromResult<List<ShopDefinition>?>(null);
        public void SetShops(List<ShopDefinition> shops) { }
        public Task<List<ShopItem>?> GetShopItemsAsync(string shopId) => Task.FromResult<List<ShopItem>?>(null);
        public void SetShopItems(string shopId, List<ShopItem> items) { }
        public void ClearAllCache() { }
        public void ClearShopItemsCache(string shopId) { }
    }
}
