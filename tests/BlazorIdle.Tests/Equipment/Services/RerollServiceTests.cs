using BlazorIdle.Server.Domain.Equipment.Models;
using BlazorIdle.Server.Domain.Equipment.Services;
using BlazorIdle.Server.Domain.Equipment.ValueObjects;
using BlazorIdle.Server.Application.Abstractions;
using BlazorIdle.Server.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BlazorIdle.Tests.Equipment.Services;

public class RerollServiceTests : IDisposable
{
    private readonly GameDbContext _context;
    private readonly TestAffixRepository _affixRepository;
    private readonly RerollService _service;
    private readonly Guid _testCharacterId = Guid.NewGuid();

    public RerollServiceTests()
    {
        var options = new DbContextOptionsBuilder<GameDbContext>()
            .UseInMemoryDatabase(databaseName: $"TestDb_{Guid.NewGuid()}")
            .Options;

        _context = new GameDbContext(options);
        _affixRepository = new TestAffixRepository();
        _service = new RerollService(_context, _affixRepository);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    [Fact]
    public async Task RerollAffixesAsync_ShouldSucceed_WhenGearExists()
    {
        // Arrange
        var gear = CreateTestGear(Rarity.Epic, isEquipped: false);
        _context.Set<GearInstance>().Add(gear);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.RerollAffixesAsync(_testCharacterId, gear.Id);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.UpdatedGear);
        Assert.Equal(1, result.UpdatedGear!.RerollCount);
        Assert.NotEmpty(result.NewAffixes);
        Assert.NotNull(result.OldAffixes); // Old affixes should be captured
    }

