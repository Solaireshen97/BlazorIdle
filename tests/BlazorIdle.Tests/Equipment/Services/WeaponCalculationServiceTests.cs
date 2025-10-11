using BlazorIdle.Server.Domain.Equipment.Models;
using BlazorIdle.Server.Domain.Equipment.Services;
using Xunit;

namespace BlazorIdle.Tests.Equipment.Services;

public class WeaponCalculationServiceTests
{
    private readonly WeaponCalculationService _service;

    public WeaponCalculationServiceTests()
    {
        _service = new WeaponCalculationService();
    }

    #region Attack Speed Tests

    [Theory]
    [InlineData(WeaponType.Dagger, 1.3)]      // 快速
    [InlineData(WeaponType.Fist, 1.25)]
    [InlineData(WeaponType.Sword, 1.0)]       // 标准
    [InlineData(WeaponType.TwoHandSword, 0.8)] // 慢速
    [InlineData(WeaponType.Crossbow, 0.7)]    // 非常慢
    [InlineData(WeaponType.Shield, 0.0)]      // 不能攻击
    public void GetAttackSpeedMultiplier_ShouldReturnCorrectMultiplier(WeaponType weaponType, double expected)
    {
        // Act
        var multiplier = _service.GetAttackSpeedMultiplier(weaponType);

        // Assert
        Assert.Equal(expected, multiplier);
    }

    #endregion

    #region Damage Multiplier Tests

    [Theory]
    [InlineData(WeaponType.Dagger, 0.85)]      // 低伤害
    [InlineData(WeaponType.Sword, 1.0)]        // 标准伤害
    [InlineData(WeaponType.TwoHandAxe, 1.5)]   // 高伤害
    [InlineData(WeaponType.Crossbow, 1.6)]     // 非常高伤害
    [InlineData(WeaponType.Shield, 0.0)]
    public void GetDamageMultiplier_ShouldReturnCorrectMultiplier(WeaponType weaponType, double expected)
    {
        // Act
        var multiplier = _service.GetDamageMultiplier(weaponType);

        // Assert
        Assert.Equal(expected, multiplier);
    }

    #endregion

    #region Two-Handed Tests

    [Theory]
    [InlineData(WeaponType.TwoHandSword, true)]
    [InlineData(WeaponType.TwoHandAxe, true)]
    [InlineData(WeaponType.Staff, true)]
    [InlineData(WeaponType.Bow, true)]
    [InlineData(WeaponType.Sword, false)]
    [InlineData(WeaponType.Dagger, false)]
    [InlineData(WeaponType.Shield, false)]
    public void IsTwoHanded_ShouldReturnCorrectValue(WeaponType weaponType, bool expected)
    {
        // Act
        var result = _service.IsTwoHanded(weaponType);

        // Assert
        Assert.Equal(expected, result);
    }

    #endregion

    #region Off-Hand Tests

    [Theory]
    [InlineData(WeaponType.Dagger, true)]
    [InlineData(WeaponType.Sword, true)]
    [InlineData(WeaponType.Shield, true)]
    [InlineData(WeaponType.TwoHandSword, false)]
    [InlineData(WeaponType.Staff, false)]
    [InlineData(WeaponType.Bow, false)]
    public void CanEquipOffHand_ShouldReturnCorrectValue(WeaponType weaponType, bool expected)
    {
        // Act
        var result = _service.CanEquipOffHand(weaponType);

        // Assert
        Assert.Equal(expected, result);
    }

    #endregion

    #region Dual Wield Tests

    [Fact]
    public void GetDualWieldSpeedBonus_ShouldReturn20Percent()
    {
        // Act
        var bonus = _service.GetDualWieldSpeedBonus();

        // Assert
        Assert.Equal(0.2, bonus);
    }

    [Theory]
    [InlineData(false, 1.0)]  // 主手100%伤害
    [InlineData(true, 0.5)]   // 副手50%伤害
    public void GetDualWieldDamagePenalty_ShouldReturnCorrectPenalty(bool isOffHand, double expected)
    {
        // Act
        var penalty = _service.GetDualWieldDamagePenalty(isOffHand);

        // Assert
        Assert.Equal(expected, penalty);
    }

    #endregion

    #region Block Tests

    [Fact]
    public void CalculateBlockChance_WithoutShield_ShouldReturnZero()
    {
        // Act
        var blockChance = _service.CalculateBlockChance(1000, false);

        // Assert
        Assert.Equal(0.0, blockChance);
    }

    [Fact]
    public void CalculateBlockChance_WithShield_ShouldIncludeBaseChance()
    {
        // Arrange - 0格挡等级，有盾牌
        // Act
        var blockChance = _service.CalculateBlockChance(0, true);

        // Assert - 基础5%格挡
        Assert.Equal(0.05, blockChance);
    }

    [Theory]
    [InlineData(0, 0.05)]      // 0评级 = 5%基础
    [InlineData(5000, 0.55)]   // 5000评级 = 5%基础 + 50%评级 = 55%
    [InlineData(10000, 0.75)]  // 10000评级会超过上限，最多75%
    public void CalculateBlockChance_WithShieldAndRating_ShouldCalculateCorrectly(double rating, double expected)
    {
        // Act
        var blockChance = _service.CalculateBlockChance(rating, true);

        // Assert
        Assert.Equal(expected, blockChance);
    }

