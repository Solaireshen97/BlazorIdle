using BlazorIdle.Server.Infrastructure.Configuration;
using Microsoft.Extensions.Options;
using Xunit;

namespace BlazorIdle.Tests.Shop;

/// <summary>
/// 商店配置测试
/// 测试配置外部化和配置验证功能
/// </summary>
public class ShopConfigurationTests
{
    #region 稀有度排序配置测试

    [Fact]
    public void ShopOptions_DefaultRarityOrderWeights_ShouldBeConfigured()
    {
        // Arrange & Act
        var options = new ShopOptions();

        // Assert
        Assert.NotNull(options.RarityOrderWeights);
        Assert.Equal(5, options.RarityOrderWeights.Count);
        Assert.Equal(1, options.RarityOrderWeights["common"]);
        Assert.Equal(2, options.RarityOrderWeights["uncommon"]);
        Assert.Equal(3, options.RarityOrderWeights["rare"]);
        Assert.Equal(4, options.RarityOrderWeights["epic"]);
        Assert.Equal(5, options.RarityOrderWeights["legendary"]);
    }

    [Fact]
    public void ShopOptions_CustomRarityOrderWeights_ShouldWork()
    {
        // Arrange
        var options = new ShopOptions
        {
            RarityOrderWeights = new Dictionary<string, int>
            {
                { "common", 10 },
                { "rare", 20 },
                { "mythic", 30 }
            }
        };

        // Act & Assert
        Assert.Equal(3, options.RarityOrderWeights.Count);
        Assert.Equal(10, options.RarityOrderWeights["common"]);
        Assert.Equal(20, options.RarityOrderWeights["rare"]);
        Assert.Equal(30, options.RarityOrderWeights["mythic"]);
    }

    #endregion

    #region 配置验证测试

    [Fact]
    public void ShopOptionsValidator_ValidConfiguration_ShouldPass()
    {
        // Arrange
        var validator = new ShopOptionsValidator();
        var options = new ShopOptions(); // 使用默认值

        // Act
        var result = validator.Validate(null, options);

        // Assert
        Assert.True(result.Succeeded);
    }

    [Fact]
    public void ShopOptionsValidator_NegativeCacheMinutes_ShouldFail()
    {
        // Arrange
        var validator = new ShopOptionsValidator();
        var options = new ShopOptions
        {
            ShopDefinitionCacheMinutes = -10
        };

        // Act
        var result = validator.Validate(null, options);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Contains("ShopDefinitionCacheMinutes 不能为负数", result.FailureMessage);
    }

    [Fact]
    public void ShopOptionsValidator_MinGreaterThanMax_PurchaseQuantity_ShouldFail()
    {
        // Arrange
        var validator = new ShopOptionsValidator();
        var options = new ShopOptions
        {
            MinPurchaseQuantity = 100,
            MaxPurchaseQuantity = 10
        };

        // Act
        var result = validator.Validate(null, options);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Contains("MinPurchaseQuantity", result.FailureMessage);
        Assert.Contains("MaxPurchaseQuantity", result.FailureMessage);
    }

    [Fact]
    public void ShopOptionsValidator_MinGreaterThanMax_Price_ShouldFail()
    {
        // Arrange
        var validator = new ShopOptionsValidator();
        var options = new ShopOptions
        {
            MinPriceAmount = 1000,
            MaxPriceAmount = 100
        };

        // Act
        var result = validator.Validate(null, options);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Contains("MinPriceAmount", result.FailureMessage);
        Assert.Contains("MaxPriceAmount", result.FailureMessage);
    }

    [Fact]
    public void ShopOptionsValidator_MinGreaterThanMax_PageSize_ShouldFail()
    {
        // Arrange
        var validator = new ShopOptionsValidator();
        var options = new ShopOptions
        {
            DefaultPageSize = 200,
            MaxPageSize = 100
        };

        // Act
        var result = validator.Validate(null, options);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Contains("DefaultPageSize", result.FailureMessage);
        Assert.Contains("MaxPageSize", result.FailureMessage);
    }

    [Fact]
    public void ShopOptionsValidator_NegativeResetSeconds_ShouldFail()
    {
        // Arrange
        var validator = new ShopOptionsValidator();
        var options = new ShopOptions
        {
            DailyResetSeconds = -100
        };

        // Act
        var result = validator.Validate(null, options);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Contains("DailyResetSeconds 不能为负数", result.FailureMessage);
    }

    [Fact]
    public void ShopOptionsValidator_InvalidLevelRequirement_ShouldFail()
    {
        // Arrange
        var validator = new ShopOptionsValidator();
        var options = new ShopOptions
        {
            MinLevelRequirement = 100,
            MaxLevelRequirement = 10
        };

        // Act
        var result = validator.Validate(null, options);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Contains("MinLevelRequirement", result.FailureMessage);
        Assert.Contains("MaxLevelRequirement", result.FailureMessage);
    }

    [Fact]
    public void ShopOptionsValidator_EmptyConfigPath_ShouldFail()
    {
        // Arrange
        var validator = new ShopOptionsValidator();
        var options = new ShopOptions
        {
            ConfigPath = ""
        };

        // Act
        var result = validator.Validate(null, options);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Contains("ConfigPath 不能为空", result.FailureMessage);
    }

    [Fact]
    public void ShopOptionsValidator_EmptyRarityWeights_ShouldFail()
    {
        // Arrange
        var validator = new ShopOptionsValidator();
        var options = new ShopOptions
        {
            RarityOrderWeights = new Dictionary<string, int>()
        };

        // Act
        var result = validator.Validate(null, options);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Contains("RarityOrderWeights 不能为空", result.FailureMessage);
    }

    [Fact]
    public void ShopOptionsValidator_NegativeRarityWeight_ShouldFail()
    {
        // Arrange
        var validator = new ShopOptionsValidator();
        var options = new ShopOptions
        {
            RarityOrderWeights = new Dictionary<string, int>
            {
                { "common", 1 },
                { "rare", -5 }
            }
        };

        // Act
        var result = validator.Validate(null, options);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Contains("稀有度权重不能为负数", result.FailureMessage);
        Assert.Contains("rare=-5", result.FailureMessage);
    }

    [Fact]
    public void ShopOptionsValidator_ZeroMinPurchaseQuantity_ShouldFail()
    {
        // Arrange
        var validator = new ShopOptionsValidator();
        var options = new ShopOptions
        {
            MinPurchaseQuantity = 0
        };

        // Act
        var result = validator.Validate(null, options);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Contains("MinPurchaseQuantity 必须至少为 1", result.FailureMessage);
    }

    #endregion
}