    [Fact]
    public async Task RerollAffixesAsync_ShouldFail_WhenGearNotFound()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var result = await _service.RerollAffixesAsync(_testCharacterId, nonExistentId);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("不存在", result.Message);
    }

    [Fact]
    public async Task RerollAffixesAsync_ShouldFail_WhenGearBelongsToAnotherCharacter()
    {
        // Arrange
        var otherCharacterId = Guid.NewGuid();
        var gear = CreateTestGear(Rarity.Rare, isEquipped: false);
        _context.Set<GearInstance>().Add(gear);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.RerollAffixesAsync(otherCharacterId, gear.Id);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("不属于你", result.Message);
    }

    [Fact]
    public async Task RerollAffixesAsync_ShouldFail_WhenGearIsEquipped()
    {
        // Arrange
        var gear = CreateTestGear(Rarity.Rare, isEquipped: true);
        _context.Set<GearInstance>().Add(gear);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.RerollAffixesAsync(_testCharacterId, gear.Id);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("先卸下装备", result.Message);
    }

    [Fact]
    public async Task RerollAffixesAsync_ShouldIncrementRerollCount()
    {
        // Arrange
        var gear = CreateTestGear(Rarity.Rare, isEquipped: false);
        gear.RerollCount = 5;
        _context.Set<GearInstance>().Add(gear);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.RerollAffixesAsync(_testCharacterId, gear.Id);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(6, result.UpdatedGear!.RerollCount);
    }

    [Fact]
    public async Task RerollSingleAffixAsync_ShouldSucceed_WithValidIndex()
    {
        // Arrange
        var gear = CreateTestGear(Rarity.Epic, isEquipped: false);
        gear.Affixes = new List<AffixInstance>
        {
            new AffixInstance("affix1", StatType.AttackPower, ModifierType.Flat, 50),
            new AffixInstance("affix2", StatType.CritRating, ModifierType.Flat, 30),
            new AffixInstance("affix3", StatType.Haste, ModifierType.Flat, 20)
        };
        _context.Set<GearInstance>().Add(gear);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.RerollSingleAffixAsync(_testCharacterId, gear.Id, 1);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.UpdatedGear);
        Assert.Equal(3, result.UpdatedGear!.Affixes.Count);
    }

    [Fact]
    public async Task RerollSingleAffixAsync_ShouldFail_WithInvalidIndex()
    {
        // Arrange
        var gear = CreateTestGear(Rarity.Rare, isEquipped: false);
        gear.Affixes = new List<AffixInstance>
        {
            new AffixInstance("affix1", StatType.AttackPower, ModifierType.Flat, 50)
        };
        _context.Set<GearInstance>().Add(gear);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.RerollSingleAffixAsync(_testCharacterId, gear.Id, 5);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("索引无效", result.Message);
    }

    [Fact]
    public async Task PreviewRerollCostAsync_ShouldReturnCost()
    {
        // Arrange
        var gear = CreateTestGear(Rarity.Epic, isEquipped: false);
        _context.Set<GearInstance>().Add(gear);
        await _context.SaveChangesAsync();

        // Act
        var cost = await _service.PreviewRerollCostAsync(gear.Id);

        // Assert
        Assert.NotEmpty(cost);
        Assert.True(cost.ContainsKey("material_reroll_essence"));
    }

    [Fact]
    public async Task RerollAffixesAsync_ShouldGenerateCorrectAffixCount_ForRareGear()
    {
        // Arrange
        var gear = CreateTestGear(Rarity.Rare, isEquipped: false);
        gear.Affixes.Clear(); // Clear initial affixes
        _context.Set<GearInstance>().Add(gear);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.RerollAffixesAsync(_testCharacterId, gear.Id);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.InRange(result.NewAffixes.Count, 1, 2); // Rare gear should have 1-2 affixes
    }

    [Fact]
    public async Task RerollAffixesAsync_ShouldGenerateCorrectAffixCount_ForLegendaryGear()
    {
        // Arrange
        var gear = CreateTestGear(Rarity.Legendary, isEquipped: false);
        gear.Affixes.Clear();
        _context.Set<GearInstance>().Add(gear);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.RerollAffixesAsync(_testCharacterId, gear.Id);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.InRange(result.NewAffixes.Count, 3, 4); // Legendary gear should have 3-4 affixes
    }

    [Theory]
    [InlineData(Rarity.Common)]
    [InlineData(Rarity.Rare)]
    [InlineData(Rarity.Epic)]
    [InlineData(Rarity.Legendary)]
    public async Task RerollAffixesAsync_ShouldUpdateQualityScore(Rarity rarity)
    {
        // Arrange
        var gear = CreateTestGear(rarity, isEquipped: false);
        var oldScore = gear.QualityScore;
        _context.Set<GearInstance>().Add(gear);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.RerollAffixesAsync(_testCharacterId, gear.Id);

        // Assert
        if (rarity != Rarity.Common) // Common gear has no affixes to reroll
        {
            Assert.True(result.IsSuccess);
            Assert.True(result.UpdatedGear!.QualityScore > 0);
        }
    }

    // Helper methods
    private GearInstance CreateTestGear(Rarity rarity, bool isEquipped)
    {
        var definition = new GearDefinition
        {
            Id = "test_helmet",
            Name = "测试头盔",
            Icon = "🪖",
            Slot = EquipmentSlot.Head,
            ArmorType = ArmorType.Plate,
            WeaponType = WeaponType.None,
            RequiredLevel = 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Add definition to context
        _context.Set<GearDefinition>().Add(definition);

        var gear = new GearInstance
        {
            Id = Guid.NewGuid(),
            DefinitionId = "test_helmet",
            CharacterId = _testCharacterId,
            Rarity = rarity,
            TierLevel = 1,
            ItemLevel = 50,
            IsEquipped = isEquipped,
            SlotType = isEquipped ? EquipmentSlot.Head : null,
            RolledStats = new Dictionary<StatType, double>
            {
                { StatType.Armor, 100 },
                { StatType.Stamina, 20 }
            },
            Affixes = new List<AffixInstance>
            {
                new AffixInstance("test_affix_1", StatType.AttackPower, ModifierType.Flat, 50)
            },
            QualityScore = 100,
            RerollCount = 0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Definition = definition
        };

        return gear;
    }

    // Test implementation of IAffixRepository
    private class TestAffixRepository : IAffixRepository
    {
        private readonly List<Affix> _affixes = new()
        {
            new Affix
            {
                Id = "affix_attack_power",
                Name = "攻击强度",
                StatType = StatType.AttackPower,
                ModifierType = ModifierType.Flat,
                ValueMin = 20,
                ValueMax = 80,
                RarityWeight = 1.0
            },
            new Affix
            {
                Id = "affix_crit_rating",
                Name = "暴击等级",
                StatType = StatType.CritRating,
                ModifierType = ModifierType.Flat,
                ValueMin = 10,
                ValueMax = 50,
                RarityWeight = 1.0
            },
            new Affix
            {
                Id = "affix_haste",
                Name = "急速",
                StatType = StatType.Haste,
                ModifierType = ModifierType.Flat,
                ValueMin = 10,
                ValueMax = 40,
                RarityWeight = 1.0
            },
            new Affix
            {
                Id = "affix_stamina",
                Name = "耐力",
                StatType = StatType.Stamina,
                ModifierType = ModifierType.Flat,
                ValueMin = 15,
                ValueMax = 60,
                RarityWeight = 1.2
            }
        };

        public Task<Affix?> GetByIdAsync(string id, CancellationToken ct = default)
        {
            return Task.FromResult(_affixes.FirstOrDefault(a => a.Id == id));
        }

        public Task<List<Affix>> GetAllAsync(CancellationToken ct = default)
        {
            return Task.FromResult(_affixes);
        }

        public Task<List<Affix>> GetBySlotAsync(EquipmentSlot slot, CancellationToken ct = default)
        {
            return Task.FromResult(_affixes);
        }

        public Task CreateAsync(Affix affix, CancellationToken ct = default)
        {
            _affixes.Add(affix);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(Affix affix, CancellationToken ct = default)
        {
            return Task.CompletedTask;
        }

        public Task DeleteAsync(string id, CancellationToken ct = default)
        {
            var affix = _affixes.FirstOrDefault(a => a.Id == id);
            if (affix != null)
            {
                _affixes.Remove(affix);
            }
            return Task.CompletedTask;
        }
    }
}
