using BlazorIdle.Server.Domain.Equipment.Models;
using BlazorIdle.Server.Domain.Equipment.Services;
using BlazorIdle.Server.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BlazorIdle.Tests.Equipment.Services;

/// <summary>
/// 属性聚合服务测试
/// </summary>
public class StatsAggregationServiceTests : IDisposable
{
    private readonly GameDbContext _context;
    private readonly EquipmentService _equipmentService;
    private readonly StatsAggregationService _service;

    public StatsAggregationServiceTests()
    {
        var options = new DbContextOptionsBuilder<GameDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new GameDbContext(options);
        var validator = new EquipmentValidator();
        var logger = NullLogger<EquipmentService>.Instance;
        _equipmentService = new EquipmentService(_context, validator, logger);
        var armorCalculator = new ArmorCalculator();
        var blockCalculator = new BlockCalculator();
        _service = new StatsAggregationService(_equipmentService, armorCalculator, blockCalculator);
    }

    [Fact]
    public async Task CalculateEquipmentStatsAsync_NoEquipment_ShouldReturnEmptyStats()
    {
        // Arrange
        var characterId = Guid.NewGuid();

        // Act
        var stats = await _service.CalculateEquipmentStatsAsync(characterId);

        // Assert
        Assert.NotNull(stats);
        Assert.Empty(stats);
    }

    [Fact]
    public async Task CalculateEquipmentStatsAsync_WithEquipment_ShouldAggregateStats()
    {
        // Arrange
        var characterId = Guid.NewGuid();
        var (headDef, headGear) = CreateTestGearWithStats(characterId, EquipmentSlot.Head);
        
        headGear.IsEquipped = true;
        headGear.SlotType = EquipmentSlot.Head;
        headGear.RolledStats = new Dictionary<StatType, double>
        {
            { StatType.Strength, 10 },
            { StatType.Stamina, 15 }
        };
        
        await _context.Set<GearDefinition>().AddAsync(headDef);
        await _context.Set<GearInstance>().AddAsync(headGear);
        await _context.SaveChangesAsync();

        // Act
        var stats = await _service.CalculateEquipmentStatsAsync(characterId);

        // Assert
        Assert.NotEmpty(stats);
        Assert.True(stats.ContainsKey(StatType.Strength));
        Assert.True(stats.ContainsKey(StatType.Stamina));
        Assert.Equal(10, stats[StatType.Strength]);
        Assert.Equal(15, stats[StatType.Stamina]);
    }

    [Fact]
    public async Task CalculateEquipmentStatsAsync_MultipleGear_ShouldSumStats()
    {
        // Arrange
        var characterId = Guid.NewGuid();
        var (headDef, headGear) = CreateTestGearWithStats(characterId, EquipmentSlot.Head);
        var (chestDef, chestGear) = CreateTestGearWithStats(characterId, EquipmentSlot.Chest);
        
        headGear.IsEquipped = true;
        headGear.SlotType = EquipmentSlot.Head;
        headGear.RolledStats = new Dictionary<StatType, double>
        {
            { StatType.Strength, 10 }
        };
        
        chestGear.IsEquipped = true;
        chestGear.SlotType = EquipmentSlot.Chest;
        chestGear.RolledStats = new Dictionary<StatType, double>
        {
            { StatType.Strength, 15 }
        };
        
        await _context.Set<GearDefinition>().AddRangeAsync(headDef, chestDef);
        await _context.Set<GearInstance>().AddRangeAsync(headGear, chestGear);
        await _context.SaveChangesAsync();

        // Act
        var stats = await _service.CalculateEquipmentStatsAsync(characterId);

        // Assert
        Assert.True(stats.ContainsKey(StatType.Strength));
        Assert.Equal(25, stats[StatType.Strength]); // 10 + 15
    }

    [Fact]
    public async Task GetEquipmentStatsSummaryAsync_ShouldReturnSummary()
    {
        // Arrange
        var characterId = Guid.NewGuid();
        var (headDef, headGear) = CreateTestGearWithStats(characterId, EquipmentSlot.Head);
        
        headGear.IsEquipped = true;
        headGear.SlotType = EquipmentSlot.Head;
        headGear.QualityScore = 100;
        headGear.RolledStats = new Dictionary<StatType, double>
        {
            { StatType.Strength, 10 }
        };
        
        await _context.Set<GearDefinition>().AddAsync(headDef);
        await _context.Set<GearInstance>().AddAsync(headGear);
        await _context.SaveChangesAsync();

        // Act
        var summary = await _service.GetEquipmentStatsSummaryAsync(characterId);

        // Assert
        Assert.NotNull(summary);
        Assert.NotEmpty(summary.Stats);
        Assert.Equal(1, summary.EquippedCount);
        Assert.Equal(100, summary.TotalQualityScore);
    }

