using BlazorIdle.Server.Domain.Shop;
using BlazorIdle.Server.Domain.Shop.ValueObjects;
using Xunit;

namespace BlazorIdle.Tests.Shop.Domain;

/// <summary>
/// 商品领域模型测试
/// </summary>
public class ShopItemTests
{
    [Fact]
    public void HasStock_WithUnlimitedStock_ShouldReturnTrue()
    {
        // Arrange
        var item = new ShopItem
        {
            StockLimit = -1
        };

        // Act
        var hasStock = item.HasStock();

        // Assert
        Assert.True(hasStock);
    }

    [Fact]
    public void HasStock_WithAvailableStock_ShouldReturnTrue()
    {
        // Arrange
        var item = new ShopItem
        {
            StockLimit = 10,
            CurrentStock = 5
        };

        // Act
        var hasStock = item.HasStock();

        // Assert
        Assert.True(hasStock);
    }

    [Fact]
    public void HasStock_WithZeroStock_ShouldReturnFalse()
    {
        // Arrange
        var item = new ShopItem
        {
            StockLimit = 10,
            CurrentStock = 0
        };

        // Act
        var hasStock = item.HasStock();

        // Assert
        Assert.False(hasStock);
    }

    [Fact]
    public void DecreaseStock_WithUnlimitedStock_ShouldAlwaysSucceed()
    {
        // Arrange
        var item = new ShopItem
        {
            StockLimit = -1
        };

        // Act
        var result = item.DecreaseStock(100);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void DecreaseStock_WithSufficientStock_ShouldDecreaseAndSucceed()
    {
        // Arrange
        var item = new ShopItem
        {
            StockLimit = 10,
            CurrentStock = 10
        };

        // Act
        var result = item.DecreaseStock(3);

        // Assert
        Assert.True(result);
        Assert.Equal(7, item.CurrentStock);
    }

    [Fact]
    public void DecreaseStock_WithInsufficientStock_ShouldFail()
    {
        // Arrange
        var item = new ShopItem
        {
            StockLimit = 10,
            CurrentStock = 2
        };

        // Act
        var result = item.DecreaseStock(5);

        // Assert
        Assert.False(result);
        Assert.Equal(2, item.CurrentStock); // Stock should not change
    }

    [Fact]
    public void Price_ShouldSerializeAndDeserializeCorrectly()
    {
        // Arrange
        var item = new ShopItem();
        var price = new Price
        {
            CurrencyType = CurrencyType.Gold,
            Amount = 100
        };

        // Act
        item.Price = price;
        var retrievedPrice = item.Price;

        // Assert
        Assert.Equal(CurrencyType.Gold, retrievedPrice.CurrencyType);
        Assert.Equal(100, retrievedPrice.Amount);
    }

    [Fact]
    public void PurchaseLimit_ShouldSerializeAndDeserializeCorrectly()
    {
        // Arrange
        var item = new ShopItem();
        var limit = new PurchaseLimit
        {
            LimitType = LimitType.Daily,
            MaxPurchases = 5
        };

        // Act
        item.PurchaseLimit = limit;
        var retrievedLimit = item.PurchaseLimit;

        // Assert
        Assert.NotNull(retrievedLimit);
        Assert.Equal(LimitType.Daily, retrievedLimit!.LimitType);
        Assert.Equal(5, retrievedLimit.MaxPurchases);
    }

    [Fact]
    public void PurchaseLimit_Null_ShouldReturnNull()
    {
        // Arrange
        var item = new ShopItem
        {
            PurchaseLimitJson = null
        };

        // Act
        var limit = item.PurchaseLimit;

        // Assert
        Assert.Null(limit);
    }
}
