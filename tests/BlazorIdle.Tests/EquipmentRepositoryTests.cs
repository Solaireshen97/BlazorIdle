using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BlazorIdle.Server.Domain.Equipment.Models;
using BlazorIdle.Server.Domain.Equipment.ValueObjects;
using BlazorIdle.Server.Infrastructure.Persistence;
using BlazorIdle.Server.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BlazorIdle.Tests;

/// <summary>
/// 装备系统仓储层单元测试
/// 测试 Phase 1: 数据基础与核心模型的仓储功能
/// </summary>
public class EquipmentRepositoryTests : IDisposable
{
    private readonly GameDbContext _context;
    private readonly GearDefinitionRepository _gearDefRepo;
    private readonly GearInstanceRepository _gearInstanceRepo;
    private readonly AffixRepository _affixRepo;

    public EquipmentRepositoryTests()
    {
        // 使用内存数据库进行测试
        var options = new DbContextOptionsBuilder<GameDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new GameDbContext(options);
        // 确保数据库已创建（对于内存数据库）
        _context.Database.EnsureCreated();
        
        _gearDefRepo = new GearDefinitionRepository(_context);
        _gearInstanceRepo = new GearInstanceRepository(_context);
        _affixRepo = new AffixRepository(_context);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    /// <summary>
    /// 测试创建和获取装备定义
    /// </summary>
    [Fact]
    public async Task GearDefinitionRepository_CreateAndGet_Success()
    {
        // Arrange
        var gearDef = new GearDefinition
        {
            Id = "test_sword",
            Name = "测试之剑",
            Icon = "⚔️",
            Slot = EquipmentSlot.MainHand,
            WeaponType = WeaponType.Sword,
            RequiredLevel = 1,
            BaseAttackSpeed = 2.6,
            BaseDamageMin = 10,
            BaseDamageMax = 20,
            BaseStats = new Dictionary<StatType, StatRange>
            {
                { StatType.AttackPower, new StatRange(5, 10) }
            },
            AllowedAffixPool = new List<string> { "str_minor", "ap_minor" },
            RarityWeights = new Dictionary<Rarity, double>
            {
                { Rarity.Common, 0.7 },
                { Rarity.Rare, 0.3 }
            }
        };

        // Act
        await _gearDefRepo.CreateAsync(gearDef);
        var retrieved = await _gearDefRepo.GetByIdAsync("test_sword");

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal("test_sword", retrieved.Id);
        Assert.Equal("测试之剑", retrieved.Name);
        Assert.Equal(EquipmentSlot.MainHand, retrieved.Slot);
        Assert.Equal(WeaponType.Sword, retrieved.WeaponType);
        Assert.Equal(2.6, retrieved.BaseAttackSpeed);
        Assert.Contains(StatType.AttackPower, retrieved.BaseStats.Keys);
    }

    /// <summary>
    /// 测试按槽位获取装备定义
    /// </summary>
    [Fact]
    public async Task GearDefinitionRepository_GetBySlot_ReturnsCorrectItems()
    {
        // Arrange
        var sword = new GearDefinition
        {
            Id = "sword_1",
            Name = "剑",
            Slot = EquipmentSlot.MainHand,
            BaseStats = new Dictionary<StatType, StatRange>(),
            AllowedAffixPool = new List<string>(),
            RarityWeights = new Dictionary<Rarity, double>()
        };

        var helmet = new GearDefinition
        {
            Id = "helmet_1",
            Name = "头盔",
            Slot = EquipmentSlot.Head,
            BaseStats = new Dictionary<StatType, StatRange>(),
            AllowedAffixPool = new List<string>(),
            RarityWeights = new Dictionary<Rarity, double>()
        };

        await _gearDefRepo.CreateAsync(sword);
        await _gearDefRepo.CreateAsync(helmet);

        // Act
        var mainHandGear = await _gearDefRepo.GetBySlotAsync(EquipmentSlot.MainHand);

        // Assert
        Assert.Single(mainHandGear);
        Assert.Equal("sword_1", mainHandGear.First().Id);
    }

    /// <summary>
    /// 测试创建和获取装备实例
    /// </summary>
    [Fact]
    public async Task GearInstanceRepository_CreateAndGet_Success()
    {
        // Arrange
        var characterId = Guid.NewGuid();
        var gearInstance = new GearInstance
        {
            Id = Guid.NewGuid(),
            DefinitionId = "test_sword",
            CharacterId = characterId,
            SlotType = EquipmentSlot.MainHand,
            Rarity = Rarity.Rare,
            TierLevel = 2,
            ItemLevel = 10,
            RolledStats = new Dictionary<StatType, double>
            {
                { StatType.AttackPower, 15 }
            },
            Affixes = new List<AffixInstance>
            {
                new AffixInstance
                {
                    AffixId = "str_minor",
                    StatType = StatType.Strength,
                    ModifierType = ModifierType.Flat,
                    RolledValue = 5,
                    DisplayText = "+5 力量"
                }
            },
            IsEquipped = false,
            IsBound = false
        };

        // Act
        await _gearInstanceRepo.CreateAsync(gearInstance);
        var retrieved = await _gearInstanceRepo.GetByIdAsync(gearInstance.Id);

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal(gearInstance.Id, retrieved.Id);
        Assert.Equal("test_sword", retrieved.DefinitionId);
        Assert.Equal(characterId, retrieved.CharacterId);
        Assert.Equal(Rarity.Rare, retrieved.Rarity);
        Assert.Equal(2, retrieved.TierLevel);
        Assert.Single(retrieved.Affixes);
        Assert.Equal("str_minor", retrieved.Affixes[0].AffixId);
    }

    /// <summary>
    /// 测试获取角色已装备的装备
    /// </summary>
    [Fact]
    public async Task GearInstanceRepository_GetEquippedGear_ReturnsOnlyEquippedItems()
    {
        // Arrange
        var characterId = Guid.NewGuid();
        
        var equippedGear = new GearInstance
        {
            Id = Guid.NewGuid(),
            DefinitionId = "sword_1",
            CharacterId = characterId,
            IsEquipped = true,
            RolledStats = new Dictionary<StatType, double>(),
            Affixes = new List<AffixInstance>()
        };

        var unequippedGear = new GearInstance
        {
            Id = Guid.NewGuid(),
            DefinitionId = "helmet_1",
            CharacterId = characterId,
            IsEquipped = false,
            RolledStats = new Dictionary<StatType, double>(),
            Affixes = new List<AffixInstance>()
        };

        await _gearInstanceRepo.CreateAsync(equippedGear);
        await _gearInstanceRepo.CreateAsync(unequippedGear);

        // Act
        var equipped = await _gearInstanceRepo.GetEquippedGearAsync(characterId);

        // Assert
        Assert.Single(equipped);
        Assert.Equal(equippedGear.Id, equipped.First().Id);
        Assert.True(equipped.First().IsEquipped);
    }

    /// <summary>
    /// 测试创建和获取词条定义
    /// </summary>
    [Fact]
    public async Task AffixRepository_CreateAndGet_Success()
    {
        // Arrange
        var affix = new Affix
        {
            Id = "test_affix",
            Name = "测试词条",
            StatType = StatType.AttackPower,
            ModifierType = ModifierType.Flat,
            ValueMin = 5,
            ValueMax = 15,
            RarityWeight = 1.0
        };

        // Act
        await _affixRepo.CreateAsync(affix);
        var retrieved = await _affixRepo.GetByIdAsync("test_affix");

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal("test_affix", retrieved.Id);
        Assert.Equal("测试词条", retrieved.Name);
        Assert.Equal(StatType.AttackPower, retrieved.StatType);
        Assert.Equal(ModifierType.Flat, retrieved.ModifierType);
        Assert.Equal(5, retrieved.ValueMin);
        Assert.Equal(15, retrieved.ValueMax);
    }

    /// <summary>
    /// 测试按ID列表批量获取词条
    /// </summary>
    [Fact]
    public async Task AffixRepository_GetByIds_ReturnsCorrectAffixes()
    {
        // Arrange
        var affix1 = new Affix
        {
            Id = "affix_1",
            Name = "词条1",
            StatType = StatType.Strength,
            ModifierType = ModifierType.Flat,
            ValueMin = 1,
            ValueMax = 5
        };

        var affix2 = new Affix
        {
            Id = "affix_2",
            Name = "词条2",
            StatType = StatType.AttackPower,
            ModifierType = ModifierType.Flat,
            ValueMin = 5,
            ValueMax = 10
        };

        await _affixRepo.CreateAsync(affix1);
        await _affixRepo.CreateAsync(affix2);

        // Act
        var affixes = await _affixRepo.GetByIdsAsync(new List<string> { "affix_1", "affix_2" });

        // Assert
        Assert.Equal(2, affixes.Count);
        Assert.Contains(affixes, a => a.Id == "affix_1");
        Assert.Contains(affixes, a => a.Id == "affix_2");
    }
}
