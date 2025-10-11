using BlazorIdle.Server.Domain.Equipment.Models;
using BlazorIdle.Server.Domain.Equipment.Services;
using BlazorIdle.Server.Domain.Equipment.ValueObjects;
using BlazorIdle.Server.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BlazorIdle.Tests.Equipment.Services;

/// <summary>
/// 测试武器信息集成功能（Phase 5）
/// </summary>
public class WeaponInfoIntegrationTests : IDisposable
{
    private readonly GameDbContext _context;
    private readonly BlazorIdle.Server.Infrastructure.Persistence.Repositories.GearInstanceRepository _gearRepo;
    private readonly EquipmentService _equipmentService;
    private readonly AttackSpeedCalculator _attackSpeedCalculator;
    private readonly StatsAggregationService _statsAggregationService;
    private readonly EquipmentStatsIntegration _service;

    public WeaponInfoIntegrationTests()
    {
        var options = new DbContextOptionsBuilder<GameDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new GameDbContext(options);
        _gearRepo = new BlazorIdle.Server.Infrastructure.Persistence.Repositories.GearInstanceRepository(_context);
        _equipmentService = new EquipmentService(_context);
        _attackSpeedCalculator = new AttackSpeedCalculator();
        var armorCalculator = new ArmorCalculator();
        var blockCalculator = new BlockCalculator();
        _statsAggregationService = new StatsAggregationService(_equipmentService, armorCalculator, blockCalculator);
        _service = new EquipmentStatsIntegration(
            _statsAggregationService,
            _gearRepo,
            _attackSpeedCalculator);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    [Fact]
    public async Task GetEquippedWeaponInfoAsync_NoWeapon_ReturnsDefault()
    {
        // Arrange
        var characterId = Guid.NewGuid();

        // Act
        var weaponInfo = await _service.GetEquippedWeaponInfoAsync(characterId);

        // Assert
        Assert.Equal(WeaponType.None, weaponInfo.WeaponType);
        Assert.Equal(2.5, weaponInfo.BaseAttackSpeed);
        Assert.False(weaponInfo.IsTwoHanded);
        Assert.False(weaponInfo.IsDualWielding);
    }

    [Fact]
    public async Task GetEquippedWeaponInfoAsync_SingleHandWeapon_ReturnsCorrectInfo()
    {
        // Arrange
        var characterId = Guid.NewGuid();
        var (swordDef, sword) = await CreateAndEquipWeapon(characterId, WeaponType.Sword, EquipmentSlot.MainHand);

        // Act
        var weaponInfo = await _service.GetEquippedWeaponInfoAsync(characterId);

        // Assert
        Assert.Equal(WeaponType.Sword, weaponInfo.WeaponType);
        Assert.Equal(2.4, weaponInfo.BaseAttackSpeed); // Sword has 2.4s base speed
        Assert.Equal(0.42, weaponInfo.DPSCoefficient, 2); // 保留2位小数
        Assert.False(weaponInfo.IsTwoHanded);
        Assert.False(weaponInfo.IsDualWielding);
    }

    private async Task<(GearDefinition, GearInstance)> CreateAndEquipWeapon(
        Guid characterId, 
        WeaponType weaponType, 
        EquipmentSlot slot)
    {
        var def = new GearDefinition
        {
            Id = $"weapon_{weaponType}_{Guid.NewGuid()}",
            Name = $"Test {weaponType}",
            WeaponType = weaponType,
            Slot = slot,
            BaseStats = new Dictionary<StatType, StatRange>(),
            AllowedAffixPool = new List<string>(),
            RarityWeights = new Dictionary<Rarity, double>()
        };

        var instance = new GearInstance
        {
            Id = Guid.NewGuid(),
            DefinitionId = def.Id,
            CharacterId = characterId,
            SlotType = slot,
            IsEquipped = true,
            Rarity = Rarity.Common,
            TierLevel = 1,
            ItemLevel = 1,
            RolledStats = new Dictionary<StatType, double>(),
            Affixes = new List<AffixInstance>()
        };

        _context.GearDefinitions.Add(def);
        _context.GearInstances.Add(instance);
        await _context.SaveChangesAsync();

        return (def, instance);
    }

    [Fact]
    public async Task GetEquippedWeaponInfoAsync_TwoHandWeapon_ReturnsCorrectInfo()
    {
        // Arrange
        var characterId = Guid.NewGuid();
        await CreateAndEquipWeapon(characterId, WeaponType.TwoHandSword, EquipmentSlot.MainHand);

        // Act
        var weaponInfo = await _service.GetEquippedWeaponInfoAsync(characterId);

        // Assert
        Assert.Equal(WeaponType.TwoHandSword, weaponInfo.WeaponType);
        Assert.Equal(3.4, weaponInfo.BaseAttackSpeed); // TwoHandSword has 3.4s base speed
        Assert.Equal(0.50, weaponInfo.DPSCoefficient, 2);
        Assert.True(weaponInfo.IsTwoHanded);
        Assert.False(weaponInfo.IsDualWielding);
    }

    [Theory]
    [InlineData(WeaponType.Dagger, 1.8)]
    [InlineData(WeaponType.Sword, 2.4)]
    [InlineData(WeaponType.Axe, 2.6)]
    [InlineData(WeaponType.TwoHandSword, 3.4)]
    [InlineData(WeaponType.Staff, 3.0)]
    public async Task GetEquippedWeaponInfoAsync_DifferentWeaponTypes_ReturnsCorrectSpeed(
        WeaponType weaponType, 
        double expectedSpeed)
    {
        // Arrange
        var characterId = Guid.NewGuid();
        await CreateAndEquipWeapon(characterId, weaponType, EquipmentSlot.MainHand);

        // Act
        var weaponInfo = await _service.GetEquippedWeaponInfoAsync(characterId);

        // Assert
        Assert.Equal(weaponType, weaponInfo.WeaponType);
        Assert.Equal(expectedSpeed, weaponInfo.BaseAttackSpeed);
    }
}
