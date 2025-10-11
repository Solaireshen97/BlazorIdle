using BlazorIdle.Server.Application.Battles;
using BlazorIdle.Server.Domain.Characters;
using BlazorIdle.Server.Domain.Combat.Enemies;
using BlazorIdle.Shared.Models;
using Xunit;
using Xunit.Abstractions;

namespace BlazorIdle.Tests.Equipment;

/// <summary>
/// 调试急速问题的测试
/// </summary>
public class HasteDebugTest
{
    private readonly ITestOutputHelper _output;

    public HasteDebugTest(ITestOutputHelper output)
    {
        _output = output;
    }

    [Theory]
    [InlineData(0.0, "No Haste")]
    [InlineData(0.25, "25% Haste")]
    [InlineData(0.5, "50% Haste")]
    public void TestHasteEffect_ShowsIndividualResults(double hastePercent, string label)
    {
        // Arrange
        var stats = new CharacterStats
        {
            AttackPower = 50.0,
            HastePercent = hastePercent
        };

        var config = new BattleSimulator.BattleConfig
        {
            BattleId = Guid.NewGuid(),
            CharacterId = Guid.NewGuid(),
            Profession = Profession.Warrior,
            Stats = stats,
            Seed = 11111UL,
            EnemyDef = EnemyRegistry.Resolve("dummy"),
            EnemyCount = 1,
            Mode = "duration"
        };

        var simulator = new BattleSimulator();

        // Act
        _output.WriteLine($"Running battle with {label} ({hastePercent:P0})...");
        var result = simulator.RunForDuration(config, 20.0);

        // Output results
        _output.WriteLine($"Results:");
        _output.WriteLine($"  Event Count: {result.Segments.Sum(s => s.EventCount)}");
        _output.WriteLine($"  Total Damage: {result.Segments.Sum(s => s.TotalDamage)}");
        _output.WriteLine($"  Killed: {result.Killed}");
        _output.WriteLine($"  KillTime: {result.KillTime}");
        
        // This test always passes - it's just for observation
        Assert.True(true);
    }
    
    [Fact]
    public void TestHasteEffect()
    {
        // Arrange
        var statsNoHaste = new CharacterStats
        {
            AttackPower = 50.0,
            HastePercent = 0.0 // No haste
        };

        var statsWithHaste = new CharacterStats
        {
            AttackPower = 50.0,
            HastePercent = 0.25 // +25% haste
        };

        var config1 = new BattleSimulator.BattleConfig
        {
            BattleId = Guid.NewGuid(),
            CharacterId = Guid.NewGuid(),
            Profession = Profession.Warrior,
            Stats = statsNoHaste,
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
        _output.WriteLine("Running battle without haste...");
        var result1 = simulator.RunForDuration(config1, 20.0);
        
        _output.WriteLine("Running battle with haste...");
        var result2 = simulator.RunForDuration(config2, 20.0);

        // Output results
        _output.WriteLine($"\nNo Haste Results:");
        _output.WriteLine($"  Event Count: {result1.Segments.Sum(s => s.EventCount)}");
        _output.WriteLine($"  Total Damage: {result1.Segments.Sum(s => s.TotalDamage)}");
        _output.WriteLine($"  Killed: {result1.Killed}, KillTime: {result1.KillTime}");

        _output.WriteLine($"\nWith Haste Results:");
        _output.WriteLine($"  Event Count: {result2.Segments.Sum(s => s.EventCount)}");
        _output.WriteLine($"  Total Damage: {result2.Segments.Sum(s => s.TotalDamage)}");
        _output.WriteLine($"  Killed: {result2.Killed}, KillTime: {result2.KillTime}");

        // Calculate expected kill time reduction
        // With 25% haste, attacks should be 25% faster
        // So kill time should be reduced by approximately 20%
        
        Assert.True(result1.Killed && result2.Killed, "Both battles should kill the enemy");
        Assert.NotNull(result1.KillTime);
        Assert.NotNull(result2.KillTime);
        
        var killTimeReduction = (result1.KillTime.Value - result2.KillTime.Value) / result1.KillTime.Value;
        _output.WriteLine($"\nKill time reduction: {killTimeReduction:P1}");
        _output.WriteLine($"Expected reduction: ~20%");

        // Assert - haste should reduce kill time
        Assert.True(result2.KillTime < result1.KillTime,
            $"Haste should reduce kill time. No haste: {result1.KillTime:F2}s, With haste: {result2.KillTime:F2}s");
    }
}
