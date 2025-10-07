using BlazorIdle.Server.Application.Battles;
using BlazorIdle.Server.Application.Battles.Offline;
using BlazorIdle.Server.Domain.Activities;
using BlazorIdle.Server.Domain.Characters;
using BlazorIdle.Shared.Models;
using System;
using System.Text.Json;
using Xunit;

namespace BlazorIdle.Tests;

/// <summary>
/// OfflineFastForwardEngine 单元测试
/// 测试离线快进引擎的核心功能
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
    /// 创建测试用角色
    /// </summary>
    private Character CreateTestCharacter()
    {
        return new Character
        {
            Id = Guid.NewGuid(),
            Name = "TestWarrior",
            Profession = Profession.Warrior,
            Level = 1,
            Strength = 10,
            Agility = 10,
            Intellect = 10,
            Stamina = 10,
            Gold = 0,
            Experience = 0
        };
    }

    /// <summary>
    /// 创建测试用战斗计划
    /// </summary>
    private ActivityPlan CreateCombatPlan(
        Guid characterId,
        LimitType limitType,
        double? limitValue,
        double executedSeconds = 0)
    {
        var payload = new CombatActivityPayload
        {
            EnemyId = "dummy",
            EnemyCount = 1,
            RespawnDelay = 1.0,
            Seed = 12345
        };

        return new ActivityPlan
        {
            Id = Guid.NewGuid(),
            CharacterId = characterId,
            SlotIndex = 0,
            Type = ActivityType.Combat,
            LimitType = limitType,
            LimitValue = limitValue,
            State = ActivityState.Running,
            CreatedAt = DateTime.UtcNow,
            StartedAt = DateTime.UtcNow,
            PayloadJson = JsonSerializer.Serialize(payload),
            ExecutedSeconds = executedSeconds
        };
    }

    [Fact]
    public void FastForward_ShouldCapOfflineTimeAt12Hours()
    {
        // Arrange
        var character = CreateTestCharacter();
        var plan = CreateCombatPlan(character.Id, LimitType.Infinite, null);

        // 20小时的离线时间
        var offlineSeconds = 20 * 3600.0;

        // Act
        var result = _engine.FastForward(character, plan, offlineSeconds);

        // Assert
        // 应该只模拟12小时
        Assert.Equal(12 * 3600.0, result.SimulatedSeconds);
        Assert.Equal(character.Id, result.CharacterId);
        Assert.Equal(plan.Id, result.PlanId);
    }

    [Fact]
    public void FastForward_InfinitePlan_ShouldUseFullOfflineTime()
    {
        // Arrange
        var character = CreateTestCharacter();
        var plan = CreateCombatPlan(character.Id, LimitType.Infinite, null);

        // 5小时的离线时间
        var offlineSeconds = 5 * 3600.0;

        // Act
        var result = _engine.FastForward(character, plan, offlineSeconds);

        // Assert
        // 无限模式应该使用全部离线时间
        Assert.Equal(offlineSeconds, result.SimulatedSeconds);
        Assert.False(result.PlanCompleted); // 无限模式永远不会完成
        Assert.Equal(offlineSeconds, result.UpdatedExecutedSeconds);
    }

    [Fact]
    public void FastForward_DurationPlan_ShouldCalculateRemainingTime()
    {
        // Arrange
        var character = CreateTestCharacter();
        var limitValue = 3600.0; // 1小时限制
        var executedSeconds = 1800.0; // 已执行30分钟
        var plan = CreateCombatPlan(character.Id, LimitType.Duration, limitValue, executedSeconds);

        // 2小时的离线时间
        var offlineSeconds = 2 * 3600.0;

        // Act
        var result = _engine.FastForward(character, plan, offlineSeconds);

        // Assert
        // 应该只模拟剩余的30分钟（1800秒）
        Assert.Equal(1800.0, result.SimulatedSeconds);
        Assert.True(result.PlanCompleted); // 应该完成
        Assert.Equal(limitValue, result.UpdatedExecutedSeconds); // 应该达到限制值
    }

    [Fact]
    public void FastForward_DurationPlan_WhenAlreadyCompleted_ShouldReturnZeroTime()
    {
        // Arrange
        var character = CreateTestCharacter();
        var limitValue = 3600.0; // 1小时限制
        var executedSeconds = 3600.0; // 已执行1小时（完成）
        var plan = CreateCombatPlan(character.Id, LimitType.Duration, limitValue, executedSeconds);

        // 1小时的离线时间
        var offlineSeconds = 3600.0;

        // Act
        var result = _engine.FastForward(character, plan, offlineSeconds);

        // Assert
        // 计划已完成，不应该模拟任何时间
        Assert.Equal(0, result.SimulatedSeconds);
        Assert.True(result.PlanCompleted);
        Assert.Equal(executedSeconds, result.UpdatedExecutedSeconds); // 保持不变
    }

    [Fact]
    public void FastForward_DurationPlan_WhenOfflineTimeLessThanRemaining_ShouldUseOfflineTime()
    {
        // Arrange
        var character = CreateTestCharacter();
        var limitValue = 7200.0; // 2小时限制
        var executedSeconds = 1800.0; // 已执行30分钟
        var plan = CreateCombatPlan(character.Id, LimitType.Duration, limitValue, executedSeconds);

        // 30分钟的离线时间（小于剩余的1.5小时）
        var offlineSeconds = 1800.0;

        // Act
        var result = _engine.FastForward(character, plan, offlineSeconds);

        // Assert
        // 应该模拟全部离线时间
        Assert.Equal(offlineSeconds, result.SimulatedSeconds);
        Assert.False(result.PlanCompleted); // 还未完成
        Assert.Equal(3600.0, result.UpdatedExecutedSeconds); // 30分钟 + 30分钟 = 1小时
    }

    [Fact]
    public void FastForward_ShouldGenerateSegments()
    {
        // Arrange
        var character = CreateTestCharacter();
        var plan = CreateCombatPlan(character.Id, LimitType.Duration, 60.0); // 1分钟计划

        // 1分钟的离线时间
        var offlineSeconds = 60.0;

        // Act
        var result = _engine.FastForward(character, plan, offlineSeconds);

        // Assert
        // 应该生成战斗段数据
        Assert.NotEmpty(result.Segments);
        Assert.True(result.Segments.Count > 0);
    }

    [Fact]
    public void FastForward_ShouldCalculateRewards()
    {
        // Arrange
        var character = CreateTestCharacter();
        var plan = CreateCombatPlan(character.Id, LimitType.Duration, 60.0); // 1分钟计划

        // 1分钟的离线时间
        var offlineSeconds = 60.0;

        // Act
        var result = _engine.FastForward(character, plan, offlineSeconds);

        // Assert
        // 应该有战斗收益（金币、经验等）
        // 具体数值取决于敌人配置，这里只验证字段存在
        Assert.True(result.Gold >= 0);
        Assert.True(result.Exp >= 0);
        Assert.NotNull(result.Loot);
    }

    [Fact]
    public void FastForward_ShouldUpdateExecutedSeconds_ForSeamlessContinuation()
    {
        // Arrange
        var character = CreateTestCharacter();
        var limitValue = 7200.0; // 2小时限制
        var executedSeconds = 3000.0; // 已执行50分钟
        var plan = CreateCombatPlan(character.Id, LimitType.Duration, limitValue, executedSeconds);

        // 30分钟的离线时间
        var offlineSeconds = 1800.0;

        // Act
        var result = _engine.FastForward(character, plan, offlineSeconds);

        // Assert
        // ExecutedSeconds 应该累加，实现无感继承
        Assert.Equal(executedSeconds + offlineSeconds, result.UpdatedExecutedSeconds);
        Assert.Equal(4800.0, result.UpdatedExecutedSeconds); // 50分钟 + 30分钟 = 80分钟

        // 验证计划对象本身也被更新了
        Assert.Equal(4800.0, plan.ExecutedSeconds);
    }

    [Fact]
    public void FastForward_SeamlessContinuation_FromMidBattle()
    {
        // Arrange
        var character = CreateTestCharacter();
        var limitValue = 3600.0; // 1小时限制
        var executedSeconds = 1200.0; // 已执行20分钟（副本打到一半）
        var plan = CreateCombatPlan(character.Id, LimitType.Duration, limitValue, executedSeconds);

        // 离线10分钟
        var offlineSeconds = 600.0;

        // Act
        var result = _engine.FastForward(character, plan, offlineSeconds);

        // Assert
        // 应该从离线点继续计算
        Assert.Equal(offlineSeconds, result.SimulatedSeconds);
        Assert.Equal(1800.0, result.UpdatedExecutedSeconds); // 20 + 10 = 30分钟
        Assert.False(result.PlanCompleted); // 还剩30分钟
    }

    [Fact]
    public void FastForward_MultipleOfflinePeriods_ShouldAccumulateCorrectly()
    {
        // Arrange
        var character = CreateTestCharacter();
        var limitValue = 3600.0; // 1小时限制
        var plan = CreateCombatPlan(character.Id, LimitType.Duration, limitValue, 0);

        // 模拟多次离线
        // 第一次离线20分钟
        var result1 = _engine.FastForward(character, plan, 1200.0);
        Assert.Equal(1200.0, result1.UpdatedExecutedSeconds);
        Assert.False(result1.PlanCompleted);

        // 第二次离线20分钟
        var result2 = _engine.FastForward(character, plan, 1200.0);
        Assert.Equal(2400.0, result2.UpdatedExecutedSeconds);
        Assert.False(result2.PlanCompleted);

        // 第三次离线30分钟（超过剩余时间）
        var result3 = _engine.FastForward(character, plan, 1800.0);
        Assert.Equal(1200.0, result3.SimulatedSeconds); // 只模拟剩余的20分钟
        Assert.Equal(3600.0, result3.UpdatedExecutedSeconds); // 达到限制
        Assert.True(result3.PlanCompleted); // 完成
    }

    [Fact]
    public void FastForward_WithDungeonPlan_ShouldWork()
    {
        // Arrange
        var character = CreateTestCharacter();
        var payload = new DungeonActivityPayload
        {
            DungeonId = "intro_cave",
            Loop = false,
            WaveDelay = 1.0,
            RunDelay = 2.0,
            Seed = 12345
        };

        var plan = new ActivityPlan
        {
            Id = Guid.NewGuid(),
            CharacterId = character.Id,
            SlotIndex = 0,
            Type = ActivityType.Dungeon,
            LimitType = LimitType.Duration,
            LimitValue = 300.0, // 5分钟
            State = ActivityState.Running,
            CreatedAt = DateTime.UtcNow,
            StartedAt = DateTime.UtcNow,
            PayloadJson = JsonSerializer.Serialize(payload),
            ExecutedSeconds = 0
        };

        // 2分钟的离线时间
        var offlineSeconds = 120.0;

        // Act
        var result = _engine.FastForward(character, plan, offlineSeconds);

        // Assert
        Assert.Equal(offlineSeconds, result.SimulatedSeconds);
        Assert.False(result.PlanCompleted); // 还有3分钟
        Assert.Equal(offlineSeconds, result.UpdatedExecutedSeconds);
        Assert.NotEmpty(result.Segments);
    }

    [Fact]
    public void FastForward_ResultShouldContainCharacterAndPlanIds()
    {
        // Arrange
        var character = CreateTestCharacter();
        var plan = CreateCombatPlan(character.Id, LimitType.Duration, 60.0);
        var offlineSeconds = 30.0;

        // Act
        var result = _engine.FastForward(character, plan, offlineSeconds);

        // Assert
        Assert.Equal(character.Id, result.CharacterId);
        Assert.Equal(plan.Id, result.PlanId);
    }
}
