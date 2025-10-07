using BlazorIdle.Server.Application.Battles;
using BlazorIdle.Server.Application.Battles.Offline;
using BlazorIdle.Server.Domain.Activities;
using BlazorIdle.Server.Domain.Characters;
using BlazorIdle.Shared.Models;
using System;
using Xunit;

namespace BlazorIdle.Tests;

/// <summary>
/// 离线快进引擎单元测试
/// </summary>
public class OfflineFastForwardEngineTests
{
    private readonly BattleSimulator _simulator;
    private readonly OfflineFastForwardEngine _engine;

    public OfflineFastForwardEngineTests()
    {
        _simulator = new BattleSimulator();
        _engine = new OfflineFastForwardEngine(_simulator);
    }

    /// <summary>
    /// 创建测试角色
    /// </summary>
    private Character CreateTestCharacter()
    {
        return new Character
        {
            Id = Guid.NewGuid(),
            Name = "TestCharacter",
            Level = 10,
            Profession = Profession.Warrior,
            Strength = 20,
            Agility = 15,
            Intellect = 10,
            Stamina = 25,
            Gold = 1000,
            Experience = 5000,
            LastSeenAtUtc = DateTime.UtcNow.AddHours(-2)
        };
    }

    /// <summary>
    /// 创建测试活动计划（Duration类型）
    /// </summary>
    private ActivityPlan CreateDurationPlan(double limitSeconds, double executedSeconds = 0)
    {
        return new ActivityPlan
        {
            Id = Guid.NewGuid(),
            CharacterId = Guid.NewGuid(),
            SlotIndex = 0,
            Type = ActivityType.Combat,
            LimitType = LimitType.Duration,
            LimitValue = limitSeconds,
            State = ActivityState.Running,
            ExecutedSeconds = executedSeconds,
            PayloadJson = @"{""EnemyId"":""dummy"",""EnemyCount"":1,""Mode"":""continuous""}"
        };
    }

    /// <summary>
    /// 创建测试活动计划（Infinite类型）
    /// </summary>
    private ActivityPlan CreateInfinitePlan(double executedSeconds = 0)
    {
        return new ActivityPlan
        {
            Id = Guid.NewGuid(),
            CharacterId = Guid.NewGuid(),
            SlotIndex = 0,
            Type = ActivityType.Combat,
            LimitType = LimitType.Infinite,
            LimitValue = null,
            State = ActivityState.Running,
            ExecutedSeconds = executedSeconds,
            PayloadJson = @"{""EnemyId"":""dummy"",""EnemyCount"":1,""Mode"":""continuous""}"
        };
    }

