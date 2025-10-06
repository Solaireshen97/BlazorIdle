using BlazorIdle.Server.Application.Battles;
using BlazorIdle.Server.Domain.Characters;
using BlazorIdle.Server.Domain.Combat;
using BlazorIdle.Server.Domain.Combat.Enemies;
using BlazorIdle.Server.Domain.Combat.Rng;
using BlazorIdle.Shared.Models;
using System;
using Xunit;

public class BattleSimulatorTests
{
    [Fact]
    public void BattleSimulator_RunForDuration_ProducesSameResultsAsBefore()
    {
        // Arrange
        var simulator = new BattleSimulator();
        var seed = 12345UL;
        var battleId = Guid.NewGuid();
        var characterId = Guid.NewGuid();
        
        var config = new BattleSimulator.BattleConfig
        {
            BattleId = battleId,
            CharacterId = characterId,
            Profession = Profession.Warrior,
            Stats = new CharacterStats(),
            Seed = seed,
            EnemyDef = EnemyRegistry.Resolve("dummy"),
            EnemyCount = 1,
            Mode = "duration"
        };
        
        // Act
        var result = simulator.RunForDuration(config, 10.0);
        
        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result.Segments);
        Assert.Equal(battleId, result.Battle.Id);
        Assert.True(result.Battle.EndedAt > 0);
    }
    
    [Fact]
    public void BattleSimulator_CreateRunningBattle_CreatesValidInstance()
    {
        // Arrange
        var simulator = new BattleSimulator();
        var seed = 54321UL;
        var battleId = Guid.NewGuid();
        var characterId = Guid.NewGuid();
        
        var config = new BattleSimulator.BattleConfig
        {
            BattleId = battleId,
            CharacterId = characterId,
            Profession = Profession.Ranger,
            Stats = new CharacterStats(),
            Seed = seed,
            EnemyDef = EnemyRegistry.Resolve("dummy"),
            EnemyCount = 1,
            Mode = "continuous"
        };
        
        // Act
        var runningBattle = simulator.CreateRunningBattle(config, 20.0);
        
        // Assert
        Assert.NotNull(runningBattle);
        Assert.Equal(battleId, runningBattle.Id);
        Assert.Equal(characterId, runningBattle.CharacterId);
        Assert.Equal(Profession.Ranger, runningBattle.Profession);
    }
}
