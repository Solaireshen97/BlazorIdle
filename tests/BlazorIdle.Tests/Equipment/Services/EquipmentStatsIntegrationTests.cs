using BlazorIdle.Server.Domain.Characters;
using BlazorIdle.Server.Domain.Equipment.Models;
using BlazorIdle.Server.Domain.Equipment.Services;
using BlazorIdle.Server.Domain.Equipment.ValueObjects;
using BlazorIdle.Shared.Models;
using Xunit;

namespace BlazorIdle.Tests.Equipment.Services;

public class EquipmentStatsIntegrationTests
{
    private readonly FakeStatsAggregationService _fakeStatsAggregationService;
    private readonly EquipmentStatsIntegration _service;

    public EquipmentStatsIntegrationTests()
    {
        _fakeStatsAggregationService = new FakeStatsAggregationService();
        _service = new EquipmentStatsIntegration(_fakeStatsAggregationService);
    }

    [Fact]
    public async Task BuildStatsWithEquipmentAsync_ShouldIncludeEquipmentStats()
    {
        // Arrange
        var characterId = Guid.NewGuid();
        var profession = Profession.Warrior;
        var primaryAttrs = new PrimaryAttributes
        {
            Strength = 20,
            Agility = 10,
            Intellect = 5,
            Stamina = 15
        };

        var equipmentStats = new Dictionary<StatType, double>
        {
            { StatType.AttackPower, 100 },
            { StatType.CritRating, 200 },
            { StatType.Haste, 0.05 },
            { StatType.Armor, 500 }
        };

        _fakeStatsAggregationService.SetEquipmentStats(characterId, equipmentStats);

        // Act
        var result = await _service.BuildStatsWithEquipmentAsync(characterId, profession, primaryAttrs);

        // Assert
        Assert.NotNull(result);
        
        // 装备提供的攻击强度应该被加到基础上
        Assert.True(result.AttackPower >= 100, "AttackPower should include equipment bonus");
        
        // 暴击评级应该转换为暴击率
        Assert.True(result.CritChance > 0, "CritChance should include converted crit rating");
        
        // 急速应该被应用
        Assert.True(result.HastePercent >= 0.05, "HastePercent should include equipment haste");
    }

    [Fact]
    public async Task BuildStatsWithEquipmentAsync_WithoutEquipment_ShouldReturnBaseStats()
    {
        // Arrange
        var characterId = Guid.NewGuid();
        var profession = Profession.Ranger;
        var primaryAttrs = new PrimaryAttributes
        {
            Strength = 5,
            Agility = 25,
            Intellect = 5,
            Stamina = 10
        };

        var emptyEquipmentStats = new Dictionary<StatType, double>();
        _fakeStatsAggregationService.SetEquipmentStats(characterId, emptyEquipmentStats);

        // Act
        var result = await _service.BuildStatsWithEquipmentAsync(characterId, profession, primaryAttrs);

        // Assert
        Assert.NotNull(result);
        
        // 应该只有基础属性和派生属性，没有装备加成
        Assert.True(result.AttackPower > 0, "Ranger should have attack power from agility");
    }

    [Fact]
    public async Task BuildStatsWithEquipmentAsync_ShouldConvertCritRatingProperly()
    {
        // Arrange
        var characterId = Guid.NewGuid();
        var profession = Profession.Ranger;
        var primaryAttrs = new PrimaryAttributes { Strength = 10, Agility = 20, Intellect = 5, Stamina = 10 };

        // 200 暴击评级 = 0.05 暴击率 (200 / 4000)
        var equipmentStats = new Dictionary<StatType, double>
        {
            { StatType.CritRating, 200 }
        };

        _fakeStatsAggregationService.SetEquipmentStats(characterId, equipmentStats);

        // Act
        var result = await _service.BuildStatsWithEquipmentAsync(characterId, profession, primaryAttrs);

        // Assert
        // 注意：总暴击率包括职业基础+属性派生+装备，所以只检查装备部分确实被应用了
        Assert.True(result.CritChance >= 0.05, 
            $"CritChance should be at least 0.05 from equipment rating, but was {result.CritChance}");
    }

