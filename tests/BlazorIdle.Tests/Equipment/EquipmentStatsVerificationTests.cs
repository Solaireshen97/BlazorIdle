using BlazorIdle.Server.Application.Battles;
using BlazorIdle.Server.Domain.Characters;
using BlazorIdle.Server.Domain.Combat.Enemies;
using BlazorIdle.Shared.Models;
using Xunit;
using Xunit.Abstractions;

namespace BlazorIdle.Tests.Equipment;

/// <summary>
/// 装备属性验证测试 - 诊断Stats传递问题
/// </summary>
public class EquipmentStatsVerificationTests
{
    private readonly ITestOutputHelper _output;

    public EquipmentStatsVerificationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void Stats_ShouldBePassedCorrectly_ToBattleContext()
    {
        // Arrange - 创建具有明确属性的Stats
        var stats = new CharacterStats
        {
            AttackPower = 100.0,
            SpellPower = 50.0,
            CritChance = 0.3,  // 30% crit
            CritMultiplier = 2.5,
            HastePercent = 0.5, // 50% haste
            ArmorPenFlat = 10.0,
            ArmorPenPct = 0.1,
            MagicPenFlat = 5.0,
            MagicPenPct = 0.05
        };

        var config = new BattleSimulator.BattleConfig
        {
            BattleId = Guid.NewGuid(),
            CharacterId = Guid.NewGuid(),
            Profession = Profession.Warrior,
            Stats = stats,
            Seed = 12345UL,
            EnemyDef = EnemyRegistry.Resolve("dummy"),
            EnemyCount = 1,
            Mode = "duration"
        };

        var simulator = new BattleSimulator();

        // Act - 运行战斗
        var result = simulator.RunForDuration(config, 10.0);

        // Assert - 输出调试信息
        _output.WriteLine($"Input Stats:");
        _output.WriteLine($"  AttackPower: {stats.AttackPower}");
        _output.WriteLine($"  CritChance: {stats.CritChance:P1}");
        _output.WriteLine($"  HastePercent: {stats.HastePercent:P1}");
        
        _output.WriteLine($"\nBattle Results:");
        _output.WriteLine($"  Total Damage: {result.Segments.Sum(s => s.TotalDamage)}");
        _output.WriteLine($"  Total Events: {result.Segments.Sum(s => s.EventCount)}");
        _output.WriteLine($"  Duration: {result.Battle.EndedAt ?? 10.0:F2}s");
        
        // 检查段标签中是否有暴击记录
        var critCount = result.Segments
            .SelectMany(s => s.TagCounters)
            .Where(t => t.Key.StartsWith("crit:"))
            .Sum(t => t.Value);
        
        _output.WriteLine($"  Crit Count: {critCount}");
        
        // 基本验证：有30%暴击率，应该有一些暴击
        // 在10秒内，假设2.5秒一次攻击，约4次攻击，至少应该有机会出现暴击
        Assert.True(result.Segments.Any(), "应该有战斗段");
        Assert.True(result.Segments.Sum(s => s.TotalDamage) > 0, "应该造成伤害");
    }

    [Fact]
    public void HastePercent_ShouldAffectAttackSpeed()
    {
        // Arrange - 比较无急速和高急速的攻击次数
        // 使用极低的攻击力确保敌人不会快速死亡
        var baseStats = new CharacterStats
        {
            AttackPower = 1.0,   // 极低攻击力以延长战斗
            CritChance = 0.0,    // 禁用暴击以获得一致性
            CritMultiplier = 2.0,
            HastePercent = 0.0 // 无急速
        };

        var hasteStats = new CharacterStats
        {
            AttackPower = 1.0,   // 极低攻击力以延长战斗
            CritChance = 0.0,    // 禁用暴击以获得一致性
            CritMultiplier = 2.0,
            HastePercent = 1.0 // 100% 急速 (应该是2倍攻击速度)
        };

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
            Stats = hasteStats,
            Seed = 11111UL, // 同样的种子确保随机性一致
            EnemyDef = EnemyRegistry.Resolve("dummy"),
            EnemyCount = 1,
            Mode = "duration"
        };

        var simulator = new BattleSimulator();

