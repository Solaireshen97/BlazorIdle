using BlazorIdle.Server.Application.Battles;
using BlazorIdle.Server.Domain.Characters;
using BlazorIdle.Server.Domain.Combat;
using BlazorIdle.Server.Domain.Combat.Enemies;
using BlazorIdle.Server.Domain.Combat.Rng;
using BlazorIdle.Server.Domain.Equipment.Models;
using BlazorIdle.Server.Domain.Equipment.Services;
using BlazorIdle.Shared.Models;
using Xunit;

namespace BlazorIdle.Tests.Equipment;

/// <summary>
/// 装备战斗集成测试 - Phase 3完成验证
/// 验证装备属性正确影响战斗结果
/// </summary>
public class EquipmentCombatIntegrationTests
{
    [Fact]
    public void EquipmentWithAttackPower_ShouldIncreaseDamage()
    {
        // Arrange - 创建两个相同角色，一个有装备加成
        var baseStats = BuildBaseCharacterStats();
        var statsWithEquipment = BuildStatsWithEquipment(baseStats, attackPowerBonus: 50);

        var config1 = new BattleSimulator.BattleConfig
        {
            BattleId = Guid.NewGuid(),
            CharacterId = Guid.NewGuid(),
            Profession = Profession.Warrior,
            Stats = baseStats,
            Seed = 12345UL,
            EnemyDef = EnemyRegistry.Resolve("dummy"),
            EnemyCount = 1,
            Mode = "duration"
        };

        var config2 = new BattleSimulator.BattleConfig
        {
            BattleId = Guid.NewGuid(),
            CharacterId = Guid.NewGuid(),
            Profession = Profession.Warrior,
            Stats = statsWithEquipment,
            Seed = 12345UL, // 相同种子确保可比性
            EnemyDef = EnemyRegistry.Resolve("dummy"),
            EnemyCount = 1,
            Mode = "duration"
        };

        var simulator = new BattleSimulator();

        // Act - 运行两场相同条件的战斗
        var result1 = simulator.RunForDuration(config1, 10.0);
        var result2 = simulator.RunForDuration(config2, 10.0);

        // Assert - 有装备加成的角色应该造成更多伤害
        var damage1 = result1.Segments.Sum(s => s.TotalDamage);
        var damage2 = result2.Segments.Sum(s => s.TotalDamage);

        Assert.True(damage2 > damage1, 
            $"装备增加攻击力后应造成更多伤害。无装备: {damage1}, 有装备: {damage2}");

        // 验证伤害增幅合理（应该显著增加，但不是无限增加）
        var damageIncrease = (damage2 - damage1) / (double)damage1;
        Assert.True(damageIncrease > 0.1 && damageIncrease < 2.0,
            $"伤害增幅应在10%-200%之间，实际增幅: {damageIncrease:P2}");
    }

    [Fact]
    public void EquipmentWithCritChance_ShouldIncreaseOverallDamage()
    {
        // Arrange
        var baseStats = BuildBaseCharacterStats();
        var statsWithCrit = BuildStatsWithEquipment(baseStats, critChanceBonus: 0.2); // +20%暴击

        var config1 = new BattleSimulator.BattleConfig
        {
            BattleId = Guid.NewGuid(),
            CharacterId = Guid.NewGuid(),
            Profession = Profession.Warrior,
            Stats = baseStats,
            Seed = 98765UL,
            EnemyDef = EnemyRegistry.Resolve("dummy"),
            EnemyCount = 1,
            Mode = "duration"
        };

        var config2 = new BattleSimulator.BattleConfig
        {
            BattleId = Guid.NewGuid(),
            CharacterId = Guid.NewGuid(),
            Profession = Profession.Warrior,
            Stats = statsWithCrit,
            Seed = 98765UL,
            EnemyDef = EnemyRegistry.Resolve("dummy"),
            EnemyCount = 1,
            Mode = "duration"
        };

        var simulator = new BattleSimulator();

        // Act - 运行较长时间以确保暴击统计有意义
        var result1 = simulator.RunForDuration(config1, 30.0);
        var result2 = simulator.RunForDuration(config2, 30.0);

        // Assert
        var damage1 = result1.Segments.Sum(s => s.TotalDamage);
        var damage2 = result2.Segments.Sum(s => s.TotalDamage);

        Assert.True(damage2 > damage1,
            $"增加暴击率后应造成更多伤害。基础: {damage1}, 高暴击: {damage2}");
    }

