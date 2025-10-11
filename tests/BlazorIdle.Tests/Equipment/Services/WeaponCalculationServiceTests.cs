using BlazorIdle.Server.Domain.Equipment.Models;
using BlazorIdle.Server.Domain.Equipment.Services;

namespace BlazorIdle.Tests.Equipment.Services;

public class WeaponCalculationServiceTests
{
    private readonly WeaponCalculationService _service;

    public WeaponCalculationServiceTests()
    {
        _service = new WeaponCalculationService();
    }

    [Theory]
    [InlineData(WeaponType.Dagger, 2.5)]
    [InlineData(WeaponType.Fist, 2.3)]
    [InlineData(WeaponType.Wand, 2.0)]
    [InlineData(WeaponType.Sword, 1.8)]
    [InlineData(WeaponType.TwoHandSword, 1.3)]
    [InlineData(WeaponType.TwoHandAxe, 1.2)]
    [InlineData(WeaponType.TwoHandMace, 1.1)]
    [InlineData(WeaponType.Shield, 0.0)]
    public void GetBaseAttackSpeed_DifferentWeaponTypes_ShouldReturnCorrectSpeed(
        WeaponType weaponType, double expectedSpeed)
    {
        // Act
        var speed = _service.GetBaseAttackSpeed(weaponType);

        // Assert
        Assert.Equal(expectedSpeed, speed);
    }

    [Theory]
    [InlineData(WeaponType.Dagger, 0.35)]
    [InlineData(WeaponType.Sword, 0.42)]
    [InlineData(WeaponType.TwoHandSword, 0.55)]
    [InlineData(WeaponType.TwoHandAxe, 0.58)]
    [InlineData(WeaponType.TwoHandMace, 0.60)]
    [InlineData(WeaponType.Shield, 0.0)]
    public void GetDamageMultiplier_DifferentWeaponTypes_ShouldReturnCorrectMultiplier(
        WeaponType weaponType, double expectedMultiplier)
    {
        // Act
        var multiplier = _service.GetDamageMultiplier(weaponType);

        // Assert
        Assert.Equal(expectedMultiplier, multiplier);
    }

    [Fact]
    public void CalculateDpsCoefficient_FastWeapon_ShouldHaveSimilarDpsToSlowWeapon()
    {
        // Arrange
        var dagger = WeaponType.Dagger;      // 快速武器
        var twoHandMace = WeaponType.TwoHandMace; // 慢速武器

        // Act
        var daggerDps = _service.CalculateDpsCoefficient(dagger);
        var maceDps = _service.CalculateDpsCoefficient(twoHandMace);

        // Assert
        // 快速单手武器和慢速双手武器DPS差异应在合理范围
        // 单手武器DPS略高是合理的（可以配合盾牌或双持）
        var ratio = daggerDps / maceDps;
        Assert.InRange(ratio, 0.8, 1.4); // 允许40%差异
    }

    [Theory]
    [InlineData(WeaponType.Dagger, 0.875)]     // 2.5 * 0.35 = 0.875
    [InlineData(WeaponType.Sword, 0.756)]      // 1.8 * 0.42 = 0.756
    [InlineData(WeaponType.TwoHandMace, 0.66)] // 1.1 * 0.60 = 0.66
    public void CalculateDpsCoefficient_ShouldCalculateCorrectly(
        WeaponType weaponType, double expectedDps)
    {
        // Act
        var dps = _service.CalculateDpsCoefficient(weaponType);

        // Assert
        Assert.Equal(expectedDps, dps, 3);
    }

