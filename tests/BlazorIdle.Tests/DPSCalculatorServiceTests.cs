using BlazorIdle.Client.Services;
using Xunit;

namespace BlazorIdle.Tests;

/// <summary>
/// DPS计算服务测试
/// </summary>
public class DPSCalculatorServiceTests
{
    private readonly DPSCalculatorService _calculator;

    public DPSCalculatorServiceTests()
    {
        _calculator = new DPSCalculatorService();
    }

    [Fact]
    public void CalculateDPS_WithNoWeapon_ReturnsZero()
    {
        // Arrange
        var attackPower = 100.0;
        var weapon = "None";

        // Act
        var dps = _calculator.CalculateDPS(attackPower, weapon);

        // Assert
        Assert.Equal(0, dps);
    }

    [Fact]
    public void CalculateDPS_WithSingleHandSword_ReturnsCorrectDPS()
    {
        // Arrange
        var attackPower = 100.0;
        var weapon = "Sword";
        
        // 单手剑: 攻击速度2.4s, DPS系数0.42
        // 武器伤害倍率 = 2.4 * 0.42 = 1.008
        // 基础DPS = 100 * 1.008 / 2.4 = 42

        // Act
        var dps = _calculator.CalculateDPS(attackPower, weapon);

        // Assert
        Assert.Equal(42.0, dps, precision: 1);
    }

    [Fact]
    public void CalculateDPS_WithTwoHandSword_ReturnsCorrectDPS()
    {
        // Arrange
        var attackPower = 100.0;
        var weapon = "TwoHandSword";
        
        // 双手剑: 攻击速度3.4s, DPS系数0.50
        // 武器伤害倍率 = 3.4 * 0.50 = 1.7
        // 基础DPS = 100 * 1.7 / 3.4 = 50

        // Act
        var dps = _calculator.CalculateDPS(attackPower, weapon);

        // Assert
        Assert.Equal(50.0, dps, precision: 1);
    }

    [Fact]
    public void CalculateDPS_WithDagger_ReturnsCorrectDPS()
    {
        // Arrange
        var attackPower = 100.0;
        var weapon = "Dagger";
        
        // 匕首: 攻击速度1.8s, DPS系数0.40
        // 武器伤害倍率 = 1.8 * 0.40 = 0.72
        // 基础DPS = 100 * 0.72 / 1.8 = 40

        // Act
        var dps = _calculator.CalculateDPS(attackPower, weapon);

        // Assert
        Assert.Equal(40.0, dps, precision: 1);
    }

    [Fact]
    public void CalculateDPS_WithHaste_IncreasesDPS()
    {
        // Arrange
        var attackPower = 100.0;
        var weapon = "Sword";
        var hastePercent = 0.20; // 20%急速
        
        // 单手剑基础攻速2.4s，20%急速后 = 2.4 / 1.2 = 2.0s
        // 武器伤害倍率 = 2.4 * 0.42 = 1.008
        // 基础DPS = 100 * 1.008 / 2.0 = 50.4

        // Act
        var dps = _calculator.CalculateDPS(attackPower, weapon, null, hastePercent);

        // Assert
        Assert.Equal(50.4, dps, precision: 1);
    }

    [Fact]
    public void CalculateDPS_WithCrit_IncreasesDPS()
    {
        // Arrange
        var attackPower = 100.0;
        var weapon = "Sword";
        var critChance = 0.25; // 25%暴击率
        var critMultiplier = 2.0;
        
        // 武器伤害倍率 = 2.4 * 0.42 = 1.008
        // 每次攻击伤害 = 100 * 1.008 = 100.8
        // 平均伤害 = 100.8 * (0.75 + 0.25 * 2.0) = 100.8 * 1.25 = 126
        // DPS = 126 / 2.4 = 52.5

        // Act
        var dps = _calculator.CalculateDPS(attackPower, weapon, null, 0, critChance, critMultiplier);

        // Assert
        Assert.Equal(52.5, dps, precision: 1);
    }