    [Fact]
    public void EquipmentWithHaste_ShouldIncreaseAttackFrequency()
    {
        // Arrange
        var baseStats = BuildBaseCharacterStats();
        var statsWithHaste = BuildStatsWithEquipment(baseStats, hasteBonus: 0.25); // +25%急速

        var config1 = new BattleSimulator.BattleConfig
        {
            BattleId = Guid.NewGuid(),
            CharacterId = Guid.NewGuid(),
            Profession = Profession.Warrior,
            Stats = baseStats,
            Seed = 11111UL,
            EnemyDef = EnemyRegistry.Resolve("dummy"),
            EnemyCount = 1,
            Mode = "duration"
        };

        var config2 = new BattleSimulator.BattleConfig
        {
            BattleId = Guid.NewGuid(),
            CharacterId = Guid.NewGuid(),
            Profession = Profession.Warrior,
            Stats = statsWithHaste,
            Seed = 11111UL,
            EnemyDef = EnemyRegistry.Resolve("dummy"),
            EnemyCount = 1,
            Mode = "duration"
        };

        var simulator = new BattleSimulator();

        // Act
        var result1 = simulator.RunForDuration(config1, 20.0);
        var result2 = simulator.RunForDuration(config2, 20.0);

        // Assert - 急速提升应增加总事件数（攻击更频繁）
        var events1 = result1.Segments.Sum(s => s.EventCount);
        var events2 = result2.Segments.Sum(s => s.EventCount);

        Assert.True(events2 > events1,
            $"急速提升后应产生更多战斗事件。基础: {events1}, 高急速: {events2}");

        // 同时总伤害也应该增加
        var damage1 = result1.Segments.Sum(s => s.TotalDamage);
        var damage2 = result2.Segments.Sum(s => s.TotalDamage);

        Assert.True(damage2 > damage1,
            $"急速提升后总伤害应增加。基础: {damage1}, 高急速: {damage2}");
    }

    /// <summary>
    /// 构建基础角色属性（无装备加成）
    /// </summary>
    private CharacterStats BuildBaseCharacterStats()
    {
        return new CharacterStats
        {
            AttackPower = 50.0,
            SpellPower = 0.0,
            CritChance = 0.05,
            CritMultiplier = 2.0,
            HastePercent = 0.0,
            ArmorPenFlat = 0.0,
            ArmorPenPct = 0.0,
            MagicPenFlat = 0.0,
            MagicPenPct = 0.0
        };
    }

    /// <summary>
    /// 构建带装备加成的角色属性
    /// </summary>
    private CharacterStats BuildStatsWithEquipment(
        CharacterStats baseStats,
        double attackPowerBonus = 0,
        double critChanceBonus = 0,
        double hasteBonus = 0)
    {
        return new CharacterStats
        {
            AttackPower = baseStats.AttackPower + attackPowerBonus,
            SpellPower = baseStats.SpellPower,
            CritChance = Math.Min(1.0, baseStats.CritChance + critChanceBonus), // Clamp to 100%
            CritMultiplier = baseStats.CritMultiplier,
            HastePercent = baseStats.HastePercent + hasteBonus,
            ArmorPenFlat = baseStats.ArmorPenFlat,
            ArmorPenPct = baseStats.ArmorPenPct,
            MagicPenFlat = baseStats.MagicPenFlat,
            MagicPenPct = baseStats.MagicPenPct
        };
    }
}
