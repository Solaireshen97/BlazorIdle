using BlazorIdle.Server.Application.Abstractions;
using BlazorIdle.Server.Application.Shop;
using BlazorIdle.Server.Domain.Characters;
using BlazorIdle.Server.Domain.Shop;
using BlazorIdle.Server.Domain.Shop.ValueObjects;
using BlazorIdle.Server.Infrastructure.Persistence;
using BlazorIdle.Shared.Models;
using BlazorIdle.Shared.Models.Shop;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace BlazorIdle.Tests.Shop.Services;

/// <summary>
/// 商店服务测试
/// </summary>
public class ShopServiceTests : IDisposable
{
    private readonly GameDbContext _context;
    private readonly IShopService _service;
    private readonly Mock<IPurchaseValidator> _mockValidator;

    public ShopServiceTests()
    {
        var options = new DbContextOptionsBuilder<GameDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new GameDbContext(options);
        _mockValidator = new Mock<IPurchaseValidator>();
        var mockLogger = new Mock<ILogger<ShopService>>();
        _service = new ShopService(_context, _mockValidator.Object, mockLogger.Object);
    }

    [Fact]
    public async Task ListShopsAsync_ShouldReturnEnabledShops()
    {
        // Arrange
        var characterId = Guid.NewGuid();
        await CreateTestCharacterAsync(characterId);
        await SeedTestShopsAsync();

        // Act
        var response = await _service.ListShopsAsync(characterId);

        // Assert
        Assert.NotNull(response);
        Assert.NotEmpty(response.Shops);
        Assert.Equal(2, response.Shops.Count); // Only enabled shops
        Assert.All(response.Shops, shop => Assert.True(shop.IsEnabled));
    }

    [Fact]
    public async Task ListShopItemsAsync_ShouldReturnItemsForShop()
    {
        // Arrange
        var characterId = Guid.NewGuid();
        await CreateTestCharacterAsync(characterId);
        await SeedTestShopsAsync();

        // Act
        var response = await _service.ListShopItemsAsync("test_shop_1", characterId);

        // Assert
        Assert.NotNull(response);
        Assert.Equal("test_shop_1", response.ShopId);
        Assert.NotEmpty(response.Items);
        Assert.Equal(2, response.Items.Count);
    }

    // Note: Purchase tests are skipped because they require database transactions
    // which are not supported by InMemory database provider.
    // Integration tests with a real database would be needed to test purchase functionality.

    [Fact]
    public async Task GetPurchaseHistoryAsync_ShouldReturnRecords()
    {
        // Arrange
        var characterId = Guid.NewGuid();
        await CreateTestCharacterAsync(characterId);
        await SeedPurchaseRecordsAsync(characterId);

        // Act
        var response = await _service.GetPurchaseHistoryAsync(characterId);

        // Assert
        Assert.NotNull(response);
        Assert.NotEmpty(response.Records);
        Assert.True(response.TotalCount > 0);
    }

    // Helper methods
    private async Task<Character> CreateTestCharacterAsync(Guid characterId, long gold = 1000)
    {
        var character = new Character
        {
            Id = characterId,
            Name = "Test Character",
            Level = 10,
            Gold = gold,
            Profession = Profession.Warrior,
            CreatedAt = DateTime.UtcNow
        };
        _context.Characters.Add(character);
        await _context.SaveChangesAsync();
        return character;
    }

    private async Task<ShopDefinition> SeedTestShopsAsync()
    {
        var shop1 = new ShopDefinition
        {
            Id = "test_shop_1",
            Name = "Test Shop 1",
            Type = ShopType.General,
            Icon = "🏪",
            Description = "Test shop",
            IsEnabled = true,
            SortOrder = 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var shop2 = new ShopDefinition
        {
            Id = "test_shop_2",
            Name = "Test Shop 2",
            Type = ShopType.General,
            Icon = "🏪",
            Description = "Test shop 2",
            IsEnabled = true,
            SortOrder = 2,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var disabledShop = new ShopDefinition
        {
            Id = "disabled_shop",
            Name = "Disabled Shop",
            Type = ShopType.General,
            Icon = "🏪",
            Description = "Disabled",
            IsEnabled = false,
            SortOrder = 3,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.ShopDefinitions.AddRange(shop1, shop2, disabledShop);

        var item1 = new ShopItem
        {
            Id = Guid.NewGuid(),
            ShopId = "test_shop_1",
            ItemType = ShopItemType.Consumable,
            ItemDefinitionId = "test_item_1",
            DisplayName = "Test Item 1",
            PriceJson = System.Text.Json.JsonSerializer.Serialize(new Price { CurrencyType = CurrencyType.Gold, Amount = 100 }),
            StockLimit = -1,
            CurrentStock = -1,
            RequiredLevel = 1,
            IsEnabled = true,
            SortOrder = 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var item2 = new ShopItem
        {
            Id = Guid.NewGuid(),
            ShopId = "test_shop_1",
            ItemType = ShopItemType.Consumable,
            ItemDefinitionId = "test_item_2",
            DisplayName = "Test Item 2",
            PriceJson = System.Text.Json.JsonSerializer.Serialize(new Price { CurrencyType = CurrencyType.Gold, Amount = 200 }),
            StockLimit = -1,
            CurrentStock = -1,
            RequiredLevel = 1,
            IsEnabled = true,
            SortOrder = 2,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.ShopItems.AddRange(item1, item2);
        await _context.SaveChangesAsync();

        return shop1;
    }

    private async Task SeedPurchaseRecordsAsync(Guid characterId)
    {
        var record1 = new PurchaseRecord
        {
            Id = Guid.NewGuid(),
            CharacterId = characterId,
            ShopId = "test_shop_1",
            ShopItemId = Guid.NewGuid(),
            ItemDefinitionId = "test_item_1",
            Quantity = 1,
            GoldPaid = 100,
            PurchasedAt = DateTime.UtcNow.AddDays(-1)
        };

        var record2 = new PurchaseRecord
        {
            Id = Guid.NewGuid(),
            CharacterId = characterId,
            ShopId = "test_shop_1",
            ShopItemId = Guid.NewGuid(),
            ItemDefinitionId = "test_item_2",
            Quantity = 2,
            GoldPaid = 400,
            PurchasedAt = DateTime.UtcNow
        };

        _context.PurchaseRecords.AddRange(record1, record2);
        await _context.SaveChangesAsync();
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }
}