    [Fact]
    public void CalculateDPS_WithDualWield_ReturnsCorrectDPS()
    {
        // Arrange
        var attackPower = 100.0;
        var mainHand = "Sword";
        var offHand = "Dagger";
        
        // 主手剑: 伤害倍率 = 2.4 * 0.42 = 1.008
        // 副手匕首: 伤害倍率 = 1.8 * 0.40 = 0.72
        // 主手伤害 = 100 * 1.008 = 100.8
        // 副手伤害 = 100 * 0.72 * 0.85 * 0.81 = 49.572 (副手系数0.85，命中率0.81)
        // 总伤害 = 100.8 + 49.572 = 150.372
        // DPS = 150.372 / 2.4 = 62.655

        // Act
        var dps = _calculator.CalculateDPS(attackPower, mainHand, offHand);

        // Assert
        Assert.Equal(62.7, dps, precision: 0);
    }

    [Fact]
    public void CalculateDPS_DualWieldWithShield_DoesNotCountAsDualWield()
    {
        // Arrange
        var attackPower = 100.0;
        var mainHand = "Sword";
        var offHand = "Shield";
        
        // 盾牌不算双持，只计算主手伤害

        // Act
        var dps = _calculator.CalculateDPS(attackPower, mainHand, offHand);

        // Assert
        // 应该等于单手剑DPS
        var singleHandDps = _calculator.CalculateDPS(attackPower, mainHand);
        Assert.Equal(singleHandDps, dps, precision: 1);
    }

    [Fact]
    public void FormatDPS_LargeValue_UsesKFormat()
    {
        // Arrange
        var dps = 1234.5;

        // Act
        var formatted = _calculator.FormatDPS(dps);

        // Assert
        Assert.Equal("1.23K", formatted);
    }

    [Fact]
    public void FormatDPS_VeryLargeValue_UsesMFormat()
    {
        // Arrange
        var dps = 1234567.8;

        // Act
        var formatted = _calculator.FormatDPS(dps);

        // Assert
        Assert.Equal("1.23M", formatted);
    }

    [Fact]
    public void FormatDPS_SmallValue_UsesPlainFormat()
    {
        // Arrange
        var dps = 123.45;

        // Act
        var formatted = _calculator.FormatDPS(dps);

        // Assert
        Assert.Equal("123.5", formatted);
    }

    [Fact]
    public void GetAttackSpeedText_WithoutHaste_ReturnsBaseSpeed()
    {
        // Arrange
        var weapon = "Sword";

        // Act
        var text = _calculator.GetAttackSpeedText(weapon);

        // Assert
        Assert.Equal("2.4s", text);
    }

    [Fact]
    public void GetAttackSpeedText_WithHaste_ReturnsSpeedWithHaste()
    {
        // Arrange
        var weapon = "Sword";
        var hastePercent = 0.25; // 25%急速

        // Act
        var text = _calculator.GetAttackSpeedText(weapon, hastePercent);

        // Assert
        Assert.Contains("2.4s", text);
        Assert.Contains("1.92s", text);
        Assert.Contains("25%", text);
    }

    [Fact]
    public void IsTwoHandedWeapon_WithTwoHandSword_ReturnsTrue()
    {
        // Act
        var result = _calculator.IsTwoHandedWeapon("TwoHandSword");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsTwoHandedWeapon_WithSword_ReturnsFalse()
    {
        // Act
        var result = _calculator.IsTwoHandedWeapon("Sword");

        // Assert
        Assert.False(result);
    }

    [Theory]
    [InlineData("Bow", true)]
    [InlineData("Staff", true)]
    [InlineData("Polearm", true)]
    [InlineData("Dagger", false)]
    [InlineData("Shield", false)]
    public void IsTwoHandedWeapon_VariousWeapons_ReturnsCorrectResult(string weaponType, bool expected)
    {
        // Act
        var result = _calculator.IsTwoHandedWeapon(weaponType);

        // Assert
        Assert.Equal(expected, result);
    }
}