    [Theory]
    [InlineData(WeaponType.TwoHandSword, true)]
    [InlineData(WeaponType.TwoHandAxe, true)]
    [InlineData(WeaponType.TwoHandMace, true)]
    [InlineData(WeaponType.Polearm, true)]
    [InlineData(WeaponType.Staff, true)]
    [InlineData(WeaponType.Bow, true)]
    [InlineData(WeaponType.Sword, false)]
    [InlineData(WeaponType.Dagger, false)]
    [InlineData(WeaponType.Shield, false)]
    public void IsTwoHandedWeapon_DifferentTypes_ShouldReturnCorrectResult(
        WeaponType weaponType, bool expected)
    {
        // Act
        var result = _service.IsTwoHandedWeapon(weaponType);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(WeaponType.Dagger, true)]
    [InlineData(WeaponType.Fist, true)]
    [InlineData(WeaponType.Sword, true)]
    [InlineData(WeaponType.Axe, true)]
    [InlineData(WeaponType.Mace, true)]
    [InlineData(WeaponType.TwoHandSword, false)]
    [InlineData(WeaponType.Staff, false)]
    [InlineData(WeaponType.Bow, false)]
    public void CanDualWield_DifferentTypes_ShouldReturnCorrectResult(
        WeaponType weaponType, bool expected)
    {
        // Act
        var result = _service.CanDualWield(weaponType);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void CalculateDualWieldAttackSpeed_TwoSameWeapons_ShouldIncreaseSpeed()
    {
        // Arrange
        var mainHandSpeed = 1.8; // 单手剑
        var offHandSpeed = 1.8;

        // Act
        var totalSpeed = _service.CalculateDualWieldAttackSpeed(mainHandSpeed, offHandSpeed);

        // Assert
        // 主手全速 + 副手90%
        var expected = mainHandSpeed + (offHandSpeed * 0.9);
        Assert.Equal(expected, totalSpeed, 2);
        Assert.True(totalSpeed > mainHandSpeed, "Dual wield should increase attack speed");
    }

    [Fact]
    public void CalculateDualWieldAttackSpeed_WithCustomPenalty_ShouldApplyPenalty()
    {
        // Arrange
        var mainHandSpeed = 1.8;
        var offHandSpeed = 1.8;
        var penalty = 0.2; // 20%惩罚

        // Act
        var totalSpeed = _service.CalculateDualWieldAttackSpeed(
            mainHandSpeed, offHandSpeed, penalty);

        // Assert
        var expected = mainHandSpeed + (offHandSpeed * 0.8);
        Assert.Equal(expected, totalSpeed, 2);
    }

    [Theory]
    [InlineData(10, 0.12)]  // 10级: 10%基础 + 2% (10/10*2%)
    [InlineData(50, 0.20)]  // 50级: 10% + 10%等级加成 (50/10*2%)
    [InlineData(100, 0.30)] // 100级: 10% + 20%等级加成 (100/10*2%)
    [InlineData(250, 0.50)] // 250级: 达到上限50%
    [InlineData(500, 0.50)] // 500级: 保持上限50%
    public void GetShieldBlockChance_DifferentLevels_ShouldCalculateCorrectly(
        int itemLevel, double expectedBlockChance)
    {
        // Act
        var blockChance = _service.GetShieldBlockChance(itemLevel);

        // Assert
        Assert.Equal(expectedBlockChance, blockChance, 2);
    }

    [Fact]
    public void GetShieldBlockChance_ShouldNotExceed50Percent()
    {
        // Arrange
        var veryHighLevel = 10000;

        // Act
        var blockChance = _service.GetShieldBlockChance(veryHighLevel);

        // Assert
        Assert.True(blockChance <= 0.50, "Block chance should not exceed 50%");
        Assert.Equal(0.50, blockChance);
    }

    [Fact]
    public void CalculateBlockedDamage_WithBlockChanceAndAmount_ShouldReduceDamage()
    {
        // Arrange
        var incomingDamage = 1000;
        var blockChance = 0.30; // 30%格挡率
        var blockAmount = 500;  // 格挡500伤害

        // Act
        var finalDamage = _service.CalculateBlockedDamage(
            incomingDamage, blockChance, blockAmount);

        // Assert
        // 期望伤害 = 1000 * 0.7 + 500 * 0.3 = 700 + 150 = 850
        Assert.True(finalDamage < incomingDamage, "Blocked damage should be less than incoming");
        Assert.Equal(850, finalDamage);
    }

    [Fact]
    public void CalculateBlockedDamage_HighBlockAmount_ShouldNotGoNegative()
    {
        // Arrange
        var incomingDamage = 500;
        var blockChance = 0.50;
        var blockAmount = 1000; // 格挡值高于伤害

        // Act
        var finalDamage = _service.CalculateBlockedDamage(
            incomingDamage, blockChance, blockAmount);

        // Assert
        // 格挡后伤害不应该为负
        Assert.True(finalDamage >= 0, "Damage should not go negative");
        Assert.True(finalDamage <= incomingDamage, "Final damage should not exceed incoming");
    }

    [Fact]
    public void GetWeaponDetails_Dagger_ShouldReturnCorrectDetails()
    {
        // Act
        var details = _service.GetWeaponDetails(WeaponType.Dagger);

        // Assert
        Assert.Equal(WeaponType.Dagger, details.WeaponType);
        Assert.Equal(2.5, details.BaseAttackSpeed);
        Assert.Equal(0.35, details.DamageMultiplier);
        Assert.Equal(0.875, details.DpsCoefficient, 3);
        Assert.False(details.IsTwoHanded);
        Assert.True(details.CanDualWield);
    }

    [Fact]
    public void GetWeaponDetails_TwoHandSword_ShouldReturnCorrectDetails()
    {
        // Act
        var details = _service.GetWeaponDetails(WeaponType.TwoHandSword);

        // Assert
        Assert.Equal(WeaponType.TwoHandSword, details.WeaponType);
        Assert.Equal(1.3, details.BaseAttackSpeed);
        Assert.Equal(0.55, details.DamageMultiplier);
        Assert.Equal(0.715, details.DpsCoefficient, 3);
        Assert.True(details.IsTwoHanded);
        Assert.False(details.CanDualWield);
    }

    [Fact]
    public void AllWeaponTypes_ShouldHaveReasonableDpsBalance()
    {
        // Arrange
        var weaponTypes = new[]
        {
            WeaponType.Dagger, WeaponType.Fist, WeaponType.Wand,
            WeaponType.Sword, WeaponType.Axe, WeaponType.Mace,
            WeaponType.TwoHandSword, WeaponType.TwoHandAxe, WeaponType.TwoHandMace,
            WeaponType.Staff, WeaponType.Polearm,
            WeaponType.Bow, WeaponType.Crossbow, WeaponType.Gun
        };

        // Act
        var dpsValues = weaponTypes
            .Select(wt => _service.CalculateDpsCoefficient(wt))
            .Where(dps => dps > 0) // 排除Shield
            .ToList();

        // Assert
        // 所有武器的DPS系数应该在一个合理范围内（0.6 - 1.0）
        Assert.All(dpsValues, dps => Assert.InRange(dps, 0.6, 1.0));
        
        // DPS系数的标准差应该不太大（<0.15）
        var average = dpsValues.Average();
        var variance = dpsValues.Select(dps => Math.Pow(dps - average, 2)).Average();
        var stdDev = Math.Sqrt(variance);
        Assert.True(stdDev < 0.15, $"DPS standard deviation too high: {stdDev}");
    }
}