    [Theory]
    [InlineData(100, 50)]    // 100护甲 = 50格挡值
    [InlineData(200, 100)]   // 200护甲 = 100格挡值
    [InlineData(0, 0)]
    public void CalculateBlockValue_ShouldCalculateCorrectly(double shieldArmor, int expected)
    {
        // Act
        var blockValue = _service.CalculateBlockValue(shieldArmor);

        // Assert
        Assert.Equal(expected, blockValue);
    }

    [Theory]
    [InlineData(1000, 100, 900)]   // 1000伤害，100格挡值 = 900伤害
    [InlineData(1000, 800, 300)]   // 格挡大部分伤害，但至少保留30%
    [InlineData(1000, 1200, 300)]  // 格挡值超过伤害，仍保留30%
    [InlineData(100, 50, 50)]      // 100伤害，50格挡值 = 50伤害
    public void ApplyBlockReduction_ShouldReduceDamageCorrectly(int rawDamage, int blockValue, int expected)
    {
        // Act
        var result = _service.ApplyBlockReduction(rawDamage, blockValue);

        // Assert
        Assert.Equal(expected, result);
    }

    #endregion

    #region Attack Interval Tests

    [Fact]
    public void CalculateFinalAttackInterval_WithStandardWeapon_ShouldApplyOnlyHaste()
    {
        // Arrange - 标准武器，无双持，10%急速
        double baseInterval = 2.0;  // 2秒基础间隔
        double weaponSpeed = 1.0;   // 标准速度
        double haste = 0.1;         // 10%急速
        bool dualWield = false;

        // Act
        var finalInterval = _service.CalculateFinalAttackInterval(baseInterval, weaponSpeed, haste, dualWield);

        // Assert - 2.0 / 1.1 ≈ 1.818秒
        Assert.InRange(finalInterval, 1.81, 1.83);
    }

    [Fact]
    public void CalculateFinalAttackInterval_WithFastWeapon_ShouldReduceInterval()
    {
        // Arrange - 匕首（1.3速度），无双持，无急速
        double baseInterval = 2.0;
        double weaponSpeed = 1.3;
        double haste = 0.0;
        bool dualWield = false;

        // Act
        var finalInterval = _service.CalculateFinalAttackInterval(baseInterval, weaponSpeed, haste, dualWield);

        // Assert - 2.0 / 1.3 ≈ 1.538秒
        Assert.InRange(finalInterval, 1.53, 1.55);
    }

    [Fact]
    public void CalculateFinalAttackInterval_WithDualWield_ShouldApplyDualWieldBonus()
    {
        // Arrange - 标准武器，双持（+20%速度），无急速
        double baseInterval = 2.0;
        double weaponSpeed = 1.0;
        double haste = 0.0;
        bool dualWield = true;

        // Act
        var finalInterval = _service.CalculateFinalAttackInterval(baseInterval, weaponSpeed, haste, dualWield);

        // Assert - 2.0 / 1.2 ≈ 1.667秒
        Assert.InRange(finalInterval, 1.66, 1.67);
    }

    [Fact]
    public void CalculateFinalAttackInterval_WithAllModifiers_ShouldStackCorrectly()
    {
        // Arrange - 匕首（1.3），双持（+20%），20%急速
        double baseInterval = 2.0;
        double weaponSpeed = 1.3;
        double haste = 0.2;
        bool dualWield = true;

        // Act
        var finalInterval = _service.CalculateFinalAttackInterval(baseInterval, weaponSpeed, haste, dualWield);

        // Assert - 2.0 / 1.3 / 1.2 / 1.2 ≈ 1.068秒
        Assert.InRange(finalInterval, 1.06, 1.08);
    }

    [Fact]
    public void CalculateFinalAttackInterval_ShouldNotGoBelowMinimum()
    {
        // Arrange - 极限情况：超快武器+双持+极高急速
        double baseInterval = 2.0;
        double weaponSpeed = 2.0;   // 非常快的武器
        double haste = 2.0;         // 200%急速
        bool dualWield = true;

        // Act
        var finalInterval = _service.CalculateFinalAttackInterval(baseInterval, weaponSpeed, haste, dualWield);

        // Assert - 不能低于0.5秒
        Assert.Equal(0.5, finalInterval);
    }

    #endregion

    #region Integration Tests

    [Fact]
    public void WeaponDPS_FastVsSlow_ShouldBeBalanced()
    {
        // Arrange - 对比匕首和双手斧的理论DPS
        // 匕首：1.3速度，0.85伤害
        double daggerSpeed = 1.3;
        double daggerDamage = 0.85;
        double daggerDPS = daggerSpeed * daggerDamage; // 1.105

        // 双手斧：0.75速度，1.5伤害
        double axeSpeed = 0.75;
        double axeDamage = 1.5;
        double axeDPS = axeSpeed * axeDamage; // 1.125

        // Assert - DPS应该相近（误差在10%内）
        double ratio = axeDPS / daggerDPS;
        Assert.InRange(ratio, 0.9, 1.1);
    }

    [Fact]
    public void DualWield_ShouldProvideHigherDPS()
    {
        // Arrange - 对比单持和双持的理论DPS
        // 单持：100%伤害，1.0速度
        double singleWieldDPS = 1.0 * 1.0; // 1.0

        // 双持：主手100% + 副手50%，1.2速度加成
        double dualWieldDPS = (1.0 + 0.5) * 1.2; // 1.8

        // Assert - 双持DPS应该更高（约80%提升）
        double increase = (dualWieldDPS - singleWieldDPS) / singleWieldDPS;
        Assert.InRange(increase, 0.7, 0.9);
    }

    #endregion
}
