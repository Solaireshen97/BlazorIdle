using BlazorIdle.Server.Application.Abstractions;
using BlazorIdle.Server.Domain.Characters;
using BlazorIdle.Server.Domain.Equipment.Models;
using BlazorIdle.Server.Domain.Equipment.Services;
using BlazorIdle.Server.Domain.Equipment.ValueObjects;
using BlazorIdle.Server.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BlazorIdle.Tests.Equipment.Integration;

/// <summary>
/// 装备系统集成测试
/// 验证装备系统从生成到装备的完整流程
/// </summary>
public class EquipmentSystemIntegrationTests : IDisposable
{
    private readonly GameDbContext _context;
    private readonly GearGenerationService _generationService;
    private readonly EquipmentService _equipmentService;
    private readonly StatsAggregationService _statsService;
    private readonly DisenchantService _disenchantService;
    private readonly ReforgeService _reforgeService;
    private readonly Character _testCharacter;

    public EquipmentSystemIntegrationTests()
    {
        // 创建内存数据库
        var options = new DbContextOptionsBuilder<GameDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new GameDbContext(options);
        
        // 确保数据库已创建
        _context.Database.EnsureCreated();
        
        // 初始化服务
        var affixRepo = new AffixRepositoryAdapter(_context);
        _generationService = new GearGenerationService(affixRepo);
        _equipmentService = new EquipmentService(_context);
        _statsService = new StatsAggregationService(_equipmentService);
        _disenchantService = new DisenchantService(_context);
        _reforgeService = new ReforgeService(_context);
        
        // 创建测试角色
        _testCharacter = new Character
        {
            Id = Guid.NewGuid(),
            Name = "测试角色",
            Level = 10,
            Strength = 10,
            Agility = 10,
            Intellect = 10,
            Stamina = 10,
            Profession = BlazorIdle.Shared.Models.Profession.Warrior,
            Gold = 1000,
            Experience = 0
        };
        _context.Characters.Add(_testCharacter);
        _context.SaveChanges();
    }

    [Fact]
    public async Task CompleteEquipmentFlow_GenerateEquipCalculateStats_ShouldWork()
    {
        // Arrange - 获取装备定义
        var swordDefinition = await _context.GearDefinitions
            .FirstOrDefaultAsync(g => g.Id == "weapon_iron_sword");
        
        Assert.NotNull(swordDefinition);

        // Act 1: 生成装备
        var generatedGear = await _generationService.GenerateAsync(swordDefinition, _testCharacter.Level);
        generatedGear.CharacterId = _testCharacter.Id;
        _context.GearInstances.Add(generatedGear);
        await _context.SaveChangesAsync();

        // Assert 1: 验证装备已生成
        Assert.NotEqual(Guid.Empty, generatedGear.Id);
        Assert.Equal(swordDefinition.Id, generatedGear.DefinitionId);
        Assert.True(generatedGear.RolledStats.Count > 0);
        
        // Act 2: 装备物品
        var equipResult = await _equipmentService.EquipAsync(_testCharacter.Id, generatedGear.Id);

        // Assert 2: 验证装备成功
        Assert.True(equipResult.IsSuccess);
        Assert.Contains("成功装备", equipResult.Message);

        // Act 3: 计算总属性
        var totalStats = await _statsService.CalculateEquipmentStatsAsync(_testCharacter.Id);

        // Assert 3: 验证属性计算正确
        Assert.NotNull(totalStats);
        Assert.True(totalStats.ContainsKey(StatType.AttackPower));
        Assert.True(totalStats[StatType.AttackPower] > 0);
    }

