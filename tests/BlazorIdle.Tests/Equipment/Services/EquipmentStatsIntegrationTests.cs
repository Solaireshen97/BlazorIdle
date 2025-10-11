using BlazorIdle.Server.Domain.Characters;
using BlazorIdle.Server.Domain.Equipment.Models;
using BlazorIdle.Server.Domain.Equipment.Services;
using BlazorIdle.Server.Domain.Equipment.ValueObjects;
using BlazorIdle.Shared.Models;
using Xunit;

namespace BlazorIdle.Tests.Equipment.Services;

public class EquipmentStatsIntegrationTests
{
    private readonly FakeStatsAggregationService _fakeStatsAggregationService;
    private readonly EquipmentStatsIntegration _service;

    public EquipmentStatsIntegrationTests()
    {
        _fakeStatsAggregationService = new FakeStatsAggregationService();
        _service = new EquipmentStatsIntegration(_fakeStatsAggregationService);
    }

    [Fact]
    public async Task BuildStatsWithEquipmentAsync_ShouldIncludeEquipmentStats()
    {
        // Arrange
        var characterId = Guid.NewGuid();
        var profession = Profession.Warrior;
        var primaryAttrs = new PrimaryAttributes
        {
            Strength = 20,
            Agility = 10,
            Intellect = 5,
            Stamina = 15
        };

        var equipmentStats = new Dictionary<StatType, double>
        {
            { StatType.AttackPower, 100 },
            { StatType.CritRating, 200 },
            { StatType.Haste, 0.05 },
            { StatType.Armor, 500 }
        };

        _fakeStatsAggregationService.SetEquipmentStats(characterId, equipmentStats);

        // Act
        var result = await _service.BuildStatsWithEquipmentAsync(characterId, profession, primaryAttrs);

        // Assert
        Assert.NotNull(result);
        
        // 装备提供的攻击强度应该被加到基础上
        Assert.True(result.AttackPower >= 100, "AttackPower should include equipment bonus");
        
        // 暴击评级应该转换为暴击率
        Assert.True(result.CritChance > 0, "CritChance should include converted crit rating");
        
        // 急速应该被应用
        Assert.True(result.HastePercent >= 0.05, "HastePercent should include equipment haste");
    }

    [Fact]
    public async Task BuildStatsWithEquipmentAsync_WithoutEquipment_ShouldReturnBaseStats()
    {
        // Arrange
        var characterId = Guid.NewGuid();
        var profession = Profession.Ranger;
        var primaryAttrs = new PrimaryAttributes
        {
            Strength = 5,
            Agility = 25,
            Intellect = 5,
            Stamina = 10
        };

        var emptyEquipmentStats = new Dictionary<StatType, double>();
        _fakeStatsAggregationService.SetEquipmentStats(characterId, emptyEquipmentStats);

        // Act
        var result = await _service.BuildStatsWithEquipmentAsync(characterId, profession, primaryAttrs);

        // Assert
        Assert.NotNull(result);
        
        // 应该只有基础属性和派生属性，没有装备加成
        Assert.True(result.AttackPower > 0, "Ranger should have attack power from agility");
    }

    [Fact]
    public async Task BuildStatsWithEquipmentAsync_ShouldConvertCritRatingProperly()
    {
        // Arrange
        var characterId = Guid.NewGuid();
        var profession = Profession.Ranger;
        var primaryAttrs = new PrimaryAttributes { Strength = 10, Agility = 20, Intellect = 5, Stamina = 10 };

        // 200 暴击评级 = 0.05 暴击率 (200 / 4000)
        var equipmentStats = new Dictionary<StatType, double>
        {
            { StatType.CritRating, 200 }
        };

        _fakeStatsAggregationService.SetEquipmentStats(characterId, equipmentStats);

        // Act
        var result = await _service.BuildStatsWithEquipmentAsync(characterId, profession, primaryAttrs);

        // Assert
        // 注意：总暴击率包括职业基础+属性派生+装备，所以只检查装备部分确实被应用了
        Assert.True(result.CritChance >= 0.05, 
            $"CritChance should be at least 0.05 from equipment rating, but was {result.CritChance}");
    }

    [Fact]
    public async Task BuildStatsWithEquipmentAsync_ShouldApplyHastePercent()
    {
        // Arrange
        var characterId = Guid.NewGuid();
        var profession = Profession.Warrior;
        var primaryAttrs = new PrimaryAttributes { Strength = 20, Agility = 10, Intellect = 5, Stamina = 15 };

        // 直接提供急速百分比
        var equipmentStats = new Dictionary<StatType, double>
        {
            { StatType.Haste, 0.10 }
        };

        _fakeStatsAggregationService.SetEquipmentStats(characterId, equipmentStats);

        // Act
        var result = await _service.BuildStatsWithEquipmentAsync(characterId, profession, primaryAttrs);

        // Assert
        Assert.True(result.HastePercent >= 0.10, 
            $"HastePercent should be at least 0.10, but was {result.HastePercent}");
    }

