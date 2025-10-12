using BlazorIdle.Server.Domain.Equipment.Models;
using BlazorIdle.Server.Domain.Equipment.Services;
using BlazorIdle.Server.Domain.Equipment.ValueObjects;
using BlazorIdle.Server.Infrastructure.Persistence;
using BlazorIdle.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BlazorIdle.Tests.Equipment.Services;

/// <summary>
/// 双持武器功能测试
/// </summary>
public class DualWieldTests
{
    private readonly GameDbContext _context;
    private readonly EquipmentService _equipmentService;
    private readonly StatsAggregationService _statsAggregationService;
    private readonly EquipmentValidator _validator;

    public DualWieldTests()
    {
        var options = new DbContextOptionsBuilder<GameDbContext>()
            .UseInMemoryDatabase(databaseName: $"TestDb_DualWield_{Guid.NewGuid()}")
            .Options;
        _context = new GameDbContext(options);
        
        var armorCalculator = new ArmorCalculator();
        var blockCalculator = new BlockCalculator();
        _validator = new EquipmentValidator();
        _equipmentService = new EquipmentService(_context, _validator);
        _statsAggregationService = new StatsAggregationService(
            _equipmentService,
            armorCalculator,
            blockCalculator
        );
    }

    /// <summary>
    /// 创建测试用的武器
    /// </summary>
    private (GearDefinition definition, GearInstance instance) CreateTestWeapon(
        Guid characterId,
        WeaponType weaponType,
        EquipmentSlot slot,
        string namePrefix = "Test")
    {
        var definition = new GearDefinition
        {
            Id = $"{namePrefix}_{weaponType}_{Guid.NewGuid()}",
            Name = $"{namePrefix} {weaponType}",
            Slot = EquipmentSlot.MainHand, // 可以装备到主手和副手
            WeaponType = weaponType,
            ArmorType = ArmorType.None,
            RequiredLevel = 1
        };

        var instance = new GearInstance
        {
            Id = Guid.NewGuid(),
            DefinitionId = definition.Id,
            Definition = definition,
            CharacterId = characterId,
            Rarity = Rarity.Common,
            TierLevel = 1,
            ItemLevel = 10,
            QualityScore = 100,
            SlotType = slot,
            IsEquipped = true,
            RolledStats = new Dictionary<StatType, double>
            {
                { StatType.AttackPower, 50 }
            },
            Affixes = new List<AffixInstance>()
        };

        return (definition, instance);
    }

    [Fact]
    public async Task IsDualWielding_WithTwoOneHandWeapons_ShouldReturnTrue()
    {
        // Arrange
        var characterId = Guid.NewGuid();
        
        var (mainDef, mainWeapon) = CreateTestWeapon(characterId, WeaponType.Sword, EquipmentSlot.MainHand, "MainHand");
        var (offDef, offWeapon) = CreateTestWeapon(characterId, WeaponType.Dagger, EquipmentSlot.OffHand, "OffHand");

        await _context.Set<GearDefinition>().AddAsync(mainDef);
        await _context.Set<GearDefinition>().AddAsync(offDef);
        await _context.Set<GearInstance>().AddAsync(mainWeapon);
        await _context.Set<GearInstance>().AddAsync(offWeapon);
        await _context.SaveChangesAsync();

        // Act
        var isDualWielding = await _statsAggregationService.IsDualWieldingAsync(characterId);

        // Assert
        Assert.True(isDualWielding, "两把单手武器应该被识别为双持");
    }

    [Fact]
    public async Task IsDualWielding_WithShield_ShouldReturnFalse()
    {
        // Arrange
        var characterId = Guid.NewGuid();
        
        var (mainDef, mainWeapon) = CreateTestWeapon(characterId, WeaponType.Sword, EquipmentSlot.MainHand, "MainHand");
        var (shieldDef, shield) = CreateTestWeapon(characterId, WeaponType.Shield, EquipmentSlot.OffHand, "Shield");

        await _context.Set<GearDefinition>().AddAsync(mainDef);
        await _context.Set<GearDefinition>().AddAsync(shieldDef);
        await _context.Set<GearInstance>().AddAsync(mainWeapon);
        await _context.Set<GearInstance>().AddAsync(shield);
        await _context.SaveChangesAsync();

        // Act
        var isDualWielding = await _statsAggregationService.IsDualWieldingAsync(characterId);

        // Assert
        Assert.False(isDualWielding, "盾牌不应该被识别为双持");
    }

