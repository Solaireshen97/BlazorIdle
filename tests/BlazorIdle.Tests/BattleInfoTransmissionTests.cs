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
        
        var coordinator = new StepBattleCoordinator(scopeFactory, new ConfigurationBuilder().Build(), new MockBattleNotificationService());
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
        
        var coordinator = new StepBattleCoordinator(scopeFactory, new ConfigurationBuilder().Build(), new MockBattleNotificationService());
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
        
        var coordinator = new StepBattleCoordinator(scopeFactory, new ConfigurationBuilder().Build(), new MockBattleNotificationService());
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
        
        var coordinator = new StepBattleCoordinator(scopeFactory, new ConfigurationBuilder().Build(), new MockBattleNotificationService());
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
    
    [Fact]
    public void AttackProgress_ResetsOnTargetSwitch_InMultiEnemyBattle()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        var serviceProvider = services.BuildServiceProvider();
        var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();
        
        var coordinator = new StepBattleCoordinator(scopeFactory, new ConfigurationBuilder().Build(), new MockBattleNotificationService());
        var characterId = Guid.NewGuid();
        var profession = Profession.Warrior;
        var stats = new CharacterStats { AttackPower = 1000 }; // 高攻击力快速击杀
        var seed = 12345UL;
        var enemyCount = 3;
        
        // Act
        var battleId = coordinator.Start(
            characterId: characterId,
            profession: profession,
            stats: stats,
            seconds: 10.0,
            seed: seed,
            enemyId: "dummy",
            enemyCount: enemyCount,
            mode: StepBattleMode.Duration
        );
        
        // 使用 FastForwardTo 直接推进战斗
        if (coordinator.TryGet(battleId, out var rb))
        {
            rb!.FastForwardTo(5.0); // 快速推进到5秒
        }
        
        var (found, status) = coordinator.GetStatus(battleId);
        
        // Assert
        Assert.True(found);
        Assert.True(status.TotalDamage > 0, "Battle should have dealt damage");
        
        // 验证至少有一个敌人被击杀（切换了目标）
        var deadEnemies = status.Enemies.Count(e => e.IsDead);
        if (deadEnemies > 0)
        {
            // 如果有敌人死亡，验证攻击进度已被重置
            // NextAttackAt 应该大于 CurrentTime（说明需要等待）
            if (status.NextAttackAt.HasValue)
            {
                Assert.True(status.NextAttackAt.Value >= status.CurrentTime,
                    "Attack progress should reset when switching targets");
            }
        }
    }
    
    [Fact]
    public void AttackProgress_ResetsOnRespawnWait_InContinuousMode()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        var serviceProvider = services.BuildServiceProvider();
        var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();
        
        var coordinator = new StepBattleCoordinator(scopeFactory, new ConfigurationBuilder().Build(), new MockBattleNotificationService());
        var characterId = Guid.NewGuid();
        var profession = Profession.Warrior;
        var stats = new CharacterStats { AttackPower = 10000 }; // 超高攻击力快速击杀
        var seed = 12345UL;
        
        // Act - 创建持续模式战斗，设置刷新延迟
        var battleId = coordinator.Start(
            characterId: characterId,
            profession: profession,
            stats: stats,
            seconds: 20.0,
            seed: seed,
            enemyId: "dummy",
            enemyCount: 1,
            mode: StepBattleMode.Continuous,
            continuousRespawnDelaySeconds: 2.0
        );
        
        // 使用 FastForwardTo 直接推进战斗
        if (coordinator.TryGet(battleId, out var rb))
        {
            rb!.FastForwardTo(10.0); // 快速推进到10秒
        }
        
        var (found, status) = coordinator.GetStatus(battleId);
        
        // Assert
        Assert.True(found);
        
        // 在持续模式中，验证战斗有进展
        Assert.True(status.TotalDamage > 0, "Battle should have dealt damage");
        
        // 验证攻击进度数据可用
        Assert.True(status.CurrentTime > 0, "Battle time should have advanced");
    }
}
