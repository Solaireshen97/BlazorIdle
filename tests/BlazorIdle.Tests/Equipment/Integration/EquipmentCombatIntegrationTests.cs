using BlazorIdle.Server.Domain.Characters;
using BlazorIdle.Server.Domain.Equipment.Models;
using BlazorIdle.Server.Domain.Equipment.Services;
using BlazorIdle.Server.Domain.Equipment.ValueObjects;
using BlazorIdle.Server.Infrastructure.Persistence;
using BlazorIdle.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BlazorIdle.Tests.Equipment.Integration;

/// <summary>
/// 装备与战斗系统集成测试
/// 验证装备属性正确影响战斗计算
/// </summary>
public class EquipmentCombatIntegrationTests : IDisposable
{
    private readonly GameDbContext _context;
    private readonly EquipmentService _equipmentService;
    private readonly StatsAggregationService _statsAggregationService;

    public EquipmentCombatIntegrationTests()
    {
        var options = new DbContextOptionsBuilder<GameDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new GameDbContext(options);
        _equipmentService = new EquipmentService(_context);
        _statsAggregationService = new StatsAggregationService(_equipmentService);
    }

    [Fact]
    public async Task StatsBuilder_ShouldIncludeEquipmentStats()
    {
        // Arrange: 创建角色
        var characterId = Guid.NewGuid();
        var character = new Character
        {
            Id = characterId,
            UserId = Guid.NewGuid(),
            Name = "测试战士",
            Profession = Profession.Warrior,
            Strength = 10,
            Agility = 10,
            Intellect = 10,
            Stamina = 10,
            CreatedAt = DateTime.UtcNow
        };
        _context.Characters.Add(character);

        // Arrange: 创建装备定义
        var weaponDef = new GearDefinition
        {
            Id = "test_sword",
            Name = "测试剑",
            Icon = "⚔️",
            Slot = EquipmentSlot.MainHand,
            ArmorType = ArmorType.None,
            WeaponType = WeaponType.Sword,
            RequiredLevel = 1,
            BaseStats = new Dictionary<StatType, StatRange>
            {
                { StatType.AttackPower, new StatRange { Min = 50, Max = 100 } }
            },
            RarityWeights = new Dictionary<Rarity, double>
            {
                { Rarity.Common, 1.0 }
            },
            AllowedAffixPool = new List<string>(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.GearDefinitions.Add(weaponDef);

        // Arrange: 创建装备实例
        var weapon = new GearInstance
        {
            Id = Guid.NewGuid(),
            DefinitionId = weaponDef.Id,
            Rarity = Rarity.Common,
            TierLevel = 1,
            ItemLevel = 10,
            RolledStats = new Dictionary<StatType, double>
            {
                { StatType.AttackPower, 75 }  // Roll到75点攻击强度
            },
            Affixes = new List<AffixInstance>(),
            QualityScore = 100,
            IsEquipped = false,
            IsBound = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.GearInstances.Add(weapon);
        await _context.SaveChangesAsync();

        // Act: 装备武器
        var equipResult = await _equipmentService.EquipAsync(characterId, weapon.Id);
        Assert.True(equipResult.IsSuccess);

        // Act: 计算装备属性
        var equipmentStats = await _statsAggregationService.CalculateEquipmentStatsAsync(characterId);

        // Assert: 装备属性应该包含武器的攻击力
        Assert.True(equipmentStats.ContainsKey(StatType.AttackPower));
        Assert.Equal(75, equipmentStats[StatType.AttackPower]);

        // Act: 使用StatsBuilder合并属性
        var baseStats = new CharacterStats
        {
            AttackPower = 100,  // 职业基础攻击力
            CritChance = 0.05,
            HastePercent = 0.0
        };

        var attrs = new PrimaryAttributes(
            character.Strength,
            character.Agility,
            character.Intellect,
            character.Stamina
        );

        var derived = StatsBuilder.BuildDerived(character.Profession, attrs);
        var equipmentCharStats = StatsBuilder.FromEquipmentStats(equipmentStats);
        var finalStats = StatsBuilder.Combine(baseStats, derived, equipmentCharStats);

        // Assert: 最终攻击力 = 基础 + 主属性转换 + 装备
        // 基础100 + 力量转换(10 * 1.0 = 10) + 装备75 = 185
        Assert.True(finalStats.AttackPower >= 175);  // 至少包含基础和装备
        Assert.True(finalStats.AttackPower <= 200);  // 合理范围内
    }

    [Fact]
    public async Task EquipmentStats_ShouldAggregateMultipleItems()
    {
        // Arrange: 创建角色
        var characterId = Guid.NewGuid();
        var character = new Character
        {
            Id = characterId,
            UserId = Guid.NewGuid(),
            Name = "测试游侠",
            Profession = Profession.Ranger,
            Strength = 5,
            Agility = 5,
            Intellect = 20,
            Stamina = 10,
            CreatedAt = DateTime.UtcNow
        };
        _context.Characters.Add(character);

        // Arrange: 创建头盔定义
        var helmetDef = new GearDefinition
        {
            Id = "test_helmet",
            Name = "测试头盔",
            Icon = "🪖",
            Slot = EquipmentSlot.Head,
            ArmorType = ArmorType.Cloth,
            WeaponType = WeaponType.None,
            RequiredLevel = 1,
            BaseStats = new Dictionary<StatType, StatRange>
            {
                { StatType.SpellPower, new StatRange { Min = 20, Max = 30 } }
            },
            RarityWeights = new Dictionary<Rarity, double> { { Rarity.Common, 1.0 } },
            AllowedAffixPool = new List<string>(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Arrange: 创建胸甲定义
        var chestDef = new GearDefinition
        {
            Id = "test_chest",
            Name = "测试胸甲",
            Icon = "🦺",
            Slot = EquipmentSlot.Chest,
            ArmorType = ArmorType.Cloth,
            WeaponType = WeaponType.None,
            RequiredLevel = 1,
            BaseStats = new Dictionary<StatType, StatRange>
            {
                { StatType.SpellPower, new StatRange { Min = 30, Max = 50 } },
                { StatType.CritChance, new StatRange { Min = 0.01, Max = 0.03 } }
            },
            RarityWeights = new Dictionary<Rarity, double> { { Rarity.Common, 1.0 } },
            AllowedAffixPool = new List<string>(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.GearDefinitions.AddRange(helmetDef, chestDef);

        // Arrange: 创建装备实例
        var helmet = new GearInstance
        {
            Id = Guid.NewGuid(),
            DefinitionId = helmetDef.Id,
            Rarity = Rarity.Common,
            TierLevel = 1,
            ItemLevel = 10,
            RolledStats = new Dictionary<StatType, double> { { StatType.SpellPower, 25 } },
            Affixes = new List<AffixInstance>(),
            QualityScore = 100,
            IsEquipped = false,
            IsBound = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var chest = new GearInstance
        {
            Id = Guid.NewGuid(),
            DefinitionId = chestDef.Id,
            Rarity = Rarity.Common,
            TierLevel = 1,
            ItemLevel = 10,
            RolledStats = new Dictionary<StatType, double>
            {
                { StatType.SpellPower, 40 },
                { StatType.CritChance, 0.02 }
            },
            Affixes = new List<AffixInstance>(),
            QualityScore = 100,
            IsEquipped = false,
            IsBound = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.GearInstances.AddRange(helmet, chest);
        await _context.SaveChangesAsync();

        // Act: 装备两件装备
        await _equipmentService.EquipAsync(characterId, helmet.Id);
        await _equipmentService.EquipAsync(characterId, chest.Id);

        // Act: 计算总属性
        var equipmentStats = await _statsAggregationService.CalculateEquipmentStatsAsync(characterId);

        // Assert: 法术强度应该是两件装备之和
        Assert.Equal(65, equipmentStats[StatType.SpellPower]); // 25 + 40
        Assert.Equal(0.02, equipmentStats[StatType.CritChance]);
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
