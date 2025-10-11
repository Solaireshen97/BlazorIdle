using BlazorIdle.Server.Domain.Equipment.Models;
using BlazorIdle.Server.Domain.Equipment.ValueObjects;
using Xunit;

namespace BlazorIdle.Tests.Equipment.Models;

/// <summary>
/// 词条实例测试
/// </summary>
public class AffixInstanceTests
{
    [Fact]
    public void AffixInstance_ShouldInitializeWithConstructor()
    {
        // Arrange & Act
        var affix = new AffixInstance("affix_str", StatType.Strength, ModifierType.Flat, 20.0);
        
        // Assert
        Assert.Equal("affix_str", affix.AffixId);
        Assert.Equal(StatType.Strength, affix.StatType);
        Assert.Equal(ModifierType.Flat, affix.ModifierType);
        Assert.Equal(20.0, affix.RolledValue);
        Assert.NotEmpty(affix.DisplayText);
    }
    
    [Theory]
    [InlineData(StatType.Strength, ModifierType.Flat, 50.0, "+50 力量")]
    [InlineData(StatType.AttackPower, ModifierType.Flat, 100.0, "+100 攻击强度")]
    [InlineData(StatType.CritRating, ModifierType.Flat, 25.0, "+25 暴击")]
    public void AffixInstance_ShouldFormatFlatModifierDisplayText(StatType statType, ModifierType modifierType, double value, string expected)
    {
        // Arrange & Act
        var affix = new AffixInstance("test", statType, modifierType, value);
        
        // Assert
        Assert.Contains(expected, affix.DisplayText);
    }
    
    [Theory]
    [InlineData(StatType.CritChance, ModifierType.Percent, 5.0)]
    [InlineData(StatType.HastePercent, ModifierType.Percent, 3.5)]
    public void AffixInstance_ShouldFormatPercentModifierDisplayText(StatType statType, ModifierType modifierType, double value)
    {
        // Arrange & Act
        var affix = new AffixInstance("test", statType, modifierType, value);
        
        // Assert
        Assert.Contains("%", affix.DisplayText);
        Assert.Contains(value.ToString("F1"), affix.DisplayText);
    }
    
    [Fact]
    public void AffixInstance_ShouldAllowEmptyConstructor()
    {
        // Arrange & Act
        var affix = new AffixInstance();
        
        // Assert
        Assert.Empty(affix.AffixId);
        Assert.Equal(0.0, affix.RolledValue);
        Assert.Empty(affix.DisplayText);
    }
}
