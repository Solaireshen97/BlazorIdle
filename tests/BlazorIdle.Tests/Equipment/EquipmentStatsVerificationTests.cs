using BlazorIdle.Server.Domain.Characters;
using BlazorIdle.Server.Domain.Equipment.Models;
using BlazorIdle.Server.Domain.Equipment.Services;
using BlazorIdle.Shared.Models;
using Xunit;

namespace BlazorIdle.Tests.Equipment;

/// <summary>
/// 验证装备属性正确转换到CharacterStats
/// </summary>
public class EquipmentStatsVerificationTests
{
    [Fact]
    public async Task HasteRating_ShouldConvertToHastePercent()
    {
        // Arrange
        var characterId = Guid.NewGuid();
        var fakeService = new FakeStatsAggregationServiceWithHaste(characterId, hasteRating: 1000);
        var integration = new EquipmentStatsIntegration(fakeService);
        
        var primaryAttrs = new PrimaryAttributes
        {
            Strength = 10,
            Agility = 10,
            Intellect = 10,
            Stamina = 10
        };
        
        // Act
        var stats = await integration.BuildStatsWithEquipmentAsync(
            characterId, 
            Profession.Warrior, 
            primaryAttrs);
        
        // Assert
        // 1000评级 / 4000 = 0.25 = 25%急速
        Assert.Equal(0.25, stats.HastePercent, 3);
    }
    
    [Fact]
    public async Task CritRating_ShouldConvertToCritChance()
    {
        // Arrange
        var characterId = Guid.NewGuid();
        var fakeService = new FakeStatsAggregationServiceWithCrit(characterId, critRating: 800);
        var integration = new EquipmentStatsIntegration(fakeService);
        
        var primaryAttrs = new PrimaryAttributes
        {
            Strength = 10,
            Agility = 10,
            Intellect = 10,
            Stamina = 10
        };
        
        // Act
        var stats = await integration.BuildStatsWithEquipmentAsync(
            characterId, 
            Profession.Warrior, 
            primaryAttrs);
        
        // Assert
        // Warrior基础暴击率 + 800评级 / 4000 = 基础 + 0.20
        // 战士基础暴击为0.05，所以总共0.25
        Assert.True(stats.CritChance >= 0.20, 
            $"Expected at least 0.20 crit chance, got {stats.CritChance}");
    }
    
    [Fact]
    public async Task HastePercent_ShouldAddDirectly()
    {
        // Arrange
        var characterId = Guid.NewGuid();
        var fakeService = new FakeStatsAggregationServiceWithHastePercent(characterId, hastePercent: 0.15);
        var integration = new EquipmentStatsIntegration(fakeService);
        
        var primaryAttrs = new PrimaryAttributes
        {
            Strength = 10,
            Agility = 10,
            Intellect = 10,
            Stamina = 10
        };
        
        // Act
        var stats = await integration.BuildStatsWithEquipmentAsync(
            characterId, 
            Profession.Warrior, 
            primaryAttrs);
        
        // Assert
        // 15%急速应该直接加到HastePercent
        Assert.Equal(0.15, stats.HastePercent, 3);
    }
}

// === 测试辅助类 ===

class FakeStatsAggregationServiceWithHaste : StatsAggregationService
{
    private readonly Guid _characterId;
    private readonly double _hasteRating;
    
    public FakeStatsAggregationServiceWithHaste(Guid characterId, double hasteRating)
        : base(null!, null!, null!)
    {
        _characterId = characterId;
        _hasteRating = hasteRating;
    }
    
    public override Task<Dictionary<StatType, double>> CalculateEquipmentStatsAsync(Guid characterId)
    {
        if (characterId != _characterId)
            return Task.FromResult(new Dictionary<StatType, double>());
            
        return Task.FromResult(new Dictionary<StatType, double>
        {
            { StatType.Haste, _hasteRating }
        });
    }
    
    public override Task<double> CalculateBlockChanceAsync(Guid characterId, double characterStrength = 0)
    {
        return Task.FromResult(0.0);
    }
    
    public override Task<WeaponType> GetMainHandWeaponTypeAsync(Guid characterId)
    {
        return Task.FromResult(WeaponType.None);
    }
    
    public override Task<WeaponType> GetOffHandWeaponTypeAsync(Guid characterId)
    {
        return Task.FromResult(WeaponType.None);
    }
    
    public override Task<bool> IsDualWieldingAsync(Guid characterId)
    {
        return Task.FromResult(false);
    }
}

class FakeStatsAggregationServiceWithCrit : StatsAggregationService
{
    private readonly Guid _characterId;
    private readonly double _critRating;
    
    public FakeStatsAggregationServiceWithCrit(Guid characterId, double critRating)
        : base(null!, null!, null!)
    {
        _characterId = characterId;
        _critRating = critRating;
    }
    
    public override Task<Dictionary<StatType, double>> CalculateEquipmentStatsAsync(Guid characterId)
    {
        if (characterId != _characterId)
            return Task.FromResult(new Dictionary<StatType, double>());
            
        return Task.FromResult(new Dictionary<StatType, double>
        {
            { StatType.CritRating, _critRating }
        });
    }
    
    public override Task<double> CalculateBlockChanceAsync(Guid characterId, double characterStrength = 0)
    {
        return Task.FromResult(0.0);
    }
    
    public override Task<WeaponType> GetMainHandWeaponTypeAsync(Guid characterId)
    {
        return Task.FromResult(WeaponType.None);
    }
    
    public override Task<WeaponType> GetOffHandWeaponTypeAsync(Guid characterId)
    {
        return Task.FromResult(WeaponType.None);
    }
    
    public override Task<bool> IsDualWieldingAsync(Guid characterId)
    {
        return Task.FromResult(false);
    }
}

class FakeStatsAggregationServiceWithHastePercent : StatsAggregationService
{
    private readonly Guid _characterId;
    private readonly double _hastePercent;
    
    public FakeStatsAggregationServiceWithHastePercent(Guid characterId, double hastePercent)
        : base(null!, null!, null!)
    {
        _characterId = characterId;
        _hastePercent = hastePercent;
    }
    
    public override Task<Dictionary<StatType, double>> CalculateEquipmentStatsAsync(Guid characterId)
    {
        if (characterId != _characterId)
            return Task.FromResult(new Dictionary<StatType, double>());
            
        return Task.FromResult(new Dictionary<StatType, double>
        {
            { StatType.HastePercent, _hastePercent }
        });
    }
    
    public override Task<double> CalculateBlockChanceAsync(Guid characterId, double characterStrength = 0)
    {
        return Task.FromResult(0.0);
    }
    
    public override Task<WeaponType> GetMainHandWeaponTypeAsync(Guid characterId)
    {
        return Task.FromResult(WeaponType.None);
    }
    
    public override Task<WeaponType> GetOffHandWeaponTypeAsync(Guid characterId)
    {
        return Task.FromResult(WeaponType.None);
    }
    
    public override Task<bool> IsDualWieldingAsync(Guid characterId)
    {
        return Task.FromResult(false);
    }
}
