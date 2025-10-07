using BlazorIdle.Server.Application.Abstractions;
using BlazorIdle.Server.Application.Battles;
using BlazorIdle.Server.Application.Battles.Offline;
using BlazorIdle.Server.Domain.Activities;
using BlazorIdle.Server.Domain.Characters;
using BlazorIdle.Server.Infrastructure.Persistence;
using BlazorIdle.Shared.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using System;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace BlazorIdle.Tests;

/// <summary>
/// OfflineSettlementService 单元测试
/// 测试离线结算服务的核心功能
/// </summary>
public class OfflineSettlementServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly GameDbContext _db;
    private readonly ICharacterRepository _characterRepo;
    private readonly IActivityPlanRepository _planRepo;
    private readonly OfflineFastForwardEngine _engine;
    private readonly OfflineSettlementService _service;
    private readonly Character _testCharacter;

    public OfflineSettlementServiceTests()
    {
        // 使用SQLite内存数据库进行测试
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<GameDbContext>()
            .UseSqlite(_connection)
            .Options;

        _db = new GameDbContext(options);
        _db.Database.EnsureCreated();

        _characterRepo = new BlazorIdle.Server.Infrastructure.Persistence.Repositories.CharacterRepository(_db);
        _planRepo = new BlazorIdle.Server.Infrastructure.Persistence.Repositories.ActivityPlanRepository(_db);
        
        var simulator = new BattleSimulator();
        _engine = new OfflineFastForwardEngine(simulator);
        _service = new OfflineSettlementService(_characterRepo, _planRepo, simulator, _engine, _db);

        // 创建测试角色
        _testCharacter = new Character
        {
            Id = Guid.NewGuid(),
            Name = "TestHero",
            Profession = Profession.Warrior,
            Level = 10,
            Strength = 20,
            Agility = 15,
            Intellect = 10,
            Stamina = 25,
            Gold = 1000,
            Experience = 5000,
            LastSeenAtUtc = DateTime.UtcNow.AddHours(-2) // 2小时前离线
        };

        _db.Characters.Add(_testCharacter);
        _db.SaveChanges();
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Close();
        _connection.Dispose();
    }

    [Fact]
    public async Task CheckAndSettleAsync_WithNoOfflineTime_ShouldReturnNoOfflineTime()
    {
        // Arrange
        var character = new Character
        {
            Id = Guid.NewGuid(),
            Name = "OnlineHero",
            Profession = Profession.Warrior,
            Level = 5,
            Strength = 15,
            Agility = 12,
            Intellect = 8,
            Stamina = 20,
            Gold = 500,
            Experience = 2000,
            LastSeenAtUtc = DateTime.UtcNow.AddSeconds(1) // 设置为未来1秒，确保离线时间 <= 0
        };
        _db.Characters.Add(character);
        await _db.SaveChangesAsync();

        // Act
        var result = await _service.CheckAndSettleAsync(character.Id);

        // Assert
        Assert.False(result.HasOfflineTime);
        Assert.True(result.OfflineSeconds <= 0);
        Assert.False(result.HasRunningPlan);
        Assert.Null(result.Settlement);
    }

    [Fact]
    public async Task CheckAndSettleAsync_WithFirstLogin_ShouldSetLastSeenAtUtc()
    {
        // Arrange
        var character = new Character
        {
            Id = Guid.NewGuid(),
            Name = "NewHero",
            Profession = Profession.Ranger,
            Level = 1,
            Strength = 10,
            Agility = 10,
            Intellect = 10,
            Stamina = 10,
            Gold = 0,
            Experience = 0,
            LastSeenAtUtc = null // 首次登录
        };
        _db.Characters.Add(character);
        await _db.SaveChangesAsync();

        // Act
        var result = await _service.CheckAndSettleAsync(character.Id);

        // Assert
        Assert.False(result.HasOfflineTime);
        Assert.Equal(0, result.OfflineSeconds);
        
        // 验证LastSeenAtUtc已被设置
        var updatedCharacter = await _characterRepo.GetAsync(character.Id);
        Assert.NotNull(updatedCharacter);
        Assert.NotNull(updatedCharacter.LastSeenAtUtc);
    }

    [Fact]
    public async Task CheckAndSettleAsync_WithOfflineButNoRunningPlan_ShouldReturnOfflineTimeOnly()
    {
        // Act
        var result = await _service.CheckAndSettleAsync(_testCharacter.Id);

        // Assert
        Assert.True(result.HasOfflineTime);
        Assert.True(result.OfflineSeconds > 0);
        Assert.False(result.HasRunningPlan);
        Assert.Null(result.Settlement);
    }

    [Fact]
    public async Task CheckAndSettleAsync_WithOfflineAndRunningPlan_ShouldCalculateRewards()
    {
        // Arrange - 创建一个正在运行的战斗计划
        var plan = CreateCombatPlan(
            characterId: _testCharacter.Id,
            limitType: LimitType.Duration,
            limitValue: 10800, // 3小时
            executedSeconds: 0,
            state: ActivityState.Running
        );
        _db.ActivityPlans.Add(plan);
        await _db.SaveChangesAsync();

        // Act
        var result = await _service.CheckAndSettleAsync(_testCharacter.Id, maxOfflineSeconds: 43200);

        // Assert
        Assert.True(result.HasOfflineTime);
        Assert.True(result.OfflineSeconds > 0);
        Assert.True(result.HasRunningPlan);
        Assert.NotNull(result.Settlement);
        
        // 验证结算结果
        Assert.Equal(_testCharacter.Id, result.Settlement.CharacterId);
        Assert.Equal(plan.Id, result.Settlement.PlanId);
        Assert.True(result.Settlement.SimulatedSeconds > 0);
        
        // 验证计划状态已更新
        var updatedPlan = await _planRepo.GetAsync(plan.Id);
        Assert.NotNull(updatedPlan);
        Assert.True(updatedPlan.ExecutedSeconds > 0);
    }

    [Fact]
    public async Task CheckAndSettleAsync_WithPlanCompletion_ShouldMarkPlanCompleted()
    {
        // Arrange - 创建一个快要完成的计划
        var plan = CreateCombatPlan(
            characterId: _testCharacter.Id,
            limitType: LimitType.Duration,
            limitValue: 3600, // 1小时
            executedSeconds: 0,
            state: ActivityState.Running
        );
        _db.ActivityPlans.Add(plan);
        await _db.SaveChangesAsync();

        // 设置离线2小时，计划应该完成
        _testCharacter.LastSeenAtUtc = DateTime.UtcNow.AddHours(-2);
        await _db.SaveChangesAsync();

        // Act
        var result = await _service.CheckAndSettleAsync(_testCharacter.Id, maxOfflineSeconds: 43200);

        // Assert
        Assert.True(result.PlanCompleted);
        Assert.NotNull(result.Settlement);
        Assert.True(result.Settlement.PlanCompleted);

        // 验证计划状态为Completed
        var updatedPlan = await _planRepo.GetAsync(plan.Id);
        Assert.NotNull(updatedPlan);
        Assert.Equal(ActivityState.Completed, updatedPlan.State);
        Assert.NotNull(updatedPlan.CompletedAt);
    }

    [Fact]
    public async Task CheckAndSettleAsync_WithPlanCompletion_ShouldReturnNextPlanId()
    {
        // Arrange - 创建两个计划：一个Running，一个Pending
        var runningPlan = CreateCombatPlan(
            characterId: _testCharacter.Id,
            limitType: LimitType.Duration,
            limitValue: 1800, // 30分钟
            executedSeconds: 0,
            state: ActivityState.Running
        );
        
        var pendingPlan = CreateCombatPlan(
            characterId: _testCharacter.Id,
            limitType: LimitType.Duration,
            limitValue: 3600, // 1小时
            executedSeconds: 0,
            state: ActivityState.Pending
        );
        pendingPlan.SlotIndex = 1; // 不同槽位
        
        _db.ActivityPlans.Add(runningPlan);
        _db.ActivityPlans.Add(pendingPlan);
        await _db.SaveChangesAsync();

        // 设置离线1小时，第一个计划应该完成
        _testCharacter.LastSeenAtUtc = DateTime.UtcNow.AddHours(-1);
        await _db.SaveChangesAsync();

        // Act
        var result = await _service.CheckAndSettleAsync(_testCharacter.Id, maxOfflineSeconds: 43200);

        // Assert
        Assert.True(result.PlanCompleted);
        Assert.NotNull(result.NextPlanId);
        Assert.Equal(pendingPlan.Id, result.NextPlanId);
    }

    [Fact]
    public async Task CheckAndSettleAsync_WithSeamlessProgress_ShouldContinueFromCurrentProgress()
    {
        // Arrange - 模拟一个副本打到一半进入离线的场景
        var plan = CreateCombatPlan(
            characterId: _testCharacter.Id,
            limitType: LimitType.Duration,
            limitValue: 7200, // 2小时
            executedSeconds: 1800, // 已执行30分钟
            state: ActivityState.Running
        );
        _db.ActivityPlans.Add(plan);
        await _db.SaveChangesAsync();

        // 离线30分钟
        _testCharacter.LastSeenAtUtc = DateTime.UtcNow.AddMinutes(-30);
        await _db.SaveChangesAsync();

        // Act
        var result = await _service.CheckAndSettleAsync(_testCharacter.Id, maxOfflineSeconds: 43200);

        // Assert
        Assert.True(result.HasRunningPlan);
        Assert.NotNull(result.Settlement);
        
        // 应该从1800秒继续计算，再加上约1800秒（30分钟）
        var updatedPlan = await _planRepo.GetAsync(plan.Id);
        Assert.NotNull(updatedPlan);
        Assert.True(updatedPlan.ExecutedSeconds >= 3500); // 约60分钟，允许一些误差
        Assert.True(updatedPlan.ExecutedSeconds <= 3700);
        
        // 计划应该未完成（还剩约1小时）
        Assert.False(result.PlanCompleted);
        Assert.Equal(ActivityState.Running, updatedPlan.State);
    }

    [Fact]
    public async Task CheckAndSettleAsync_WithBattleStateJson_ShouldPreserveProgress()
    {
        // Arrange - 创建一个带有战斗状态快照的计划
        var plan = CreateCombatPlan(
            characterId: _testCharacter.Id,
            limitType: LimitType.Duration,
            limitValue: 3600,
            executedSeconds: 600, // 已执行10分钟
            state: ActivityState.Running
        );
        
        // 模拟战斗状态快照
        var battleState = new BattleState
        {
            SnapshotAtSeconds = 600,
            Enemies = new System.Collections.Generic.List<EnemyHealthState>
            {
                new EnemyHealthState
                {
                    EnemyId = "dummy",
                    CurrentHp = 500,
                    MaxHp = 1000,
                    IsDead = false
                }
            }
        };
        plan.BattleStateJson = JsonSerializer.Serialize(battleState);
        
        _db.ActivityPlans.Add(plan);
        await _db.SaveChangesAsync();

        // 离线10分钟
        _testCharacter.LastSeenAtUtc = DateTime.UtcNow.AddMinutes(-10);
        await _db.SaveChangesAsync();

        // Act
        var result = await _service.CheckAndSettleAsync(_testCharacter.Id, maxOfflineSeconds: 43200);

        // Assert
        Assert.True(result.HasRunningPlan);
        Assert.NotNull(result.Settlement);
        
        // 验证战斗状态快照已更新
        var updatedPlan = await _planRepo.GetAsync(plan.Id);
        Assert.NotNull(updatedPlan);
        Assert.NotNull(updatedPlan.BattleStateJson);
        
        // 验证执行时长已累加
        Assert.True(updatedPlan.ExecutedSeconds >= 1100); // 约20分钟
        Assert.True(updatedPlan.ExecutedSeconds <= 1300);
    }

    [Fact]
    public async Task CheckAndSettleAsync_WithMaxOfflineCapExceeded_ShouldCapAtMaximum()
    {
        // Arrange
        var plan = CreateCombatPlan(
            characterId: _testCharacter.Id,
            limitType: LimitType.Infinite,
            limitValue: null,
            executedSeconds: 0,
            state: ActivityState.Running
        );
        _db.ActivityPlans.Add(plan);
        await _db.SaveChangesAsync();

        // 离线24小时（超过12小时上限）
        _testCharacter.LastSeenAtUtc = DateTime.UtcNow.AddHours(-24);
        await _db.SaveChangesAsync();

        // Act - 使用12小时上限
        var result = await _service.CheckAndSettleAsync(_testCharacter.Id, maxOfflineSeconds: 43200);

        // Assert
        Assert.True(result.HasRunningPlan);
        Assert.NotNull(result.Settlement);
        
        // 模拟时长应该被限制在12小时
        Assert.True(result.Settlement.SimulatedSeconds <= 43200);
        Assert.Equal(43200, result.Settlement.SimulatedSeconds);
    }

    [Fact]
    public async Task ApplySettlementAsync_ShouldUpdateCharacterGoldAndExp()
    {
        // Arrange
        var initialGold = _testCharacter.Gold;
        var initialExp = _testCharacter.Experience;
        
        var settlement = new OfflineFastForwardResult
        {
            CharacterId = _testCharacter.Id,
            PlanId = Guid.NewGuid(),
            SimulatedSeconds = 1800,
            PlanCompleted = false,
            Gold = 5000,
            Exp = 10000,
            UpdatedExecutedSeconds = 1800
        };

        // Act
        await _service.ApplySettlementAsync(_testCharacter.Id, settlement);

        // Assert
        var updatedCharacter = await _characterRepo.GetAsync(_testCharacter.Id);
        Assert.NotNull(updatedCharacter);
        Assert.Equal(initialGold + 5000, updatedCharacter.Gold);
        Assert.Equal(initialExp + 10000, updatedCharacter.Experience);
    }

    [Fact]
    public async Task ApplySettlementAsync_WithNonExistentCharacter_ShouldThrowException()
    {
        // Arrange
        var nonExistentCharacterId = Guid.NewGuid();
        var settlement = new OfflineFastForwardResult
        {
            CharacterId = nonExistentCharacterId,
            PlanId = Guid.NewGuid(),
            SimulatedSeconds = 1800,
            Gold = 1000,
            Exp = 2000,
            UpdatedExecutedSeconds = 1800
        };

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.ApplySettlementAsync(nonExistentCharacterId, settlement)
        );
    }

    // 辅助方法：创建战斗计划
    private ActivityPlan CreateCombatPlan(
        Guid characterId,
        LimitType limitType,
        double? limitValue,
        double executedSeconds,
        ActivityState state)
    {
        var payload = new CombatActivityPayload
        {
            EnemyId = "dummy",
            EnemyCount = 1,
            RespawnDelay = 3.0,
            Seed = 12345UL
        };

        return new ActivityPlan
        {
            Id = Guid.NewGuid(),
            CharacterId = characterId,
            SlotIndex = 0,
            Type = ActivityType.Combat,
            LimitType = limitType,
            LimitValue = limitValue,
            State = state,
            CreatedAt = DateTime.UtcNow,
            StartedAt = state == ActivityState.Running ? DateTime.UtcNow.AddSeconds(-executedSeconds) : null,
            PayloadJson = JsonSerializer.Serialize(payload),
            BattleId = state == ActivityState.Running ? Guid.NewGuid() : null,
            ExecutedSeconds = executedSeconds
        };
    }
}
