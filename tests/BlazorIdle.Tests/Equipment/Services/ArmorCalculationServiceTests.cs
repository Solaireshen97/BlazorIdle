using BlazorIdle.Server.Domain.Equipment.Models;
using BlazorIdle.Server.Domain.Equipment.Services;
using Xunit;

namespace BlazorIdle.Tests.Equipment.Services;

public class ArmorCalculationServiceTests
{
    private readonly ArmorCalculationService _service;

    public ArmorCalculationServiceTests()
    {
        _service = new ArmorCalculationService();
    }

    [Theory]
    [InlineData(100, 10, 0.0909)]  // 100/(100+50*10+400) = 100/1100 ≈ 9.09%
    [InlineData(300, 30, 0.1429)]  // 300/(300+50*30+400) = 300/2100 ≈ 14.29%
    [InlineData(1000, 10, 0.5263)] // 1000/(1000+50*10+400) = 1000/1900 ≈ 52.63%
    [InlineData(2000, 10, 0.6897)] // 2000/(2000+50*10+400) = 2000/2900 ≈ 68.97%
    [InlineData(600, 50, 0.1714)]  // 600/(600+50*50+400) = 600/3500 ≈ 17.14%
    public void CalculateArmorReduction_ShouldReturnExpectedReduction(double armor, int attackerLevel, double expected)
    {
        // Act
        var reduction = _service.CalculateArmorReduction(armor, attackerLevel);

        // Assert
        Assert.InRange(reduction, expected - 0.01, expected + 0.01);
    }

    [Fact]
    public void CalculateArmorReduction_WithZeroArmor_ShouldReturnZero()
    {
        // Act
        var reduction = _service.CalculateArmorReduction(0, 30);

        // Assert
        Assert.Equal(0, reduction);
    }

    [Fact]
    public void CalculateArmorReduction_ShouldCapAt75Percent()
    {
        // Arrange
        double hugeArmor = 100000;

        // Act
        var reduction = _service.CalculateArmorReduction(hugeArmor, 10);

        // Assert
        Assert.Equal(0.75, reduction);
    }

    [Theory]
    [InlineData(1000, 0, 1000)]  // 1000伤害，0%减伤 = 1000伤害
    [InlineData(1000, 0.25, 750)] // 1000伤害，25%减伤 = 750伤害
    [InlineData(1000, 0.5, 500)]  // 1000伤害，50%减伤 = 500伤害
    [InlineData(1000, 0.75, 250)] // 1000伤害，75%减伤 = 250伤害
    public void ApplyArmorReduction_ShouldReduceDamageCorrectly(int rawDamage, double reduction, int expected)
    {
        // Act
        var result = _service.ApplyArmorReduction(rawDamage, reduction);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(1000, 200, 0.2, 640)]  // (1000-200) * (1-0.2) = 640
    [InlineData(1000, 0, 0.2, 800)]    // 1000 * (1-0.2) = 800
    [InlineData(1000, 200, 0, 800)]    // (1000-200) * 1 = 800
    [InlineData(500, 600, 0.2, 0)]     // 穿透大于护甲，有效护甲为0
    public void CalculateEffectiveArmor_ShouldApplyPenetrationCorrectly(
        double totalArmor, 
        double flatPen, 
        double percentPen, 
        double expected)
    {
        // Act
        var effectiveArmor = _service.CalculateEffectiveArmor(totalArmor, flatPen, percentPen);

        // Assert
        Assert.Equal(expected, effectiveArmor);
    }

    [Theory]
    [InlineData(ArmorType.None, 0.0)]
    [InlineData(ArmorType.Cloth, 0.5)]
    [InlineData(ArmorType.Leather, 1.0)]
    [InlineData(ArmorType.Mail, 1.5)]
    [InlineData(ArmorType.Plate, 2.0)]
    public void GetArmorTypeMultiplier_ShouldReturnCorrectMultiplier(ArmorType type, double expected)
    {
        // Act
        var multiplier = _service.GetArmorTypeMultiplier(type);

        // Assert
        Assert.Equal(expected, multiplier);
    }

    [Theory]
    [InlineData(10, ArmorType.Cloth, 30, 1, 12)]    // T1布甲: 10*0.5*3*0.8 = 12
    [InlineData(10, ArmorType.Plate, 30, 2, 60)]    // T2板甲: 10*2.0*3*1.0 = 60
    [InlineData(10, ArmorType.Plate, 50, 3, 120)]   // T3板甲: 10*2.0*5*1.2 = 120
    [InlineData(10, ArmorType.Leather, 20, 1, 16)]  // T1皮甲: 10*1.0*2*0.8 = 16
    public void CalculateItemArmor_ShouldCalculateCorrectly(
        double baseArmor, 
        ArmorType armorType, 
        int itemLevel, 
        int tierLevel, 
        double expected)
    {
        // Act
        var armor = _service.CalculateItemArmor(baseArmor, armorType, itemLevel, tierLevel);

        // Assert
        Assert.Equal(expected, armor);
    }

    [Theory]
    [InlineData(EquipmentSlot.Chest, 1.5)]
    [InlineData(EquipmentSlot.Legs, 1.3)]
    [InlineData(EquipmentSlot.Shoulder, 1.2)]
    [InlineData(EquipmentSlot.Head, 1.2)]
    [InlineData(EquipmentSlot.Hands, 1.0)]
    [InlineData(EquipmentSlot.Feet, 1.0)]
    public void GetSlotArmorMultiplier_ShouldReturnCorrectMultiplier(EquipmentSlot slot, double expected)
    {
        // Act
        var multiplier = _service.GetSlotArmorMultiplier(slot);

        // Assert
        Assert.Equal(expected, multiplier);
    }

    [Fact]
    public void CalculateArmorReduction_IntegrationTest_ClothVsLeveledEnemy()
    {
        // Arrange - 布甲角色（100护甲）对战30级敌人
        // Formula: 100/(100+50*30+400) = 100/2000 = 5%
        double clothArmor = 100;
        int enemyLevel = 30;

        // Act
        var reduction = _service.CalculateArmorReduction(clothArmor, enemyLevel);
        var damageAfterReduction = _service.ApplyArmorReduction(1000, reduction);

        // Assert - 应该减伤约5%，剩余950左右
        Assert.InRange(reduction, 0.04, 0.06);
        Assert.InRange(damageAfterReduction, 940, 960);
    }

    [Fact]
    public void CalculateArmorReduction_IntegrationTest_PlateVsLeveledEnemy()
    {
        // Arrange - 板甲角色（1000护甲）对战30级敌人
        // Formula: 1000/(1000+50*30+400) = 1000/2900 ≈ 34.48%
        double plateArmor = 1000;
        int enemyLevel = 30;

        // Act
        var reduction = _service.CalculateArmorReduction(plateArmor, enemyLevel);
        var damageAfterReduction = _service.ApplyArmorReduction(1000, reduction);

        // Assert - 应该减伤约34.5%，剩余655左右
        Assert.InRange(reduction, 0.33, 0.36);
        Assert.InRange(damageAfterReduction, 640, 670);
    }

    [Fact]
    public void CalculateEffectiveArmor_WithMaxPenetration_ShouldNotGoNegative()
    {
        // Arrange - 过量穿透
        double armor = 1000;
        double flatPen = 2000;
        double percentPen = 0.5;

        // Act
        var effectiveArmor = _service.CalculateEffectiveArmor(armor, flatPen, percentPen);

        // Assert - 有效护甲不能为负
        Assert.True(effectiveArmor >= 0);
    }
}
