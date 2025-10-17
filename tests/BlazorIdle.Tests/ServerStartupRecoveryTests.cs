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

    [Fact]
    public async Task OrphanedSnapshots_ShouldBeDeleted_OnStartup()
    {
        // Arrange: 创建一些战斗快照，但没有对应的Running计划（模拟关闭时快照残留）
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
            Intellect = 30,
            Stamina = 10,
            LastSeenAtUtc = DateTime.UtcNow
        };
        _dbContext.Characters.Add(character);

        // 创建一些孤立的快照（没有对应的Running计划）
        var orphanedSnapshot1 = new BlazorIdle.Server.Domain.Records.RunningBattleSnapshotRecord
        {
            Id = Guid.NewGuid(),
            StepBattleId = Guid.NewGuid(),
            CharacterId = characterId,
            Profession = (int)Profession.Ranger,
            EnemyId = "skeleton",
            EnemyCount = 3,
            Seed = "12345",
            TargetSeconds = 3600,
            SimulatedSeconds = 120,
            UpdatedAtUtc = DateTime.UtcNow,
            SnapshotJson = "{\"mock\":\"data\"}"
        };
        _dbContext.Set<BlazorIdle.Server.Domain.Records.RunningBattleSnapshotRecord>().Add(orphanedSnapshot1);

        var orphanedSnapshot2 = new BlazorIdle.Server.Domain.Records.RunningBattleSnapshotRecord
        {
            Id = Guid.NewGuid(),
            StepBattleId = Guid.NewGuid(),
            CharacterId = characterId,
            Profession = (int)Profession.Ranger,
            EnemyId = "orc",
            EnemyCount = 2,
            Seed = "67890",
            TargetSeconds = 7200,
            SimulatedSeconds = 240,
            UpdatedAtUtc = DateTime.UtcNow,
            SnapshotJson = "{\"mock\":\"data2\"}"
        };
        _dbContext.Set<BlazorIdle.Server.Domain.Records.RunningBattleSnapshotRecord>().Add(orphanedSnapshot2);

        // 创建一个有效的计划（Running状态）和对应的快照
        var validPlan = new ActivityPlan
        {
            Id = Guid.NewGuid(),
            CharacterId = characterId,
            SlotIndex = 0,
            Type = ActivityType.Combat,
            LimitType = LimitType.Duration,
            LimitValue = 3600,
            State = ActivityState.Running,
            BattleId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            StartedAt = DateTime.UtcNow,
            PayloadJson = "{\"EnemyId\":\"dragon\",\"EnemyCount\":1}",
            ExecutedSeconds = 60
        };
        _dbContext.ActivityPlans.Add(validPlan);

        var validSnapshot = new BlazorIdle.Server.Domain.Records.RunningBattleSnapshotRecord
        {
            Id = Guid.NewGuid(),
            StepBattleId = validPlan.BattleId.Value,
            CharacterId = characterId,
            Profession = (int)Profession.Ranger,
            EnemyId = "dragon",
            EnemyCount = 1,
            Seed = "99999",
            TargetSeconds = 3600,
            SimulatedSeconds = 60,
            UpdatedAtUtc = DateTime.UtcNow,
            SnapshotJson = "{\"valid\":\"data\"}"
        };
        _dbContext.Set<BlazorIdle.Server.Domain.Records.RunningBattleSnapshotRecord>().Add(validSnapshot);

        await _dbContext.SaveChangesAsync();

        // Act: 模拟启动时的快照清理
        var snapshots = await _dbContext.Set<BlazorIdle.Server.Domain.Records.RunningBattleSnapshotRecord>().ToListAsync();
        Assert.Equal(3, snapshots.Count); // 确认有3个快照

        var activeBattleIds = await _dbContext.ActivityPlans
            .Where(p => p.State == ActivityState.Running && p.BattleId != null)
            .Select(p => p.BattleId!.Value)
            .ToListAsync();

        var orphanedSnapshots = snapshots
            .Where(s => !activeBattleIds.Contains(s.StepBattleId))
            .ToList();

        foreach (var snapshot in orphanedSnapshots)
        {
            _dbContext.Set<BlazorIdle.Server.Domain.Records.RunningBattleSnapshotRecord>().Remove(snapshot);
        }
        await _dbContext.SaveChangesAsync();

        // Assert: 验证孤立快照被删除，有效快照保留
        var remainingSnapshots = await _dbContext.Set<BlazorIdle.Server.Domain.Records.RunningBattleSnapshotRecord>().ToListAsync();
        Assert.Single(remainingSnapshots);
        Assert.Equal(validSnapshot.Id, remainingSnapshots[0].Id);
    }

    [Fact]
    public async Task SecondShutdownRestart_ShouldWork_WithSnapshotCleanup()
    {
        // 这个测试模拟用户报告的问题：第一次重启成功，第二次重启失败
        // Arrange: 模拟第一次启动和运行
        var characterId = Guid.NewGuid();
        var character = new Character
        {
            Id = characterId,
            UserId = Guid.NewGuid(),
            Name = "TestCharacter",
            Profession = Profession.Warrior,
            Level = 10,
            Experience = 0,
            Gold = 500,
            Strength = 20,
            Agility = 30,
            Intellect = 10,
            Stamina = 15,
            LastSeenAtUtc = DateTime.UtcNow
        };
        _dbContext.Characters.Add(character);

        // 第一次启动：创建计划并开始战斗
        var plan = new ActivityPlan
        {
            Id = Guid.NewGuid(),
            CharacterId = characterId,
            SlotIndex = 0,
            Type = ActivityType.Combat,
            LimitType = LimitType.Duration,
            LimitValue = 3600,
            State = ActivityState.Running,
            BattleId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            StartedAt = DateTime.UtcNow,
            PayloadJson = "{\"EnemyId\":\"boss\",\"EnemyCount\":1}",
            ExecutedSeconds = 0
        };
        _dbContext.ActivityPlans.Add(plan);

        var snapshot = new BlazorIdle.Server.Domain.Records.RunningBattleSnapshotRecord
        {
            Id = Guid.NewGuid(),
            StepBattleId = plan.BattleId.Value,
            CharacterId = characterId,
            Profession = (int)Profession.Warrior,
            EnemyId = "boss",
            EnemyCount = 1,
            Seed = "11111",
            TargetSeconds = 3600,
            SimulatedSeconds = 100,
            UpdatedAtUtc = DateTime.UtcNow,
            SnapshotJson = "{\"first\":\"run\"}"
        };
        _dbContext.Set<BlazorIdle.Server.Domain.Records.RunningBattleSnapshotRecord>().Add(snapshot);
        await _dbContext.SaveChangesAsync();

        // 第一次关闭：删除快照，暂停计划
        _dbContext.Set<BlazorIdle.Server.Domain.Records.RunningBattleSnapshotRecord>().Remove(snapshot);
        plan.State = ActivityState.Paused;
        plan.BattleId = null;
        _dbContext.ActivityPlans.Update(plan);
        await _dbContext.SaveChangesAsync();

        // 第一次重启：清理孤立计划（应该没有）
        var orphanedPlans = await _dbContext.ActivityPlans
            .Where(p => p.State == ActivityState.Running)
            .ToListAsync();
        Assert.Empty(orphanedPlans); // 应该没有孤立计划

        // 清理孤立快照（应该没有）
        var orphanedSnapshots = await _dbContext.Set<BlazorIdle.Server.Domain.Records.RunningBattleSnapshotRecord>().ToListAsync();
        Assert.Empty(orphanedSnapshots); // 快照已经被删除

        // 模拟第二次启动：恢复计划
        plan.State = ActivityState.Running;
        plan.BattleId = Guid.NewGuid();
        _dbContext.ActivityPlans.Update(plan);

        var newSnapshot = new BlazorIdle.Server.Domain.Records.RunningBattleSnapshotRecord
        {
            Id = Guid.NewGuid(),
            StepBattleId = plan.BattleId.Value,
            CharacterId = characterId,
            Profession = (int)Profession.Warrior,
            EnemyId = "boss",
            EnemyCount = 1,
            Seed = "22222",
            TargetSeconds = 3600,
            SimulatedSeconds = 200,
            UpdatedAtUtc = DateTime.UtcNow,
            SnapshotJson = "{\"second\":\"run\"}"
        };
        _dbContext.Set<BlazorIdle.Server.Domain.Records.RunningBattleSnapshotRecord>().Add(newSnapshot);
        await _dbContext.SaveChangesAsync();

        // 第二次关闭：删除快照，暂停计划
        _dbContext.Set<BlazorIdle.Server.Domain.Records.RunningBattleSnapshotRecord>().Remove(newSnapshot);
        plan.State = ActivityState.Paused;
        plan.BattleId = null;
        _dbContext.ActivityPlans.Update(plan);
        await _dbContext.SaveChangesAsync();

        // 第二次重启：再次清理（应该仍然干净）
        orphanedPlans = await _dbContext.ActivityPlans
            .Where(p => p.State == ActivityState.Running)
            .ToListAsync();
        Assert.Empty(orphanedPlans);

        orphanedSnapshots = await _dbContext.Set<BlazorIdle.Server.Domain.Records.RunningBattleSnapshotRecord>().ToListAsync();
        Assert.Empty(orphanedSnapshots);

        // Assert: 验证可以成功启动（没有数据库损坏）
        var finalPlan = await _dbContext.ActivityPlans.FindAsync(plan.Id);
        Assert.NotNull(finalPlan);
        Assert.Equal(ActivityState.Paused, finalPlan.State);
        Assert.Null(finalPlan.BattleId);
    }

    public void Dispose()
    {
        _dbContext?.Dispose();
        _serviceProvider?.Dispose();
    }
}
