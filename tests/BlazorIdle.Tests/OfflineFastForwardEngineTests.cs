using BlazorIdle.Server.Application.Battles;
using BlazorIdle.Server.Application.Battles.Offline;
using BlazorIdle.Server.Domain.Activities;
using BlazorIdle.Server.Domain.Characters;
using BlazorIdle.Shared.Models;
using System;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace BlazorIdle.Tests;

/// <summary>
/// 离线快进引擎单元测试
/// 测试重点：
/// 1. 离线时长上限控制（12小时）
/// 2. Duration 计划剩余时长计算
/// 3. Infinite 计划模拟全部离线时长
/// 4. 计划完成状态判断
/// 5. 无感衔接（从 ExecutedSeconds 继续）
/// </summary>
public class OfflineFastForwardEngineTests
{
    private readonly BattleSimulator _simulator;
    private readonly FakeCharacterRepository _characterRepo;
    private readonly OfflineFastForwardEngine _engine;

    public OfflineFastForwardEngineTests()
    {
        _simulator = new BattleSimulator();
        _characterRepo = new FakeCharacterRepository();
        _engine = new OfflineFastForwardEngine(_simulator, _characterRepo);
    }

    [Fact]
    public async Task FastForward_WithOfflineTimeExceedingCap_OnlySimulatesCapDuration()
    {
        // Arrange: 离线15小时，但上限12小时
        var character = CreateTestCharacter();
        var plan = CreateCombatPlan(limitType: LimitType.Duration, limitValue: 50000); // 约13.9小时
        var offlineSeconds = 15 * 3600; // 15小时
        var maxCapSeconds = 12 * 3600; // 12小时

        // Act
        var result = await _engine.FastForwardAsync(character, plan, offlineSeconds, maxCapSeconds);

        // Assert: 应该只模拟12小时
        Assert.True(result.SimulatedSeconds <= maxCapSeconds);
        Assert.Equal(maxCapSeconds, result.SimulatedSeconds);
    }

    [Fact]
    public async Task FastForward_WithDurationPlan_CalculatesRemainingTimeCorrectly()
    {
        // Arrange: Duration 计划，总3600秒，已执行1000秒，离线5000秒
        var character = CreateTestCharacter();
        var plan = CreateCombatPlan(limitType: LimitType.Duration, limitValue: 3600);
        plan.ExecutedSeconds = 1000; // 已执行1000秒
        var offlineSeconds = 5000;

        // Act
        var result = await _engine.FastForwardAsync(character, plan, offlineSeconds);

        // Assert: 剩余2600秒，应该模拟2600秒
        Assert.Equal(2600, result.SimulatedSeconds);
        Assert.Equal(3600, result.UpdatedExecutedSeconds); // 1000 + 2600
        Assert.True(result.PlanCompleted); // 计划应该完成
    }

    [Fact]
    public async Task FastForward_WithDurationPlan_AlreadyCompleted_ReturnsZeroSimulation()
    {
        // Arrange: Duration 计划已完成
        var character = CreateTestCharacter();
        var plan = CreateCombatPlan(limitType: LimitType.Duration, limitValue: 3600);
        plan.ExecutedSeconds = 3600; // 已完成
        var offlineSeconds = 1000;

        // Act
        var result = await _engine.FastForwardAsync(character, plan, offlineSeconds);

        // Assert: 无需模拟
        Assert.Equal(0, result.SimulatedSeconds);
        Assert.True(result.PlanCompleted);
        Assert.Equal(3600, result.UpdatedExecutedSeconds);
    }

    [Fact]
    public async Task FastForward_WithInfinitePlan_SimulatesAllAvailableTime()
    {
        // Arrange: Infinite 计划，离线3600秒
        var character = CreateTestCharacter();
        var plan = CreateCombatPlan(limitType: LimitType.Infinite, limitValue: null);
        plan.ExecutedSeconds = 1000; // 已执行1000秒（Infinite 类型不影响）
        var offlineSeconds = 3600;

        // Act
        var result = await _engine.FastForwardAsync(character, plan, offlineSeconds);

        // Assert: 应该模拟全部3600秒
        Assert.Equal(3600, result.SimulatedSeconds);
        Assert.Equal(4600, result.UpdatedExecutedSeconds); // 1000 + 3600
        Assert.False(result.PlanCompleted); // Infinite 永不完成
    }

    [Fact]
    public async Task FastForward_WithDurationPlanPartialComplete_StopsAtLimit()
    {
        // Arrange: Duration 计划，总2000秒，已执行1500秒，离线1000秒
        var character = CreateTestCharacter();
        var plan = CreateCombatPlan(limitType: LimitType.Duration, limitValue: 2000);
        plan.ExecutedSeconds = 1500;
        var offlineSeconds = 1000;

        // Act
        var result = await _engine.FastForwardAsync(character, plan, offlineSeconds);

        // Assert: 剩余500秒，只模拟500秒
        Assert.Equal(500, result.SimulatedSeconds);
        Assert.Equal(2000, result.UpdatedExecutedSeconds);
        Assert.True(result.PlanCompleted);
    }

    [Fact]
    public async Task FastForward_GeneratesSegmentsAndEconomyRewards()
    {
        // Arrange
        var character = CreateTestCharacter();
        var plan = CreateCombatPlan(limitType: LimitType.Duration, limitValue: 100);
        var offlineSeconds = 100;

        // Act
        var result = await _engine.FastForwardAsync(character, plan, offlineSeconds);

        // Assert: 应该有战斗段和经济收益
        Assert.NotEmpty(result.Segments);
        Assert.True(result.Gold >= 0);
        Assert.True(result.Exp >= 0);
        Assert.True(result.TotalKills >= 0);
    }

