using BlazorIdle.Server.Application.Battles;
using BlazorIdle.Server.Domain.Characters;
using BlazorIdle.Server.Domain.Combat.Enemies;
using BlazorIdle.Server.Domain.Combat.Professions;
using BlazorIdle.Server.Domain.Equipment.Models;
using BlazorIdle.Server.Domain.Equipment.Services;
using BlazorIdle.Server.Domain.Equipment.ValueObjects;
using BlazorIdle.Server.Infrastructure.Persistence;
using BlazorIdle.Shared.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace BlazorIdle.Tests.Equipment.Integration;

/// <summary>
/// 装备-战斗集成测试
/// 验证装备属性确实影响战斗结果
/// </summary>
public class EquipmentBattleIntegrationTests : IDisposable
{
    private readonly GameDbContext _context;
    private readonly EquipmentStatsIntegration _equipmentStats;
    private readonly BattleRunner _battleRunner;
    private readonly Guid _characterId;

    public EquipmentBattleIntegrationTests()
    {
        // 创建内存数据库
        var options = new DbContextOptionsBuilder<GameDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        
        _context = new GameDbContext(options);
        
        // 创建服务
        var equipmentService = new EquipmentService(_context);
        var statsAggregationService = new StatsAggregationService(equipmentService);
        _equipmentStats = new EquipmentStatsIntegration(statsAggregationService);
        var simulator = new BattleSimulator();
        _battleRunner = new BattleRunner(simulator);
        
        // 创建测试角色
        _characterId = Guid.NewGuid();
        _context.Characters.Add(new Character
        {
            Id = _characterId,
            Name = "TestWarrior",
            Profession = Profession.Warrior,
            Level = 10,
            Strength = 20,
            Agility = 15,
            Intellect = 10,
            Stamina = 25
        });
        _context.SaveChanges();
    }

