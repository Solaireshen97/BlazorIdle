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
        _output.WriteLine($"  Segments: {result1.Segments.Count}");

        _output.WriteLine($"\nWith Haste Results:");
        _output.WriteLine($"  Event Count: {result2.Segments.Sum(s => s.EventCount)}");
        _output.WriteLine($"  Total Damage: {result2.Segments.Sum(s => s.TotalDamage)}");
        _output.WriteLine($"  Segments: {result2.Segments.Count}");

        // Calculate expected attack frequency
        // Base attack interval for Warrior (from ProfessionRegistry)
        // With 25% haste, attacks should be 25% faster
        // So in 20 seconds, we should get ~25% more attacks
        
        var events1 = result1.Segments.Sum(s => s.EventCount);
        var events2 = result2.Segments.Sum(s => s.EventCount);
        
        _output.WriteLine($"\nEvent increase: {((events2 - events1) / (double)events1):P1}");
        _output.WriteLine($"Expected increase: ~25%");

        // Assert
        Assert.True(events2 > events1,
            $"Haste should increase event count. No haste: {events1}, With haste: {events2}");
    }
}
