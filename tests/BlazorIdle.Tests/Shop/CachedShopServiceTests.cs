using BlazorIdle.Server.Application.Abstractions;
using BlazorIdle.Server.Application.Shop;
using BlazorIdle.Shared.Models.Shop;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace BlazorIdle.Tests.Shop;

/// <summary>
/// 缓存商店服务测试
/// </summary>
public class CachedShopServiceTests
{
    private readonly Mock<IShopService> _mockInnerService;
    private readonly IMemoryCache _cache;
    private readonly ShopSettings _settings;
    private readonly CachedShopService _cachedService;

    public CachedShopServiceTests()
    {
        _mockInnerService = new Mock<IShopService>();
        _cache = new MemoryCache(new MemoryCacheOptions());
        _settings = new ShopSettings
        {
            EnableCache = true,
            ShopDefinitionCacheMinutes = 5,
            ShopItemsCacheMinutes = 2
        };

        var settingsOptions = Options.Create(_settings);
        _cachedService = new CachedShopService(_mockInnerService.Object, _cache, settingsOptions);
    }

    [Fact]
    public async Task ListShopsAsync_WithCacheEnabled_ShouldCacheResult()
    {
        // Arrange
        var characterId = Guid.NewGuid().ToString();
        var expectedResponse = new ListShopsResponse
        {
            Shops = new List<ShopDto>
            {
                new ShopDto { Id = "test_shop", Name = "Test Shop" }
            }
        };

        _mockInnerService
            .Setup(x => x.ListShopsAsync(characterId))
            .ReturnsAsync(expectedResponse);

        // Act - 第一次调用
        var result1 = await _cachedService.ListShopsAsync(characterId);
        
        // Act - 第二次调用（应该从缓存获取）
        var result2 = await _cachedService.ListShopsAsync(characterId);

        // Assert
        Assert.Same(result1, result2); // 应该是同一个对象实例（从缓存获取）
        _mockInnerService.Verify(x => x.ListShopsAsync(characterId), Times.Once); // 只调用一次内部服务
    }

    [Fact]
    public async Task GetShopItemsAsync_WithCacheEnabled_ShouldCacheResult()
    {
        // Arrange
        var shopId = "test_shop";
        var characterId = Guid.NewGuid().ToString();
        var expectedResponse = new ListShopItemsResponse
        {
            Items = new List<ShopItemDto>
            {
                new ShopItemDto { Id = "item1", ItemName = "Test Item" }
            }
        };

        _mockInnerService
            .Setup(x => x.GetShopItemsAsync(shopId, characterId))
            .ReturnsAsync(expectedResponse);

        // Act - 第一次调用
        var result1 = await _cachedService.GetShopItemsAsync(shopId, characterId);
        
        // Act - 第二次调用（应该从缓存获取）
        var result2 = await _cachedService.GetShopItemsAsync(shopId, characterId);

        // Assert
        Assert.Same(result1, result2); // 应该是同一个对象实例（从缓存获取）
        _mockInnerService.Verify(x => x.GetShopItemsAsync(shopId, characterId), Times.Once); // 只调用一次内部服务
    }

    [Fact]
    public async Task PurchaseItemAsync_OnSuccess_ShouldInvalidateCache()
    {
        // Arrange
        var characterId = Guid.NewGuid().ToString();
        var purchaseRequest = new PurchaseRequest
        {
            ShopItemId = "item1",
            Quantity = 1
        };

        var shopsResponse = new ListShopsResponse
        {
            Shops = new List<ShopDto>
            {
                new ShopDto { Id = "test_shop", Name = "Test Shop" }
            }
        };

        _mockInnerService
            .Setup(x => x.ListShopsAsync(characterId))
            .ReturnsAsync(shopsResponse);

        _mockInnerService
            .Setup(x => x.PurchaseItemAsync(characterId, purchaseRequest))
            .ReturnsAsync(new PurchaseResponse { Success = true });

        // Act - 先查询商店（建立缓存）
        await _cachedService.ListShopsAsync(characterId);
        
        // Act - 购买物品（应该清除缓存）
        await _cachedService.PurchaseItemAsync(characterId, purchaseRequest);
        
        // Act - 再次查询商店（应该重新调用内部服务）
        await _cachedService.ListShopsAsync(characterId);

        // Assert
        _mockInnerService.Verify(x => x.ListShopsAsync(characterId), Times.Exactly(2)); // 调用两次内部服务
    }

    [Fact]
    public async Task ListShopsAsync_WithCacheDisabled_ShouldNotCache()
    {
        // Arrange
        var characterId = Guid.NewGuid().ToString();
        var settings = new ShopSettings { EnableCache = false };
        var settingsOptions = Options.Create(settings);
        var service = new CachedShopService(_mockInnerService.Object, _cache, settingsOptions);

        var expectedResponse = new ListShopsResponse
        {
            Shops = new List<ShopDto>
            {
                new ShopDto { Id = "test_shop", Name = "Test Shop" }
            }
        };

        _mockInnerService
            .Setup(x => x.ListShopsAsync(characterId))
            .ReturnsAsync(expectedResponse);

        // Act - 调用两次
        await service.ListShopsAsync(characterId);
        await service.ListShopsAsync(characterId);

        // Assert
        _mockInnerService.Verify(x => x.ListShopsAsync(characterId), Times.Exactly(2)); // 应该调用两次内部服务（没有缓存）
    }

    [Fact]
    public async Task PurchaseItemAsync_OnFailure_ShouldNotInvalidateCache()
    {
        // Arrange
        var characterId = Guid.NewGuid().ToString();
        var purchaseRequest = new PurchaseRequest
        {
            ShopItemId = "item1",
            Quantity = 1
        };

        var shopsResponse = new ListShopsResponse
        {
            Shops = new List<ShopDto>
            {
                new ShopDto { Id = "test_shop", Name = "Test Shop" }
            }
        };

        _mockInnerService
            .Setup(x => x.ListShopsAsync(characterId))
            .ReturnsAsync(shopsResponse);

        _mockInnerService
            .Setup(x => x.PurchaseItemAsync(characterId, purchaseRequest))
            .ReturnsAsync(new PurchaseResponse { Success = false, Message = "金币不足" });

        // Act - 先查询商店（建立缓存）
        await _cachedService.ListShopsAsync(characterId);
        
        // Act - 购买失败（不应该清除缓存）
        await _cachedService.PurchaseItemAsync(characterId, purchaseRequest);
        
        // Act - 再次查询商店（应该从缓存获取）
        await _cachedService.ListShopsAsync(characterId);

        // Assert
        _mockInnerService.Verify(x => x.ListShopsAsync(characterId), Times.Once); // 只调用一次内部服务
    }
}
