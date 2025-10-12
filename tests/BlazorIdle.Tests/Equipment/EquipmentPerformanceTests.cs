using BlazorIdle.Server.Domain.Characters;
using BlazorIdle.Server.Domain.Equipment.Models;
using BlazorIdle.Server.Domain.Equipment.Services;
using BlazorIdle.Server.Infrastructure.Persistence;
using BlazorIdle.Shared.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace BlazorIdle.Tests.Equipment;

/// <summary>
/// 装备系统性能测试
/// Phase 5.7: 验证装备操作性能满足要求（< 200ms）
/// </summary>
public class EquipmentPerformanceTests : IDisposable
{
    private readonly GameDbContext _context;
    private readonly EquipmentService _equipmentService;
    private readonly StatsAggregationService _statsService;
    private readonly ITestOutputHelper _output;

    public EquipmentPerformanceTests(ITestOutputHelper output)
    {
        _output = output;
        
        var options = new DbContextOptionsBuilder<GameDbContext>()
            .UseInMemoryDatabase($"EquipmentPerf_{Guid.NewGuid()}")
            .Options;

        _context = new GameDbContext(options);
        var validator = new EquipmentValidator();
        _equipmentService = new EquipmentService(_context, validator);
        
        var armorCalc = new ArmorCalculator();
        var blockCalc = new BlockCalculator();
        _statsService = new StatsAggregationService(_equipmentService, armorCalc, blockCalc);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    [Fact]
    public async Task EquipItem_Performance_ShouldBeUnder200ms()
    {
        // Arrange
        var character = await CreateTestCharacterAsync();
        var (definition, gear) = CreateTestGear(character.Id, EquipmentSlot.Head);
        
        await _context.Set<GearDefinition>().AddAsync(definition);
        await _context.Set<GearInstance>().AddAsync(gear);
        await _context.SaveChangesAsync();
        
        // Warm-up: execute once to load caches
        await _equipmentService.EquipAsync(character.Id, gear.Id);
        await _equipmentService.UnequipAsync(character.Id, EquipmentSlot.Head);
        
        // Act - Measure equip operation
        var stopwatch = Stopwatch.StartNew();
        var result = await _equipmentService.EquipAsync(character.Id, gear.Id);
        stopwatch.Stop();
        
        // Assert
        Assert.True(result.IsSuccess, $"装备失败: {result.Message}");
        Assert.True(stopwatch.ElapsedMilliseconds < 200, 
            $"装备操作耗时 {stopwatch.ElapsedMilliseconds}ms，超过200ms限制");
        
        _output.WriteLine($"✓ 装备操作耗时: {stopwatch.ElapsedMilliseconds}ms");
    }

    [Fact]
    public async Task UnequipItem_Performance_ShouldBeUnder200ms()
    {
        // Arrange
        var character = await CreateTestCharacterAsync();
        var (definition, gear) = CreateTestGear(character.Id, EquipmentSlot.Chest);
        
        await _context.Set<GearDefinition>().AddAsync(definition);
        await _context.Set<GearInstance>().AddAsync(gear);
        await _context.SaveChangesAsync();
        
        await _equipmentService.EquipAsync(character.Id, gear.Id);
        
        // Warm-up
        await _equipmentService.UnequipAsync(character.Id, EquipmentSlot.Chest);
        await _equipmentService.EquipAsync(character.Id, gear.Id);
        
        // Act - Measure unequip operation
        var stopwatch = Stopwatch.StartNew();
        var result = await _equipmentService.UnequipAsync(character.Id, EquipmentSlot.Chest);
        stopwatch.Stop();
        
        // Assert
        Assert.True(result.IsSuccess, $"卸下失败: {result.Message}");
        Assert.True(stopwatch.ElapsedMilliseconds < 200, 
            $"卸下装备耗时 {stopwatch.ElapsedMilliseconds}ms，超过200ms限制");
        
        _output.WriteLine($"✓ 卸下装备耗时: {stopwatch.ElapsedMilliseconds}ms");
    }

    [Fact]
    public async Task CalculateEquipmentStats_Performance_ShouldBeUnder100ms()
    {
        // Arrange - equip multiple items
        var character = await CreateTestCharacterAsync();
        
        // Create and equip several items
        var slots = new[] { EquipmentSlot.Head, EquipmentSlot.Chest, EquipmentSlot.Legs, EquipmentSlot.Feet };
        foreach (var slot in slots)
        {
            var (definition, gear) = CreateTestGear(character.Id, slot);
            await _context.Set<GearDefinition>().AddAsync(definition);
            await _context.Set<GearInstance>().AddAsync(gear);
            await _context.SaveChangesAsync();
            await _equipmentService.EquipAsync(character.Id, gear.Id);
        }
        
        // Warm-up
        await _statsService.CalculateEquipmentStatsAsync(character.Id);
        
        // Act - Measure stats calculation
        var stopwatch = Stopwatch.StartNew();
        var stats = await _statsService.CalculateEquipmentStatsAsync(character.Id);
        stopwatch.Stop();
        
        // Assert
        Assert.NotEmpty(stats);
        Assert.True(stopwatch.ElapsedMilliseconds < 100, 
            $"属性计算耗时 {stopwatch.ElapsedMilliseconds}ms，超过100ms预期");
        
        _output.WriteLine($"✓ 属性计算耗时: {stopwatch.ElapsedMilliseconds}ms");
        _output.WriteLine($"  计算了 {stats.Count} 个属性");
    }

    [Fact]
    public async Task GetEquippedGear_Performance_ShouldBeUnder50ms()
    {
        // Arrange
        var character = await CreateTestCharacterAsync();
        
        // Equip several items
        var slots = new[] 
        { 
            EquipmentSlot.Head, EquipmentSlot.Chest, EquipmentSlot.Legs, 
            EquipmentSlot.Feet, EquipmentSlot.Hands, EquipmentSlot.Waist 
        };
        
        foreach (var slot in slots)
        {
            var (definition, gear) = CreateTestGear(character.Id, slot);
            await _context.Set<GearDefinition>().AddAsync(definition);
            await _context.Set<GearInstance>().AddAsync(gear);
            await _context.SaveChangesAsync();
            await _equipmentService.EquipAsync(character.Id, gear.Id);
        }
        
        // Warm-up
        await _equipmentService.GetEquippedGearAsync(character.Id);
        
        // Act - Measure query performance
        var stopwatch = Stopwatch.StartNew();
        var equippedGear = await _equipmentService.GetEquippedGearAsync(character.Id);
        stopwatch.Stop();
        
        // Assert
        Assert.NotEmpty(equippedGear);
        Assert.True(stopwatch.ElapsedMilliseconds < 50, 
            $"查询装备列表耗时 {stopwatch.ElapsedMilliseconds}ms，超过50ms预期");
        
        _output.WriteLine($"✓ 查询装备列表耗时: {stopwatch.ElapsedMilliseconds}ms");
        _output.WriteLine($"  返回了 {equippedGear.Count} 件装备");
    }

    [Fact]
    public async Task BlockChanceCalculation_Performance_ShouldBeUnder10ms()
    {
        // Arrange
        var character = await CreateTestCharacterAsync();
        var (definition, shield) = CreateTestShield(character.Id);
        
        await _context.Set<GearDefinition>().AddAsync(definition);
        await _context.Set<GearInstance>().AddAsync(shield);
        await _context.SaveChangesAsync();
        await _equipmentService.EquipAsync(character.Id, shield.Id);
        
        // Warm-up
        await _statsService.CalculateBlockChanceAsync(character.Id, character.Strength);
        
        // Act - Measure block chance calculation
        var stopwatch = Stopwatch.StartNew();
        var blockChance = await _statsService.CalculateBlockChanceAsync(character.Id, character.Strength);
        stopwatch.Stop();
        
        // Assert
        Assert.True(blockChance > 0);
        Assert.True(stopwatch.ElapsedMilliseconds < 10, 
            $"格挡率计算耗时 {stopwatch.ElapsedMilliseconds}ms，超过10ms预期");
        
        _output.WriteLine($"✓ 格挡率计算耗时: {stopwatch.ElapsedMilliseconds}ms");
        _output.WriteLine($"  格挡率: {blockChance:P1}");
    }

    /// <summary>
    /// 性能基准测试 - 多次操作取平均值
    /// </summary>
    [Fact]
    public async Task EquipmentOperations_BenchmarkTest()
    {
        const int iterations = 10;
        var equipTimes = new long[iterations];
        var unequipTimes = new long[iterations];
        var statsCalcTimes = new long[iterations];
        
        for (int i = 0; i < iterations; i++)
        {
            // Create fresh character and gear for each iteration
            var character = await CreateTestCharacterAsync();
            var (definition, gear) = CreateTestGear(character.Id, EquipmentSlot.Chest);
            
            await _context.Set<GearDefinition>().AddAsync(definition);
            await _context.Set<GearInstance>().AddAsync(gear);
            await _context.SaveChangesAsync();
            
            // Measure equip
            var sw = Stopwatch.StartNew();
            await _equipmentService.EquipAsync(character.Id, gear.Id);
            equipTimes[i] = sw.ElapsedMilliseconds;
            
            // Measure stats calculation
            sw.Restart();
            await _statsService.CalculateEquipmentStatsAsync(character.Id);
            statsCalcTimes[i] = sw.ElapsedMilliseconds;
            
            // Measure unequip
            sw.Restart();
            await _equipmentService.UnequipAsync(character.Id, EquipmentSlot.Chest);
            unequipTimes[i] = sw.ElapsedMilliseconds;
        }
        
        // Calculate statistics
        var avgEquip = equipTimes.Average();
        var avgUnequip = unequipTimes.Average();
        var avgStatsCalc = statsCalcTimes.Average();
        
        var maxEquip = equipTimes.Max();
        var maxUnequip = unequipTimes.Max();
        var maxStatsCalc = statsCalcTimes.Max();
        
        // Report results
        _output.WriteLine($"\n📊 性能基准测试结果 ({iterations} 次迭代):");
        _output.WriteLine($"  装备操作 - 平均: {avgEquip:F1}ms, 最大: {maxEquip}ms");
        _output.WriteLine($"  卸下操作 - 平均: {avgUnequip:F1}ms, 最大: {maxUnequip}ms");
        _output.WriteLine($"  属性计算 - 平均: {avgStatsCalc:F1}ms, 最大: {maxStatsCalc}ms");
        
        // Assert - average should be well under limits
        Assert.True(avgEquip < 100, $"装备操作平均耗时过长: {avgEquip}ms");
        Assert.True(avgUnequip < 100, $"卸下操作平均耗时过长: {avgUnequip}ms");
        Assert.True(avgStatsCalc < 50, $"属性计算平均耗时过长: {avgStatsCalc}ms");
    }

    #region Helper Methods

    private async Task<Character> CreateTestCharacterAsync()
    {
        var character = new Character
        {
            Id = Guid.NewGuid(),
            Name = $"PerfTest_{Guid.NewGuid().ToString()[..8]}",
            Profession = Profession.Warrior,
            Level = 10,
            Strength = 20,
            Agility = 15,
            Intellect = 10,
            Stamina = 25
        };

        await _context.Characters.AddAsync(character);
        await _context.SaveChangesAsync();
        return character;
    }

    private (GearDefinition, GearInstance) CreateTestGear(Guid characterId, EquipmentSlot slot)
    {
        var definition = new GearDefinition
        {
            Id = $"test_{slot}_{Guid.NewGuid().ToString()[..8]}",
            Name = $"Test {slot} Gear",
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
            Rarity = Rarity.Rare,
            TierLevel = 1,
            ItemLevel = 15,
            RolledStats = new()
            {
                { StatType.Armor, 50 },
                { StatType.Strength, 10 }
            },
            Definition = definition
        };

        return (definition, gear);
    }

    private (GearDefinition, GearInstance) CreateTestShield(Guid characterId)
    {
        var definition = new GearDefinition
        {
            Id = $"test_shield_{Guid.NewGuid().ToString()[..8]}",
            Name = "Test Shield",
            Slot = EquipmentSlot.OffHand,
            ArmorType = ArmorType.None,
            WeaponType = WeaponType.Shield,
            RequiredLevel = 1
        };

        var shield = new GearInstance
        {
            Id = Guid.NewGuid(),
            DefinitionId = definition.Id,
            CharacterId = characterId,
            Rarity = Rarity.Epic,
            TierLevel = 2,
            ItemLevel = 25,
            RolledStats = new()
            {
                { StatType.Armor, 100 },
                { StatType.Stamina, 15 }
            },
            Definition = definition
        };

        return (definition, shield);
    }

    #endregion
}
