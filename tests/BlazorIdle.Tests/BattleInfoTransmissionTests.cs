using BlazorIdle.Server.Application.Battles.Step;
using BlazorIdle.Server.Domain.Characters;
using BlazorIdle.Server.Domain.Combat.Enemies;
using BlazorIdle.Shared.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using Xunit;

namespace BlazorIdle.Tests;

public class BattleInfoTransmissionTests
{
    [Fact]
    public void GetStatus_ReturnsPlayerMaxHp_BasedOnStamina()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        var serviceProvider = services.BuildServiceProvider();
        var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();
        
        var coordinator = new StepBattleCoordinator(scopeFactory, new ConfigurationBuilder().Build());
        var characterId = Guid.NewGuid();
        var profession = Profession.Warrior;
        var stats = new CharacterStats();
        var seed = 12345UL;
        var stamina = 20; // 耐力值
        
        // Act
        var battleId = coordinator.Start(
            characterId: characterId,
            profession: profession,
            stats: stats,
            seconds: 5.0,
            seed: seed,
            enemyId: "dummy",
            enemyCount: 1,
            mode: StepBattleMode.Duration,
            dungeonId: null,
            stamina: stamina
        );
        
        var (found, status) = coordinator.GetStatus(battleId);
        
        // Assert
        Assert.True(found);
        Assert.Equal(200, status.PlayerMaxHp); // 20 * 10 = 200
        Assert.Equal(1.0, status.PlayerHpPercent); // 玩家始终满血
    }
    
    [Fact]
    public void GetStatus_ReturnsEnemyHealthList_ForMultipleEnemies()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        var serviceProvider = services.BuildServiceProvider();
        var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();
        
        var coordinator = new StepBattleCoordinator(scopeFactory, new ConfigurationBuilder().Build());
        var characterId = Guid.NewGuid();
        var profession = Profession.Warrior;
        var stats = new CharacterStats { AttackPower = 50 };
        var seed = 12345UL;
        var enemyCount = 3;
        
        // Act
        var battleId = coordinator.Start(
            characterId: characterId,
            profession: profession,
            stats: stats,
            seconds: 0.1,
            seed: seed,
            enemyId: "dummy",
            enemyCount: enemyCount,
            mode: StepBattleMode.Duration
        );
        
        var (found, status) = coordinator.GetStatus(battleId);
        
        // Assert
        Assert.True(found);
        Assert.NotNull(status.Enemies);
        Assert.Equal(enemyCount, status.Enemies.Count);
        
        foreach (var enemy in status.Enemies)
        {
            Assert.NotEmpty(enemy.EnemyId);
            Assert.NotEmpty(enemy.EnemyName);
            Assert.True(enemy.MaxHp > 0);
            Assert.True(enemy.CurrentHp >= 0);
            Assert.True(enemy.CurrentHp <= enemy.MaxHp);
            Assert.InRange(enemy.HpPercent, 0.0, 1.0);
        }
    }
    
    [Fact]
    public void GetStatus_ReturnsAttackProgressInfo()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        var serviceProvider = services.BuildServiceProvider();
        var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();
        
        var coordinator = new StepBattleCoordinator(scopeFactory, new ConfigurationBuilder().Build());
        var characterId = Guid.NewGuid();
        var profession = Profession.Warrior;
        var stats = new CharacterStats { AttackPower = 50 };
        var seed = 12345UL;
        
        // Act
        var battleId = coordinator.Start(
            characterId: characterId,
            profession: profession,
            stats: stats,
            seconds: 5.0,
            seed: seed,
            enemyId: "dummy",
            enemyCount: 1,
            mode: StepBattleMode.Duration
        );
        
        var (found, status) = coordinator.GetStatus(battleId);
        
        // Assert
        Assert.True(found);
        Assert.NotNull(status.NextAttackAt);
        Assert.NotNull(status.NextSpecialAt);
        Assert.True(status.NextAttackAt >= 0); // Can be 0 at start
        Assert.True(status.NextSpecialAt >= 0); // Can be 0 at start
        Assert.True(status.CurrentTime >= 0);
    }
    
    [Fact]
    public void GetStatus_DungeonMode_ReturnsWaveAndMonsterCount()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        var serviceProvider = services.BuildServiceProvider();
        var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();
        
        var coordinator = new StepBattleCoordinator(scopeFactory, new ConfigurationBuilder().Build());
        var characterId = Guid.NewGuid();
        var profession = Profession.Warrior;
        var stats = new CharacterStats { AttackPower = 50 };
        var seed = 12345UL;
        
        // Act
        var battleId = coordinator.Start(
            characterId: characterId,
            profession: profession,
            stats: stats,
            seconds: 1.0,
            seed: seed,
            enemyId: null,
            enemyCount: 1,
            mode: StepBattleMode.DungeonSingle,
            dungeonId: "intro_cave"
        );
        
        var (found, status) = coordinator.GetStatus(battleId);
        
        // Assert
        Assert.True(found);
        Assert.Equal("dungeonsingle", status.Mode);
        Assert.NotNull(status.WaveIndex);
        Assert.NotNull(status.RunCount);
        Assert.NotEmpty(status.Enemies);
    }
}
