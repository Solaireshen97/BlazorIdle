using BlazorIdle.Server.Domain.Equipment.Models;
using BlazorIdle.Server.Domain.Equipment.Services;
using BlazorIdle.Shared.Models;
using Xunit;

namespace BlazorIdle.Tests.Equipment.Services;

/// <summary>
/// 装备验证服务测试
/// </summary>
public class EquipmentValidatorTests
{
    private readonly EquipmentValidator _validator;

    public EquipmentValidatorTests()
    {
        _validator = new EquipmentValidator();
    }

    [Theory]
    [InlineData(Profession.Warrior, ArmorType.Plate, true)]
    [InlineData(Profession.Warrior, ArmorType.Mail, true)]
    [InlineData(Profession.Warrior, ArmorType.Leather, true)]
    [InlineData(Profession.Warrior, ArmorType.Cloth, true)]
    [InlineData(Profession.Ranger, ArmorType.Mail, true)]
    [InlineData(Profession.Ranger, ArmorType.Leather, true)]
    [InlineData(Profession.Ranger, ArmorType.Plate, false)]
    public void ValidateArmorType_ShouldValidateCorrectly(
        Profession profession, ArmorType armorType, bool shouldSucceed)
    {
        // Act
        var result = _validator.ValidateArmorType(profession, armorType);

        // Assert
        Assert.Equal(shouldSucceed, result.IsSuccess);
        if (!shouldSucceed)
        {
            Assert.NotNull(result.ErrorMessage);
        }
    }

    [Theory]
    [InlineData(Profession.Warrior, WeaponType.Sword, true)]
    [InlineData(Profession.Warrior, WeaponType.Shield, true)]
    [InlineData(Profession.Warrior, WeaponType.TwoHandSword, true)]
    [InlineData(Profession.Warrior, WeaponType.Wand, false)]
    [InlineData(Profession.Ranger, WeaponType.Bow, true)]
    [InlineData(Profession.Ranger, WeaponType.Sword, true)]
    [InlineData(Profession.Ranger, WeaponType.Shield, false)]
    public void ValidateWeaponType_ShouldValidateCorrectly(
        Profession profession, WeaponType weaponType, bool shouldSucceed)
    {
        // Act
        var result = _validator.ValidateWeaponType(profession, weaponType);

        // Assert
        Assert.Equal(shouldSucceed, result.IsSuccess);
        if (!shouldSucceed)
        {
            Assert.NotNull(result.ErrorMessage);
        }
    }

    [Theory]
    [InlineData(60, 50, true)]   // Level 60, requires 50
    [InlineData(50, 50, true)]   // Equal levels
    [InlineData(49, 50, false)]  // Too low level
    [InlineData(1, 60, false)]   // Much too low
    public void ValidateLevel_ShouldValidateCorrectly(
        int characterLevel, int requiredLevel, bool shouldSucceed)
    {
        // Act
        var result = _validator.ValidateLevel(characterLevel, requiredLevel);

        // Assert
        Assert.Equal(shouldSucceed, result.IsSuccess);
        if (!shouldSucceed)
        {
            Assert.NotNull(result.ErrorMessage);
        }
    }

    [Theory]
    [InlineData(EquipmentSlot.Head, EquipmentSlot.Head, true)]
    [InlineData(EquipmentSlot.Chest, EquipmentSlot.Chest, true)]
    [InlineData(EquipmentSlot.TwoHand, EquipmentSlot.MainHand, true)]  // TwoHand can go to MainHand
    [InlineData(EquipmentSlot.MainHand, EquipmentSlot.MainHand, true)]
    [InlineData(EquipmentSlot.MainHand, EquipmentSlot.OffHand, true)]  // Can dual wield
    [InlineData(EquipmentSlot.OffHand, EquipmentSlot.MainHand, false)] // OffHand only to OffHand
    [InlineData(EquipmentSlot.Head, EquipmentSlot.Chest, false)]       // Wrong slot
    public void ValidateSlot_ShouldValidateCorrectly(
        EquipmentSlot gearSlot, EquipmentSlot targetSlot, bool shouldSucceed)
    {
        // Act
        var result = _validator.ValidateSlot(gearSlot, targetSlot);

        // Assert
        Assert.Equal(shouldSucceed, result.IsSuccess);
        if (!shouldSucceed)
        {
            Assert.NotNull(result.ErrorMessage);
        }
    }