    [Fact]
    public async Task IsDualWielding_WithTwoHandWeapon_ShouldReturnFalse()
    {
        // Arrange
        var characterId = Guid.NewGuid();
        
        var definition = new GearDefinition
        {
            Id = $"TwoHand_{Guid.NewGuid()}",
            Name = "Test TwoHand Sword",
            Slot = EquipmentSlot.TwoHand,
            WeaponType = WeaponType.TwoHandSword,
            ArmorType = ArmorType.None,
            RequiredLevel = 1
        };

        var weapon = new GearInstance
        {
            Id = Guid.NewGuid(),
            DefinitionId = definition.Id,
            Definition = definition,
            CharacterId = characterId,
            Rarity = Rarity.Common,
            TierLevel = 1,
            ItemLevel = 10,
            QualityScore = 100,
            SlotType = EquipmentSlot.TwoHand,
            IsEquipped = true,
            RolledStats = new Dictionary<StatType, double>
            {
                { StatType.AttackPower, 100 }
            },
            Affixes = new List<AffixInstance>()
        };

        await _context.Set<GearDefinition>().AddAsync(definition);
        await _context.Set<GearInstance>().AddAsync(weapon);
        await _context.SaveChangesAsync();

        // Act
        var isDualWielding = await _statsAggregationService.IsDualWieldingAsync(characterId);

        // Assert
        Assert.False(isDualWielding, "双手武器不应该被识别为双持");
    }

    [Fact]
    public async Task IsDualWielding_WithOnlyMainHand_ShouldReturnFalse()
    {
        // Arrange
        var characterId = Guid.NewGuid();
        
        var (mainDef, mainWeapon) = CreateTestWeapon(characterId, WeaponType.Sword, EquipmentSlot.MainHand, "MainHand");

        await _context.Set<GearDefinition>().AddAsync(mainDef);
        await _context.Set<GearInstance>().AddAsync(mainWeapon);
        await _context.SaveChangesAsync();

        // Act
        var isDualWielding = await _statsAggregationService.IsDualWieldingAsync(characterId);

        // Assert
        Assert.False(isDualWielding, "只有主手武器不应该被识别为双持");
    }

    [Fact]
    public async Task GetDualWieldDamageMultiplier_WhenDualWielding_ShouldReturn085()
    {
        // Arrange
        var characterId = Guid.NewGuid();
        
        var (mainDef, mainWeapon) = CreateTestWeapon(characterId, WeaponType.Sword, EquipmentSlot.MainHand, "MainHand");
        var (offDef, offWeapon) = CreateTestWeapon(characterId, WeaponType.Axe, EquipmentSlot.OffHand, "OffHand");

        await _context.Set<GearDefinition>().AddAsync(mainDef);
        await _context.Set<GearDefinition>().AddAsync(offDef);
        await _context.Set<GearInstance>().AddAsync(mainWeapon);
        await _context.Set<GearInstance>().AddAsync(offWeapon);
        await _context.SaveChangesAsync();

        // Act
        var multiplier = await _statsAggregationService.GetDualWieldDamageMultiplierAsync(characterId);

        // Assert
        Assert.Equal(0.85, multiplier, 2);
    }

    [Fact]
    public async Task GetDualWieldDamageMultiplier_WhenNotDualWielding_ShouldReturn1()
    {
        // Arrange
        var characterId = Guid.NewGuid();
        
        var (mainDef, mainWeapon) = CreateTestWeapon(characterId, WeaponType.Sword, EquipmentSlot.MainHand, "MainHand");

        await _context.Set<GearDefinition>().AddAsync(mainDef);
        await _context.Set<GearInstance>().AddAsync(mainWeapon);
        await _context.SaveChangesAsync();

        // Act
        var multiplier = await _statsAggregationService.GetDualWieldDamageMultiplierAsync(characterId);

        // Assert
        Assert.Equal(1.0, multiplier);
    }

    [Fact]
    public void CanDualWield_WithSwordAndDagger_ShouldBeAllowed()
    {
        // 测试AttackSpeedCalculator的静态方法
        Assert.True(AttackSpeedCalculator.CanDualWield(WeaponType.Sword));
        Assert.True(AttackSpeedCalculator.CanDualWield(WeaponType.Dagger));
        Assert.True(AttackSpeedCalculator.CanDualWield(WeaponType.Axe));
        Assert.True(AttackSpeedCalculator.CanDualWield(WeaponType.Mace));
        Assert.True(AttackSpeedCalculator.CanDualWield(WeaponType.Fist));
    }

    [Fact]
    public void CanDualWield_WithStaffAndBow_ShouldNotBeAllowed()
    {
        // 双手武器不能双持
        Assert.False(AttackSpeedCalculator.CanDualWield(WeaponType.Staff));
        Assert.False(AttackSpeedCalculator.CanDualWield(WeaponType.Bow));
        Assert.False(AttackSpeedCalculator.CanDualWield(WeaponType.TwoHandSword));
        
        // 盾牌和魔杖也不能双持（特殊规则）
        Assert.False(AttackSpeedCalculator.CanDualWield(WeaponType.Shield));
        Assert.False(AttackSpeedCalculator.CanDualWield(WeaponType.Wand));
    }
}
