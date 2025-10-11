using BlazorIdle.Server.Application.Abstractions;
using BlazorIdle.Server.Domain.Equipment.Models;
using BlazorIdle.Server.Domain.Equipment.Services;
using BlazorIdle.Server.Domain.Equipment.ValueObjects;
using Xunit;

namespace BlazorIdle.Tests.Equipment.Services;

/// <summary>
/// 装备生成服务测试
/// </summary>
public class GearGenerationServiceTests
{
    private readonly GearGenerationService _service;
    private readonly FakeAffixRepository _fakeAffixRepository;

    public GearGenerationServiceTests()
    {
        _fakeAffixRepository = new FakeAffixRepository();
        _service = new GearGenerationService(_fakeAffixRepository);
        
        // 设置默认的词条定义
        SetupDefaultAffixes();
    }

    private void SetupDefaultAffixes()
    {
        var testAffixes = new List<Affix>
        {
            new Affix { Id = "affix_str", Name = "力量", StatType = StatType.Strength, ModifierType = ModifierType.Flat, ValueMin = 5, ValueMax = 10 },
            new Affix { Id = "affix_agi", Name = "敏捷", StatType = StatType.Agility, ModifierType = ModifierType.Flat, ValueMin = 5, ValueMax = 10 },
            new Affix { Id = "affix_crit", Name = "暴击", StatType = StatType.CritChance, ModifierType = ModifierType.Percent, ValueMin = 1, ValueMax = 3 },
            new Affix { Id = "affix_1", Name = "攻击", StatType = StatType.AttackPower, ModifierType = ModifierType.Flat, ValueMin = 10, ValueMax = 20 },
            new Affix { Id = "affix_2", Name = "耐力", StatType = StatType.Stamina, ModifierType = ModifierType.Flat, ValueMin = 10, ValueMax = 20 },
            new Affix { Id = "affix_3", Name = "急速", StatType = StatType.Haste, ModifierType = ModifierType.Flat, ValueMin = 5, ValueMax = 15 },
            new Affix { Id = "affix_4", Name = "暴击等级", StatType = StatType.CritRating, ModifierType = ModifierType.Flat, ValueMin = 5, ValueMax = 15 }
        };

        foreach (var affix in testAffixes)
        {
            _fakeAffixRepository.AddAffix(affix);
        }
    }

    /// <summary>
    /// 用于测试的假Affix仓储实现
    /// </summary>
    private class FakeAffixRepository : IAffixRepository
    {
        private readonly Dictionary<string, Affix> _affixes = new();

        public void AddAffix(Affix affix)
        {
            _affixes[affix.Id] = affix;
        }

        public Task<Affix?> GetByIdAsync(string id, CancellationToken ct = default)
        {
            _affixes.TryGetValue(id, out var affix);
            return Task.FromResult(affix);
        }

        public Task<List<Affix>> GetAllAsync(CancellationToken ct = default)
        {
            return Task.FromResult(_affixes.Values.ToList());
        }

        public Task<List<Affix>> GetBySlotAsync(EquipmentSlot slot, CancellationToken ct = default)
        {
            return Task.FromResult(_affixes.Values
                .Where(a => a.AllowedSlots == null || a.AllowedSlots.Contains(slot))
                .ToList());
        }

        public Task CreateAsync(Affix affix, CancellationToken ct = default)
        {
            _affixes[affix.Id] = affix;
            return Task.CompletedTask;
        }

        public Task UpdateAsync(Affix affix, CancellationToken ct = default)
        {
            _affixes[affix.Id] = affix;
            return Task.CompletedTask;
        }

