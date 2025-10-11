using BlazorIdle.Server.Domain.Characters;
using BlazorIdle.Server.Domain.Combat.Combatants;
using BlazorIdle.Server.Domain.Combat.Damage;
using BlazorIdle.Server.Domain.Equipment.Models;
using BlazorIdle.Server.Domain.Equipment.Services;
using BlazorIdle.Shared.Models;
using Xunit;

namespace BlazorIdle.Tests.Equipment;

/// <summary>
/// 护甲系统集成测试
/// 验证护甲值正确集成到战斗系统并减免伤害
/// </summary>
public class ArmorIntegrationTests
{
    /// <summary>
    /// 测试无护甲时受到全额伤害
    /// </summary>
    [Fact]
    public void PlayerWithoutArmor_ShouldTakeFullDamage()
    {
        // Arrange - 创建无护甲的玩家
        var stats = new CharacterStats
        {
            AttackPower = 50,
            Armor = 0 // 无护甲
        };
        var player = new PlayerCombatant("player1", "Test Player", stats, stamina: 10);

        int baseDamage = 100;
        int enemyLevel = 5;

        // Act - 受到物理伤害
        var actualDamage = player.ReceiveDamage(baseDamage, DamageType.Physical, 0.0, enemyLevel);

        // Assert - 应受到全额伤害
        Assert.Equal(100, actualDamage);
    }

    /// <summary>
    /// 测试有护甲时物理伤害被减免
    /// </summary>
    [Fact]
    public void PlayerWithArmor_ShouldReducePhysicalDamage()
    {
        // Arrange - 创建有护甲的玩家
        var stats = new CharacterStats
        {
            AttackPower = 50,
            Armor = 400 // 400点护甲
        };
        var player = new PlayerCombatant("player1", "Test Player", stats, stamina: 10);

        int baseDamage = 100;
        int enemyLevel = 5; // 等级5的敌人

        // Act - 受到物理伤害
        var actualDamage = player.ReceiveDamage(baseDamage, DamageType.Physical, 0.0, enemyLevel);

        // Assert - 伤害应被减免
        // 公式: 400 / (400 + 50 * 5 + 400) = 400 / 1050 ≈ 0.381 (38.1%减伤)
        // 实际伤害 ≈ 100 * (1 - 0.381) = 61.9 → 62
        Assert.True(actualDamage < baseDamage, $"应减免部分伤害，实际: {actualDamage}，基础: {baseDamage}");
        Assert.InRange(actualDamage, 60, 63); // 允许一定的舍入误差
    }

    /// <summary>
    /// 测试护甲不影响魔法伤害
    /// </summary>
    [Fact]
    public void PlayerArmor_ShouldNotReduceMagicDamage()
    {
        // Arrange - 创建有护甲的玩家
        var stats = new CharacterStats
        {
            AttackPower = 50,
            Armor = 400 // 400点护甲
        };
        var player = new PlayerCombatant("player1", "Test Player", stats, stamina: 10);

        int baseDamage = 100;
        int enemyLevel = 5;

        // Act - 受到魔法伤害
        var actualDamage = player.ReceiveDamage(baseDamage, DamageType.Magic, 0.0, enemyLevel);

        // Assert - 护甲不减免魔法伤害
        Assert.Equal(100, actualDamage);
    }

    /// <summary>
    /// 测试护甲不影响真实伤害
    /// </summary>
    [Fact]
    public void PlayerArmor_ShouldNotReduceTrueDamage()
    {
        // Arrange - 创建有护甲的玩家
        var stats = new CharacterStats
        {
            AttackPower = 50,
            Armor = 400
        };
        var player = new PlayerCombatant("player1", "Test Player", stats, stamina: 10);

        int baseDamage = 100;
        int enemyLevel = 5;

        // Act - 受到真实伤害
        var actualDamage = player.ReceiveDamage(baseDamage, DamageType.True, 0.0, enemyLevel);

        // Assert - 护甲不减免真实伤害
        Assert.Equal(100, actualDamage);
    }

