using BlazorIdle.Server.Domain.Equipment.Models;
using BlazorIdle.Server.Domain.Equipment.ValueObjects;
using Xunit;

namespace BlazorIdle.Tests.Equipment.Models;

/// <summary>
/// 装备实例测试
/// </summary>
public class GearInstanceTests
{
    [Fact]
    public void GearInstance_ShouldInitializeWithDefaultValues()
    {
        // Arrange & Act
        var gear = new GearInstance();
        
        // Assert
        Assert.Equal(Guid.Empty, gear.Id); // Default Guid is Empty
        Assert.Empty(gear.DefinitionId);
        Assert.Null(gear.CharacterId);
        Assert.Null(gear.SlotType);
        Assert.Equal(Rarity.Common, gear.Rarity);
        Assert.Equal(1, gear.TierLevel);
        Assert.NotNull(gear.RolledStats);
        Assert.Empty(gear.RolledStats);
        Assert.NotNull(gear.Affixes);
        Assert.Empty(gear.Affixes);
        Assert.False(gear.IsEquipped);
        Assert.False(gear.IsBound);
    }
    
    [Fact]
    public void GearInstance_ShouldAllowSettingProperties()
    {
        // Arrange
        var characterId = Guid.NewGuid();
        var gear = new GearInstance
        {
            DefinitionId = "sword_iron",
            CharacterId = characterId,
            SlotType = EquipmentSlot.MainHand,
            Rarity = Rarity.Rare,
            TierLevel = 2,
            ItemLevel = 10,
            IsEquipped = true,
            IsBound = true
        };
        
        // Act & Assert
        Assert.Equal("sword_iron", gear.DefinitionId);
        Assert.Equal(characterId, gear.CharacterId);
        Assert.Equal(EquipmentSlot.MainHand, gear.SlotType);
        Assert.Equal(Rarity.Rare, gear.Rarity);
        Assert.Equal(2, gear.TierLevel);
        Assert.Equal(10, gear.ItemLevel);
        Assert.True(gear.IsEquipped);
        Assert.True(gear.IsBound);
    }
    
    [Fact]
    public void GearInstance_ShouldAcceptRolledStats()
    {
        // Arrange
        var gear = new GearInstance();
        var stats = new Dictionary<StatType, double>
        {
            { StatType.AttackPower, 50.0 },
            { StatType.Strength, 10.0 },
            { StatType.CritRating, 5.0 }
        };
        
        // Act
        gear.RolledStats = stats;
        
        // Assert
        Assert.Equal(3, gear.RolledStats.Count);
        Assert.Equal(50.0, gear.RolledStats[StatType.AttackPower]);
        Assert.Equal(10.0, gear.RolledStats[StatType.Strength]);
        Assert.Equal(5.0, gear.RolledStats[StatType.CritRating]);
    }
    
    [Fact]
    public void GearInstance_ShouldAcceptAffixes()
    {
        // Arrange
        var gear = new GearInstance();
        var affixes = new List<AffixInstance>
        {
            new AffixInstance("affix_str", StatType.Strength, ModifierType.Flat, 15.0),
            new AffixInstance("affix_crit", StatType.CritRating, ModifierType.Flat, 8.0)
        };
        
        // Act
        gear.Affixes = affixes;
        
        // Assert
        Assert.Equal(2, gear.Affixes.Count);
        Assert.Equal("affix_str", gear.Affixes[0].AffixId);
        Assert.Equal(StatType.Strength, gear.Affixes[0].StatType);
        Assert.Equal(15.0, gear.Affixes[0].RolledValue);
    }
    
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void GearInstance_ShouldAcceptValidTierLevels(int tier)
    {
        // Arrange & Act
        var gear = new GearInstance { TierLevel = tier };
        
        // Assert
        Assert.Equal(tier, gear.TierLevel);
    }
}