    [Fact]
    public async Task FastForward_WithDungeonPlan_ParsesConfigurationCorrectly()
    {
        // Arrange: 地城计划
        var character = CreateTestCharacter();
        var plan = CreateDungeonPlan(dungeonId: "intro_cave", loop: false);
        var offlineSeconds = 200;

        // Act
        var result = await _engine.FastForwardAsync(character, plan, offlineSeconds);

        // Assert: 应该成功模拟
        Assert.Equal(200, result.SimulatedSeconds);
        Assert.NotNull(result.Segments);
    }

    [Fact]
    public async Task FastForward_SeamlessContinuation_FromOfflinePoint()
    {
        // Arrange: 模拟无感衔接场景
        // 场景：战斗打到一半（ExecutedSeconds=500），离线300秒，再上线
        var character = CreateTestCharacter();
        var plan = CreateCombatPlan(limitType: LimitType.Duration, limitValue: 1000);
        plan.ExecutedSeconds = 500; // 已执行500秒

        // Act: 第一次离线300秒
        var result1 = await _engine.FastForwardAsync(character, plan, 300);

        // Assert: 应该从500秒继续，模拟300秒
        Assert.Equal(300, result1.SimulatedSeconds);
        Assert.Equal(800, result1.UpdatedExecutedSeconds); // 500 + 300
        Assert.False(result1.PlanCompleted); // 还剩200秒

        // Arrange: 更新计划状态后再次离线
        plan.ExecutedSeconds = result1.UpdatedExecutedSeconds;

        // Act: 第二次离线500秒
        var result2 = await _engine.FastForwardAsync(character, plan, 500);

        // Assert: 应该从800秒继续，只模拟剩余200秒
        Assert.Equal(200, result2.SimulatedSeconds);
        Assert.Equal(1000, result2.UpdatedExecutedSeconds);
        Assert.True(result2.PlanCompleted);
    }

    [Fact]
    public async Task FastForward_WithVeryShortOfflineTime_HandlesCorrectly()
    {
        // Arrange: 离线时间很短（10秒）
        var character = CreateTestCharacter();
        var plan = CreateCombatPlan(limitType: LimitType.Duration, limitValue: 1000);
        var offlineSeconds = 10;

        // Act
        var result = await _engine.FastForwardAsync(character, plan, offlineSeconds);

        // Assert
        Assert.Equal(10, result.SimulatedSeconds);
        Assert.Equal(10, result.UpdatedExecutedSeconds);
        Assert.False(result.PlanCompleted);
    }

    // Helper methods
    private Character CreateTestCharacter()
    {
        return new Character
        {
            Id = Guid.NewGuid(),
            Name = "TestUser",
            Profession = Profession.Warrior,
            Strength = 10,
            Agility = 10,
            Intellect = 10,
            Stamina = 10,
            Level = 1,
            Experience = 0,
            Gold = 1000
        };
    }

    private ActivityPlan CreateCombatPlan(LimitType limitType, double? limitValue)
    {
        var payload = new CombatActivityPayload
        {
            EnemyId = "dummy",
            EnemyCount = 1
        };

        return new ActivityPlan
        {
            Id = Guid.NewGuid(),
            CharacterId = Guid.NewGuid(),
            SlotIndex = 0,
            Type = ActivityType.Combat,
            LimitType = limitType,
            LimitValue = limitValue,
            State = ActivityState.Running,
            PayloadJson = JsonSerializer.Serialize(payload),
            BattleId = Guid.NewGuid(),
            ExecutedSeconds = 0,
            CreatedAt = DateTime.UtcNow,
            StartedAt = DateTime.UtcNow
        };
    }

    private ActivityPlan CreateDungeonPlan(string dungeonId, bool loop)
    {
        var payload = new DungeonActivityPayload
        {
            DungeonId = dungeonId,
            Loop = loop
        };

        return new ActivityPlan
        {
            Id = Guid.NewGuid(),
            CharacterId = Guid.NewGuid(),
            SlotIndex = 0,
            Type = ActivityType.Dungeon,
            LimitType = LimitType.Duration,
            LimitValue = 1000,
            State = ActivityState.Running,
            PayloadJson = JsonSerializer.Serialize(payload),
            BattleId = Guid.NewGuid(),
            ExecutedSeconds = 0,
            CreatedAt = DateTime.UtcNow,
            StartedAt = DateTime.UtcNow
        };
    }

    // Fake repository for testing
    private class FakeCharacterRepository : BlazorIdle.Server.Application.Abstractions.ICharacterRepository
    {
        public Task<Character?> GetAsync(Guid id, System.Threading.CancellationToken ct = default)
        {
            return Task.FromResult<Character?>(null);
        }

        public Task<Character?> GetByUsernameAsync(string username, System.Threading.CancellationToken ct = default)
        {
            return Task.FromResult<Character?>(null);
        }

        public Task<System.Collections.Generic.IEnumerable<Character>> GetByUserIdAsync(Guid userId, System.Threading.CancellationToken ct = default)
        {
            return Task.FromResult<System.Collections.Generic.IEnumerable<Character>>(Array.Empty<Character>());
        }

        public Task AddAsync(Character character, System.Threading.CancellationToken ct = default)
        {
            return Task.CompletedTask;
        }

        public Task UpdateAsync(Character character, System.Threading.CancellationToken ct = default)
        {
            return Task.CompletedTask;
        }

        public Task DeleteAsync(Guid id, System.Threading.CancellationToken ct = default)
        {
            return Task.CompletedTask;
        }
    }
}