    /// <summary>
    /// 测试高护甲有减伤上限（75%）
    /// </summary>
    [Fact]
    public void PlayerWithHighArmor_ShouldHaveReductionCap()
    {
        // Arrange - 创建超高护甲的玩家
        var stats = new CharacterStats
        {
            AttackPower = 50,
            Armor = 10000 // 极高护甲
        };
        var player = new PlayerCombatant("player1", "Test Player", stats, stamina: 10);

        int baseDamage = 100;
        int enemyLevel = 5;

        // Act - 受到物理伤害
        var actualDamage = player.ReceiveDamage(baseDamage, DamageType.Physical, 0.0, enemyLevel);

        // Assert - 最多减免75%，至少受到25伤害
        Assert.True(actualDamage >= 25, $"减伤不应超过75%，实际伤害: {actualDamage}");
        Assert.InRange(actualDamage, 25, 26); // 25 + 可能的舍入误差
    }

    /// <summary>
    /// 测试不同护甲类型的护甲值计算
    /// </summary>
    [Fact]
    public void ArmorCalculator_ShouldCalculateDifferentArmorTypes()
    {
        // Arrange
        var calculator = new ArmorCalculator();
        int itemLevel = 50;
        var slot = EquipmentSlot.Chest; // 胸甲槽位系数1.5

        // Act & Assert
        var clothArmor = calculator.CalculateArmorValue(ArmorType.Cloth, slot, itemLevel);
        var leatherArmor = calculator.CalculateArmorValue(ArmorType.Leather, slot, itemLevel);
        var mailArmor = calculator.CalculateArmorValue(ArmorType.Mail, slot, itemLevel);
        var plateArmor = calculator.CalculateArmorValue(ArmorType.Plate, slot, itemLevel);

        // 布甲 < 皮甲 < 锁甲 < 板甲
        Assert.True(clothArmor < leatherArmor, "布甲护甲值应小于皮甲");
        Assert.True(leatherArmor < mailArmor, "皮甲护甲值应小于锁甲");
        Assert.True(mailArmor < plateArmor, "锁甲护甲值应小于板甲");

        // 验证系数比例 (0.5 : 1.0 : 1.5 : 2.0)
        Assert.Equal(leatherArmor * 0.5, clothArmor, precision: 1);
        Assert.Equal(leatherArmor * 1.5, mailArmor, precision: 1);
        Assert.Equal(leatherArmor * 2.0, plateArmor, precision: 1);
    }

    /// <summary>
    /// 测试盾牌提供护甲值
    /// </summary>
    [Fact]
    public void ArmorCalculator_ShieldShouldProvideArmor()
    {
        // Arrange
        var calculator = new ArmorCalculator();
        int itemLevel = 50;

        // Act
        var shieldArmor = calculator.CalculateShieldArmorValue(itemLevel);
        var leatherChestArmor = calculator.CalculateArmorValue(ArmorType.Leather, EquipmentSlot.Chest, itemLevel);

        // Assert - 盾牌应该提供可观的护甲值（系数2.25，相当于1.5倍的皮甲胸甲）
        Assert.Equal(itemLevel * 2.25, shieldArmor);
        Assert.True(shieldArmor > leatherChestArmor, "盾牌护甲应高于皮甲胸甲");
    }

    /// <summary>
    /// 测试装备属性集成服务正确应用护甲
    /// </summary>
    [Fact]
    public async Task EquipmentStatsIntegration_ShouldApplyArmorToStats()
    {
        // Arrange
        var fakeAggregationService = new FakeStatsAggregationServiceWithArmor(500); // 返回500护甲
        var integration = new EquipmentStatsIntegration(fakeAggregationService);

        var primaryAttrs = new PrimaryAttributes(10, 10, 10, 10);

        // Act
        var stats = await integration.BuildStatsWithEquipmentAsync(
            Guid.NewGuid(),
            Profession.Warrior,
            primaryAttrs
        );

        // Assert
        Assert.Equal(500, stats.Armor);
    }
}

/// <summary>
/// 测试用的 Fake 服务，返回指定的护甲值
/// </summary>
public class FakeStatsAggregationServiceWithArmor : StatsAggregationService
{
    private readonly double _armorValue;

    public FakeStatsAggregationServiceWithArmor(double armorValue)
        : base(null!, null!, null!)
    {
        _armorValue = armorValue;
    }

    public override Task<Dictionary<StatType, double>> CalculateEquipmentStatsAsync(Guid characterId)
    {
        var stats = new Dictionary<StatType, double>
        {
            { StatType.Armor, _armorValue }
        };
        return Task.FromResult(stats);
    }
}
