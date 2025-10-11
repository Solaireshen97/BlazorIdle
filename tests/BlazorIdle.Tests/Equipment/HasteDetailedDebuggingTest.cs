using BlazorIdle.Server.Application.Battles;
using BlazorIdle.Server.Domain.Characters;
using BlazorIdle.Server.Domain.Combat.Enemies;
using BlazorIdle.Shared.Models;
using Xunit;
using Xunit.Abstractions;

namespace BlazorIdle.Tests.Equipment;

/// <summary>
/// 急速详细调试 - 通过模拟器查看实际战斗段
/// </summary>
public class HasteDetailedDebuggingTest
{
    private readonly ITestOutputHelper _output;

    public HasteDetailedDebuggingTest(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void CompareHasteBattles_DetailedOutput()
    {
        // Arrange
        var noHasteStats = new CharacterStats
        {
            AttackPower = 10.0,
            CritChance = 0.0,
            CritMultiplier = 2.0,
            HastePercent = 0.0 // 无急速
        };

        var hasteStats = new CharacterStats
        {
            AttackPower = 10.0,
            CritChance = 0.0,
            CritMultiplier = 2.0,
            HastePercent = 1.0 // 100% 急速
        };

        var config1 = new BattleSimulator.BattleConfig
        {
            BattleId = Guid.NewGuid(),
            CharacterId = Guid.NewGuid(),
            Profession = Profession.Warrior,
            Stats = noHasteStats,
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
            Seed = 11111UL,
            EnemyDef = EnemyRegistry.Resolve("dummy"),
            EnemyCount = 1,
            Mode = "duration"
        };

        var simulator = new BattleSimulator();

        // Act
        var result1 = simulator.RunForDuration(config1, 10.0);
        var result2 = simulator.RunForDuration(config2, 10.0);

        // Assert
        _output.WriteLine("=== 无急速战斗 ===");
        _output.WriteLine($"战斗总时长: {result1.Battle.EndedAt:F2}s");
        _output.WriteLine($"段数: {result1.Segments.Count}");
        _output.WriteLine($"总事件: {result1.Segments.Sum(s => s.EventCount)}");
        _output.WriteLine($"总伤害: {result1.Segments.Sum(s => s.TotalDamage)}");
        _output.WriteLine($"攻击间隔: {config1.Stats.HastePercent:P0} 急速");
        
        foreach (var seg in result1.Segments.Take(3))
        {
            _output.WriteLine($"  段 {seg.StartTime:F2}-{seg.EndTime:F2}: {seg.EventCount}事件, {seg.TotalDamage}伤害");
            if (seg.DamageBySource.ContainsKey("basic_attack"))
            {
                _output.WriteLine($"    基础攻击伤害: {seg.DamageBySource["basic_attack"]}");
            }
        }

        _output.WriteLine("\n=== 100%急速战斗 ===");
        _output.WriteLine($"战斗总时长: {result2.Battle.EndedAt:F2}s");
        _output.WriteLine($"段数: {result2.Segments.Count}");
        _output.WriteLine($"总事件: {result2.Segments.Sum(s => s.EventCount)}");
        _output.WriteLine($"总伤害: {result2.Segments.Sum(s => s.TotalDamage)}");
        _output.WriteLine($"攻击间隔: {config2.Stats.HastePercent:P0} 急速");
        
        foreach (var seg in result2.Segments.Take(3))
        {
            _output.WriteLine($"  段 {seg.StartTime:F2}-{seg.EndTime:F2}: {seg.EventCount}事件, {seg.TotalDamage}伤害");
            if (seg.DamageBySource.ContainsKey("basic_attack"))
            {
                _output.WriteLine($"    基础攻击伤害: {seg.DamageBySource["basic_attack"]}");
            }
        }

        _output.WriteLine("\n=== 差异分析 ===");
        _output.WriteLine($"事件比: {(double)result2.Segments.Sum(s => s.EventCount) / result1.Segments.Sum(s => s.EventCount):F2}x");
        _output.WriteLine($"伤害比: {(double)result2.Segments.Sum(s => s.TotalDamage) / result1.Segments.Sum(s => s.TotalDamage):F2}x");
    }
}
