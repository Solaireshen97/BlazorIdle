using BlazorIdle.Server.Domain.Equipment.Models;
using BlazorIdle.Server.Domain.Equipment.Services;
using Xunit;

namespace BlazorIdle.Tests.Equipment.Services;

/// <summary>
/// 攻击速度计算服务测试
/// </summary>
public class AttackSpeedCalculatorTests
{
    private readonly AttackSpeedCalculator _calculator;

    public AttackSpeedCalculatorTests()
    {
        _calculator = new AttackSpeedCalculator();
    }

    [Theory]
    [InlineData(WeaponType.Dagger, 1.8)]
    [InlineData(WeaponType.Sword, 2.4)]
    [InlineData(WeaponType.TwoHandSword, 3.4)]
    [InlineData(WeaponType.Staff, 3.0)]
    [InlineData(WeaponType.Bow, 2.8)]
    public void GetBaseAttackSpeed_ShouldReturnCorrectSpeed(WeaponType weaponType, double expected)
    {
        // Act
        var speed = _calculator.GetBaseAttackSpeed(weaponType);

        // Assert
        Assert.Equal(expected, speed);
    }

    [Theory]
    [InlineData(WeaponType.Sword, 0.0, 2.4)]    // 2.4 / (1 + 0) = 2.4
    [InlineData(WeaponType.Sword, 0.1, 2.18)]   // 2.4 / (1 + 0.1) ≈ 2.18
    [InlineData(WeaponType.Sword, 0.25, 1.92)]  // 2.4 / (1 + 0.25) = 1.92
    [InlineData(WeaponType.Dagger, 0.2, 1.5)]   // 1.8 / (1 + 0.2) = 1.5
    public void CalculateAttackSpeed_WithHaste_ShouldReduceAttackInterval(
        WeaponType weaponType, double hastePercent, double expected)
    {
        // Act
        var speed = _calculator.CalculateAttackSpeed(weaponType, hastePercent);

        // Assert
        Assert.Equal(expected, speed, precision: 2);
    }

    [Fact]
    public void CalculateAttackSpeed_WithNegativeHaste_ShouldCapAtMinus50Percent()
    {
        // Arrange
        var weaponType = WeaponType.Sword;
        double hastePercent = -0.8; // -80% haste (should cap at -50%)

        // Act
        var speed = _calculator.CalculateAttackSpeed(weaponType, hastePercent);

        // Assert
        // 2.4 / (1 - 0.5) = 2.4 / 0.5 = 4.8
        Assert.Equal(4.8, speed, precision: 1);
    }

    [Theory]
    [InlineData(WeaponType.Dagger, 0.40)]
    [InlineData(WeaponType.Sword, 0.42)]
    [InlineData(WeaponType.TwoHandSword, 0.50)]
    [InlineData(WeaponType.Staff, 0.52)]
    [InlineData(WeaponType.Bow, 0.45)]
    public void GetWeaponDPSCoefficient_ShouldReturnCorrectCoefficient(
        WeaponType weaponType, double expected)
    {
        // Act
        var coeff = _calculator.GetWeaponDPSCoefficient(weaponType);

        // Assert
        Assert.Equal(expected, coeff);
    }

    [Theory]
    [InlineData(WeaponType.Dagger, 0.72)]     // 1.8 * 0.40 = 0.72
    [InlineData(WeaponType.Sword, 1.008)]     // 2.4 * 0.42 = 1.008
    [InlineData(WeaponType.TwoHandSword, 1.7)] // 3.4 * 0.50 = 1.7
    public void CalculateWeaponDamageMultiplier_ShouldReturnCorrectMultiplier(
        WeaponType weaponType, double expected)
    {
        // Act
        var multiplier = _calculator.CalculateWeaponDamageMultiplier(weaponType);

        // Assert
        Assert.Equal(expected, multiplier, precision: 2);
    }

    [Theory]
    [InlineData(WeaponType.TwoHandSword, true)]
    [InlineData(WeaponType.TwoHandAxe, true)]
    [InlineData(WeaponType.Staff, true)]
    [InlineData(WeaponType.Bow, true)]
    [InlineData(WeaponType.Sword, false)]
    [InlineData(WeaponType.Dagger, false)]
    [InlineData(WeaponType.Shield, false)]
    public void IsTwoHandedWeapon_ShouldIdentifyTwoHandWeapons(WeaponType weaponType, bool expected)
    {
        // Act
        var isTwoHanded = AttackSpeedCalculator.IsTwoHandedWeapon(weaponType);

        // Assert
        Assert.Equal(expected, isTwoHanded);
    }

    [Theory]
    [InlineData(WeaponType.Dagger, true)]
    [InlineData(WeaponType.Sword, true)]
    [InlineData(WeaponType.Axe, true)]
    [InlineData(WeaponType.Mace, true)]
    [InlineData(WeaponType.Fist, true)]
    [InlineData(WeaponType.Wand, false)]
    [InlineData(WeaponType.Staff, false)]
    [InlineData(WeaponType.TwoHandSword, false)]
    [InlineData(WeaponType.Shield, false)]
    public void CanDualWield_ShouldIdentifyDualWieldableWeapons(WeaponType weaponType, bool expected)
    {
        // Act
        var canDualWield = AttackSpeedCalculator.CanDualWield(weaponType);

        // Assert
        Assert.Equal(expected, canDualWield);
    }

    [Fact]
    public void GetDualWieldHitPenalty_ShouldReturn19Percent()
    {
        // Act
        var penalty = AttackSpeedCalculator.GetDualWieldHitPenalty();

        // Assert
        Assert.Equal(0.19, penalty);
    }

    [Fact]
    public void GetDualWieldDPSBonus_ShouldReturn37Percent()
    {
        // Act
        var bonus = AttackSpeedCalculator.GetDualWieldDPSBonus();

        // Assert
        Assert.Equal(0.37, bonus);
    }

    [Theory]
    [InlineData(WeaponType.Sword, "单手剑")]
    [InlineData(WeaponType.Dagger, "匕首")]
    [InlineData(WeaponType.TwoHandSword, "双手剑")]
    [InlineData(WeaponType.Bow, "弓")]
    [InlineData(WeaponType.Shield, "盾牌")]
    [InlineData(WeaponType.None, "空手")]
    public void GetWeaponTypeName_ShouldReturnCorrectName(WeaponType weaponType, string expected)
    {
        // Act
        var name = AttackSpeedCalculator.GetWeaponTypeName(weaponType);

        // Assert
        Assert.Equal(expected, name);
    }
}
