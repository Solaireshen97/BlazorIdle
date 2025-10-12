using BlazorIdle.Server.Domain.Shop;
using BlazorIdle.Server.Domain.Shop.ValueObjects;
using System.Text.Json;
using Xunit;

namespace BlazorIdle.Tests.Shop;

/// <summary>
/// 商店系统领域模型单元测试
/// </summary>
public class ShopDomainTests
{
    #region Price Tests

    [Fact]
    public void Price_IsValid_WithValidGoldPrice_ShouldReturnTrue()
    {
        // Arrange
        var price = new Price
        {
            CurrencyType = CurrencyType.Gold,
            Amount = 100
        };

        // Act & Assert
        Assert.True(price.IsValid());
    }

    [Fact]
    public void Price_IsValid_WithZeroAmount_ShouldReturnFalse()
    {
        // Arrange
        var price = new Price
        {
            CurrencyType = CurrencyType.Gold,
            Amount = 0
        };

        // Act & Assert
        Assert.False(price.IsValid());
    }

    [Fact]
    public void Price_IsValid_WithItemCurrencyButNoCurrencyId_ShouldReturnFalse()
    {
        // Arrange
        var price = new Price
        {
            CurrencyType = CurrencyType.Item,
            Amount = 10,
            CurrencyId = null
        };

        // Act & Assert
        Assert.False(price.IsValid());
    }

    [Fact]
    public void Price_IsValid_WithItemCurrencyAndCurrencyId_ShouldReturnTrue()
    {
        // Arrange
        var price = new Price
        {
            CurrencyType = CurrencyType.Item,
            Amount = 10,
            CurrencyId = "dragon_scale"
        };

        // Act & Assert
        Assert.True(price.IsValid());
    }

    #endregion

    #region PurchaseLimit Tests

    [Fact]
    public void PurchaseLimit_IsUnlimited_WithUnlimitedType_ShouldReturnTrue()
    {
        // Arrange
        var limit = new PurchaseLimit
        {
            Type = LimitType.Unlimited
        };

        // Act & Assert
        Assert.True(limit.IsUnlimited());
    }

    [Fact]
    public void PurchaseLimit_IsUnlimited_WithPerCharacterType_ShouldReturnFalse()
    {
        // Arrange
        var limit = new PurchaseLimit
        {
            Type = LimitType.PerCharacter,
            MaxPurchases = 1
        };

        // Act & Assert
        Assert.False(limit.IsUnlimited());
    }

    [Fact]
    public void PurchaseLimit_IsValid_WithUnlimitedType_ShouldReturnTrue()
    {
        // Arrange
        var limit = new PurchaseLimit
        {
            Type = LimitType.Unlimited
        };

        // Act & Assert
        Assert.True(limit.IsValid());
    }

    [Fact]
    public void PurchaseLimit_IsValid_WithDailyLimitAndValidMaxPurchases_ShouldReturnTrue()
    {
        // Arrange
        var limit = new PurchaseLimit
        {
            Type = LimitType.Daily,
            MaxPurchases = 5
        };

        // Act & Assert
        Assert.True(limit.IsValid());
    }

    [Fact]
    public void PurchaseLimit_IsValid_WithDailyLimitButZeroMaxPurchases_ShouldReturnFalse()
    {
        // Arrange
        var limit = new PurchaseLimit
        {
            Type = LimitType.Daily,
            MaxPurchases = 0
        };

        // Act & Assert
        Assert.False(limit.IsValid());
    }

    #endregion

    #region ShopItem Tests

    [Fact]
    public void ShopItem_GetPrice_ShouldDeserializeCorrectly()
    {
        // Arrange
        var price = new Price
        {
            CurrencyType = CurrencyType.Gold,
            Amount = 500
        };
        var item = new ShopItem
        {
            Id = "test_item",
            ShopId = "test_shop",
            ItemDefinitionId = "sword",
            ItemName = "铁剑",
            ItemIcon = "⚔️",
            PriceJson = JsonSerializer.Serialize(price),
            PurchaseLimitJson = JsonSerializer.Serialize(new PurchaseLimit { Type = LimitType.Unlimited })
        };

        // Act
        var deserializedPrice = item.GetPrice();

        // Assert
        Assert.Equal(CurrencyType.Gold, deserializedPrice.CurrencyType);
        Assert.Equal(500, deserializedPrice.Amount);
    }

