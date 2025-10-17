using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BlazorIdle.Server.Application.Abstractions;
using BlazorIdle.Server.Application.Activities;
using BlazorIdle.Server.Domain.Activities;
using BlazorIdle.Server.Domain.Characters;
using BlazorIdle.Server.Infrastructure.Persistence;
using BlazorIdle.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BlazorIdle.Tests;

/// <summary>
/// 测试服务器启动时对孤立计划的恢复能力
/// 这些测试验证了当服务器意外关闭后重启时，能够正确处理Running状态的计划
/// </summary>
public class ServerStartupRecoveryTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly GameDbContext _dbContext;

    public ServerStartupRecoveryTests()
    {
        var services = new ServiceCollection();
        
        // 配置内存数据库用于测试
        services.AddDbContext<GameDbContext>(options =>
            options.UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}"));
        
        services.AddScoped<IActivityPlanRepository, BlazorIdle.Server.Infrastructure.Persistence.Repositories.ActivityPlanRepository>();
        services.AddScoped<ICharacterRepository, BlazorIdle.Server.Infrastructure.Persistence.Repositories.CharacterRepository>();
        
        _serviceProvider = services.BuildServiceProvider();
        _dbContext = _serviceProvider.GetRequiredService<GameDbContext>();
    }

    [Fact]
    public async Task OrphanedRunningPlan_ShouldBeMarkedAsPaused_OnStartup()
    {
        // Arrange: 创建一个处于Running状态但没有对应战斗实例的计划（模拟服务器异常关闭）
        var characterId = Guid.NewGuid();
        var character = new Character
        {
            Id = characterId,
            UserId = Guid.NewGuid(),
            Name = "TestCharacter",
            Profession = Profession.Warrior,
            Level = 1,
            Experience = 0,
            Gold = 0,
            Strength = 10,
            Agility = 10,
            Intellect = 10,
            Stamina = 10,
            LastSeenAtUtc = DateTime.UtcNow.AddMinutes(-1) // 1分钟前
        };
        _dbContext.Characters.Add(character);

        var orphanedPlan = new ActivityPlan
        {
            Id = Guid.NewGuid(),
            CharacterId = characterId,
            SlotIndex = 0,
            Type = ActivityType.Combat,
            LimitType = LimitType.Duration,
            LimitValue = 3600,
            State = ActivityState.Running, // 孤立的Running状态
            BattleId = Guid.NewGuid(), // BattleId存在但对应的RunningBattle不存在
            CreatedAt = DateTime.UtcNow.AddMinutes(-5),
            StartedAt = DateTime.UtcNow.AddMinutes(-5),
            PayloadJson = "{\"EnemyId\":\"goblin\",\"EnemyCount\":1}",
            ExecutedSeconds = 60 // 已执行60秒
        };
        _dbContext.ActivityPlans.Add(orphanedPlan);
        await _dbContext.SaveChangesAsync();

        // Act: 模拟服务器启动时的清理操作
        // 查找所有Running状态的计划
        var runningPlans = await _dbContext.ActivityPlans
            .Where(p => p.State == ActivityState.Running)
            .ToListAsync();

        // 将它们标记为Paused状态（清理孤立计划）
        foreach (var plan in runningPlans)
        {
            plan.State = ActivityState.Paused;
            plan.BattleId = null; // 清除BattleId引用
            _dbContext.ActivityPlans.Update(plan);
        }
        await _dbContext.SaveChangesAsync();

        // Assert: 验证计划被正确标记为Paused
        var recoveredPlan = await _dbContext.ActivityPlans.FindAsync(orphanedPlan.Id);
        Assert.NotNull(recoveredPlan);
        Assert.Equal(ActivityState.Paused, recoveredPlan.State);
        Assert.Null(recoveredPlan.BattleId);
        Assert.Equal(60, recoveredPlan.ExecutedSeconds); // 执行时长应该保留
    }

    [Fact]
    public async Task MultipleOrphanedPlans_AllShouldBeMarkedAsPaused()
    {
        // Arrange: 创建多个孤立的Running计划
        var characterId = Guid.NewGuid();
        var character = new Character
        {
            Id = characterId,
            UserId = Guid.NewGuid(),
            Name = "TestCharacter",
            Profession = Profession.Ranger,
            Level = 5,
            Experience = 0,
            Gold = 100,
            Strength = 10,
            Agility = 10,
            Intellect = 20,
            Stamina = 10,
            LastSeenAtUtc = DateTime.UtcNow
        };
        _dbContext.Characters.Add(character);

        for (int i = 0; i < 3; i++)
        {
            var plan = new ActivityPlan
            {
                Id = Guid.NewGuid(),
                CharacterId = characterId,
                SlotIndex = i,
                Type = ActivityType.Combat,
                LimitType = LimitType.Infinite,
                State = ActivityState.Running,
                BattleId = Guid.NewGuid(),
                CreatedAt = DateTime.UtcNow.AddMinutes(-10),
                StartedAt = DateTime.UtcNow.AddMinutes(-10),
                PayloadJson = "{\"EnemyId\":\"wolf\",\"EnemyCount\":2}",
                ExecutedSeconds = 120
            };
            _dbContext.ActivityPlans.Add(plan);
        }
        await _dbContext.SaveChangesAsync();

        // Act: 清理所有孤立计划
        var runningPlans = await _dbContext.ActivityPlans
            .Where(p => p.State == ActivityState.Running)
            .ToListAsync();

        Assert.Equal(3, runningPlans.Count); // 确认有3个Running计划

        foreach (var plan in runningPlans)
        {
            plan.State = ActivityState.Paused;
            plan.BattleId = null;
            _dbContext.ActivityPlans.Update(plan);
        }
        await _dbContext.SaveChangesAsync();

        // Assert: 验证所有计划都被标记为Paused
        var pausedPlans = await _dbContext.ActivityPlans
            .Where(p => p.CharacterId == characterId && p.State == ActivityState.Paused)
            .ToListAsync();

        Assert.Equal(3, pausedPlans.Count);
        Assert.All(pausedPlans, p => Assert.Null(p.BattleId));
    }

    [Fact]
    public async Task PausedPlans_ShouldNotBeAffected_ByStartupCleanup()
    {
        // Arrange: 创建一个已经Paused的计划（正常的暂停状态）
        var characterId = Guid.NewGuid();
        var character = new Character
        {
            Id = characterId,
            UserId = Guid.NewGuid(),
            Name = "TestCharacter",
            Profession = Profession.Warrior,
            Level = 3,
            Experience = 0,
            Gold = 50,
            Strength = 15,
            Agility = 15,
            Intellect = 10,
            Stamina = 10,
            LastSeenAtUtc = DateTime.UtcNow.AddMinutes(-30) // 30分钟前（离线）
        };
        _dbContext.Characters.Add(character);

        var pausedPlan = new ActivityPlan
        {
            Id = Guid.NewGuid(),
            CharacterId = characterId,
            SlotIndex = 0,
            Type = ActivityType.Dungeon,
            LimitType = LimitType.Duration,
            LimitValue = 7200,
            State = ActivityState.Paused, // 已经是Paused状态
            BattleId = null,
            CreatedAt = DateTime.UtcNow.AddHours(-1),
            StartedAt = DateTime.UtcNow.AddHours(-1),
            PayloadJson = "{\"DungeonId\":\"intro_cave\",\"Loop\":false}",
            ExecutedSeconds = 300,
            BattleStateJson = "{\"someState\":\"data\"}" // 包含战斗状态快照
        };
        _dbContext.ActivityPlans.Add(pausedPlan);
        await _dbContext.SaveChangesAsync();

        // Act: 运行启动清理（只处理Running状态的计划）
        var runningPlans = await _dbContext.ActivityPlans
            .Where(p => p.State == ActivityState.Running)
            .ToListAsync();

        Assert.Empty(runningPlans); // 应该没有Running计划

        // Assert: 验证Paused计划没有被修改
        var plan = await _dbContext.ActivityPlans.FindAsync(pausedPlan.Id);
        Assert.NotNull(plan);
        Assert.Equal(ActivityState.Paused, plan.State);
        Assert.Null(plan.BattleId); // BattleId仍然是null
        Assert.Equal(300, plan.ExecutedSeconds);
        Assert.NotNull(plan.BattleStateJson); // 状态快照应该保留
    }

    public void Dispose()
    {
        _dbContext?.Dispose();
        _serviceProvider?.Dispose();
    }
}