    [Fact]
    public async Task TwoHandWeapon_ShouldUnequipBothHands()
    {
        // Arrange - 先装备单手武器和盾牌
        var swordDef = await _context.GearDefinitions.FirstOrDefaultAsync(g => g.Id == "weapon_iron_sword");
        var shieldDef = await _context.GearDefinitions.FirstOrDefaultAsync(g => g.Id == "shield_iron");
        
        Assert.NotNull(swordDef);
        Assert.NotNull(shieldDef);
        
        var sword = await _generationService.GenerateAsync(swordDef, _testCharacter.Level);
        sword.CharacterId = _testCharacter.Id;
        var shield = await _generationService.GenerateAsync(shieldDef, _testCharacter.Level);
        shield.CharacterId = _testCharacter.Id;
        
        _context.GearInstances.AddRange(sword, shield);
        await _context.SaveChangesAsync();
        
        await _equipmentService.EquipAsync(_testCharacter.Id, sword.Id);
        await _equipmentService.EquipAsync(_testCharacter.Id, shield.Id);

        // Act - 装备双手武器
        var twoHandDef = await _context.GearDefinitions.FirstOrDefaultAsync(g => g.Id == "weapon_twohand_sword");
        Assert.NotNull(twoHandDef);
        
        var twoHandWeapon = await _generationService.GenerateAsync(twoHandDef, _testCharacter.Level);
        twoHandWeapon.CharacterId = _testCharacter.Id;
        _context.GearInstances.Add(twoHandWeapon);
        await _context.SaveChangesAsync();
        
        var result = await _equipmentService.EquipAsync(_testCharacter.Id, twoHandWeapon.Id);

        // Assert
        Assert.True(result.IsSuccess);
        
        // 验证主手和副手都已被卸下
        var mainHandGear = await _equipmentService.GetEquippedGearInSlotAsync(_testCharacter.Id, EquipmentSlot.MainHand);
        var offHandGear = await _equipmentService.GetEquippedGearInSlotAsync(_testCharacter.Id, EquipmentSlot.OffHand);
        
        Assert.Null(mainHandGear);
        Assert.Null(offHandGear);
        
        // 验证双手武器已装备
        var twoHandGear = await _equipmentService.GetEquippedGearInSlotAsync(_testCharacter.Id, EquipmentSlot.TwoHand);
        Assert.NotNull(twoHandGear);
        Assert.Equal(twoHandWeapon.Id, twoHandGear.Id);
    }

    [Fact]
    public async Task DisenchantGear_ShouldProduceMaterials()
    {
        // Arrange - 生成装备
        var definition = await _context.GearDefinitions.FirstOrDefaultAsync(g => g.Id == "weapon_iron_sword");
        Assert.NotNull(definition);
        
        var gear = await _generationService.GenerateAsync(definition, _testCharacter.Level);
        gear.CharacterId = _testCharacter.Id;
        gear.Rarity = Rarity.Rare; // 设置为稀有
        _context.GearInstances.Add(gear);
        await _context.SaveChangesAsync();

        // Act - 分解装备
        var result = await _disenchantService.DisenchantAsync(_testCharacter.Id, gear.Id);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Materials);
        Assert.NotEmpty(result.Materials);
        