        // Act - 使用更长的战斗时间确保有足够的攻击次数
        var result1 = simulator.RunForDuration(config1, 60.0);  // 60秒
        var result2 = simulator.RunForDuration(config2, 60.0);

        // Assert - 统计基础攻击次数而不是总事件数
        var attacks1 = result1.Segments
            .Where(s => s.DamageBySource.ContainsKey("basic_attack"))
            .Sum(s => s.DamageBySource["basic_attack"]) / 20; // 假设每次攻击约20伤害
        
        var attacks2 = result2.Segments
            .Where(s => s.DamageBySource.ContainsKey("basic_attack"))
            .Sum(s => s.DamageBySource["basic_attack"]) / 20;
            
        var damage1 = result1.Segments.Sum(s => s.TotalDamage);
        var damage2 = result2.Segments.Sum(s => s.TotalDamage);

        _output.WriteLine($"无急速: ~{attacks1}次攻击, {damage1}总伤害");
        _output.WriteLine($"100%急速: ~{attacks2}次攻击, {damage2}总伤害");
        _output.WriteLine($"攻击增长比: {(double)attacks2 / Math.Max(1, attacks1):F2}x");

        // 100%急速应该显著增加攻击次数（接近2倍）
        Assert.True(attacks2 > attacks1 * 1.5, 
            $"100%急速应该显著增加攻击次数。无急速: {attacks1}, 高急速: {attacks2}");
    }

    [Fact]
    public void CritChance_ShouldAffectDamage()
    {
        // Arrange - 比较低暴击和高暴击的伤害
        var lowCritStats = new CharacterStats
        {
            AttackPower = 50.0,
            CritChance = 0.05, // 5% 暴击
            CritMultiplier = 2.0,
            HastePercent = 0.0
        };

        var highCritStats = new CharacterStats
        {
            AttackPower = 50.0,
            CritChance = 0.5, // 50% 暴击
            CritMultiplier = 2.0,
            HastePercent = 0.0
        };

        var config1 = new BattleSimulator.BattleConfig
        {
            BattleId = Guid.NewGuid(),
            CharacterId = Guid.NewGuid(),
            Profession = Profession.Warrior,
            Stats = lowCritStats,
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
            Stats = highCritStats,
            Seed = 98765UL,
            EnemyDef = EnemyRegistry.Resolve("dummy"),
            EnemyCount = 1,
            Mode = "duration"
        };

        var simulator = new BattleSimulator();

        // Act - 运行较长时间以获得统计意义
        var result1 = simulator.RunForDuration(config1, 30.0);
        var result2 = simulator.RunForDuration(config2, 30.0);

        // Assert
        var damage1 = result1.Segments.Sum(s => s.TotalDamage);
        var damage2 = result2.Segments.Sum(s => s.TotalDamage);

        var crit1 = result1.Segments
            .SelectMany(s => s.TagCounters)
            .Where(t => t.Key.StartsWith("crit:"))
            .Sum(t => t.Value);
            
        var crit2 = result2.Segments
            .SelectMany(s => s.TagCounters)
            .Where(t => t.Key.StartsWith("crit:"))
            .Sum(t => t.Value);

        _output.WriteLine($"5%暴击:");
        _output.WriteLine($"  总伤害: {damage1}");
        _output.WriteLine($"  暴击次数: {crit1}");
        
        _output.WriteLine($"50%暴击:");
        _output.WriteLine($"  总伤害: {damage2}");
        _output.WriteLine($"  暴击次数: {crit2}");
        
        _output.WriteLine($"伤害增长比: {(double)damage2 / damage1:F2}x");
        _output.WriteLine($"暴击增长比: {(double)crit2 / Math.Max(1, crit1):F2}x");

        // 50%暴击应该显著多于5%暴击
        Assert.True(crit2 > crit1 * 3, 
            $"50%暴击率应该产生更多暴击。5%暴击: {crit1}, 50%暴击: {crit2}");
        
        // 更多暴击应该导致更高伤害
        Assert.True(damage2 > damage1, 
            $"更高暴击率应该造成更多伤害。5%暴击: {damage1}, 50%暴击: {damage2}");
    }
}