    [Fact]
    public void ShopItem_SetPrice_ShouldSerializeCorrectly()
    {
        // Arrange
        var item = new ShopItem
        {
            Id = "test_item",
            ShopId = "test_shop",
            ItemDefinitionId = "sword",
            ItemName = "铁剑",
            ItemIcon = "⚔️",
            PurchaseLimitJson = JsonSerializer.Serialize(new PurchaseLimit { Type = LimitType.Unlimited })
        };
        var price = new Price
        {
            CurrencyType = CurrencyType.Gold,
            Amount = 1000
        };

        // Act
        item.SetPrice(price);
        var deserializedPrice = item.GetPrice();

        // Assert
        Assert.Equal(CurrencyType.Gold, deserializedPrice.CurrencyType);
        Assert.Equal(1000, deserializedPrice.Amount);
    }

    [Fact]
    public void ShopItem_GetPurchaseLimit_ShouldDeserializeCorrectly()
    {
        // Arrange
        var limit = new PurchaseLimit
        {
            Type = LimitType.Daily,
            MaxPurchases = 5
        };
        var item = new ShopItem
        {
            Id = "test_item",
            ShopId = "test_shop",
            ItemDefinitionId = "potion",
            ItemName = "药水",
            ItemIcon = "🧪",
            PriceJson = JsonSerializer.Serialize(new Price { CurrencyType = CurrencyType.Gold, Amount = 50 }),
            PurchaseLimitJson = JsonSerializer.Serialize(limit)
        };

        // Act
        var deserializedLimit = item.GetPurchaseLimit();

        // Assert
        Assert.Equal(LimitType.Daily, deserializedLimit.Type);
        Assert.Equal(5, deserializedLimit.MaxPurchases);
    }

    #endregion

    #region PurchaseCounter Tests

    [Fact]
    public void PurchaseCounter_GenerateId_ShouldCreateCorrectFormat()
    {
        // Arrange
        var characterId = Guid.NewGuid();
        var shopItemId = "test_item";

        // Act
        var id = PurchaseCounter.GenerateId(characterId, shopItemId);

        // Assert
        Assert.Equal($"{characterId}_test_item", id);
    }

    [Fact]
    public void PurchaseCounter_ShouldReset_WhenPeriodExpired_ShouldReturnTrue()
    {
        // Arrange
        var counter = new PurchaseCounter
        {
            Id = "test_counter",
            CharacterId = Guid.NewGuid(),
            ShopItemId = "test_item",
            PurchaseCount = 5,
            PeriodStartAt = DateTime.UtcNow.AddDays(-2), // 2天前
            LastPurchasedAt = DateTime.UtcNow.AddDays(-1)
        };

        // Act
        var shouldReset = counter.ShouldReset(86400); // 1天周期

        // Assert
        Assert.True(shouldReset);
    }

    [Fact]
    public void PurchaseCounter_ShouldReset_WhenPeriodNotExpired_ShouldReturnFalse()
    {
        // Arrange
        var counter = new PurchaseCounter
        {
            Id = "test_counter",
            CharacterId = Guid.NewGuid(),
            ShopItemId = "test_item",
            PurchaseCount = 2,
            PeriodStartAt = DateTime.UtcNow.AddHours(-12), // 12小时前
            LastPurchasedAt = DateTime.UtcNow.AddHours(-6)
        };

        // Act
        var shouldReset = counter.ShouldReset(86400); // 1天周期

        // Assert
        Assert.False(shouldReset);
    }

    [Fact]
    public void PurchaseCounter_Reset_ShouldResetCountAndPeriodStart()
    {
        // Arrange
        var counter = new PurchaseCounter
        {
            Id = "test_counter",
            CharacterId = Guid.NewGuid(),
            ShopItemId = "test_item",
            PurchaseCount = 10,
            PeriodStartAt = DateTime.UtcNow.AddDays(-7),
            LastPurchasedAt = DateTime.UtcNow.AddDays(-1)
        };
        var beforeReset = DateTime.UtcNow;

        // Act
        counter.Reset();

        // Assert
        Assert.Equal(0, counter.PurchaseCount);
        Assert.True(counter.PeriodStartAt >= beforeReset);
    }

    [Fact]
    public void PurchaseCounter_IncrementCount_ShouldIncreaseCountAndUpdateTime()
    {
        // Arrange
        var counter = new PurchaseCounter
        {
            Id = "test_counter",
            CharacterId = Guid.NewGuid(),
            ShopItemId = "test_item",
            PurchaseCount = 0,
            PeriodStartAt = DateTime.UtcNow,
            LastPurchasedAt = DateTime.UtcNow
        };
        var beforeIncrement = DateTime.UtcNow;

        // Act
        counter.IncrementCount(3);

        // Assert
        Assert.Equal(3, counter.PurchaseCount);
        Assert.True(counter.LastPurchasedAt >= beforeIncrement);
    }

    #endregion
}