    [Fact]
    public async Task GetEquipmentArmorAsync_ShouldReturnTotalArmor()
    {
        // Arrange
        var characterId = Guid.NewGuid();
        var equipmentStats = new Dictionary<StatType, double>
        {
            { StatType.Armor, 750 }
        };

        _fakeStatsAggregationService.SetEquipmentStats(characterId, equipmentStats);

        // Act
        var result = await _service.GetEquipmentArmorAsync(characterId);

        // Assert
        Assert.Equal(750, result);
    }

    [Fact]
    public async Task GetEquipmentArmorAsync_WithoutArmor_ShouldReturnZero()
    {
        // Arrange
        var characterId = Guid.NewGuid();
        var emptyEquipmentStats = new Dictionary<StatType, double>();

        _fakeStatsAggregationService.SetEquipmentStats(characterId, emptyEquipmentStats);

        // Act
        var result = await _service.GetEquipmentArmorAsync(characterId);

        // Assert
        Assert.Equal(0, result);
    }

    [Fact]
    public async Task BuildStatsWithEquipmentAsync_ShouldClampCritChanceToOne()
    {
        // Arrange
        var characterId = Guid.NewGuid();
        var profession = Profession.Ranger;
        var primaryAttrs = new PrimaryAttributes { Strength = 10, Agility = 20, Intellect = 5, Stamina = 10 };

        // 超大的暴击值
        var equipmentStats = new Dictionary<StatType, double>
        {
            { StatType.CritChance, 2.0 } // 200% 暴击（应该被限制到100%）
        };

        _fakeStatsAggregationService.SetEquipmentStats(characterId, equipmentStats);

        // Act
        var result = await _service.BuildStatsWithEquipmentAsync(characterId, profession, primaryAttrs);

        // Assert
        Assert.True(result.CritChance <= 1.0, "CritChance should be clamped to 1.0");
    }

    [Fact]
    public async Task BuildStatsWithEquipmentAsync_ShouldApplyMultipleStatTypes()
    {
        // Arrange
        var characterId = Guid.NewGuid();
        var profession = Profession.Warrior;
        var primaryAttrs = new PrimaryAttributes { Strength = 20, Agility = 10, Intellect = 5, Stamina = 15 };

        var equipmentStats = new Dictionary<StatType, double>
        {
            { StatType.AttackPower, 100 },
            { StatType.SpellPower, 50 },
            { StatType.CritRating, 200 }
        };

        _fakeStatsAggregationService.SetEquipmentStats(characterId, equipmentStats);

        // Act
        var result = await _service.BuildStatsWithEquipmentAsync(characterId, profession, primaryAttrs);

        // Assert
        Assert.True(result.AttackPower >= 100, "AttackPower should include equipment bonus");
        Assert.True(result.SpellPower >= 50, "SpellPower should include equipment bonus");
        Assert.True(result.CritChance > 0, "CritChance should include converted crit rating");
    }

    [Fact]
    public async Task CalculateAttackSpeedAsync_NoWeapon_ShouldUseProfessionBaseSpeed()
    {
        // Arrange
        var characterId = Guid.NewGuid();
        var professionBaseSpeed = 2.0; // 职业基础攻击间隔2秒
        var hastePercent = 0.0;
        
        // No weapon equipped
        _fakeStatsAggregationService.SetWeaponType(characterId, new WeaponTypeInfo
        {
            MainHandType = WeaponType.None,
            OffHandType = WeaponType.None,
            IsTwoHanded = false,
            IsDualWielding = false
        });

        // Act
        var attackSpeed = await _service.CalculateAttackSpeedAsync(characterId, professionBaseSpeed, hastePercent);

        // Assert
        Assert.Equal(2.0, attackSpeed, precision: 2);
    }

    [Fact]
    public async Task CalculateAttackSpeedAsync_TwoHandSword_ShouldUseWeaponSpeed()
    {
        // Arrange
        var characterId = Guid.NewGuid();
        var professionBaseSpeed = 2.0;
        var hastePercent = 0.0;
        
        // Two-hand sword equipped (base speed 3.4 seconds)
        _fakeStatsAggregationService.SetWeaponType(characterId, new WeaponTypeInfo
        {
            MainHandType = WeaponType.TwoHandSword,
            OffHandType = WeaponType.None,
            IsTwoHanded = true,
            IsDualWielding = false
        });

        // Act
        var attackSpeed = await _service.CalculateAttackSpeedAsync(characterId, professionBaseSpeed, hastePercent);

        // Assert
        // TwoHandSword base speed is 3.4 seconds
        Assert.Equal(3.4, attackSpeed, precision: 2);
    }