        public Task DeleteAsync(string id, CancellationToken ct = default)
        {
            _affixes.Remove(id);
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task GenerateAsync_ShouldCreateValidGearInstance()
    {
        // Arrange
        var definition = CreateTestGearDefinition();
        var characterLevel = 10;

        // Act
        var gear = await _service.GenerateAsync(definition, characterLevel);

        // Assert
        Assert.NotNull(gear);
        Assert.NotEqual(Guid.Empty, gear.Id);
        Assert.Equal(definition.Id, gear.DefinitionId);
        Assert.True(gear.ItemLevel >= characterLevel);
        Assert.InRange(gear.TierLevel, 1, 3);
        Assert.Contains(gear.Rarity, new[] { Rarity.Common, Rarity.Rare, Rarity.Epic, Rarity.Legendary });
        Assert.False(gear.IsEquipped);
        Assert.False(gear.IsBound);
    }

    [Fact]
    public async Task GenerateAsync_ShouldRollBaseStats()
    {
        // Arrange
        var definition = CreateTestGearDefinition();
        definition.BaseStats = new Dictionary<StatType, StatRange>
        {
            { StatType.Strength, new StatRange(10, 20) },
            { StatType.Stamina, new StatRange(15, 25) }
        };
        var characterLevel = 10;

        // Act
        var gear = await _service.GenerateAsync(definition, characterLevel);

        // Assert
        Assert.NotEmpty(gear.RolledStats);
        Assert.True(gear.RolledStats.ContainsKey(StatType.Strength));
        Assert.True(gear.RolledStats.ContainsKey(StatType.Stamina));
        Assert.InRange(gear.RolledStats[StatType.Strength], 8, 24); // 考虑品级系数
        Assert.InRange(gear.RolledStats[StatType.Stamina], 12, 30);
    }

    [Fact]
    public async Task GenerateAsync_CommonRarity_ShouldHaveNoAffixes()
    {
        // Arrange
        var definition = CreateTestGearDefinition();
        definition.RarityWeights = new Dictionary<Rarity, double>
        {
            { Rarity.Common, 100.0 } // 强制普通品质
        };
        var characterLevel = 10;

        // Act
        var gear = await _service.GenerateAsync(definition, characterLevel);

        // Assert
        Assert.Equal(Rarity.Common, gear.Rarity);
        Assert.Empty(gear.Affixes);
    }

    [Fact]
    public async Task GenerateAsync_RareRarity_ShouldHaveOneAffix()
    {
        // Arrange
        var definition = CreateTestGearDefinition();
        definition.RarityWeights = new Dictionary<Rarity, double>
        {
            { Rarity.Rare, 100.0 } // 强制稀有品质
        };
        definition.AllowedAffixPool = new List<string> { "affix_1", "affix_2", "affix_3" };
        var characterLevel = 10;

        // Act
        var gear = await _service.GenerateAsync(definition, characterLevel);

        // Assert
        Assert.Equal(Rarity.Rare, gear.Rarity);
        Assert.Single(gear.Affixes);
    }

    [Fact]
    public async Task GenerateAsync_EpicRarity_ShouldHaveTwoAffixes()
    {
        // Arrange
        var definition = CreateTestGearDefinition();
        definition.RarityWeights = new Dictionary<Rarity, double>
        {
            { Rarity.Epic, 100.0 } // 强制史诗品质
        };
        definition.AllowedAffixPool = new List<string> { "affix_1", "affix_2", "affix_3" };
        var characterLevel = 10;

        // Act
        var gear = await _service.GenerateAsync(definition, characterLevel);

        // Assert
        Assert.Equal(Rarity.Epic, gear.Rarity);
        Assert.Equal(2, gear.Affixes.Count);
    }

    [Fact]
    public async Task GenerateAsync_LegendaryRarity_ShouldHaveThreeAffixes()
    {
        // Arrange
        var definition = CreateTestGearDefinition();
        definition.RarityWeights = new Dictionary<Rarity, double>
        {
            { Rarity.Legendary, 100.0 } // 强制传说品质
        };
        definition.AllowedAffixPool = new List<string> { "affix_1", "affix_2", "affix_3", "affix_4" };
        var characterLevel = 10;

        // Act
        var gear = await _service.GenerateAsync(definition, characterLevel);

        // Assert
        Assert.Equal(Rarity.Legendary, gear.Rarity);
        Assert.Equal(3, gear.Affixes.Count);
    }

    [Fact]
    public async Task GenerateAsync_ShouldCalculateQualityScore()
    {
        // Arrange
        var definition = CreateTestGearDefinition();
        var characterLevel = 10;

        // Act
        var gear = await _service.GenerateAsync(definition, characterLevel);

        // Assert
        Assert.True(gear.QualityScore > 0);
    }

    [Fact]
    public async Task GenerateAsync_HigherCharacterLevel_ShouldProduceHigherItemLevel()
    {
        // Arrange
        var definition = CreateTestGearDefinition();
        definition.RarityWeights = new Dictionary<Rarity, double>
        {
            { Rarity.Common, 100.0 } // 固定品质以便比较
        };

        // Act
        var gear1 = await _service.GenerateAsync(definition, 5);
        var gear2 = await _service.GenerateAsync(definition, 20);

        // Assert
        Assert.True(gear2.ItemLevel > gear1.ItemLevel);
    }

    [Fact]
    public async Task GenerateAsync_WithSetId_ShouldPreserveSetId()
    {
        // Arrange
        var definition = CreateTestGearDefinition();
        definition.SetId = "warrior_set";
        var characterLevel = 10;

        // Act
        var gear = await _service.GenerateAsync(definition, characterLevel);

        // Assert
        Assert.Equal("warrior_set", gear.SetId);
    }

    private GearDefinition CreateTestGearDefinition()
    {
        return new GearDefinition
        {
            Id = "test_sword",
            Name = "测试之剑",
            Icon = "⚔️",
            Slot = EquipmentSlot.MainHand,
            ArmorType = ArmorType.None,
            WeaponType = WeaponType.Sword,
            RequiredLevel = 1,
            BaseStats = new Dictionary<StatType, StatRange>
            {
                { StatType.AttackPower, new StatRange(20, 30) }
            },
            AllowedAffixPool = new List<string> { "affix_str", "affix_agi", "affix_crit" },
            RarityWeights = new Dictionary<Rarity, double>
            {
                { Rarity.Common, 40.0 },
                { Rarity.Rare, 30.0 },
                { Rarity.Epic, 20.0 },
                { Rarity.Legendary, 10.0 }
            }
        };
    }
}