    [Fact]
    public async Task CalculateEquipmentStatsAsync_WithSetBonus_ShouldApplyCumulativeBonus()
    {
        // Arrange
        var characterId = Guid.NewGuid();
        var setId = "test_set";
        
        // 创建6件套装装备
        var gearPieces = new List<GearInstance>();
        var definitions = new List<GearDefinition>();
        var slots = new[] 
        { 
            EquipmentSlot.Head, EquipmentSlot.Chest, EquipmentSlot.Legs, 
            EquipmentSlot.Hands, EquipmentSlot.Feet, EquipmentSlot.Waist 
        };
        
        foreach (var slot in slots)
        {
            var (def, gear) = CreateTestGearWithStats(characterId, slot);
            gear.SetId = setId;
            gear.IsEquipped = true;
            gear.SlotType = slot;
            gear.RolledStats = new Dictionary<StatType, double>
            {
                { StatType.Strength, 5 } // 基础属性
            };
            
            definitions.Add(def);
            gearPieces.Add(gear);
        }
        
        await _context.Set<GearDefinition>().AddRangeAsync(definitions);
        await _context.Set<GearInstance>().AddRangeAsync(gearPieces);
        await _context.SaveChangesAsync();

        // Act
        var stats = await _service.CalculateEquipmentStatsAsync(characterId);

        // Assert
        // 基础属性: 6件 × 5力量 = 30力量
        Assert.Equal(30, stats[StatType.Strength]);
        
        // 套装加成应该是累加的:
        // 2件套: +50攻击力
        // 4件套: +50攻击力(总共100) + 50暴击
        // 6件套: +100攻击力(总共200) + 50暴击(总共100) + 100急速
        Assert.True(stats.ContainsKey(StatType.AttackPower));
        Assert.Equal(200, stats[StatType.AttackPower]);
        
        Assert.True(stats.ContainsKey(StatType.CritRating));
        Assert.Equal(100, stats[StatType.CritRating]);
        
        Assert.True(stats.ContainsKey(StatType.Haste));
        Assert.Equal(100, stats[StatType.Haste]);
    }

    [Fact]
    public async Task CalculateEquipmentStatsAsync_With4PieceSet_ShouldApplyCorrectBonus()
    {
        // Arrange
        var characterId = Guid.NewGuid();
        var setId = "test_set";
        
        // 创建4件套装装备
        var gearPieces = new List<GearInstance>();
        var definitions = new List<GearDefinition>();
        var slots = new[] { EquipmentSlot.Head, EquipmentSlot.Chest, EquipmentSlot.Legs, EquipmentSlot.Hands };
        
        foreach (var slot in slots)
        {
            var (def, gear) = CreateTestGearWithStats(characterId, slot);
            gear.SetId = setId;
            gear.IsEquipped = true;
            gear.SlotType = slot;
            gear.RolledStats = new Dictionary<StatType, double>();
            
            definitions.Add(def);
            gearPieces.Add(gear);
        }
        
        await _context.Set<GearDefinition>().AddRangeAsync(definitions);
        await _context.Set<GearInstance>().AddRangeAsync(gearPieces);
        await _context.SaveChangesAsync();

        // Act
        var stats = await _service.CalculateEquipmentStatsAsync(characterId);

        // Assert
        // 2件套: +50攻击力
        // 4件套: +50攻击力(总共100) + 50暴击
        Assert.True(stats.ContainsKey(StatType.AttackPower));
        Assert.Equal(100, stats[StatType.AttackPower]);
        
        Assert.True(stats.ContainsKey(StatType.CritRating));
        Assert.Equal(50, stats[StatType.CritRating]);
        
        // 6件套加成不应该存在
        Assert.False(stats.ContainsKey(StatType.Haste));
    }

    private (GearDefinition, GearInstance) CreateTestGearWithStats(Guid characterId, EquipmentSlot slot)
    {
        var definition = new GearDefinition
        {
            Id = $"test_{slot}_{Guid.NewGuid()}",
            Name = $"测试装备 {slot}",
            Slot = slot,
            ArmorType = ArmorType.Plate,
            WeaponType = WeaponType.None,
            RequiredLevel = 1
        };

        var gear = new GearInstance
        {
            Id = Guid.NewGuid(),
            DefinitionId = definition.Id,
            CharacterId = characterId,
            Rarity = Rarity.Common,
            TierLevel = 1,
            ItemLevel = 10,
            IsEquipped = false,
            Definition = definition
        };

        return (definition, gear);
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
