using BlazorIdle.Server.Domain.Equipment.Models;
using BlazorIdle.Server.Domain.Equipment.Services;
using Xunit;

namespace BlazorIdle.Tests.Equipment;

/// <summary>
/// 测试双持武器系统（Phase 5）
/// </summary>
public class DualWieldTests
{
    // Note: The async tests for GetOffHandWeaponTypeAsync and IsDualWieldingAsync
    // require integration with the database, which is tested in integration tests.
    // Here we focus on the calculator logic which can be tested in isolation.

    [Fact]
    public void CanDualWield_DualWieldableWeapons_ReturnsTrue()
    {
        // Arrange & Act & Assert
        Assert.True(AttackSpeedCalculator.CanDualWield(WeaponType.Sword));
        Assert.True(AttackSpeedCalculator.CanDualWield(WeaponType.Axe));
        Assert.True(AttackSpeedCalculator.CanDualWield(WeaponType.Mace));
        Assert.True(AttackSpeedCalculator.CanDualWield(WeaponType.Dagger));
        Assert.True(AttackSpeedCalculator.CanDualWield(WeaponType.Fist));
    }

    [Fact]
    public void CanDualWield_NonDualWieldableWeapons_ReturnsFalse()
    {
        // Arrange & Act & Assert
        Assert.False(AttackSpeedCalculator.CanDualWield(WeaponType.TwoHandSword));
        Assert.False(AttackSpeedCalculator.CanDualWield(WeaponType.Staff));
        Assert.False(AttackSpeedCalculator.CanDualWield(WeaponType.Bow));
        Assert.False(AttackSpeedCalculator.CanDualWield(WeaponType.Shield));
        Assert.False(AttackSpeedCalculator.CanDualWield(WeaponType.Wand));
    }

    [Fact]
    public void GetDualWieldHitPenalty_ShouldReturn19Percent()
    {
        // Act
        var penalty = AttackSpeedCalculator.GetDualWieldHitPenalty();

        // Assert
        Assert.Equal(0.19, penalty, precision: 2);
    }

    [Fact]
    public void GetDualWieldDPSBonus_ShouldReturn37Percent()
    {
        // Act
        var bonus = AttackSpeedCalculator.GetDualWieldDPSBonus();

        // Assert
        Assert.Equal(0.37, bonus, precision: 2);
    }

    [Fact]
    public void WeaponDamageCalculator_SingleHand_CalculatesCorrectly()
    {
        // Arrange
        var attackSpeedCalc = new AttackSpeedCalculator();
        var calculator = new WeaponDamageCalculator(attackSpeedCalc);
        const double baseDamage = 10;
        const double attackPower = 50;

        // Act
        var damage = calculator.CalculateWeaponDamage(
            baseDamage,
            attackPower,
            WeaponType.Sword,
            WeaponType.None,
            isDualWielding: false);

        // Assert
        // 预期伤害 = (10 + 50) * 剑的伤害倍率(2.4 * 0.42) ≈ 60.48
        Assert.True(damage > 50 && damage < 70);
    }

    [Fact]
    public void WeaponDamageCalculator_DualWield_CalculatesCorrectly()
    {
        // Arrange
        var attackSpeedCalc = new AttackSpeedCalculator();
        var calculator = new WeaponDamageCalculator(attackSpeedCalc);
        const double baseDamage = 10;
        const double attackPower = 50;

        // Act
        var damage = calculator.CalculateWeaponDamage(
            baseDamage,
            attackPower,
            WeaponType.Sword,
            WeaponType.Dagger,
            isDualWielding: true);

        // Assert
        // 双持伤害 = 主手伤害 + 副手伤害 * 0.85
        // 应该比单手高，但不到双倍
        var singleHandDamage = calculator.CalculateWeaponDamage(
            baseDamage,
            attackPower,
            WeaponType.Sword,
            WeaponType.None,
            isDualWielding: false);
        
        Assert.True(damage > singleHandDamage);
        Assert.True(damage < singleHandDamage * 2);
    }

    [Fact]
    public void GetOffHandHitChance_NotDualWielding_Returns100Percent()
    {
        // Arrange
        var attackSpeedCalc = new AttackSpeedCalculator();
        var calculator = new WeaponDamageCalculator(attackSpeedCalc);

        // Act
        var hitChance = calculator.GetOffHandHitChance(isDualWielding: false);

        // Assert
        Assert.Equal(1.0, hitChance);
    }

    [Fact]
    public void GetOffHandHitChance_DualWielding_Returns81Percent()
    {
        // Arrange
        var attackSpeedCalc = new AttackSpeedCalculator();
        var calculator = new WeaponDamageCalculator(attackSpeedCalc);

        // Act
        var hitChance = calculator.GetOffHandHitChance(isDualWielding: true);

        // Assert
        Assert.Equal(0.81, hitChance, precision: 2);
    }

}
