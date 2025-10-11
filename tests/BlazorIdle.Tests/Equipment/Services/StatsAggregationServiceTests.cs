using BlazorIdle.Server.Domain.Equipment.Models;
using BlazorIdle.Server.Domain.Equipment.Services;
using BlazorIdle.Server.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
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
        _equipmentService = new EquipmentService(_context);
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

    [Fact]
    public async Task GetEquippedWeaponTypeAsync_NoWeapon_ShouldReturnNone()
    {
        // Arrange
        var characterId = Guid.NewGuid();

        // Act
        var weaponInfo = await _service.GetEquippedWeaponTypeAsync(characterId);

        // Assert
        Assert.NotNull(weaponInfo);
        Assert.Equal(WeaponType.None, weaponInfo.MainHandType);
        Assert.Equal(WeaponType.None, weaponInfo.OffHandType);
        Assert.False(weaponInfo.IsTwoHanded);
        Assert.False(weaponInfo.IsDualWielding);
    }

    [Fact]
    public async Task GetEquippedWeaponTypeAsync_TwoHandWeapon_ShouldReturnTwoHanded()
    {
        // Arrange
        var characterId = Guid.NewGuid();
        var (weaponDef, weapon) = CreateTestWeapon(characterId, EquipmentSlot.TwoHand, WeaponType.TwoHandSword);
        
        weapon.IsEquipped = true;
        weapon.SlotType = EquipmentSlot.TwoHand;
        
        await _context.Set<GearDefinition>().AddAsync(weaponDef);
        await _context.Set<GearInstance>().AddAsync(weapon);
        await _context.SaveChangesAsync();

        // Act
        var weaponInfo = await _service.GetEquippedWeaponTypeAsync(characterId);

        // Assert
        Assert.NotNull(weaponInfo);
        Assert.Equal(WeaponType.TwoHandSword, weaponInfo.MainHandType);
        Assert.Equal(WeaponType.None, weaponInfo.OffHandType);
        Assert.True(weaponInfo.IsTwoHanded);
        Assert.False(weaponInfo.IsDualWielding);
    }

    [Fact]
    public async Task GetEquippedWeaponTypeAsync_DualWield_ShouldReturnDualWielding()
    {
        // Arrange
        var characterId = Guid.NewGuid();
        var (mainHandDef, mainHand) = CreateTestWeapon(characterId, EquipmentSlot.MainHand, WeaponType.Sword);
        var (offHandDef, offHand) = CreateTestWeapon(characterId, EquipmentSlot.OffHand, WeaponType.Dagger);
        
        mainHand.IsEquipped = true;
        mainHand.SlotType = EquipmentSlot.MainHand;
        
        offHand.IsEquipped = true;
        offHand.SlotType = EquipmentSlot.OffHand;
        
        await _context.Set<GearDefinition>().AddRangeAsync(mainHandDef, offHandDef);
        await _context.Set<GearInstance>().AddRangeAsync(mainHand, offHand);
        await _context.SaveChangesAsync();

        // Act
        var weaponInfo = await _service.GetEquippedWeaponTypeAsync(characterId);

        // Assert
        Assert.NotNull(weaponInfo);
        Assert.Equal(WeaponType.Sword, weaponInfo.MainHandType);
        Assert.Equal(WeaponType.Dagger, weaponInfo.OffHandType);
        Assert.False(weaponInfo.IsTwoHanded);
        Assert.True(weaponInfo.IsDualWielding);
    }

    [Fact]
    public async Task GetEquippedWeaponTypeAsync_WeaponAndShield_ShouldNotBeDualWielding()
    {
        // Arrange
        var characterId = Guid.NewGuid();
        var (weaponDef, weapon) = CreateTestWeapon(characterId, EquipmentSlot.MainHand, WeaponType.Sword);
        var (shieldDef, shield) = CreateTestWeapon(characterId, EquipmentSlot.OffHand, WeaponType.Shield);
        
        weapon.IsEquipped = true;
        weapon.SlotType = EquipmentSlot.MainHand;
        
        shield.IsEquipped = true;
        shield.SlotType = EquipmentSlot.OffHand;
        
        await _context.Set<GearDefinition>().AddRangeAsync(weaponDef, shieldDef);
        await _context.Set<GearInstance>().AddRangeAsync(weapon, shield);
        await _context.SaveChangesAsync();

        // Act
        var weaponInfo = await _service.GetEquippedWeaponTypeAsync(characterId);

        // Assert
        Assert.NotNull(weaponInfo);
        Assert.Equal(WeaponType.Sword, weaponInfo.MainHandType);
        Assert.Equal(WeaponType.Shield, weaponInfo.OffHandType);
        Assert.False(weaponInfo.IsTwoHanded);
        Assert.False(weaponInfo.IsDualWielding); // Shield doesn't count as dual wielding
    }

    private (GearDefinition, GearInstance) CreateTestWeapon(Guid characterId, EquipmentSlot slot, WeaponType weaponType)
    {
        var definition = new GearDefinition
        {
            Id = $"test_weapon_{slot}_{weaponType}_{Guid.NewGuid()}",
            Name = $"测试武器 {weaponType}",
            Slot = slot,
            ArmorType = ArmorType.None,
            WeaponType = weaponType,
            RequiredLevel = 1
        };

        var gear = new GearInstance
        {
            Id = Guid.NewGuid(),
            DefinitionId = definition.Id,
            CharacterId = characterId,
            Rarity = Rarity.Common,
            TierLevel = 1,
            ItemLevel = 50,
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
