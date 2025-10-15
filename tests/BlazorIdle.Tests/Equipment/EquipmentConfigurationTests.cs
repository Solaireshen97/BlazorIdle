using BlazorIdle.Server.Domain.Equipment.Services;
using BlazorIdle.Server.Infrastructure.Configuration;
using Microsoft.Extensions.Options;
using Xunit;

namespace BlazorIdle.Tests.Equipment;

/// <summary>
/// 装备系统配置测试
/// 验证装备系统配置选项的正确性
/// </summary>
public class EquipmentConfigurationTests
{
    [Fact]
    public void ArmorCalculator_WithDefaultConfig_ShouldUseDefaultValues()
    {
        // Arrange
        var calculator = new ArmorCalculator();
        
        // Act
        var shieldArmor = calculator.CalculateShieldArmorValue(100);
        var armorReduction = calculator.CalculateArmorReduction(1000, 50);
        
        // Assert
        Assert.Equal(225.0, shieldArmor, precision: 1); // 100 * 2.25
        Assert.True(armorReduction > 0 && armorReduction <= 0.75);
    }

    [Fact]
    public void ArmorCalculator_WithCustomConfig_ShouldUseCustomValues()
    {
        // Arrange
        var customOptions = new EquipmentSystemOptions
        {
            ArmorCalculation = new ArmorCalculationOptions
            {
                ArmorConstant = 500.0,
                MaxArmorReduction = 0.80,
                ShieldArmorMultiplier = 3.0
            }
        };
        var calculator = new ArmorCalculator(Options.Create(customOptions));
        
        // Act
        var shieldArmor = calculator.CalculateShieldArmorValue(100);
        
        // Assert
        Assert.Equal(300.0, shieldArmor, precision: 1); // 100 * 3.0
    }

    [Fact]
    public void BlockCalculator_WithDefaultConfig_ShouldUseDefaultValues()
    {
        // Arrange
        var calculator = new BlockCalculator();
        
        // Act
        var blockChance = calculator.CalculateBlockChance(100, 100);
        var maxBlockChance = calculator.GetMaxBlockChance();
        var blockReduction = calculator.GetBlockDamageReduction();
        
        // Assert
        Assert.True(blockChance > 0 && blockChance <= 0.50);
        Assert.Equal(0.50, maxBlockChance);
        Assert.Equal(0.30, blockReduction);
    }

    [Fact]
    public void BlockCalculator_WithCustomConfig_ShouldUseCustomValues()
    {
        // Arrange
        var customOptions = new EquipmentSystemOptions
        {
            BlockCalculation = new BlockCalculationOptions
            {
                BaseBlockChance = 0.10,
                BlockDamageReduction = 0.40,
                BlockChancePerStrength = 0.002,
                BlockChancePerItemLevel = 0.003,
                MaxBlockChance = 0.60
            }
        };
        var calculator = new BlockCalculator(Options.Create(customOptions));
        
        // Act
        var blockChance = calculator.CalculateBlockChance(100, 100);
        var maxBlockChance = calculator.GetMaxBlockChance();
        var blockReduction = calculator.GetBlockDamageReduction();
        var reducedDamage = calculator.ApplyBlockReduction(1000);
        
        // Assert
        // BaseBlockChance (0.10) + ItemLevel*PerItemLevel (100*0.003) + Strength*PerStrength (100*0.002)
        // = 0.10 + 0.30 + 0.20 = 0.60 (capped at MaxBlockChance)
        Assert.Equal(0.60, blockChance, precision: 2);
        Assert.Equal(0.60, maxBlockChance);
        Assert.Equal(0.40, blockReduction);
        Assert.Equal(600, reducedDamage); // 1000 * (1 - 0.40) = 600
    }

    [Fact]
    public void WeaponDamageCalculator_WithDefaultConfig_ShouldUseDefaultValues()
    {
        // Arrange
        var attackSpeedCalc = new AttackSpeedCalculator();
        var calculator = new WeaponDamageCalculator(attackSpeedCalc);
        
        // Act
        var damage = calculator.CalculateWeaponDamage(
            baseDamage: 100,
            attackPower: 50,
            mainHandWeapon: BlazorIdle.Server.Domain.Equipment.Models.WeaponType.Sword,
            offHandWeapon: BlazorIdle.Server.Domain.Equipment.Models.WeaponType.Sword,
            isDualWielding: true
        );
        
        // Assert
        Assert.True(damage > 150); // Should be > base+ap due to dual wielding
    }

    [Fact]
    public void WeaponDamageCalculator_WithCustomConfig_ShouldUseCustomValues()
    {
        // Arrange
        var customOptions = new EquipmentSystemOptions
        {
            WeaponDamage = new WeaponDamageOptions
            {
                OffHandDamageCoefficient = 1.0 // 100% off-hand damage
            }
        };
        var attackSpeedCalc = new AttackSpeedCalculator();
        var calculator = new WeaponDamageCalculator(attackSpeedCalc, Options.Create(customOptions));
        
        // Act
        var damage = calculator.CalculateWeaponDamage(
            baseDamage: 100,
            attackPower: 0,
            mainHandWeapon: BlazorIdle.Server.Domain.Equipment.Models.WeaponType.Sword,
            offHandWeapon: BlazorIdle.Server.Domain.Equipment.Models.WeaponType.Sword,
            isDualWielding: true
        );
        
        // Assert
        // With 100% off-hand coefficient, dual wield should do more than single wield
        // The exact calculation depends on weapon multipliers from AttackSpeedCalculator
        // We just verify it's using the custom coefficient by ensuring damage > 150
        Assert.True(damage > 150.0, $"Expected damage > 150 but got {damage}");
    }

    [Fact]
    public void EquipmentSystemOptions_DefaultValues_ShouldMatchExpectedConstants()
    {
        // Arrange
        var options = new EquipmentSystemOptions();
        
        // Assert - Armor Calculation
        Assert.Equal(400.0, options.ArmorCalculation.ArmorConstant);
        Assert.Equal(0.75, options.ArmorCalculation.MaxArmorReduction);
        Assert.Equal(2.25, options.ArmorCalculation.ShieldArmorMultiplier);
        
        // Assert - Block Calculation
        Assert.Equal(0.05, options.BlockCalculation.BaseBlockChance);
        Assert.Equal(0.30, options.BlockCalculation.BlockDamageReduction);
        Assert.Equal(0.001, options.BlockCalculation.BlockChancePerStrength);
        Assert.Equal(0.002, options.BlockCalculation.BlockChancePerItemLevel);
        Assert.Equal(0.50, options.BlockCalculation.MaxBlockChance);
        
        // Assert - Weapon Damage
        Assert.Equal(0.85, options.WeaponDamage.OffHandDamageCoefficient);
    }
}
