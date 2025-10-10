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
/// 测试 Step 4: 技能系统UI功能
/// </summary>
public class SkillStatusDisplayTests
{
    [Fact]
    public void GetStatus_ReturnsSkills_ForWarrior()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        var serviceProvider = services.BuildServiceProvider();
        var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();
        
        var coordinator = new StepBattleCoordinator(scopeFactory, new ConfigurationBuilder().Build());
        var characterId = Guid.NewGuid();
        var profession = Profession.Warrior;
        
        // 使用较高的属性值以便触发技能
        var stats = new CharacterStats 
        { 
            AttackPower = 100,
            CritChance = 0.3,
            HastePercent = 0.1
        };
        var seed = 12345UL;
        
        // Act - 战斗一段时间
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
        Assert.NotNull(status.Skills);
        
        // 战士应该有技能配置
        Assert.True(status.Skills.Count > 0);
        
        // 验证技能数据结构
        foreach (var skill in status.Skills)
        {
            Assert.NotNull(skill.Id);
            Assert.NotNull(skill.Name);
            Assert.True(skill.SlotIndex > 0);
            Assert.True(skill.MaxCharges > 0);
            Assert.True(skill.CurrentCharges >= 0);
            Assert.True(skill.CurrentCharges <= skill.MaxCharges);
            Assert.True(skill.CooldownRemaining >= 0);
        }
    }
    
    [Fact]
    public void GetStatus_ReturnsSkills_ForRanger()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        var serviceProvider = services.BuildServiceProvider();
        var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();
        
        var coordinator = new StepBattleCoordinator(scopeFactory, new ConfigurationBuilder().Build());
        var characterId = Guid.NewGuid();
        var profession = Profession.Ranger;
        var stats = new CharacterStats { AttackPower = 80, CritChance = 0.25 };
        var seed = 12345UL;
        
        // Act
        var battleId = coordinator.Start(
            characterId: characterId,
            profession: profession,
            stats: stats,
            seconds: 8.0,
            seed: seed,
            enemyId: "dummy",
            enemyCount: 1,
            mode: StepBattleMode.Duration
        );
        
        var (found, status) = coordinator.GetStatus(battleId);
        
        // Assert
        Assert.True(found);
        Assert.NotNull(status.Skills);
        Assert.True(status.Skills.Count > 0);
    }
    
    [Fact]
    public void SkillStatusDto_HasCorrectStructure()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        var serviceProvider = services.BuildServiceProvider();
        var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();
        
        var coordinator = new StepBattleCoordinator(scopeFactory, new ConfigurationBuilder().Build());
        var characterId = Guid.NewGuid();
        var profession = Profession.Warrior;
        var stats = new CharacterStats { AttackPower = 100 };
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
        Assert.NotNull(status.Skills);
        
        // 如果有技能，验证其属性
        if (status.Skills.Count > 0)
        {
            var skill = status.Skills.First();
            
            // 验证基本属性
            Assert.False(string.IsNullOrEmpty(skill.Id));
            Assert.False(string.IsNullOrEmpty(skill.Name));
            Assert.True(skill.SlotIndex >= 1);
            Assert.True(skill.Priority >= 0);
            
            // 验证状态属性
            Assert.True(skill.MaxCharges >= 1);
            Assert.InRange(skill.CurrentCharges, 0, skill.MaxCharges);
            Assert.True(skill.CooldownRemaining >= 0);
            
            // 验证技能参数
            Assert.True(skill.BaseDamage >= 0);
            Assert.True(skill.CostAmount >= 0);
        }
    }
    
    [Fact]
    public void GetStatus_SkillsHaveIncrementalSlotIndex()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        var serviceProvider = services.BuildServiceProvider();
        var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();
        
        var coordinator = new StepBattleCoordinator(scopeFactory, new ConfigurationBuilder().Build());
        var characterId = Guid.NewGuid();
        var profession = Profession.Warrior;
        var stats = new CharacterStats { AttackPower = 100 };
        var seed = 12345UL;
        
        // Act
        var battleId = coordinator.Start(
            characterId: characterId,
            profession: profession,
            stats: stats,
            seconds: 3.0,
            seed: seed,
            enemyId: "dummy",
            enemyCount: 1,
            mode: StepBattleMode.Duration
        );
        
        var (found, status) = coordinator.GetStatus(battleId);
        
        // Assert
        Assert.True(found);
        Assert.NotNull(status.Skills);
        
        if (status.Skills.Count > 1)
        {
            // 验证槽位索引是递增的
            for (int i = 0; i < status.Skills.Count; i++)
            {
                Assert.Equal(i + 1, status.Skills[i].SlotIndex);
            }
        }
    }
}