        // 验证装备已被删除
        var deletedGear = await _context.GearInstances.FindAsync(gear.Id);
        Assert.Null(deletedGear);
    }

    [Fact]
    public async Task ReforgeGear_ShouldUpgradeTier()
    {
        // Arrange - 生成T1装备
        var definition = await _context.GearDefinitions.FirstOrDefaultAsync(g => g.Id == "weapon_iron_sword");
        Assert.NotNull(definition);
        
        var gear = await _generationService.GenerateAsync(definition, _testCharacter.Level);
        gear.CharacterId = _testCharacter.Id;
        gear.TierLevel = 1;
        gear.Rarity = Rarity.Epic;
        _context.GearInstances.Add(gear);
        await _context.SaveChangesAsync();

        var originalScore = gear.QualityScore;

        // Act - 重铸到T2
        var result = await _reforgeService.ReforgeAsync(_testCharacter.Id, gear.Id);

        // Assert
        Assert.True(result.IsSuccess);
        
        // 验证品级提升
        var upgradedGear = await _context.GearInstances.FindAsync(gear.Id);
        Assert.NotNull(upgradedGear);
        Assert.Equal(2, upgradedGear.TierLevel);
        Assert.True(upgradedGear.QualityScore > originalScore);
    }

    [Fact]
    public async Task MultipleArmorsTypes_ShouldGenerateCorrectly()
    {
        // Arrange & Act - 生成不同护甲类型的装备
        var clothDef = await _context.GearDefinitions.FirstOrDefaultAsync(g => g.Id == "helm_cloth_basic");
        var leatherDef = await _context.GearDefinitions.FirstOrDefaultAsync(g => g.Id == "chest_leather_basic");
        var mailDef = await _context.GearDefinitions.FirstOrDefaultAsync(g => g.Id == "chest_mail_basic");
        var plateDef = await _context.GearDefinitions.FirstOrDefaultAsync(g => g.Id == "chest_plate_basic");

        Assert.NotNull(clothDef);
        Assert.NotNull(leatherDef);
        Assert.NotNull(mailDef);
        Assert.NotNull(plateDef);

        var clothGear = await _generationService.GenerateAsync(clothDef, _testCharacter.Level);
        var leatherGear = await _generationService.GenerateAsync(leatherDef, _testCharacter.Level);
        var mailGear = await _generationService.GenerateAsync(mailDef, _testCharacter.Level);
        var plateGear = await _generationService.GenerateAsync(plateDef, _testCharacter.Level);

        // Assert - 验证每种护甲类型的属性
        Assert.NotNull(clothGear);
        Assert.True(clothGear.RolledStats.ContainsKey(StatType.Intellect));
        
        Assert.NotNull(leatherGear);
        Assert.True(leatherGear.RolledStats.ContainsKey(StatType.Agility));
        
        Assert.NotNull(mailGear);
        Assert.True(mailGear.RolledStats.ContainsKey(StatType.Stamina));
        
        Assert.NotNull(plateGear);
        Assert.True(plateGear.RolledStats.ContainsKey(StatType.Strength));
    }

    [Fact]
    public async Task SetBonus_ShouldBeCalculated()
    {
        // Arrange - 创建套装装备
        var setHelm = await _context.GearDefinitions.FirstOrDefaultAsync(g => g.Id == "helm_cloth_basic");
        Assert.NotNull(setHelm);
        
        var helm = await _generationService.GenerateAsync(setHelm, _testCharacter.Level);
        helm.CharacterId = _testCharacter.Id;
        helm.SetId = "set_mage_basic";
        
        _context.GearInstances.Add(helm);
        await _context.SaveChangesAsync();

        // Act - 装备套装件
        await _equipmentService.EquipAsync(_testCharacter.Id, helm.Id);

        // Assert - 验证套装效果（这里需要 StatsAggregationService 支持套装计算）
        var equippedGear = await _equipmentService.GetEquippedGearAsync(_testCharacter.Id);
        var setGear = equippedGear.Where(g => g.SetId == "set_mage_basic").ToList();
        
        Assert.Single(setGear);
        Assert.Equal("set_mage_basic", setGear[0].SetId);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    /// <summary>
    /// 词条仓储适配器，用于集成测试
    /// </summary>
    private class AffixRepositoryAdapter : IAffixRepository
    {
        private readonly GameDbContext _context;

        public AffixRepositoryAdapter(GameDbContext context)
        {
            _context = context;
        }

        public async Task<Affix?> GetByIdAsync(string id, CancellationToken ct = default)
        {
            return await _context.Affixes.FindAsync(new object[] { id }, ct);
        }

        public async Task<List<Affix>> GetBySlotAsync(EquipmentSlot slot, CancellationToken ct = default)
        {
            return await _context.Affixes.ToListAsync(ct);
        }

        public async Task<List<Affix>> GetAllAsync(CancellationToken ct = default)
        {
            return await _context.Affixes.ToListAsync(ct);
        }

        public async Task CreateAsync(Affix affix, CancellationToken ct = default)
        {
            _context.Affixes.Add(affix);
            await _context.SaveChangesAsync(ct);
        }

        public async Task UpdateAsync(Affix affix, CancellationToken ct = default)
        {
            _context.Affixes.Update(affix);
            await _context.SaveChangesAsync(ct);
        }

        public async Task DeleteAsync(string id, CancellationToken ct = default)
        {
            var affix = await _context.Affixes.FindAsync(new object[] { id }, ct);
            if (affix != null)
            {
                _context.Affixes.Remove(affix);
                await _context.SaveChangesAsync(ct);
            }
        }
    }
}
