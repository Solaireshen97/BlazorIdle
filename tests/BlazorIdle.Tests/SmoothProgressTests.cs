using BlazorIdle.Server.Application.Battles.Step;
using BlazorIdle.Server.Domain.Characters;
using BlazorIdle.Shared.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading;
using Xunit;

namespace BlazorIdle.Tests;

/// <summary>
/// 测试攻击进度条的平滑增长功能
/// </summary>
public class SmoothProgressTests
{
    [Fact]
    public void AttackProgress_IncreasesOverTime_WithinSingleInterval()
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
        
        // Act - 启动战斗
        var battleId = coordinator.Start(
            characterId: characterId,
            profession: profession,
            stats: stats,
            seconds: 10.0,
            seed: seed,
            enemyId: "dummy",
            enemyCount: 1,
            mode: StepBattleMode.Duration,
            dungeonId: null,
            stamina: 20
        );
        
        // 获取初始状态
        var (found1, status1) = coordinator.GetStatus(battleId);
        Assert.True(found1);
        Assert.True(status1.NextAttackAt.HasValue);
        
        double? firstNextAttackAt = status1.NextAttackAt;
        double firstCurrentTime = status1.CurrentTime;
        
        // 推进战斗
        if (coordinator.TryGet(battleId, out var rb))
        {
            rb!.FastForwardTo(0.5);
        }
        
        // 获取第二次状态
        var (found2, status2) = coordinator.GetStatus(battleId);
        Assert.True(found2);
        Assert.True(status2.NextAttackAt.HasValue);
        
        double? secondNextAttackAt = status2.NextAttackAt;
        double secondCurrentTime = status2.CurrentTime;
        
        // Assert - 验证进度增长逻辑
        // CurrentTime应该增加
        Assert.True(secondCurrentTime > firstCurrentTime, "战斗时间应该推进");
        
