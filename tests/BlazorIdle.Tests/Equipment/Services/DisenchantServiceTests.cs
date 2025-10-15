using BlazorIdle.Server.Domain.Equipment.Models;
using BlazorIdle.Server.Domain.Equipment.Services;
using BlazorIdle.Server.Domain.Equipment.ValueObjects;
using BlazorIdle.Server.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BlazorIdle.Tests.Equipment.Services;

/// <summary>
/// 装备分解服务测试
/// </summary>
public class DisenchantServiceTests : IDisposable
{
    private readonly GameDbContext _context;
    private readonly DisenchantService _service;

    public DisenchantServiceTests()
    {
        var options = new DbContextOptionsBuilder<GameDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new GameDbContext(options);
        var logger = NullLogger<DisenchantService>.Instance;
        _service = new DisenchantService(_context, logger);
    }

    [Fact]
    public async Task DisenchantAsync_ValidGear_ShouldDisenchantSuccessfully()
    {
        // Arrange
        var characterId = Guid.NewGuid();
        var (definition, gear) = CreateTestGear(characterId, Rarity.Rare);

        await _context.Set<GearDefinition>().AddAsync(definition);
        await _context.Set<GearInstance>().AddAsync(gear);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.DisenchantAsync(characterId, gear.Id);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotEmpty(result.Materials);

        // 验证装备已被删除
        var deletedGear = await _context.Set<GearInstance>().FindAsync(gear.Id);
        Assert.Null(deletedGear);
    }

    [Fact]
    public async Task DisenchantAsync_NonExistentGear_ShouldFail()
    {
        // Arrange
        var characterId = Guid.NewGuid();
        var nonExistentGearId = Guid.NewGuid();

        // Act
        var result = await _service.DisenchantAsync(characterId, nonExistentGearId);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("不存在", result.Message);
    }

    [Fact]
    public async Task DisenchantAsync_GearOwnedByOtherCharacter_ShouldFail()
    {
        // Arrange
        var characterId1 = Guid.NewGuid();
        var characterId2 = Guid.NewGuid();
        var (definition, gear) = CreateTestGear(characterId1, Rarity.Rare);

        await _context.Set<GearDefinition>().AddAsync(definition);
        await _context.Set<GearInstance>().AddAsync(gear);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.DisenchantAsync(characterId2, gear.Id);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("不属于", result.Message);
    }

    [Fact]
    public async Task DisenchantAsync_EquippedGear_ShouldFail()
    {
        // Arrange
        var characterId = Guid.NewGuid();
        var (definition, gear) = CreateTestGear(characterId, Rarity.Rare);
        gear.IsEquipped = true;
        gear.SlotType = EquipmentSlot.Head;

        await _context.Set<GearDefinition>().AddAsync(definition);
        await _context.Set<GearInstance>().AddAsync(gear);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.DisenchantAsync(characterId, gear.Id);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("卸下", result.Message);
    }

    [Theory]
    [InlineData(Rarity.Common)]
    [InlineData(Rarity.Rare)]
    [InlineData(Rarity.Epic)]
    [InlineData(Rarity.Legendary)]
    public async Task DisenchantAsync_DifferentRarities_ShouldProduceDifferentMaterials(Rarity rarity)
    {
        // Arrange
        var characterId = Guid.NewGuid();
        var (definition, gear) = CreateTestGear(characterId, rarity);

        await _context.Set<GearDefinition>().AddAsync(definition);
        await _context.Set<GearInstance>().AddAsync(gear);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.DisenchantAsync(characterId, gear.Id);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotEmpty(result.Materials);

        // 稀有品质以上应该产出稀有材料
        if (rarity != Rarity.Common)
        {
            Assert.Contains(result.Materials, m => m.Key.Contains("essence"));
        }
    }

    [Fact]
    public async Task PreviewDisenchantAsync_ShouldReturnMaterialsWithoutRemoving()
    {
        // Arrange
        var characterId = Guid.NewGuid();
        var (definition, gear) = CreateTestGear(characterId, Rarity.Epic);

        await _context.Set<GearDefinition>().AddAsync(definition);
        await _context.Set<GearInstance>().AddAsync(gear);
        await _context.SaveChangesAsync();

        // Act
        var materials = await _service.PreviewDisenchantAsync(gear.Id);

        // Assert
        Assert.NotEmpty(materials);

        // 验证装备未被删除
        var stillExists = await _context.Set<GearInstance>().FindAsync(gear.Id);
        Assert.NotNull(stillExists);
    }

    [Fact]
    public async Task DisenchantBatchAsync_ShouldDisenchantMultipleGear()
    {
        // Arrange
        var characterId = Guid.NewGuid();
        var gearIds = new List<Guid>();

        for (int i = 0; i < 3; i++)
        {
            var (definition, gear) = CreateTestGear(characterId, Rarity.Rare);
            definition.Id = $"test_gear_{i}";

            await _context.Set<GearDefinition>().AddAsync(definition);
            await _context.Set<GearInstance>().AddAsync(gear);
            gearIds.Add(gear.Id);
        }

        await _context.SaveChangesAsync();

        // Act
        var result = await _service.DisenchantBatchAsync(characterId, gearIds);

        // Assert
        Assert.Equal(3, result.SuccessCount);
        Assert.Equal(0, result.FailCount);
        Assert.NotEmpty(result.TotalMaterials);
    }

    private (GearDefinition, GearInstance) CreateTestGear(Guid characterId, Rarity rarity)
    {
        var definition = new GearDefinition
        {
            Id = "test_gear",
            Name = "测试装备",
            Icon = "⚔️",
            Slot = EquipmentSlot.Head,
            ArmorType = ArmorType.Plate,
            WeaponType = WeaponType.None,
            RequiredLevel = 10,
            BaseStats = new Dictionary<StatType, StatRange>
            {
                { StatType.Armor, new StatRange(50, 100) }
            }
        };

        var gear = new GearInstance
        {
            Id = Guid.NewGuid(),
            DefinitionId = definition.Id,
            CharacterId = characterId,
            Rarity = rarity,
            TierLevel = 1,
            ItemLevel = 10,
            RolledStats = new Dictionary<StatType, double>
            {
                { StatType.Armor, 75 }
            },
            Affixes = new List<AffixInstance>(),
            QualityScore = 100,
            IsEquipped = false,
            IsBound = false,
            Definition = definition
        };

        return (definition, gear);
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