    [Fact]
    public void FastForward_WithNullCharacter_ThrowsArgumentNullException()
    {
        // Arrange
        var plan = CreateDurationPlan(3600);

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => _engine.FastForward(null!, plan, 1800));
    }

    [Fact]
    public void FastForward_WithNullPlan_ThrowsArgumentNullException()
    {
        // Arrange
        var character = CreateTestCharacter();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => _engine.FastForward(character, null!, 1800));
    }

    [Fact]
    public void FastForward_WithNegativeOfflineSeconds_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var character = CreateTestCharacter();
        var plan = CreateDurationPlan(3600);

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => _engine.FastForward(character, plan, -100));
    }

    [Fact]
    public void FastForward_WithOfflineTimeExceeding12Hours_CapAt12Hours()
    {
        // Arrange
        var character = CreateTestCharacter();
        var plan = CreateInfinitePlan(); // 无限制计划
        var offlineSeconds = 50000.0; // 约13.9小时
        var maxCapSeconds = 43200.0; // 12小时

        // Act
        var result = _engine.FastForward(character, plan, offlineSeconds, maxCapSeconds);

        // Assert
        Assert.Equal(character.Id, result.CharacterId);
        Assert.Equal(plan.Id, result.PlanId);
        Assert.Equal(maxCapSeconds, result.SimulatedSeconds); // 应该被限制在12小时
        Assert.True(result.UpdatedExecutedSeconds <= maxCapSeconds);
    }

    [Fact]
    public void FastForward_DurationPlan_CalculatesRemainingTimeCorrectly()
    {
        // Arrange
        var character = CreateTestCharacter();
        var limitSeconds = 7200.0; // 2小时限制
        var executedSeconds = 1800.0; // 已执行30分钟
        var plan = CreateDurationPlan(limitSeconds, executedSeconds);
        var offlineSeconds = 3600.0; // 离线1小时

        // Act
        var result = _engine.FastForward(character, plan, offlineSeconds);

        // Assert
        Assert.Equal(character.Id, result.CharacterId);
        Assert.Equal(plan.Id, result.PlanId);
        
        // 剩余时长 = 7200 - 1800 = 5400秒
        // 实际模拟 = min(5400, 3600) = 3600秒
        Assert.Equal(3600.0, result.SimulatedSeconds);
        
        // 更新后的执行时长 = 1800 + 3600 = 5400秒
        Assert.Equal(5400.0, result.UpdatedExecutedSeconds);
        
        // 计划未完成（5400 < 7200）
        Assert.False(result.PlanCompleted);
    }

    [Fact]
    public void FastForward_DurationPlan_CompletesWhenRemainingTimeIsLessThanOfflineTime()
    {
        // Arrange
        var character = CreateTestCharacter();
        var limitSeconds = 3600.0; // 1小时限制
        var executedSeconds = 2700.0; // 已执行45分钟
        var plan = CreateDurationPlan(limitSeconds, executedSeconds);
        var offlineSeconds = 1800.0; // 离线30分钟

        // Act
        var result = _engine.FastForward(character, plan, offlineSeconds);

        // Assert
        Assert.Equal(character.Id, result.CharacterId);
        Assert.Equal(plan.Id, result.PlanId);
        
        // 剩余时长 = 3600 - 2700 = 900秒（15分钟）
        // 实际模拟 = min(900, 1800) = 900秒
        Assert.Equal(900.0, result.SimulatedSeconds);
        
        // 更新后的执行时长 = 2700 + 900 = 3600秒
        Assert.Equal(3600.0, result.UpdatedExecutedSeconds);
        
        // 计划已完成（3600 >= 3600）
        Assert.True(result.PlanCompleted);
    }

    [Fact]
    public void FastForward_DurationPlan_AlreadyCompleted_SimulatesZeroSeconds()
    {
        // Arrange
        var character = CreateTestCharacter();
        var limitSeconds = 3600.0; // 1小时限制
        var executedSeconds = 3600.0; // 已执行1小时（已完成）
        var plan = CreateDurationPlan(limitSeconds, executedSeconds);
        var offlineSeconds = 1800.0; // 离线30分钟

        // Act
        var result = _engine.FastForward(character, plan, offlineSeconds);

        // Assert
        Assert.Equal(character.Id, result.CharacterId);
        Assert.Equal(plan.Id, result.PlanId);
        
        // 剩余时长 = 3600 - 3600 = 0秒
        Assert.Equal(0.0, result.SimulatedSeconds);
        
        // 执行时长不变
        Assert.Equal(3600.0, result.UpdatedExecutedSeconds);
        
        // 计划已完成
        Assert.True(result.PlanCompleted);
        
        // 收益为0
        Assert.Equal(0, result.Gold);
        Assert.Equal(0, result.Exp);
        Assert.Equal(0, result.TotalKills);
    }

    [Fact]
    public void FastForward_InfinitePlan_SimulatesFullOfflineTime()
    {
        // Arrange
        var character = CreateTestCharacter();
        var executedSeconds = 5000.0; // 已执行约1.4小时
        var plan = CreateInfinitePlan(executedSeconds);
        var offlineSeconds = 3600.0; // 离线1小时

        // Act
        var result = _engine.FastForward(character, plan, offlineSeconds);

        // Assert
        Assert.Equal(character.Id, result.CharacterId);
        Assert.Equal(plan.Id, result.PlanId);
        
        // 无限制计划应该模拟全部离线时长
        Assert.Equal(3600.0, result.SimulatedSeconds);
        
        // 更新后的执行时长 = 5000 + 3600 = 8600秒
        Assert.Equal(8600.0, result.UpdatedExecutedSeconds);
        
        // 无限制计划永远不会完成
        Assert.False(result.PlanCompleted);
    }

    [Fact]
    public void FastForward_InfinitePlan_WithLongOfflineTime_CapsAt12Hours()
    {
        // Arrange
        var character = CreateTestCharacter();
        var plan = CreateInfinitePlan();
        var offlineSeconds = 100000.0; // 约27.8小时
        var maxCapSeconds = 43200.0; // 12小时

        // Act
        var result = _engine.FastForward(character, plan, offlineSeconds, maxCapSeconds);

        // Assert
        Assert.Equal(character.Id, result.CharacterId);
        Assert.Equal(plan.Id, result.PlanId);
        
        // 应该被限制在12小时
        Assert.Equal(maxCapSeconds, result.SimulatedSeconds);
        Assert.Equal(maxCapSeconds, result.UpdatedExecutedSeconds);
        
        // 无限制计划不会完成
        Assert.False(result.PlanCompleted);
    }

    [Fact]
    public void FastForward_GeneratesValidRewards()
    {
        // Arrange
        var character = CreateTestCharacter();
        var plan = CreateDurationPlan(3600.0);
        var offlineSeconds = 1800.0; // 离线30分钟

        // Act
        var result = _engine.FastForward(character, plan, offlineSeconds);

        // Assert
        Assert.Equal(character.Id, result.CharacterId);
        Assert.Equal(plan.Id, result.PlanId);
        Assert.Equal(1800.0, result.SimulatedSeconds);
        
        // 应该产生一些收益（具体数值取决于战斗模拟）
        Assert.True(result.Gold >= 0);
        Assert.True(result.Exp >= 0);
        Assert.True(result.TotalKills >= 0);
        Assert.NotNull(result.Loot);
    }

    [Fact]
    public void FastForward_WithShortOfflineTime_WorksCorrectly()
    {
        // Arrange
        var character = CreateTestCharacter();
        var plan = CreateDurationPlan(3600.0);
        var offlineSeconds = 60.0; // 离线1分钟

        // Act
        var result = _engine.FastForward(character, plan, offlineSeconds);

        // Assert
        Assert.Equal(character.Id, result.CharacterId);
        Assert.Equal(plan.Id, result.PlanId);
        Assert.Equal(60.0, result.SimulatedSeconds);
        Assert.Equal(60.0, result.UpdatedExecutedSeconds);
        Assert.False(result.PlanCompleted);
    }

    [Fact]
    public void FastForward_MultipleInvocations_AccumulateExecutedSeconds()
    {
        // Arrange
        var character = CreateTestCharacter();
        var plan = CreateDurationPlan(3600.0, 0); // 1小时限制，初始执行0秒

        // Act - 第一次离线30分钟
        var result1 = _engine.FastForward(character, plan, 1800.0);
        
        // 手动更新 plan 的 ExecutedSeconds（模拟数据库更新）
        plan.ExecutedSeconds = result1.UpdatedExecutedSeconds;
        
        // Act - 第二次离线30分钟
        var result2 = _engine.FastForward(character, plan, 1800.0);

        // Assert - 第一次
        Assert.Equal(1800.0, result1.SimulatedSeconds);
        Assert.Equal(1800.0, result1.UpdatedExecutedSeconds);
        Assert.False(result1.PlanCompleted);

        // Assert - 第二次
        Assert.Equal(1800.0, result2.SimulatedSeconds);
        Assert.Equal(3600.0, result2.UpdatedExecutedSeconds);
        Assert.True(result2.PlanCompleted); // 第二次应该完成
    }
}