    [Fact]
    public void ValidateEquip_AllValidConditions_ShouldSucceed()
    {
        // Arrange
        var definition = new GearDefinition
        {
            Id = "test_sword",
            Slot = EquipmentSlot.MainHand,
            ArmorType = ArmorType.None,
            WeaponType = WeaponType.Sword,
            RequiredLevel = 50
        };

        // Act
        var result = _validator.ValidateEquip(definition, Profession.Warrior, 60, EquipmentSlot.MainHand);

        // Assert
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void ValidateEquip_InsufficientLevel_ShouldFail()
    {
        // Arrange
        var definition = new GearDefinition
        {
            Id = "test_sword",
            Slot = EquipmentSlot.MainHand,
            ArmorType = ArmorType.None,
            WeaponType = WeaponType.Sword,
            RequiredLevel = 60
        };

        // Act
        var result = _validator.ValidateEquip(definition, Profession.Warrior, 50, EquipmentSlot.MainHand);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("需要等级", result.ErrorMessage);
    }

    [Fact]
    public void ValidateEquip_WrongProfessionForArmor_ShouldFail()
    {
        // Arrange
        var definition = new GearDefinition
        {
            Id = "test_plate",
            Slot = EquipmentSlot.Chest,
            ArmorType = ArmorType.Plate,
            WeaponType = WeaponType.None,
            RequiredLevel = 50
        };

        // Act
        var result = _validator.ValidateEquip(definition, Profession.Ranger, 60, EquipmentSlot.Chest);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("无法装备", result.ErrorMessage);
    }

    [Fact]
    public void ValidateEquip_WrongProfessionForWeapon_ShouldFail()
    {
        // Arrange
        var definition = new GearDefinition
        {
            Id = "test_wand",
            Slot = EquipmentSlot.MainHand,
            ArmorType = ArmorType.None,
            WeaponType = WeaponType.Wand,
            RequiredLevel = 50
        };

        // Act
        var result = _validator.ValidateEquip(definition, Profession.Warrior, 60, EquipmentSlot.MainHand);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("无法装备", result.ErrorMessage);
    }

    [Fact]
    public void GetAllowedArmorTypes_Warrior_ShouldReturnAllTypes()
    {
        // Act
        var allowed = EquipmentValidator.GetAllowedArmorTypes(Profession.Warrior);

        // Assert
        Assert.Contains(ArmorType.Plate, allowed);
        Assert.Contains(ArmorType.Mail, allowed);
        Assert.Contains(ArmorType.Leather, allowed);
        Assert.Contains(ArmorType.Cloth, allowed);
    }

    [Fact]
    public void GetAllowedArmorTypes_Ranger_ShouldReturnCorrectTypes()
    {
        // Act
        var allowed = EquipmentValidator.GetAllowedArmorTypes(Profession.Ranger);

        // Assert
        Assert.Contains(ArmorType.Mail, allowed);
        Assert.Contains(ArmorType.Leather, allowed);
        Assert.Contains(ArmorType.Cloth, allowed);
        Assert.DoesNotContain(ArmorType.Plate, allowed);
    }

    [Fact]
    public void GetAllowedWeaponTypes_Warrior_ShouldIncludeMeleeWeapons()
    {
        // Act
        var allowed = EquipmentValidator.GetAllowedWeaponTypes(Profession.Warrior);

        // Assert
        Assert.Contains(WeaponType.Sword, allowed);
        Assert.Contains(WeaponType.Axe, allowed);
        Assert.Contains(WeaponType.Shield, allowed);
        Assert.DoesNotContain(WeaponType.Wand, allowed);
    }

    [Fact]
    public void GetAllowedWeaponTypes_Ranger_ShouldIncludeRangedWeapons()
    {
        // Act
        var allowed = EquipmentValidator.GetAllowedWeaponTypes(Profession.Ranger);

        // Assert
        Assert.Contains(WeaponType.Bow, allowed);
        Assert.Contains(WeaponType.Crossbow, allowed);
        Assert.Contains(WeaponType.Gun, allowed);
        Assert.DoesNotContain(WeaponType.Shield, allowed);
    }
}
