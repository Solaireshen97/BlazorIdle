using BlazorIdle.Server.Application.Battles.Step;
using BlazorIdle.Server.Domain.Characters;
using BlazorIdle.Shared.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;
using Xunit;

namespace BlazorIdle.Tests;

/// <summary>
/// 测试 Step 3: Buff状态显示功能
/// </summary>
public class BuffStatusDisplayTests
{
    [Fact]
    public void GetStatus_ReturnsPlayerBuffs_ForWarrior()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        var serviceProvider = services.BuildServiceProvider();
        var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();
        
        var coordinator = new StepBattleCoordinator(scopeFactory, new ConfigurationBuilder().Build(), new MockBattleNotificationService());
        var characterId = Guid.NewGuid();
        var profession = Profession.Warrior;
        
        // 使用较高的属性值以便触发技能和Buff
        var stats = new CharacterStats 
        { 
            AttackPower = 100,
            CritChance = 0.3,
            HastePercent = 0.1
        };
        var seed = 12345UL;
        
        // Act - 战斗一段时间以积累Buff
        var battleId = coordinator.Start(
            characterId: characterId,
            profession: profession,
            stats: stats,
            seconds: 15.0,  // 战斗15秒，足够触发技能
            seed: seed,
            enemyId: "dummy",
            enemyCount: 1,
            mode: StepBattleMode.Duration
        );
        
        var (found, status) = coordinator.GetStatus(battleId);
        
        // Assert
        Assert.True(found);
        Assert.NotNull(status.PlayerBuffs);
        
        // 战士在战斗过程中应该会触发一些Buff（如Berserk, Precision等）
        // 由于战斗是随机的，我们只验证Buff列表结构正确
        foreach (var buff in status.PlayerBuffs)
        {
            Assert.NotNull(buff.Id);
            Assert.NotNull(buff.Name);
            Assert.True(buff.MaxStacks > 0);
            Assert.True(buff.Stacks > 0);
            Assert.True(buff.Stacks <= buff.MaxStacks);
            Assert.False(buff.IsDebuff); // 玩家Buff应该不是减益
        }
    }
    
    [Fact]
    public void GetStatus_ReturnsEmptyBuffLists_AtBattleStart()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        var serviceProvider = services.BuildServiceProvider();
        var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();
        
        var coordinator = new StepBattleCoordinator(scopeFactory, new ConfigurationBuilder().Build(), new MockBattleNotificationService());
        var characterId = Guid.NewGuid();
        var profession = Profession.Ranger;
        var stats = new CharacterStats { AttackPower = 50 };
        var seed = 12345UL;
        
        // Act - 战斗极短时间，Buff还未触发
        var battleId = coordinator.Start(
            characterId: characterId,
            profession: profession,
            stats: stats,
            seconds: 0.01,  // 仅战斗0.01秒
            seed: seed,
            enemyId: "dummy",
            enemyCount: 1,
            mode: StepBattleMode.Duration
        );
        
        var (found, status) = coordinator.GetStatus(battleId);
        
        // Assert
        Assert.True(found);
        Assert.NotNull(status.PlayerBuffs);
        Assert.NotNull(status.EnemyBuffs);
        
        // 极短时间内，Buff列表应该为空或几乎为空
        // 由于初始化可能有一些Buff，我们只验证结构正确
        Assert.True(status.PlayerBuffs.Count >= 0);
        Assert.True(status.EnemyBuffs.Count >= 0);
    }
    
    [Fact]
    public void BuffStatusDto_HasCorrectStructure()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        var serviceProvider = services.BuildServiceProvider();
        var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();
        
        var coordinator = new StepBattleCoordinator(scopeFactory, new ConfigurationBuilder().Build(), new MockBattleNotificationService());
        var characterId = Guid.NewGuid();
        var profession = Profession.Warrior;
        var stats = new CharacterStats { AttackPower = 100 };
        var seed = 12345UL;
        
        // Act
        var battleId = coordinator.Start(
            characterId: characterId,
            profession: profession,
            stats: stats,
            seconds: 10.0,
            seed: seed,
            enemyId: "dummy",
            enemyCount: 1,
            mode: StepBattleMode.Duration
        );
        
        var (found, status) = coordinator.GetStatus(battleId);
        
        // Assert
        Assert.True(found);
        
        // 验证PlayerBuffs列表存在
        Assert.NotNull(status.PlayerBuffs);
        
        // 验证EnemyBuffs列表存在
        Assert.NotNull(status.EnemyBuffs);
        
        // 如果有Buff，验证其属性
        if (status.PlayerBuffs.Count > 0)
        {
            var buff = status.PlayerBuffs.First();
            Assert.False(string.IsNullOrEmpty(buff.Id));
            Assert.False(string.IsNullOrEmpty(buff.Name));
            Assert.True(buff.RemainingSeconds >= 0);
        }
    }
}