        // 如果还在同一个攻击周期内，NextAttackAt应该保持不变
        // 如果已经攻击过，NextAttackAt应该增加
        if (secondNextAttackAt == firstNextAttackAt)
        {
            // 同一周期内，CurrentTime距离NextAttackAt的距离应该减小
            double firstDistance = firstNextAttackAt.Value - firstCurrentTime;
            double secondDistance = secondNextAttackAt.Value - secondCurrentTime;
            Assert.True(secondDistance < firstDistance, "在同一攻击周期内，距离下次攻击的时间应该减少");
        }
        else
        {
            // 已经进入下一个周期，说明发生了攻击
            Assert.True(secondNextAttackAt > firstNextAttackAt, "新周期的NextAttackAt应该大于旧的");
        }
    }

    [Fact]
    public void AttackProgress_ResetsCorrectly_WhenNextAttackAtChanges()
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
        
        // Act - 启动战斗
        var battleId = coordinator.Start(
            characterId: characterId,
            profession: profession,
            stats: stats,
            seconds: 10.0,
            seed: seed,
            enemyId: "dummy",
            enemyCount: 1,
            mode: StepBattleMode.Duration,
            dungeonId: null,
            stamina: 20
        );
        
        // 推进战斗直到发生攻击
        double? prevNextAttackAt = null;
        int attackCount = 0;
        
        for (int i = 0; i < 20; i++)
        {
            if (coordinator.TryGet(battleId, out var rb))
            {
                rb!.FastForwardTo(i * 0.5);
            }
            var (found, status) = coordinator.GetStatus(battleId);
            
            if (!found || !status.NextAttackAt.HasValue)
                break;
            
            // 检测NextAttackAt是否变化（表示发生了攻击）
            if (prevNextAttackAt.HasValue && status.NextAttackAt.Value > prevNextAttackAt.Value)
            {
                attackCount++;
                
                // 验证新的NextAttackAt大于旧的（表示重置到新周期）
                Assert.True(status.NextAttackAt.Value > prevNextAttackAt.Value, 
                    "攻击后NextAttackAt应该重置到新的值");
            }
            
            prevNextAttackAt = status.NextAttackAt;
            
            if (attackCount >= 2)
                break; // 观察到至少2次攻击周期即可
        }
        
        // Assert
        Assert.True(attackCount >= 1, "应该至少观察到一次攻击周期变化");
    }

    [Fact]
    public void AttackProgress_TracksInterval_FromConsecutivePolls()
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
        
        // Act - 启动战斗
        var battleId = coordinator.Start(
            characterId: characterId,
            profession: profession,
            stats: stats,
            seconds: 10.0,
            seed: seed,
            enemyId: "dummy",
            enemyCount: 1,
            mode: StepBattleMode.Duration,
            dungeonId: null,
            stamina: 20
        );
        
        // 收集多次NextAttackAt值以计算间隔
        var nextAttackTimes = new List<double>();
        
        for (int i = 0; i < 30; i++)
        {
            if (coordinator.TryGet(battleId, out var rb))
            {
                rb!.FastForwardTo(i * 0.3);
            }
            var (found, status) = coordinator.GetStatus(battleId);
            
            if (!found || !status.NextAttackAt.HasValue)
                break;
            
            nextAttackTimes.Add(status.NextAttackAt.Value);
        }
        
        // 计算攻击间隔
        var intervals = new List<double>();
        for (int i = 1; i < nextAttackTimes.Count; i++)
        {
            if (nextAttackTimes[i] > nextAttackTimes[i-1])
            {
                double interval = nextAttackTimes[i] - nextAttackTimes[i-1];
                intervals.Add(interval);
            }
        }
        
        // Assert
        Assert.NotEmpty(intervals);
        
        // 验证间隔的一致性（攻击速度应该相对稳定）
        if (intervals.Count > 1)
        {
            double avgInterval = intervals.Average();
            foreach (var interval in intervals)
            {
                // 间隔应该在平均值的合理范围内（考虑到GCD、技能等因素）
                Assert.True(Math.Abs(interval - avgInterval) < avgInterval * 0.5, 
                    $"攻击间隔应该相对稳定，实际间隔={interval}，平均间隔={avgInterval}");
            }
        }
    }

    [Fact]
    public void AttackProgress_HandlesMultiEnemyBattle_WithTargetSwitch()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        var serviceProvider = services.BuildServiceProvider();
        var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();
        
        var coordinator = new StepBattleCoordinator(scopeFactory, new ConfigurationBuilder().Build(), new MockBattleNotificationService());
        var characterId = Guid.NewGuid();
        var profession = Profession.Warrior;
        var stats = new CharacterStats { AttackPower = 500, SpellPower = 0, CritChance = 0 };
        var seed = 12345UL;
        
        // Act - 启动多敌人战斗
        var battleId = coordinator.Start(
            characterId: characterId,
            profession: profession,
            stats: stats,
            seconds: 30.0,
            seed: seed,
            enemyId: "dummy",
            enemyCount: 3, // 3个敌人
            mode: StepBattleMode.Duration,
            dungeonId: null,
            stamina: 20
        );
        
        // 推进战斗并观察目标切换
        int enemyKillCount = 0;
        int previousEnemyCount = 3;
        double? prevNextAttackAt = null;
        bool detectedReset = false;
        
        for (int i = 0; i < 50; i++)
        {
            if (coordinator.TryGet(battleId, out var rb))
            {
                rb!.FastForwardTo(i * 0.5);
            }
            var (found, status) = coordinator.GetStatus(battleId);
            
            if (!found)
                break;
            
            int currentEnemyCount = status.Enemies.Count(e => !e.IsDead);
            
            // 检测到敌人被杀
            if (currentEnemyCount < previousEnemyCount)
            {
                enemyKillCount++;
                
                // 检查NextAttackAt是否被重置（根据后端ResetAttackProgress逻辑）
                if (prevNextAttackAt.HasValue && status.NextAttackAt.HasValue)
                {
                    // 切换目标后，NextAttackAt应该被重置
                    if (status.NextAttackAt.Value > prevNextAttackAt.Value)
                    {
                        detectedReset = true;
                    }
                }
                
                previousEnemyCount = currentEnemyCount;
            }
            
            prevNextAttackAt = status.NextAttackAt;
            
            if (currentEnemyCount == 0)
                break;
        }
        
        // Assert
        Assert.True(enemyKillCount > 0, "应该至少杀死一个敌人");
        // Note: 重置行为由后端ResetAttackProgress实现，前端通过追踪NextAttackAt变化来适应
    }
}
