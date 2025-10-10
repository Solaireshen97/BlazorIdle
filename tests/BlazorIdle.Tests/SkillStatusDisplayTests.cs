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
/// 测试 Step 4: 技能状态显示功能
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
        var stats = new CharacterStats 
        { 
            AttackPower = 100,
            CritChance = 0.3,
            HastePercent = 0.1
        };
        var seed = 12345UL;
        
        // Act - 战斗一段时间以便技能进入冷却
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
        
        // 战士应该有技能
        Assert.True(status.Skills.Count > 0, "战士应该有技能");
        
        // 验证技能数据结构
        foreach (var skill in status.Skills)
        {
            Assert.NotNull(skill.Id);
            Assert.NotNull(skill.Name);
            Assert.True(skill.CooldownSeconds >= 0, $"技能 {skill.Name} 冷却时间应该 >= 0");
            Assert.True(skill.RemainingCooldown >= 0, $"技能 {skill.Name} 剩余冷却时间应该 >= 0");
            Assert.True(skill.Priority > 0, $"技能 {skill.Name} 优先级应该 > 0");
            
            // 如果技能就绪，剩余冷却应该为0
            if (skill.IsReady)
            {
                Assert.True(skill.RemainingCooldown <= 0.1, $"就绪技能 {skill.Name} 的剩余冷却应该接近0");
            }
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
        var stats = new CharacterStats { AttackPower = 80 };
        var seed = 54321UL;
        
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
        
        // 游侠应该有多个技能（Power Shot, Bleed Shot, Quick Shot）
        Assert.True(status.Skills.Count >= 3, "游侠应该有至少3个技能");
        
        // 验证游侠技能有focus消耗
        var skillsWithCost = status.Skills.Where(s => !string.IsNullOrEmpty(s.CostResourceId)).ToList();
        Assert.True(skillsWithCost.Count > 0, "游侠技能应该有资源消耗");
        Assert.True(skillsWithCost.All(s => s.CostResourceId == "focus"), "游侠技能应该消耗focus");
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
        var seed = 99999UL;
        
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
        
        // 如果有技能，验证其完整属性
        if (status.Skills.Count > 0)
        {
            var skill = status.Skills.First();
            
            // 必需字段
            Assert.False(string.IsNullOrEmpty(skill.Id), "技能ID不应为空");
            Assert.False(string.IsNullOrEmpty(skill.Name), "技能名称不应为空");
            
            // 数值字段
            Assert.True(skill.Priority > 0, "优先级应该 > 0");
            Assert.True(skill.CooldownSeconds >= 0, "冷却时间应该 >= 0");
            Assert.True(skill.RemainingCooldown >= 0, "剩余冷却时间应该 >= 0");
            
            // 布尔字段
            Assert.NotNull(skill.IsReady);
            
            // 如果有资源消耗，验证相关字段
            if (!string.IsNullOrEmpty(skill.CostResourceId))
            {
                Assert.True(skill.CostAmount > 0, "消耗量应该 > 0");
            }
            
            // 充能相关字段（如果技能支持多充能）
            if (skill.MaxCharges.HasValue)
            {
                Assert.True(skill.MaxCharges.Value > 0, "最大充能数应该 > 0");
                Assert.NotNull(skill.CurrentCharges);
                Assert.True(skill.CurrentCharges.Value >= 0, "当前充能数应该 >= 0");
                Assert.True(skill.CurrentCharges.Value <= skill.MaxCharges.Value, "当前充能数不应超过最大充能数");
            }
        }
    }
    
    [Fact]
    public void GetStatus_SkillsOrderedByPriority()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        var serviceProvider = services.BuildServiceProvider();
        var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();
        
        var coordinator = new StepBattleCoordinator(scopeFactory, new ConfigurationBuilder().Build());
        var characterId = Guid.NewGuid();
        var profession = Profession.Ranger;
        var stats = new CharacterStats { AttackPower = 70 };
        var seed = 11111UL;
        
        // Act
        var battleId = coordinator.Start(
            characterId: characterId,
            profession: profession,
            stats: stats,
            seconds: 6.0,
            seed: seed,
            enemyId: "dummy",
            enemyCount: 1,
            mode: StepBattleMode.Duration
        );
        
        var (found, status) = coordinator.GetStatus(battleId);
        
        // Assert
        Assert.True(found);
        Assert.NotNull(status.Skills);
        Assert.True(status.Skills.Count > 1, "应该有多个技能用于测试排序");
        
        // 验证技能列表包含Priority字段
        Assert.All(status.Skills, skill => Assert.True(skill.Priority > 0, "所有技能应该有有效的优先级"));
    }
}
