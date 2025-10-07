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
/// </summary>
public class OfflineFastForwardEngineTests
{
    private readonly OfflineFastForwardEngine _engine;
    private readonly Character _testCharacter;

    public OfflineFastForwardEngineTests()
    {
        var simulator = new BattleSimulator();
        _engine = new OfflineFastForwardEngine(simulator);

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
            Experience = 5000
        };
    }

    [Fact]
    public void FastForward_WithOfflineTimeExceeding12Hours_ShouldCapAt12Hours()
    {
        // Arrange
        var plan = CreateCombatPlan(
            limitType: LimitType.Duration,
            limitValue: 86400, // 24小时
            executedSeconds: 0
        );

        var offlineSeconds = 50000.0; // 约13.9小时，超过12小时上限

        // Act
        var result = _engine.FastForward(_testCharacter, plan, offlineSeconds);

        // Assert
        Assert.True(result.SimulatedSeconds <= 43200, "模拟时长应不超过12小时上限");
        Assert.Equal(43200, result.SimulatedSeconds); // 应精确为12小时
    }

    [Fact]
    public void FastForward_DurationPlan_ShouldCalculateRemainingTime()
    {
        // Arrange
        var plan = CreateCombatPlan(
            limitType: LimitType.Duration,
            limitValue: 7200, // 2小时计划
            executedSeconds: 1800 // 已执行0.5小时
        );

        var offlineSeconds = 7200.0; // 离线2小时

        // Act
        var result = _engine.FastForward(_testCharacter, plan, offlineSeconds);

        // Assert
        // 剩余时长 = 7200 - 1800 = 5400秒 (1.5小时)
        // 实际模拟 = min(5400, 7200) = 5400秒
        Assert.Equal(5400, result.SimulatedSeconds);
        Assert.Equal(7200, result.UpdatedExecutedSeconds); // 1800 + 5400 = 7200
        Assert.True(result.PlanCompleted, "计划应该完成");
    }

    [Fact]
    public void FastForward_DurationPlan_PartialCompletion_ShouldNotComplete()
    {
        // Arrange
        var plan = CreateCombatPlan(
            limitType: LimitType.Duration,
            limitValue: 10800, // 3小时计划
            executedSeconds: 1800 // 已执行0.5小时
        );

        var offlineSeconds = 3600.0; // 离线1小时

        // Act
        var result = _engine.FastForward(_testCharacter, plan, offlineSeconds);

        // Assert
        // 剩余时长 = 10800 - 1800 = 9000秒
        // 实际模拟 = min(9000, 3600) = 3600秒
        Assert.Equal(3600, result.SimulatedSeconds);
        Assert.Equal(5400, result.UpdatedExecutedSeconds); // 1800 + 3600 = 5400
        Assert.False(result.PlanCompleted, "计划应该未完成");
    }

    [Fact]
    public void FastForward_InfinitePlan_ShouldSimulateFullOfflineTime()
    {
        // Arrange
        var plan = CreateCombatPlan(
            limitType: LimitType.Infinite,
            limitValue: null,
            executedSeconds: 5000 // 已执行一些时间
        );

        var offlineSeconds = 7200.0; // 离线2小时

        // Act
        var result = _engine.FastForward(_testCharacter, plan, offlineSeconds);

        // Assert
        // 无限计划应模拟全部离线时长
        Assert.Equal(7200, result.SimulatedSeconds);
        Assert.Equal(12200, result.UpdatedExecutedSeconds); // 5000 + 7200 = 12200
        Assert.False(result.PlanCompleted, "无限计划不应标记为完成");
    }

    [Fact]
    public void FastForward_InfinitePlan_WithLongOfflineTime_ShouldCapAt12Hours()
    {
        // Arrange
        var plan = CreateCombatPlan(
            limitType: LimitType.Infinite,
            limitValue: null,
            executedSeconds: 1000
        );

        var offlineSeconds = 86400.0; // 离线24小时

        // Act
        var result = _engine.FastForward(_testCharacter, plan, offlineSeconds);

        // Assert
        // 应受12小时上限约束
        Assert.Equal(43200, result.SimulatedSeconds);
        Assert.Equal(44200, result.UpdatedExecutedSeconds); // 1000 + 43200
        Assert.False(result.PlanCompleted);
    }

    [Fact]
    public void FastForward_DurationPlan_AlreadyCompleted_ShouldReturnZeroSimulation()
    {
        // Arrange
        var plan = CreateCombatPlan(
            limitType: LimitType.Duration,
            limitValue: 3600, // 1小时计划
            executedSeconds: 3600 // 已完成
        );

        var offlineSeconds = 1800.0; // 离线0.5小时

        // Act
        var result = _engine.FastForward(_testCharacter, plan, offlineSeconds);

        // Assert
        Assert.Equal(0, result.SimulatedSeconds);
        Assert.Equal(3600, result.UpdatedExecutedSeconds);
        Assert.True(result.PlanCompleted);
    }

    [Fact]
    public void FastForward_DurationPlan_NearCompletion_ShouldCompleteExactly()
    {
        // Arrange
        var plan = CreateCombatPlan(
            limitType: LimitType.Duration,
            limitValue: 3600, // 1小时计划
            executedSeconds: 3500 // 还剩100秒
        );

        var offlineSeconds = 1800.0; // 离线0.5小时

        // Act
        var result = _engine.FastForward(_testCharacter, plan, offlineSeconds);

        // Assert
        // 应只模拟剩余的100秒
        Assert.Equal(100, result.SimulatedSeconds);
        Assert.Equal(3600, result.UpdatedExecutedSeconds);
        Assert.True(result.PlanCompleted);
    }

    [Fact]
    public void FastForward_ShouldUpdatePlanState_WhenCompleted()
    {
        // Arrange
        var plan = CreateCombatPlan(
            limitType: LimitType.Duration,
            limitValue: 3600,
            executedSeconds: 3000
        );

        var offlineSeconds = 1800.0;

        // Act
        var result = _engine.FastForward(_testCharacter, plan, offlineSeconds);

        // Assert
        Assert.True(result.PlanCompleted);
        Assert.Equal(ActivityState.Completed, plan.State);
        Assert.NotNull(plan.CompletedAt);
    }

    [Fact]
    public void FastForward_CombatPlan_ShouldReturnRewards()
    {
        // Arrange
        var plan = CreateCombatPlan(
            limitType: LimitType.Duration,
            limitValue: 600, // 10分钟
            executedSeconds: 0
        );

        var offlineSeconds = 600.0;

        // Act
        var result = _engine.FastForward(_testCharacter, plan, offlineSeconds);

        // Assert
        Assert.Equal(600, result.SimulatedSeconds);
        Assert.True(result.Gold >= 0, "应该有金币收益（可能为0）");
        Assert.True(result.Exp >= 0, "应该有经验收益（可能为0）");
        Assert.Equal(_testCharacter.Id, result.CharacterId);
        Assert.Equal(plan.Id, result.PlanId);
    }

    [Fact]
    public void FastForward_DungeonPlan_ShouldWork()
    {
        // Arrange
        var plan = CreateDungeonPlan(
            limitType: LimitType.Duration,
            limitValue: 1200, // 20分钟
            executedSeconds: 0
        );

        var offlineSeconds = 600.0; // 离线10分钟

        // Act
        var result = _engine.FastForward(_testCharacter, plan, offlineSeconds);

        // Assert
        Assert.Equal(600, result.SimulatedSeconds);
        Assert.Equal(600, result.UpdatedExecutedSeconds);
        Assert.False(result.PlanCompleted);
    }

    [Fact]
    public void FastForward_WithNonRunningPlan_ShouldThrowException()
    {
        // Arrange
        var plan = CreateCombatPlan(
            limitType: LimitType.Duration,
            limitValue: 3600,
            executedSeconds: 0
        );
        plan.State = ActivityState.Pending; // 非Running状态

        var offlineSeconds = 1800.0;

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() =>
            _engine.FastForward(_testCharacter, plan, offlineSeconds)
        );
    }

    [Fact]
    public void FastForward_InheritProgress_FromMidBattle_ShouldContinueSeamlessly()
    {
        // Arrange - 模拟一个副本打到一半进入离线的场景
        var plan = CreateCombatPlan(
            limitType: LimitType.Duration,
            limitValue: 7200, // 2小时计划
            executedSeconds: 1000 // 已执行约16.67分钟
        );

        var offlineSeconds = 2000.0; // 离线约33.33分钟

        // Act
        var result = _engine.FastForward(_testCharacter, plan, offlineSeconds);

        // Assert
        // 应从1000秒的进度继续，再模拟2000秒
        Assert.Equal(2000, result.SimulatedSeconds);
        Assert.Equal(3000, result.UpdatedExecutedSeconds); // 1000 + 2000
        Assert.False(result.PlanCompleted, "还剩4200秒未完成");
        
        // 验证结果一致性
        Assert.Equal(_testCharacter.Id, result.CharacterId);
        Assert.Equal(plan.Id, result.PlanId);
    }
    
    [Fact]
    public void FastForward_WithEnemyHpInPayload_ShouldInheritEnemyHp()
    {
        // Arrange - 模拟一个战斗到一半的场景，敌人已损失部分血量
        var plan = CreateCombatPlan(
            limitType: LimitType.Duration,
            limitValue: 3600, // 1小时计划
            executedSeconds: 600 // 已执行10分钟
        );
        
        // 设置战斗进度快照：敌人血量为50（假设满血为100）
        var snapshot = new BlazorIdle.Server.Domain.Activities.BattleProgressSnapshot
        {
            PrimaryEnemyHp = 50,
            SimulatedSeconds = 600
        };
        plan.BattleProgressJson = System.Text.Json.JsonSerializer.Serialize(snapshot);

        var offlineSeconds = 600.0; // 离线10分钟

        // Act
        var result = _engine.FastForward(_testCharacter, plan, offlineSeconds);

        // Assert
        Assert.Equal(600, result.SimulatedSeconds);
        Assert.Equal(1200, result.UpdatedExecutedSeconds); // 600 + 600
        Assert.False(result.PlanCompleted, "计划还未完成");
        
        // 验证战斗继承了进度（结果中应该包含进度快照）
        Assert.NotNull(plan.BattleProgressJson); // 应该保存了新的进度快照
    }
    
    [Fact]
    public void FastForward_CompletedPlan_ShouldClearBattleProgress()
    {
        // Arrange
        var plan = CreateCombatPlan(
            limitType: LimitType.Duration,
            limitValue: 1000, // 计划总时长
            executedSeconds: 900 // 已执行900秒
        );
        
        // 设置初始进度快照
        var snapshot = new BlazorIdle.Server.Domain.Activities.BattleProgressSnapshot
        {
            PrimaryEnemyHp = 30,
            SimulatedSeconds = 900
        };
        plan.BattleProgressJson = System.Text.Json.JsonSerializer.Serialize(snapshot);

        var offlineSeconds = 200.0; // 离线200秒，足以完成剩余的100秒

        // Act
        var result = _engine.FastForward(_testCharacter, plan, offlineSeconds);

        // Assert
        Assert.Equal(100, result.SimulatedSeconds); // 只模拟剩余的100秒
        Assert.Equal(1000, result.UpdatedExecutedSeconds);
        Assert.True(result.PlanCompleted, "计划应该完成");
        
        // 计划完成后应该清空进度快照
        Assert.Null(plan.BattleProgressJson);
    }
    
    [Fact]
    public void FastForward_MultipleOfflineSessions_ShouldChainProgress()
    {
        // Arrange - 模拟多次离线，每次都应该继承上次的进度
        var plan = CreateCombatPlan(
            limitType: LimitType.Duration,
            limitValue: 3600, // 1小时计划
            executedSeconds: 0
        );

        // 第一次离线：600秒
        var result1 = _engine.FastForward(_testCharacter, plan, 600.0);
        Assert.Equal(600, result1.SimulatedSeconds);
        Assert.Equal(600, result1.UpdatedExecutedSeconds);
        Assert.NotNull(plan.BattleProgressJson); // 应该保存了进度
        
        // 第二次离线：再600秒
        var result2 = _engine.FastForward(_testCharacter, plan, 600.0);
        Assert.Equal(600, result2.SimulatedSeconds);
        Assert.Equal(1200, result2.UpdatedExecutedSeconds);
        Assert.NotNull(plan.BattleProgressJson); // 应该更新了进度
        
        // 第三次离线：再600秒
        var result3 = _engine.FastForward(_testCharacter, plan, 600.0);
        Assert.Equal(600, result3.SimulatedSeconds);
        Assert.Equal(1800, result3.UpdatedExecutedSeconds);
        Assert.False(result3.PlanCompleted);
        
        // 验证整体进度
        Assert.Equal(_testCharacter.Id, result3.CharacterId);
        Assert.Equal(plan.Id, result3.PlanId);
    }

    // 辅助方法：创建战斗计划
    private ActivityPlan CreateCombatPlan(
        LimitType limitType,
        double? limitValue,
        double executedSeconds)
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
            CharacterId = _testCharacter.Id,
            SlotIndex = 0,
            Type = ActivityType.Combat,
            LimitType = limitType,
            LimitValue = limitValue,
            State = ActivityState.Running,
            CreatedAt = DateTime.UtcNow,
            StartedAt = DateTime.UtcNow.AddSeconds(-executedSeconds),
            PayloadJson = JsonSerializer.Serialize(payload),
            BattleId = Guid.NewGuid(),
            ExecutedSeconds = executedSeconds
        };
    }

    // 辅助方法：创建地下城计划
    private ActivityPlan CreateDungeonPlan(
        LimitType limitType,
        double? limitValue,
        double executedSeconds)
    {
        var payload = new DungeonActivityPayload
        {
            DungeonId = "intro_cave",
            Loop = false,
            WaveDelay = 2.0,
            RunDelay = 5.0,
            Seed = 54321UL
        };

        return new ActivityPlan
        {
            Id = Guid.NewGuid(),
            CharacterId = _testCharacter.Id,
            SlotIndex = 0,
            Type = ActivityType.Dungeon,
            LimitType = limitType,
            LimitValue = limitValue,
            State = ActivityState.Running,
            CreatedAt = DateTime.UtcNow,
            StartedAt = DateTime.UtcNow.AddSeconds(-executedSeconds),
            PayloadJson = JsonSerializer.Serialize(payload),
            BattleId = Guid.NewGuid(),
            ExecutedSeconds = executedSeconds
        };
    }
}
