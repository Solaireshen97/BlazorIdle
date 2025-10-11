using BlazorIdle.Server.Domain.Equipment.Models;
using BlazorIdle.Server.Domain.Equipment.Services;
using BlazorIdle.Server.Domain.Equipment.ValueObjects;
using BlazorIdle.Server.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BlazorIdle.Tests.Equipment.Services;

public class ArmorCalculationServiceTests
{
    private readonly ArmorCalculationService _service;

    public ArmorCalculationServiceTests()
    {
        _service = new ArmorCalculationService();
    }

    [Fact]
    public void CalculateArmorReduction_ZeroArmor_ShouldReturnZero()
    {
        // Arrange
        var armor = 0;
        var attackerLevel = 10;

        // Act
        var reduction = _service.CalculateArmorReduction(armor, attackerLevel);

        // Assert
        Assert.Equal(0, reduction);
    }

    [Theory]
    [InlineData(100, 10, 0.1)] // 低护甲: 100 / (100 + 50*10 + 400) = 0.1
    [InlineData(500, 10, 0.357)] // 中等护甲: 500 / (500 + 50*10 + 400) = 0.357
    [InlineData(1000, 10, 0.526)] // 高护甲: 1000 / (1000 + 50*10 + 400) = 0.526
    [InlineData(3000, 10, 0.75)] // 超高护甲，达到上限
    public void CalculateArmorReduction_DifferentArmorValues_ShouldCalculateCorrectly(
        double armor, int level, double expectedReduction)
    {
        // Act
        var reduction = _service.CalculateArmorReduction(armor, level);

        // Assert
        Assert.Equal(expectedReduction, reduction, 2); // 精确到小数点后2位
    }

    [Fact]
    public void CalculateArmorReduction_VeryHighArmor_ShouldNotExceedCap()
    {
        // Arrange
        var armor = 10000; // 非常高的护甲
        var attackerLevel = 50;

        // Act
        var reduction = _service.CalculateArmorReduction(armor, attackerLevel);

        // Assert
        Assert.True(reduction <= 0.75, "Armor reduction should not exceed 75%");
    }

    [Theory]
    [InlineData(ArmorType.Cloth, 0.5)]
    [InlineData(ArmorType.Leather, 1.0)]
    [InlineData(ArmorType.Mail, 1.5)]
    [InlineData(ArmorType.Plate, 2.0)]
    [InlineData(ArmorType.None, 1.0)]
    public void GetArmorTypeMultiplier_DifferentTypes_ShouldReturnCorrectMultiplier(
        ArmorType armorType, double expectedMultiplier)
    {
        // Act
        var multiplier = _service.GetArmorTypeMultiplier(armorType);

        // Assert
        Assert.Equal(expectedMultiplier, multiplier);
    }

    [Fact]
    public void CalculateGearArmorContribution_ClothArmor_ShouldApplyCorrectMultiplier()
    {
        // Arrange
        var gear = CreateTestGear(ArmorType.Cloth, armorStat: 100);

        // Act
        var contribution = _service.CalculateGearArmorContribution(gear);

        // Assert
        Assert.Equal(50, contribution); // 100 * 0.5
    }

    [Fact]
    public void CalculateGearArmorContribution_PlateArmor_ShouldApplyCorrectMultiplier()
    {
        // Arrange
        var gear = CreateTestGear(ArmorType.Plate, armorStat: 100);

        // Act
        var contribution = _service.CalculateGearArmorContribution(gear);

        // Assert
        Assert.Equal(200, contribution); // 100 * 2.0
    }

    [Fact]
    public void CalculateGearArmorContribution_NoArmorStat_ShouldReturnZero()
    {
        // Arrange
        var gear = CreateTestGear(ArmorType.Plate, armorStat: 0);

        // Act
        var contribution = _service.CalculateGearArmorContribution(gear);

        // Assert
        Assert.Equal(0, contribution);
    }

    [Fact]
    public void CalculateTotalArmor_MultipleGear_ShouldSumCorrectly()
    {
        // Arrange
        var equippedGear = new List<GearInstance>
        {
            CreateTestGear(ArmorType.Cloth, armorStat: 100),  // 50
            CreateTestGear(ArmorType.Leather, armorStat: 100), // 100
            CreateTestGear(ArmorType.Mail, armorStat: 100),   // 150
            CreateTestGear(ArmorType.Plate, armorStat: 100)   // 200
        };

        // Act
        var totalArmor = _service.CalculateTotalArmor(equippedGear);

        // Assert
        Assert.Equal(500, totalArmor); // 50 + 100 + 150 + 200
    }

    [Fact]
    public void CalculateMitigatedDamage_WithArmor_ShouldReduceDamage()
    {
        // Arrange
        var incomingDamage = 1000;
        var armor = 500; // ~35.7% reduction at level 10
        var attackerLevel = 10;

        // Act
        var mitigated = _service.CalculateMitigatedDamage(incomingDamage, armor, attackerLevel);

        // Assert
        Assert.True(mitigated < incomingDamage, "Mitigated damage should be less than incoming");
        Assert.True(mitigated > 0, "Mitigated damage should be positive");
        Assert.InRange(mitigated, 600, 700); // 约64.3%的伤害通过
    }

    [Fact]
    public void CalculateMitigatedDamage_ZeroArmor_ShouldNotReduceDamage()
    {
        // Arrange
        var incomingDamage = 1000;
        var armor = 0;
        var attackerLevel = 10;

        // Act
        var mitigated = _service.CalculateMitigatedDamage(incomingDamage, armor, attackerLevel);

        // Assert
        Assert.Equal(incomingDamage, mitigated);
    }

    [Fact]
    public void CalculateMitigatedDamage_HighArmor_ShouldRespectCap()
    {
        // Arrange
        var incomingDamage = 1000;
        var armor = 5000; // 足够高达到75%上限
        var attackerLevel = 10;

        // Act
        var mitigated = _service.CalculateMitigatedDamage(incomingDamage, armor, attackerLevel);

        // Assert
        Assert.Equal(250, mitigated); // 1000 * (1 - 0.75) = 250
    }

    [Fact]
    public void GetArmorDetails_ShouldReturnCorrectDetails()
    {
        // Arrange
        var armor = 500;
        var level = 10;

        // Act
        var details = _service.GetArmorDetails(armor, level);

        // Assert
        Assert.Equal(500, details.TotalArmor);
        Assert.Equal(10, details.EffectiveAgainstLevel);
        Assert.InRange(details.ReductionPercent, 35, 36); // ~35.7%
    }

    // Helper methods
    private GearInstance CreateTestGear(ArmorType armorType, double armorStat)
    {
        var definition = new GearDefinition
        {
            Id = $"test_{armorType}",
            Name = $"Test {armorType} Armor",
            Slot = EquipmentSlot.Chest,
            ArmorType = armorType,
            WeaponType = WeaponType.None,
            BaseStats = new Dictionary<StatType, StatRange>()
        };

        var gear = new GearInstance
        {
            Id = Guid.NewGuid(),
            DefinitionId = definition.Id,
            Definition = definition,
            Rarity = Rarity.Common,
            TierLevel = 1,
            ItemLevel = 10,
            RolledStats = new Dictionary<StatType, double>(),
            Affixes = new List<AffixInstance>()
        };

        if (armorStat > 0)
        {
            gear.RolledStats[StatType.Armor] = armorStat;
        }

        return gear;
    }
}
