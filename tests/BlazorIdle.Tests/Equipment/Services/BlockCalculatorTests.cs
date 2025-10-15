using BlazorIdle.Server.Domain.Equipment.Models;
using BlazorIdle.Server.Domain.Equipment.Services;
using Xunit;

namespace BlazorIdle.Tests.Equipment.Services;

/// <summary>
/// 格挡计算服务测试
/// </summary>
public class BlockCalculatorTests
{
    private readonly BlockCalculator _calculator;

    public BlockCalculatorTests()
    {
        _calculator = new BlockCalculator();
    }

    [Fact]
    public void CalculateBlockChance_WithShieldOnly_ShouldProvideBaseChance()
    {
        // Arrange
        int shieldItemLevel = 60;
        double characterStrength = 0;

        // Act
        var blockChance = _calculator.CalculateBlockChance(shieldItemLevel, characterStrength);

        // Assert
        // Base 5% + 60 * 0.2% = 5% + 12% = 17%
        Assert.Equal(0.17, blockChance, precision: 2);
    }

    [Fact]
    public void CalculateBlockChance_WithStrength_ShouldIncreaseChance()
    {
        // Arrange
        int shieldItemLevel = 60;
        double characterStrength = 100;

        // Act
        var blockChance = _calculator.CalculateBlockChance(shieldItemLevel, characterStrength);

        // Assert
        // Base 5% + 60 * 0.2% + 100 * 0.1% = 5% + 12% + 10% = 27%
        Assert.Equal(0.27, blockChance, precision: 2);
    }

    [Fact]
    public void CalculateBlockChance_VeryHighValues_ShouldCapAt50Percent()
    {
        // Arrange
        int shieldItemLevel = 500;
        double characterStrength = 1000;

        // Act
        var blockChance = _calculator.CalculateBlockChance(shieldItemLevel, characterStrength);

        // Assert
        Assert.Equal(0.50, blockChance, precision: 2);
    }

    [Theory]
    [InlineData(100, 70)]   // 100 * (1 - 0.30) = 70
    [InlineData(500, 350)]  // 500 * 0.70 = 350
    [InlineData(1000, 700)] // 1000 * 0.70 = 700
    public void ApplyBlockReduction_ShouldReduce30Percent(int incomingDamage, int expected)
    {
        // Act
        var reducedDamage = _calculator.ApplyBlockReduction(incomingDamage);

        // Assert
        Assert.Equal(expected, reducedDamage);
    }

    [Fact]
    public void RollBlock_WithHighChance_ShouldBlockFrequently()
    {
        // Arrange
        double blockChance = 0.9; // 90%
        var random = new Random(12345);
        int blocks = 0;
        int trials = 1000;

        // Act
        for (int i = 0; i < trials; i++)
        {
            if (_calculator.RollBlock(blockChance, random))
            {
                blocks++;
            }
        }

        // Assert
        // Should block approximately 90% of the time (allow some variance)
        Assert.InRange(blocks, 850, 950);
    }

    [Fact]
    public void RollBlock_WithLowChance_ShouldBlockRarely()
    {
        // Arrange
        double blockChance = 0.1; // 10%
        var random = new Random(12345);
        int blocks = 0;
        int trials = 1000;

        // Act
        for (int i = 0; i < trials; i++)
        {
            if (_calculator.RollBlock(blockChance, random))
            {
                blocks++;
            }
        }

        // Assert
        // Should block approximately 10% of the time (allow some variance)
        Assert.InRange(blocks, 50, 150);
    }

    [Fact]
    public void RollBlock_WithZeroChance_ShouldNeverBlock()
    {
        // Arrange
        double blockChance = 0.0;
        var random = new Random(12345);

        // Act & Assert
        for (int i = 0; i < 100; i++)
        {
            Assert.False(_calculator.RollBlock(blockChance, random));
        }
    }

    [Theory]
    [InlineData(WeaponType.Shield, true)]
    [InlineData(WeaponType.Sword, false)]
    [InlineData(WeaponType.Axe, false)]
    [InlineData(WeaponType.None, false)]
    public void IsShield_ShouldIdentifyShields(WeaponType weaponType, bool expected)
    {
        // Act
        var isShield = BlockCalculator.IsShield(weaponType);

        // Assert
        Assert.Equal(expected, isShield);
    }

    [Fact]
    public void GetBlockDamageReduction_ShouldReturn30Percent()
    {
        // Act
        var reduction = _calculator.GetBlockDamageReduction();

        // Assert
        Assert.Equal(0.30, reduction);
    }

    [Fact]
    public void GetMaxBlockChance_ShouldReturn50Percent()
    {
        // Act
        var maxChance = _calculator.GetMaxBlockChance();

        // Assert
        Assert.Equal(0.50, maxChance);
    }
}
