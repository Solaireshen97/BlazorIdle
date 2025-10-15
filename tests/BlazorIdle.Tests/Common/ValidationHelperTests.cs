using BlazorIdle.Server.Domain.Common.Utilities;
using Xunit;

namespace BlazorIdle.Tests.Common;

/// <summary>
/// ValidationHelper 工具类的单元测试
/// 验证所有验证方法的正常和异常情况
/// </summary>
public class ValidationHelperTests
{
    #region ValidateGuid Tests

    [Fact]
    public void ValidateGuid_ValidGuid_DoesNotThrow()
    {
        // Arrange
        var validGuid = Guid.NewGuid();

        // Act & Assert - 不应抛出异常
        var exception = Record.Exception(() => 
            ValidationHelper.ValidateGuid(validGuid, "testParam"));
        
        Assert.Null(exception);
    }

    [Fact]
    public void ValidateGuid_EmptyGuid_ThrowsArgumentException()
    {
        // Arrange
        var emptyGuid = Guid.Empty;

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            ValidationHelper.ValidateGuid(emptyGuid, "characterId"));

        Assert.Contains("characterId", exception.Message);
        Assert.Contains("不能为空", exception.Message);
        Assert.Equal("characterId", exception.ParamName);
    }

    #endregion

    #region ValidateNotNull Tests

    [Fact]
    public void ValidateNotNull_ValidObject_DoesNotThrow()
    {
        // Arrange
        var validObject = "test string";

        // Act & Assert - 不应抛出异常
        var exception = Record.Exception(() =>
            ValidationHelper.ValidateNotNull(validObject, "testParam"));

        Assert.Null(exception);
    }

    [Fact]
    public void ValidateNotNull_NullObject_ThrowsArgumentNullException()
    {
        // Arrange
        string? nullObject = null;

        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() =>
            ValidationHelper.ValidateNotNull(nullObject, "character"));

        Assert.Contains("character", exception.Message);
        Assert.Equal("character", exception.ParamName);
    }

    #endregion

    #region ValidatePositive (int) Tests

    [Theory]
    [InlineData(1)]
    [InlineData(100)]
    [InlineData(int.MaxValue)]
    public void ValidatePositive_Int_PositiveValue_DoesNotThrow(int value)
    {
        // Act & Assert - 不应抛出异常
        var exception = Record.Exception(() =>
            ValidationHelper.ValidatePositive(value, "testParam"));

        Assert.Null(exception);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    [InlineData(int.MinValue)]
    public void ValidatePositive_Int_NonPositiveValue_ThrowsArgumentException(int value)
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            ValidationHelper.ValidatePositive(value, "level"));

        Assert.Contains("level", exception.Message);
        Assert.Contains("必须为正数", exception.Message);
        Assert.Equal("level", exception.ParamName);
    }

    #endregion

    #region ValidatePositive (double) Tests

    [Theory]
    [InlineData(0.1)]
    [InlineData(1.0)]
    [InlineData(100.5)]
    public void ValidatePositive_Double_PositiveValue_DoesNotThrow(double value)
    {
        // Act & Assert - 不应抛出异常
        var exception = Record.Exception(() =>
            ValidationHelper.ValidatePositive(value, "testParam"));

        Assert.Null(exception);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-0.1)]
    [InlineData(-100.5)]
    public void ValidatePositive_Double_NonPositiveValue_ThrowsArgumentException(double value)
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            ValidationHelper.ValidatePositive(value, "damage"));

        Assert.Contains("damage", exception.Message);
        Assert.Contains("必须为正数", exception.Message);
        Assert.Equal("damage", exception.ParamName);
    }

    #endregion

    #region ValidateRange (int) Tests

    [Theory]
    [InlineData(1, 1, 10)]    // 最小边界
    [InlineData(5, 1, 10)]    // 中间值
    [InlineData(10, 1, 10)]   // 最大边界
    public void ValidateRange_Int_ValueInRange_DoesNotThrow(int value, int min, int max)
    {
        // Act & Assert - 不应抛出异常
        var exception = Record.Exception(() =>
            ValidationHelper.ValidateRange(value, min, max, "testParam"));

        Assert.Null(exception);
    }

    [Theory]
    [InlineData(0, 1, 10)]    // 小于最小值
    [InlineData(11, 1, 10)]   // 大于最大值
    [InlineData(-5, 1, 10)]   // 远小于范围
    [InlineData(100, 1, 10)]  // 远大于范围
    public void ValidateRange_Int_ValueOutOfRange_ThrowsArgumentOutOfRangeException(int value, int min, int max)
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            ValidationHelper.ValidateRange(value, min, max, "level"));

        Assert.Contains("level", exception.Message);
        Assert.Contains($"{min}", exception.Message);
        Assert.Contains($"{max}", exception.Message);
        Assert.Equal("level", exception.ParamName);
    }

    #endregion

    #region ValidateRange (double) Tests

    [Theory]
    [InlineData(0.0, 0.0, 1.0)]   // 最小边界
    [InlineData(0.5, 0.0, 1.0)]   // 中间值
    [InlineData(1.0, 0.0, 1.0)]   // 最大边界
    public void ValidateRange_Double_ValueInRange_DoesNotThrow(double value, double min, double max)
    {
        // Act & Assert - 不应抛出异常
        var exception = Record.Exception(() =>
            ValidationHelper.ValidateRange(value, min, max, "testParam"));

        Assert.Null(exception);
    }

    [Theory]
    [InlineData(-0.1, 0.0, 1.0)]  // 小于最小值
    [InlineData(1.1, 0.0, 1.0)]   // 大于最大值
    public void ValidateRange_Double_ValueOutOfRange_ThrowsArgumentOutOfRangeException(double value, double min, double max)
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            ValidationHelper.ValidateRange(value, min, max, "criticalChance"));

        Assert.Contains("criticalChance", exception.Message);
        Assert.Equal("criticalChance", exception.ParamName);
    }

    #endregion

    #region ValidateNonNegative (int) Tests

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(100)]
    [InlineData(int.MaxValue)]
    public void ValidateNonNegative_Int_NonNegativeValue_DoesNotThrow(int value)
    {
        // Act & Assert - 不应抛出异常
        var exception = Record.Exception(() =>
            ValidationHelper.ValidateNonNegative(value, "testParam"));

        Assert.Null(exception);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-100)]
    [InlineData(int.MinValue)]
    public void ValidateNonNegative_Int_NegativeValue_ThrowsArgumentException(int value)
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            ValidationHelper.ValidateNonNegative(value, "count"));

        Assert.Contains("count", exception.Message);
        Assert.Contains("不能为负数", exception.Message);
        Assert.Equal("count", exception.ParamName);
    }

    #endregion

    #region ValidateNonNegative (double) Tests

    [Theory]
    [InlineData(0.0)]
    [InlineData(0.1)]
    [InlineData(100.5)]
    public void ValidateNonNegative_Double_NonNegativeValue_DoesNotThrow(double value)
    {
        // Act & Assert - 不应抛出异常
        var exception = Record.Exception(() =>
            ValidationHelper.ValidateNonNegative(value, "testParam"));

        Assert.Null(exception);
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(-100.5)]
    public void ValidateNonNegative_Double_NegativeValue_ThrowsArgumentException(double value)
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            ValidationHelper.ValidateNonNegative(value, "amount"));

        Assert.Contains("amount", exception.Message);
        Assert.Contains("不能为负数", exception.Message);
        Assert.Equal("amount", exception.ParamName);
    }

    #endregion
}