    [Fact]
    public async Task CalculateAttackSpeedAsync_WithHaste_ShouldReduceAttackInterval()
    {
        // Arrange
        var characterId = Guid.NewGuid();
        var professionBaseSpeed = 2.4;
        var hastePercent = 0.25; // 25% haste
        
        // Sword equipped (base speed 2.4 seconds)
        _fakeStatsAggregationService.SetWeaponType(characterId, new WeaponTypeInfo
        {
            MainHandType = WeaponType.Sword,
            OffHandType = WeaponType.None,
            IsTwoHanded = false,
            IsDualWielding = false
        });

        // Act
        var attackSpeed = await _service.CalculateAttackSpeedAsync(characterId, professionBaseSpeed, hastePercent);

        // Assert
        // 2.4 / (1 + 0.25) = 2.4 / 1.25 = 1.92
        Assert.Equal(1.92, attackSpeed, precision: 2);
    }

    [Fact]
    public async Task CalculateAttackSpeedAsync_DualWield_ShouldAverageWeaponSpeeds()
    {
        // Arrange
        var characterId = Guid.NewGuid();
        var professionBaseSpeed = 2.0;
        var hastePercent = 0.0;
        
        // Dual wielding: Sword (2.4s) + Dagger (1.8s)
        _fakeStatsAggregationService.SetWeaponType(characterId, new WeaponTypeInfo
        {
            MainHandType = WeaponType.Sword,
            OffHandType = WeaponType.Dagger,
            IsTwoHanded = false,
            IsDualWielding = true
        });

        // Act
        var attackSpeed = await _service.CalculateAttackSpeedAsync(characterId, professionBaseSpeed, hastePercent);

        // Assert
        // Average of 2.4 and 1.8 = 2.1
        Assert.Equal(2.1, attackSpeed, precision: 2);
    }

    [Fact]
    public async Task CalculateAttackSpeedAsync_WeaponAndShield_ShouldUseMainHandSpeed()
    {
        // Arrange
        var characterId = Guid.NewGuid();
        var professionBaseSpeed = 2.0;
        var hastePercent = 0.0;
        
        // Sword + Shield (shield doesn't count as dual wielding)
        _fakeStatsAggregationService.SetWeaponType(characterId, new WeaponTypeInfo
        {
            MainHandType = WeaponType.Sword,
            OffHandType = WeaponType.Shield,
            IsTwoHanded = false,
            IsDualWielding = false
        });

        // Act
        var attackSpeed = await _service.CalculateAttackSpeedAsync(characterId, professionBaseSpeed, hastePercent);

        // Assert
        // Should use Sword base speed: 2.4 seconds
        Assert.Equal(2.4, attackSpeed, precision: 2);
    }
}

/// <summary>
/// 假装备属性聚合服务用于测试
/// </summary>
internal class FakeStatsAggregationService : StatsAggregationService
{
    private readonly Dictionary<Guid, Dictionary<StatType, double>> _equipmentStatsCache = new();

    public FakeStatsAggregationService() : base(null!, new ArmorCalculator(), new BlockCalculator())
    {
    }

    public void SetEquipmentStats(Guid characterId, Dictionary<StatType, double> stats)
    {
        _equipmentStatsCache[characterId] = stats;
    }

    public override Task<Dictionary<StatType, double>> CalculateEquipmentStatsAsync(Guid characterId)
    {
        if (_equipmentStatsCache.TryGetValue(characterId, out var stats))
        {
            return Task.FromResult(stats);
        }
        
        return Task.FromResult(new Dictionary<StatType, double>());
    }

    public override Task<double> CalculateBlockChanceAsync(Guid characterId, double characterStrength = 0)
    {
        // Return 0 for tests - simulates no shield equipped
        return Task.FromResult(0.0);
    }

    private readonly Dictionary<Guid, WeaponTypeInfo> _weaponTypeCache = new();

    public void SetWeaponType(Guid characterId, WeaponTypeInfo weaponInfo)
    {
        _weaponTypeCache[characterId] = weaponInfo;
    }

    public override Task<WeaponTypeInfo> GetEquippedWeaponTypeAsync(Guid characterId)
    {
        if (_weaponTypeCache.TryGetValue(characterId, out var weaponInfo))
        {
            return Task.FromResult(weaponInfo);
        }
        
        // Default: no weapon equipped
        return Task.FromResult(new WeaponTypeInfo
        {
            MainHandType = WeaponType.None,
            OffHandType = WeaponType.None,
            IsTwoHanded = false,
            IsDualWielding = false
        });
    }
}
