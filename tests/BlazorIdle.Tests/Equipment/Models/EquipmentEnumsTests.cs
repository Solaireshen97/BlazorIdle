using BlazorIdle.Server.Domain.Equipment.Models;
using Xunit;

namespace BlazorIdle.Tests.Equipment.Models;

/// <summary>
/// 装备系统枚举测试
/// </summary>
public class EquipmentEnumsTests
{
    [Theory]
    [InlineData(EquipmentSlot.Head)]
    [InlineData(EquipmentSlot.Neck)]
    [InlineData(EquipmentSlot.Shoulder)]
    [InlineData(EquipmentSlot.Back)]
    [InlineData(EquipmentSlot.Chest)]
    [InlineData(EquipmentSlot.Wrist)]
    [InlineData(EquipmentSlot.Hands)]
    [InlineData(EquipmentSlot.Waist)]
    [InlineData(EquipmentSlot.Legs)]
    [InlineData(EquipmentSlot.Feet)]
    [InlineData(EquipmentSlot.Finger1)]
    [InlineData(EquipmentSlot.Finger2)]
    [InlineData(EquipmentSlot.Trinket1)]
    [InlineData(EquipmentSlot.Trinket2)]
    [InlineData(EquipmentSlot.MainHand)]
    [InlineData(EquipmentSlot.OffHand)]
    [InlineData(EquipmentSlot.TwoHand)]
    public void EquipmentSlot_ShouldHaveAllDefinedSlots(EquipmentSlot slot)
    {
        // Arrange & Act
        var slotName = slot.ToString();
        
        // Assert
        Assert.NotNull(slotName);
        Assert.NotEmpty(slotName);
    }
    
    [Fact]
    public void EquipmentSlot_ShouldHave17Slots()
    {
        // Arrange & Act
        var slotCount = Enum.GetValues<EquipmentSlot>().Length;
        
        // Assert
        Assert.Equal(17, slotCount);
    }
    
    [Theory]
    [InlineData(ArmorType.None)]
    [InlineData(ArmorType.Cloth)]
    [InlineData(ArmorType.Leather)]
    [InlineData(ArmorType.Mail)]
    [InlineData(ArmorType.Plate)]
    public void ArmorType_ShouldHaveAllDefinedTypes(ArmorType armorType)
    {
        // Arrange & Act
        var typeName = armorType.ToString();
        
        // Assert
        Assert.NotNull(typeName);
        Assert.NotEmpty(typeName);
    }
    
    [Fact]
    public void ArmorType_ShouldHave5Types()
    {
        // Arrange & Act
        var typeCount = Enum.GetValues<ArmorType>().Length;
        
        // Assert
        Assert.Equal(5, typeCount);
    }
    
    [Theory]
    [InlineData(WeaponType.Sword)]
    [InlineData(WeaponType.Dagger)]
    [InlineData(WeaponType.Axe)]
    [InlineData(WeaponType.TwoHandSword)]
    [InlineData(WeaponType.Staff)]
    [InlineData(WeaponType.Bow)]
    [InlineData(WeaponType.Shield)]
    public void WeaponType_ShouldHaveAllDefinedTypes(WeaponType weaponType)
    {
        // Arrange & Act
        var typeName = weaponType.ToString();
        
        // Assert
        Assert.NotNull(typeName);
        Assert.NotEmpty(typeName);
    }
    
    [Fact]
    public void WeaponType_ShouldHave16Types()
    {
        // Arrange & Act
        var typeCount = Enum.GetValues<WeaponType>().Length;
        
        // Assert
        Assert.Equal(16, typeCount); // None + 15 weapon types
    }
    
    [Theory]
    [InlineData(Rarity.Common)]
    [InlineData(Rarity.Rare)]
    [InlineData(Rarity.Epic)]
    [InlineData(Rarity.Legendary)]
    public void Rarity_ShouldHaveAllDefinedRarities(Rarity rarity)
    {
        // Arrange & Act
        var rarityName = rarity.ToString();
        
        // Assert
        Assert.NotNull(rarityName);
        Assert.NotEmpty(rarityName);
    }
    
    [Fact]
    public void Rarity_ShouldHave4Rarities()
    {
        // Arrange & Act
        var rarityCount = Enum.GetValues<Rarity>().Length;
        
        // Assert
        Assert.Equal(4, rarityCount);
    }
    
    [Theory]
    [InlineData(StatType.Strength)]
    [InlineData(StatType.Agility)]
    [InlineData(StatType.Intellect)]
    [InlineData(StatType.Stamina)]
    [InlineData(StatType.AttackPower)]
    [InlineData(StatType.Armor)]
    [InlineData(StatType.Haste)]
    [InlineData(StatType.CritRating)]
    public void StatType_ShouldHaveAllDefinedStats(StatType statType)
    {
        // Arrange & Act
        var statName = statType.ToString();
        
        // Assert
        Assert.NotNull(statName);
        Assert.NotEmpty(statName);
    }
    
    [Theory]
    [InlineData(ModifierType.Flat)]
    [InlineData(ModifierType.Percent)]
    [InlineData(ModifierType.Proc)]
    public void ModifierType_ShouldHaveAllDefinedModifiers(ModifierType modifierType)
    {
        // Arrange & Act
        var modifierName = modifierType.ToString();
        
        // Assert
        Assert.NotNull(modifierName);
        Assert.NotEmpty(modifierName);
    }
}
