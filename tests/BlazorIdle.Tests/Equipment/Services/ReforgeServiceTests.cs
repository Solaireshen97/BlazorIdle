using BlazorIdle.Server.Domain.Equipment.Models;
using BlazorIdle.Server.Domain.Equipment.Services;
using BlazorIdle.Server.Domain.Equipment.ValueObjects;
using BlazorIdle.Server.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BlazorIdle.Tests.Equipment.Services;

/// <summary>
/// 装备重铸服务测试
/// </summary>
public class ReforgeServiceTests : IDisposable
{
    private readonly GameDbContext _context;
    private readonly ReforgeService _service;

    public ReforgeServiceTests()
    {
        var options = new DbContextOptionsBuilder<GameDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new GameDbContext(options);
        _service = new ReforgeService(_context);
    }

    [Fact]
    public async Task ReforgeAsync_ValidGear_ShouldUpgradeTier()
    {
        // Arrange
        var characterId = Guid.NewGuid();
        var (definition, gear) = CreateTestGear(characterId, 1);
        var originalArmorValue = gear.RolledStats[StatType.Armor];

        await _context.Set<GearDefinition>().AddAsync(definition);
        await _context.Set<GearInstance>().AddAsync(gear);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.ReforgeAsync(characterId, gear.Id);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.ReforgedGear);
        Assert.Equal(2, result.ReforgedGear.TierLevel);

        // 验证属性增加了
        var newArmorValue = result.ReforgedGear.RolledStats[StatType.Armor];
        Assert.True(newArmorValue > originalArmorValue);
    }

    [Fact]
    public async Task ReforgeAsync_MaxTierGear_ShouldFail()
    {
        // Arrange
        var characterId = Guid.NewGuid();
        var (definition, gear) = CreateTestGear(characterId, 3); // T3 已是最高品级

        await _context.Set<GearDefinition>().AddAsync(definition);
        await _context.Set<GearInstance>().AddAsync(gear);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.ReforgeAsync(characterId, gear.Id);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("最高品级", result.Message);
    }

    [Fact]
    public async Task ReforgeAsync_NonExistentGear_ShouldFail()
    {
        // Arrange
        var characterId = Guid.NewGuid();
        var nonExistentGearId = Guid.NewGuid();

        // Act
        var result = await _service.ReforgeAsync(characterId, nonExistentGearId);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("不存在", result.Message);
    }

    [Fact]
    public async Task ReforgeAsync_GearOwnedByOtherCharacter_ShouldFail()
    {
        // Arrange
        var characterId1 = Guid.NewGuid();
        var characterId2 = Guid.NewGuid();
        var (definition, gear) = CreateTestGear(characterId1, 1);

        await _context.Set<GearDefinition>().AddAsync(definition);
        await _context.Set<GearInstance>().AddAsync(gear);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.ReforgeAsync(characterId2, gear.Id);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("不属于", result.Message);
    }

    [Theory]
    [InlineData(1, 2, 1.25)] // T1->T2: 0.8->1.0, ratio = 1.25
    [InlineData(2, 3, 1.2)]  // T2->T3: 1.0->1.2, ratio = 1.2
    public async Task ReforgeAsync_ShouldApplyCorrectMultiplier(int fromTier, int toTier, double expectedRatio)
    {
        // Arrange
        var characterId = Guid.NewGuid();
        var (definition, gear) = CreateTestGear(characterId, fromTier);
        var originalValue = gear.RolledStats[StatType.Armor];

        await _context.Set<GearDefinition>().AddAsync(definition);
        await _context.Set<GearInstance>().AddAsync(gear);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.ReforgeAsync(characterId, gear.Id);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(toTier, result.ReforgedGear!.TierLevel); // 验证品级确实提升到toTier
        var newValue = result.ReforgedGear!.RolledStats[StatType.Armor];
        var actualRatio = newValue / originalValue;

        // 允许浮点误差
        Assert.InRange(actualRatio, expectedRatio - 0.01, expectedRatio + 0.01);
    }

    [Fact]
    public async Task PreviewReforgeCostAsync_ShouldShowCostAndPreview()
    {
        // Arrange
        var characterId = Guid.NewGuid();
        var (definition, gear) = CreateTestGear(characterId, 1);

        await _context.Set<GearDefinition>().AddAsync(definition);
        await _context.Set<GearInstance>().AddAsync(gear);
        await _context.SaveChangesAsync();

        // Act
        var preview = await _service.PreviewReforgeCostAsync(gear.Id);

        // Assert
        Assert.True(preview.CanReforge);
        Assert.Equal(1, preview.CurrentTier);
        Assert.Equal(2, preview.NextTier);
        Assert.NotEmpty(preview.Cost);
        Assert.NotEmpty(preview.CurrentStats);
        Assert.NotEmpty(preview.PreviewStats);

        // 预览属性应该大于当前属性
        var currentArmor = preview.CurrentStats[StatType.Armor];
        var previewArmor = preview.PreviewStats[StatType.Armor];
        Assert.True(previewArmor > currentArmor);
    }

    [Fact]
    public async Task PreviewReforgeCostAsync_MaxTierGear_ShouldIndicateCannotReforge()
    {
        // Arrange
        var characterId = Guid.NewGuid();
        var (definition, gear) = CreateTestGear(characterId, 3);

        await _context.Set<GearDefinition>().AddAsync(definition);
        await _context.Set<GearInstance>().AddAsync(gear);
        await _context.SaveChangesAsync();

        // Act
        var preview = await _service.PreviewReforgeCostAsync(gear.Id);

        // Assert
        Assert.False(preview.CanReforge);
        Assert.Contains("最高品级", preview.Message);
    }

    [Fact]
    public async Task ReforgeAsync_ShouldRecalculateScore()
    {
        // Arrange
        var characterId = Guid.NewGuid();
        var (definition, gear) = CreateTestGear(characterId, 1);

        await _context.Set<GearDefinition>().AddAsync(definition);
        await _context.Set<GearInstance>().AddAsync(gear);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.ReforgeAsync(characterId, gear.Id);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.ReforgedGear);
        // Quality score should be recalculated after reforge
        Assert.True(result.ReforgedGear.QualityScore > 0, "Quality score should be recalculated");
    }

    private (GearDefinition, GearInstance) CreateTestGear(Guid characterId, int tierLevel)
    {
        var definition = new GearDefinition
        {
            Id = "test_reforge_gear",
            Name = "测试重铸装备",
            Icon = "🛡️",
            Slot = EquipmentSlot.Chest,
            ArmorType = ArmorType.Plate,
            WeaponType = WeaponType.None,
            RequiredLevel = 20,
            BaseStats = new Dictionary<StatType, StatRange>
            {
                { StatType.Armor, new StatRange(100, 200) }
            }
        };

        // 根据品级应用系数
        var tierMultiplier = tierLevel switch
        {
            1 => 0.8,
            2 => 1.0,
            3 => 1.2,
            _ => 1.0
        };

        var gear = new GearInstance
        {
            Id = Guid.NewGuid(),
            DefinitionId = definition.Id,
            CharacterId = characterId,
            Rarity = Rarity.Epic,
            TierLevel = tierLevel,
            ItemLevel = 20,
            RolledStats = new Dictionary<StatType, double>
            {
                { StatType.Armor, 150 * tierMultiplier }
            },
            Affixes = new List<AffixInstance>
            {
                new AffixInstance("affix_str", StatType.Strength, ModifierType.Flat, 20)
            },
            QualityScore = (int)(200 * tierMultiplier),
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
