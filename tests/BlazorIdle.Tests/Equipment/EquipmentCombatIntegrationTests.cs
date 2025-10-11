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

        // 注意：不能使用相同的seed，因为RNG会产生相同的暴击判定序列
        // 相反，我们运行多次测试并比较平均击杀时间
        var simulator = new BattleSimulator();
        var killTimes1 = new List<double>();
        var killTimes2 = new List<double>();

        // 运行10次战斗以获得统计意义
        for (int i = 0; i < 10; i++)
        {
            var config1 = new BattleSimulator.BattleConfig
            {
                BattleId = Guid.NewGuid(),
                CharacterId = Guid.NewGuid(),
                Profession = Profession.Warrior,
                Stats = baseStats,
                Seed = (ulong)(98765 + i * 1000),
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
                Seed = (ulong)(98765 + i * 1000),
                EnemyDef = EnemyRegistry.Resolve("dummy"),
                EnemyCount = 1,
                Mode = "duration"
            };

            var result1 = simulator.RunForDuration(config1, 30.0);
            var result2 = simulator.RunForDuration(config2, 30.0);

            if (result1.Killed && result1.KillTime.HasValue)
                killTimes1.Add(result1.KillTime.Value);
            if (result2.Killed && result2.KillTime.HasValue)
                killTimes2.Add(result2.KillTime.Value);
        }

        // Assert - 平均击杀时间应该更短（因为更高的暴击率）
        var avgKillTime1 = killTimes1.Average();
        var avgKillTime2 = killTimes2.Average();

        Assert.True(avgKillTime2 < avgKillTime1,
            $"更高暴击率应减少平均击杀时间。基础: {avgKillTime1:F2}s, 高暴击: {avgKillTime2:F2}s");
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

        // Assert - 急速应该减少击杀时间（攻击更频繁）
        Assert.True(result1.Killed && result2.Killed, "两场战斗都应该击杀敌人");
        Assert.NotNull(result1.KillTime);
        Assert.NotNull(result2.KillTime);
        
        Assert.True(result2.KillTime < result1.KillTime,
            $"急速提升后击杀时间应减少。基础: {result1.KillTime:F2}s, 高急速: {result2.KillTime:F2}s");

        // 验证击杀时间的减少幅度合理（应该接近急速百分比）
        var timeReduction = (result1.KillTime.Value - result2.KillTime.Value) / result1.KillTime.Value;
        Assert.True(timeReduction > 0.10 && timeReduction < 0.35,
            $"击杀时间减少应在10%-35%之间，实际: {timeReduction:P1}");
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