    [Fact]
    public async Task Battle_WithEquipment_ShouldDealMoreDamage()
    {
        // Arrange - 创建一件武器装备
        var weaponDef = new GearDefinition
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
                { StatType.AttackPower, new StatRange { Min = 50, Max = 50 } }
            },
            AllowedAffixPool = new List<string>(),
            RarityWeights = new Dictionary<Rarity, double> { { Rarity.Common, 1.0 } }
        };
        _context.GearDefinitions.Add(weaponDef);
        
        var weapon = new GearInstance
        {
            Id = Guid.NewGuid(),
            DefinitionId = "test_sword",
            CharacterId = _characterId,
            SlotType = EquipmentSlot.MainHand,
            Rarity = Rarity.Common,
            TierLevel = 1,
            ItemLevel = 10,
            RolledStats = new Dictionary<StatType, double>
            {
                { StatType.AttackPower, 50 }
            },
            Affixes = new List<AffixInstance>(),
            IsEquipped = true,
            QualityScore = 100
        };
        _context.GearInstances.Add(weapon);
        await _context.SaveChangesAsync();

        // Act - 运行两场战斗：有装备 vs 无装备
        var profession = Profession.Warrior;
        var attrs = new PrimaryAttributes(20, 15, 10, 25);
        
        // 战斗1: 有装备
        var statsWithEquipment = await _equipmentStats.BuildStatsWithEquipmentAsync(_characterId, profession, attrs);
        var damageWithEquipment = RunBattle(statsWithEquipment, profession);
        
        // 战斗2: 无装备 (卸下装备)
        weapon.IsEquipped = false;
        weapon.SlotType = null;
        await _context.SaveChangesAsync();
        
        var statsWithoutEquipment = await _equipmentStats.BuildStatsWithEquipmentAsync(_characterId, profession, attrs);
        var damageWithoutEquipment = RunBattle(statsWithoutEquipment, profession);

        // Assert - 有装备的伤害应该更高
        Assert.True(damageWithEquipment > damageWithoutEquipment,
            $"有装备伤害({damageWithEquipment})应该大于无装备伤害({damageWithoutEquipment})");
        
        // 验证装备属性确实被应用
        Assert.True(statsWithEquipment.AttackPower > statsWithoutEquipment.AttackPower,
            "有装备的攻击强度应该更高");
    }

    [Fact]
    public async Task Battle_WithArmor_ShouldHaveArmorValue()
    {
        // Arrange - 创建一件护甲装备
        var armorDef = new GearDefinition
        {
            Id = "test_chest",
            Name = "测试胸甲",
            Icon = "🛡️",
            Slot = EquipmentSlot.Chest,
            ArmorType = ArmorType.Plate,
            WeaponType = WeaponType.None,
            RequiredLevel = 1,
            BaseStats = new Dictionary<StatType, StatRange>
            {
                { StatType.Armor, new StatRange { Min = 100, Max = 100 } }
            },
            AllowedAffixPool = new List<string>(),
            RarityWeights = new Dictionary<Rarity, double> { { Rarity.Common, 1.0 } }
        };
        _context.GearDefinitions.Add(armorDef);
        
        var armor = new GearInstance
        {
            Id = Guid.NewGuid(),
            DefinitionId = "test_chest",
            CharacterId = _characterId,
            SlotType = EquipmentSlot.Chest,
            Rarity = Rarity.Common,
            TierLevel = 1,
            ItemLevel = 10,
            RolledStats = new Dictionary<StatType, double>
            {
                { StatType.Armor, 100 }
            },
            Affixes = new List<AffixInstance>(),
            IsEquipped = true,
            QualityScore = 100
        };
        _context.GearInstances.Add(armor);
        await _context.SaveChangesAsync();

        // Act - 获取装备属性
        var profession = Profession.Warrior;
        var attrs = new PrimaryAttributes(20, 15, 10, 25);
        var statsWithArmor = await _equipmentStats.BuildStatsWithEquipmentAsync(_characterId, profession, attrs);
        
        // 获取护甲值
        var armorValue = await _equipmentStats.GetEquipmentArmorAsync(_characterId);

        // Assert - 验证护甲值存在
        Assert.True(armorValue > 0, "应该有护甲值");
        // Plate护甲有2.0倍系数，所以100 * 2.0 * 1.5(胸甲槽位系数) = 300
        Assert.Equal(300, armorValue);
    }

    [Fact]
    public async Task Battle_WithCritGear_ShouldHaveHigherCritChance()
    {
        // Arrange - 创建一件暴击装备
        var critGearDef = new GearDefinition
        {
            Id = "test_crit_gear",
            Name = "暴击饰品",
            Icon = "💎",
            Slot = EquipmentSlot.Trinket1,
            ArmorType = ArmorType.None,
            WeaponType = WeaponType.None,
            RequiredLevel = 1,
            BaseStats = new Dictionary<StatType, StatRange>
            {
                { StatType.CritRating, new StatRange { Min = 200, Max = 200 } }
            },
            AllowedAffixPool = new List<string>(),
            RarityWeights = new Dictionary<Rarity, double> { { Rarity.Common, 1.0 } }
        };
        _context.GearDefinitions.Add(critGearDef);
        
        var critGear = new GearInstance
        {
            Id = Guid.NewGuid(),
            DefinitionId = "test_crit_gear",
            CharacterId = _characterId,
            SlotType = EquipmentSlot.Trinket1,
            Rarity = Rarity.Common,
            TierLevel = 1,
            ItemLevel = 10,
            RolledStats = new Dictionary<StatType, double>
            {
                { StatType.CritRating, 200 }
            },
            Affixes = new List<AffixInstance>(),
            IsEquipped = true,
            QualityScore = 100
        };
        _context.GearInstances.Add(critGear);
        await _context.SaveChangesAsync();

        // Act - 获取装备属性
        var profession = Profession.Warrior;
        var attrs = new PrimaryAttributes(20, 15, 10, 25);
        var statsWithCrit = await _equipmentStats.BuildStatsWithEquipmentAsync(_characterId, profession, attrs);
        
        // 卸下装备对比
        critGear.IsEquipped = false;
        critGear.SlotType = null;
        await _context.SaveChangesAsync();
        var statsWithoutCrit = await _equipmentStats.BuildStatsWithEquipmentAsync(_characterId, profession, attrs);

        // Assert - 暴击率应该提升
        Assert.True(statsWithCrit.CritChance > statsWithoutCrit.CritChance,
            $"有暴击装备的暴击率({statsWithCrit.CritChance:P2})应该高于无装备({statsWithoutCrit.CritChance:P2})");
        
        // 200暴击评级 = 0.05 (5%) 暴击率
        var expectedCritIncrease = 200.0 / 4000.0;
        Assert.True(Math.Abs(statsWithCrit.CritChance - statsWithoutCrit.CritChance - expectedCritIncrease) < 0.001,
            "暴击率提升应该符合转换公式");
    }

    private long RunBattle(CharacterStats stats, Profession profession)
    {
        var module = ProfessionRegistry.Resolve(profession);
        var battle = new Server.Domain.Combat.Battle
        {
            Id = Guid.NewGuid(),
            CharacterId = _characterId,
            AttackIntervalSeconds = module.BaseAttackInterval,
            SpecialIntervalSeconds = module.BaseSpecialInterval,
            StartedAt = 0
        };
        
        var enemyDef = EnemyRegistry.Resolve("dummy");
        // 使用固定种子确保结果可重复，但不同的攻击力会导致不同的伤害
        var rng = new Server.Domain.Combat.Rng.RngContext((ulong)stats.AttackPower);
        
        var segments = _battleRunner.RunForDuration(
            battle,
            10.0, // 10秒战斗
            profession,
            rng,
            out var killed,
            out var killTime,
            out var overkill,
            module: module,
            stats: stats
        );
        
        return segments.Sum(s => s.TotalDamage);
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