    [Fact]
    public async Task BuildStatsWithEquipmentAsync_ShouldApplyHastePercent()
    {
        // Arrange
        var characterId = Guid.NewGuid();
        var profession = Profession.Warrior;
        var primaryAttrs = new PrimaryAttributes { Strength = 20, Agility = 10, Intellect = 5, Stamina = 15 };

        // 直接提供急速百分比
        var equipmentStats = new Dictionary<StatType, double>
        {
            { StatType.Haste, 0.10 }
        };

        _fakeStatsAggregationService.SetEquipmentStats(characterId, equipmentStats);

        // Act
        var result = await _service.BuildStatsWithEquipmentAsync(characterId, profession, primaryAttrs);

        // Assert
        Assert.True(result.HastePercent >= 0.10, 
            $"HastePercent should be at least 0.10, but was {result.HastePercent}");
    }

    [Fact]
    public async Task GetEquipmentArmorAsync_ShouldReturnTotalArmor()
    {
        // Arrange
        var characterId = Guid.NewGuid();
        var equipmentStats = new Dictionary<StatType, double>
        {
            { StatType.Armor, 750 }
        };

        _fakeStatsAggregationService.SetEquipmentStats(characterId, equipmentStats);

        // Act
        var result = await _service.GetEquipmentArmorAsync(characterId);

        // Assert
        Assert.Equal(750, result);
    }

    [Fact]
    public async Task GetEquipmentArmorAsync_WithoutArmor_ShouldReturnZero()
    {
        // Arrange
        var characterId = Guid.NewGuid();
        var emptyEquipmentStats = new Dictionary<StatType, double>();

        _fakeStatsAggregationService.SetEquipmentStats(characterId, emptyEquipmentStats);

        // Act
        var result = await _service.GetEquipmentArmorAsync(characterId);

        // Assert
        Assert.Equal(0, result);
    }

    [Fact]
    public async Task BuildStatsWithEquipmentAsync_ShouldClampCritChanceToOne()
    {
        // Arrange
        var characterId = Guid.NewGuid();
        var profession = Profession.Ranger;
        var primaryAttrs = new PrimaryAttributes { Strength = 10, Agility = 20, Intellect = 5, Stamina = 10 };

        // 超大的暴击值
        var equipmentStats = new Dictionary<StatType, double>
        {
            { StatType.CritChance, 2.0 } // 200% 暴击（应该被限制到100%）
        };

        _fakeStatsAggregationService.SetEquipmentStats(characterId, equipmentStats);

        // Act
        var result = await _service.BuildStatsWithEquipmentAsync(characterId, profession, primaryAttrs);

        // Assert
        Assert.True(result.CritChance <= 1.0, "CritChance should be clamped to 1.0");
    }

    [Fact]
    public async Task BuildStatsWithEquipmentAsync_ShouldApplyMultipleStatTypes()
    {
        // Arrange
        var characterId = Guid.NewGuid();
        var profession = Profession.Warrior;
        var primaryAttrs = new PrimaryAttributes { Strength = 20, Agility = 10, Intellect = 5, Stamina = 15 };

        var equipmentStats = new Dictionary<StatType, double>
        {
            { StatType.AttackPower, 100 },
            { StatType.SpellPower, 50 },
            { StatType.CritRating, 200 }
        };

        _fakeStatsAggregationService.SetEquipmentStats(characterId, equipmentStats);

        // Act
        var result = await _service.BuildStatsWithEquipmentAsync(characterId, profession, primaryAttrs);

        // Assert
        Assert.True(result.AttackPower >= 100, "AttackPower should include equipment bonus");
        Assert.True(result.SpellPower >= 50, "SpellPower should include equipment bonus");
        Assert.True(result.CritChance > 0, "CritChance should include converted crit rating");
    }
}

/// <summary>
/// 假装备属性聚合服务用于测试
/// </summary>
internal class FakeStatsAggregationService : StatsAggregationService
{
    private readonly Dictionary<Guid, Dictionary<StatType, double>> _equipmentStatsCache = new();

    public FakeStatsAggregationService() : base(null!)
    {
    }

    public void SetEquipmentStats(Guid characterId, Dictionary<StatType, double> stats)
    {
        _equipmentStatsCache[characterId] = stats;
    }

    public override Task<Dictionary<StatType, double>> CalculateEquipmentStatsAsync(Guid characterId)
    {
        if (_equipmentStatsCache.TryGetValue(characterId, out var stats))
        {
            return Task.FromResult(stats);
        }
        
        return Task.FromResult(new Dictionary<StatType, double>());
    }
}
