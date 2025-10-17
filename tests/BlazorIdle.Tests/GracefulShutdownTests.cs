using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using BlazorIdle.Server.Application.Activities;
using BlazorIdle.Server.Application.Abstractions;
using BlazorIdle.Server.Domain.Activities;
using BlazorIdle.Server.Domain.Characters;
using BlazorIdle.Server.Infrastructure.Persistence;
using BlazorIdle.Shared.Models;

namespace BlazorIdle.Tests;

/// <summary>
/// 测试服务器优雅关闭功能，确保在关闭时正确保存战斗状态和活动计划
/// </summary>
public class GracefulShutdownTests
{
    private ServiceProvider CreateServiceProvider()
    {
        var services = new ServiceCollection();
        
        // 配置内存数据库
        services.AddDbContext<GameDbContext>(opt => 
            opt.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        
        // 注册日志
        services.AddLogging(builder => builder.AddConsole());
        
        // 注册配置（测试用的简单配置）
        var configBuilder = new ConfigurationBuilder();
        var configData = new Dictionary<string, string?>
        {
            ["Combat:RewardFlushIntervalSeconds"] = "10.0",
            ["Combat:EnablePeriodicRewards"] = "true"
        };
        configBuilder.AddInMemoryCollection(configData);
        services.AddSingleton<IConfiguration>(configBuilder.Build());
        
        // 注册仓储
        services.AddScoped<ICharacterRepository, BlazorIdle.Server.Infrastructure.Persistence.Repositories.CharacterRepository>();
        services.AddScoped<IActivityPlanRepository, BlazorIdle.Server.Infrastructure.Persistence.Repositories.ActivityPlanRepository>();
        
        // 注册战斗协调器和相关服务
        services.AddSingleton<BlazorIdle.Server.Application.Battles.Step.StepBattleCoordinator>();
        services.AddScoped<BlazorIdle.Server.Application.Battles.Step.StepBattleFinalizer>();
        
        // 注册活动计划服务
        services.AddScoped<ActivityPlanService>();
        
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task PausePlan_ShouldSaveBattleState_WhenPlanIsRunning()
    {
        // Arrange
        var provider = CreateServiceProvider();
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GameDbContext>();
        var planService = scope.ServiceProvider.GetRequiredService<ActivityPlanService>();

        // 创建测试角色
        var character = new Character
        {
            Id = Guid.NewGuid(),
            Name = "测试角色",
            Level = 10,
            Profession = Profession.Warrior,
            Strength = 20,
            Agility = 15,
            Intellect = 10,
            Stamina = 20,
            Gold = 100,
            Experience = 50
        };
        db.Characters.Add(character);
        await db.SaveChangesAsync();

        // 创建战斗计划
        var payload = System.Text.Json.JsonSerializer.Serialize(new
        {
            EnemyId = "dummy",
            EnemyCount = 1,
            RespawnDelay = 5.0,
            Seed = (ulong?)null
        });

        var plan = await planService.CreatePlanAsync(
            character.Id,
            0,
            ActivityType.Combat,
            LimitType.Duration,
            60.0,
            payload,
            CancellationToken.None
        );

        // 等待计划运行一小段时间
        await Task.Delay(100);

        // Act - 暂停计划（模拟服务器关闭）
        var paused = await planService.PausePlanAsync(plan.Id, CancellationToken.None);

        // Assert
        Assert.True(paused, "计划应该成功暂停");
        
        // 验证计划状态
        var updatedPlan = await db.ActivityPlans.FirstOrDefaultAsync(p => p.Id == plan.Id);
        Assert.NotNull(updatedPlan);
        Assert.Equal(ActivityState.Paused, updatedPlan.State);
        
        // 验证战斗状态已保存
        Assert.NotNull(updatedPlan.BattleStateJson);
        Assert.NotEmpty(updatedPlan.BattleStateJson);
        
        // 验证 BattleId 已清除（战斗已停止）
        Assert.Null(updatedPlan.BattleId);
    }

    [Fact]
    public async Task StartPlan_ShouldRestoreBattleState_AfterPause()
    {
        // Arrange
        var provider = CreateServiceProvider();
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GameDbContext>();
        var planService = scope.ServiceProvider.GetRequiredService<ActivityPlanService>();

        // 创建测试角色
        var character = new Character
        {
            Id = Guid.NewGuid(),
            Name = "测试角色",
            Level = 10,
            Profession = Profession.Warrior,
            Strength = 20,
            Agility = 15,
            Intellect = 10,
            Stamina = 20,
            Gold = 100,
            Experience = 50
        };
        db.Characters.Add(character);
        await db.SaveChangesAsync();

        // 创建并启动战斗计划
        var payload = System.Text.Json.JsonSerializer.Serialize(new
        {
            EnemyId = "dummy",
            EnemyCount = 1,
            RespawnDelay = 5.0,
            Seed = (ulong?)null
        });

        var plan = await planService.CreatePlanAsync(
            character.Id,
            0,
            ActivityType.Combat,
            LimitType.Duration,
            60.0,
            payload,
            CancellationToken.None
        );

        // 等待计划运行
        await Task.Delay(100);

        // 暂停计划（模拟服务器关闭）
        await planService.PausePlanAsync(plan.Id, CancellationToken.None);

        // 验证战斗状态已保存
        var pausedPlan = await db.ActivityPlans.FirstOrDefaultAsync(p => p.Id == plan.Id);
        Assert.NotNull(pausedPlan);
        Assert.Equal(ActivityState.Paused, pausedPlan.State);
        var savedBattleStateJson = pausedPlan.BattleStateJson;
        Assert.NotNull(savedBattleStateJson);

        // Act - 重新启动计划（模拟服务器重启后恢复）
        var battleId = await planService.StartPlanAsync(plan.Id, CancellationToken.None);

        // Assert
        Assert.NotEqual(Guid.Empty, battleId);
        
        // 验证计划状态已恢复为运行中
        var resumedPlan = await db.ActivityPlans.FirstOrDefaultAsync(p => p.Id == plan.Id);
        Assert.NotNull(resumedPlan);
        Assert.Equal(ActivityState.Running, resumedPlan.State);
        Assert.NotNull(resumedPlan.BattleId);
        
        // 验证战斗状态快照仍然存在（用于后续保存）
        Assert.NotNull(resumedPlan.BattleStateJson);
    }

    [Fact]
    public async Task MultipleRunningPlans_ShouldAllBePaused_OnShutdown()
    {
        // Arrange
        var provider = CreateServiceProvider();
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GameDbContext>();
        var planService = scope.ServiceProvider.GetRequiredService<ActivityPlanService>();
        var planRepo = scope.ServiceProvider.GetRequiredService<IActivityPlanRepository>();

        // 创建多个角色和计划
        var characters = new[]
        {
            new Character { Id = Guid.NewGuid(), Name = "角色1", Profession = Profession.Warrior, Strength = 20, Agility = 15, Intellect = 10, Stamina = 20 },
            new Character { Id = Guid.NewGuid(), Name = "角色2", Profession = Profession.Ranger, Strength = 10, Agility = 25, Intellect = 15, Stamina = 15 },
            new Character { Id = Guid.NewGuid(), Name = "角色3", Profession = Profession.Warrior, Strength = 15, Agility = 20, Intellect = 10, Stamina = 15 }
        };

        foreach (var character in characters)
        {
            db.Characters.Add(character);
        }
        await db.SaveChangesAsync();

        var payload = System.Text.Json.JsonSerializer.Serialize(new
        {
            EnemyId = "dummy",
            EnemyCount = 1,
            RespawnDelay = 5.0,
            Seed = (ulong?)null
        });

        // 为每个角色创建运行中的计划
        foreach (var character in characters)
        {
            await planService.CreatePlanAsync(
                character.Id,
                0,
                ActivityType.Combat,
                LimitType.Duration,
                60.0,
                payload,
                CancellationToken.None
            );
        }

        // 等待所有计划开始运行
        await Task.Delay(100);

        // 验证有3个运行中的计划
        var runningPlans = await planRepo.GetAllRunningPlansAsync(CancellationToken.None);
        Assert.Equal(3, runningPlans.Count);

        // Act - 暂停所有运行中的计划（模拟服务器关闭）
        foreach (var plan in runningPlans)
        {
            await planService.PausePlanAsync(plan.Id, CancellationToken.None);
        }

        // Assert - 验证所有计划都已暂停
        var pausedPlans = await db.ActivityPlans
            .Where(p => p.State == ActivityState.Paused)
            .ToListAsync();
        
        Assert.Equal(3, pausedPlans.Count);
        
        // 验证所有计划都保存了战斗状态
        foreach (var plan in pausedPlans)
        {
            Assert.NotNull(plan.BattleStateJson);
            Assert.NotEmpty(plan.BattleStateJson);
            Assert.Null(plan.BattleId); // 战斗应该已停止
        }

        // 验证没有运行中的计划
        var stillRunning = await planRepo.GetAllRunningPlansAsync(CancellationToken.None);
        Assert.Empty(stillRunning);
    }
}
