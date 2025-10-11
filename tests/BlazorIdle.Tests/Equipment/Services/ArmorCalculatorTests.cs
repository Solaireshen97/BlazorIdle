using BlazorIdle.Server.Domain.Equipment.Models;
using BlazorIdle.Server.Domain.Equipment.Services;
using Xunit;

namespace BlazorIdle.Tests.Equipment.Services;

/// <summary>
/// 护甲计算服务测试
/// </summary>
public class ArmorCalculatorTests
{
    private readonly ArmorCalculator _calculator;

    public ArmorCalculatorTests()
    {
        _calculator = new ArmorCalculator();
    }

    [Theory]
    [InlineData(ArmorType.Cloth, EquipmentSlot.Chest, 60, 45.0)]  // 60 * 1.5 * 0.5 = 45
    [InlineData(ArmorType.Leather, EquipmentSlot.Chest, 60, 90.0)] // 60 * 1.5 * 1.0 = 90
    [InlineData(ArmorType.Mail, EquipmentSlot.Chest, 60, 135.0)]   // 60 * 1.5 * 1.5 = 135
    [InlineData(ArmorType.Plate, EquipmentSlot.Chest, 60, 180.0)]  // 60 * 1.5 * 2.0 = 180
    public void CalculateArmorValue_DifferentArmorTypes_ShouldReturnCorrectValues(
        ArmorType armorType, EquipmentSlot slot, int itemLevel, double expected)
    {
        // Act
        var armor = _calculator.CalculateArmorValue(armorType, slot, itemLevel);

        // Assert
        Assert.Equal(expected, armor, precision: 1);
    }

    [Theory]
    [InlineData(EquipmentSlot.Head, 50, 50.0)]     // 50 * 1.0 * 1.0 = 50
    [InlineData(EquipmentSlot.Chest, 50, 75.0)]    // 50 * 1.5 * 1.0 = 75
    [InlineData(EquipmentSlot.Legs, 50, 65.0)]     // 50 * 1.3 * 1.0 = 65
    [InlineData(EquipmentSlot.Shoulder, 50, 40.0)] // 50 * 0.8 * 1.0 = 40
    public void CalculateArmorValue_DifferentSlots_ShouldApplySlotMultipliers(
        EquipmentSlot slot, int itemLevel, double expected)
    {
        // Act
        var armor = _calculator.CalculateArmorValue(ArmorType.Leather, slot, itemLevel);

        // Assert
        Assert.Equal(expected, armor, precision: 1);
    }

    [Theory]
    [InlineData(EquipmentSlot.Neck, 0.0)]      // 项链无护甲
    [InlineData(EquipmentSlot.Finger1, 0.0)]   // 戒指无护甲
    [InlineData(EquipmentSlot.Trinket1, 0.0)]  // 饰品无护甲
    [InlineData(EquipmentSlot.MainHand, 0.0)]  // 武器无护甲
    public void CalculateArmorValue_JewelryAndWeapons_ShouldReturnZero(EquipmentSlot slot, double expected)
    {
        // Act
        var armor = _calculator.CalculateArmorValue(ArmorType.Leather, slot, 60);

        // Assert
        Assert.Equal(expected, armor);
    }

    [Fact]
    public void CalculateShieldArmorValue_ShouldProvideHighArmor()
    {
        // Arrange
        int itemLevel = 60;

        // Act
        var armor = _calculator.CalculateShieldArmorValue(itemLevel);

        // Assert
        // 盾牌护甲 = 60 * 2.25 = 135
        Assert.Equal(135.0, armor, precision: 1);
    }

    [Theory]
    [InlineData(500, 60, 0.14)]   // 500 / (500 + 50*60 + 400) ≈ 0.14
    [InlineData(1000, 60, 0.23)]  // 1000 / (1000 + 50*60 + 400) ≈ 0.23
    [InlineData(0, 60, 0.0)]      // 无护甲
    [InlineData(100, 60, 0.03)]   // 100 / (100 + 50*60 + 400) ≈ 0.029
    public void CalculateArmorReduction_ShouldCalculateCorrectReduction(
        double totalArmor, int attackerLevel, double maxExpected)
    {
        // Act
        var reduction = _calculator.CalculateArmorReduction(totalArmor, attackerLevel);

        // Assert
        Assert.InRange(reduction, 0.0, maxExpected);
        Assert.True(reduction <= 0.75, "Reduction should be capped at 75%");
    }

    [Fact]
    public void CalculateArmorReduction_ZeroArmor_ShouldReturnZero()
    {
        // Act
        var reduction = _calculator.CalculateArmorReduction(0, 60);

        // Assert
        Assert.Equal(0.0, reduction);
    }

    [Fact]
    public void CalculateArmorReduction_VeryHighArmor_ShouldCapAt75Percent()
    {
        // Act
        var reduction = _calculator.CalculateArmorReduction(10000, 60);

        // Assert
        Assert.Equal(0.75, reduction, precision: 2);
    }

    [Theory]
    [InlineData(ArmorType.None, "无")]
    [InlineData(ArmorType.Cloth, "布甲")]
    [InlineData(ArmorType.Leather, "皮甲")]
    [InlineData(ArmorType.Mail, "锁甲")]
    [InlineData(ArmorType.Plate, "板甲")]
    public void GetArmorTypeName_ShouldReturnCorrectName(ArmorType armorType, string expected)
    {
        // Act
        var name = ArmorCalculator.GetArmorTypeName(armorType);

        // Assert
        Assert.Equal(expected, name);
    }

    [Theory]
    [InlineData(ArmorType.None, 0.0)]
    [InlineData(ArmorType.Cloth, 0.5)]
    [InlineData(ArmorType.Leather, 1.0)]
    [InlineData(ArmorType.Mail, 1.5)]
    [InlineData(ArmorType.Plate, 2.0)]
    public void GetArmorTypeMultiplier_ShouldReturnCorrectMultiplier(
        ArmorType armorType, double expected)
    {
        // Act
        var multiplier = ArmorCalculator.GetArmorTypeMultiplier(armorType);

        // Assert
        Assert.Equal(expected, multiplier);
    }
}
