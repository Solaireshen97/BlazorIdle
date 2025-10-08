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
/// 测试无限时长任务的离线结算功能
/// </summary>
public class OfflineInfiniteTaskTests
{
    private readonly OfflineFastForwardEngine _engine;
    private readonly Character _testCharacter;

    public OfflineInfiniteTaskTests()
    {
        var simulator = new BattleSimulator();
        _engine = new OfflineFastForwardEngine(simulator);

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
    public void OfflineSettlement_InfiniteTask_ShouldNotMarkAsCompleted()
    {
        // Arrange
        var plan = CreateInfiniteCombatPlan();
        
        // Act
        var result = _engine.FastForward(_testCharacter, plan, 60.0); // 离线1分钟

        // Assert
        Assert.False(result.PlanCompleted, "无限时长任务不应该被标记为完成");
        Assert.Equal(ActivityState.Running, plan.State);
        Assert.Equal(60.0, result.SimulatedSeconds);
        Assert.Equal(60.0, result.UpdatedExecutedSeconds);
    }

    [Fact]
    public void OfflineSettlement_InfiniteTask_MultipleOfflinePeriods_ShouldAccumulateTime()
    {
        // Arrange
        var plan = CreateInfiniteCombatPlan();
        
        // Act - 第一次离线1分钟
        var result1 = _engine.FastForward(_testCharacter, plan, 60.0);
        
        // Act - 第二次离线2分钟（假设玩家短暂上线后再次离线）
        var result2 = _engine.FastForward(_testCharacter, plan, 120.0);

        // Assert
        Assert.False(result1.PlanCompleted, "第一次结算后不应完成");
        Assert.False(result2.PlanCompleted, "第二次结算后不应完成");
        Assert.Equal(60.0, result1.UpdatedExecutedSeconds);
        Assert.Equal(180.0, result2.UpdatedExecutedSeconds); // 60 + 120
        Assert.Equal(ActivityState.Running, plan.State);
    }

    [Fact]
    public void OfflineSettlement_InfiniteTask_LongOffline_ShouldRespect12HourCap()
    {
        // Arrange
        var plan = CreateInfiniteCombatPlan();
        var offlineSeconds = 86400.0; // 离线24小时
        
        // Act
        var result = _engine.FastForward(_testCharacter, plan, offlineSeconds);

        // Assert
        Assert.False(result.PlanCompleted, "无限任务永远不应完成");
        Assert.Equal(43200.0, result.SimulatedSeconds); // 应该被限制在12小时
        Assert.Equal(43200.0, result.UpdatedExecutedSeconds);
        Assert.True(result.Gold > 0, "应该获得金币收益");
        Assert.True(result.Exp > 0, "应该获得经验收益");
    }

    [Fact]
    public void OfflineSettlement_InfiniteTask_ShouldPreserveBattleState()
    {
        // Arrange
        var plan = CreateInfiniteCombatPlan();
        
        // Act - 第一次离线结算
        var result1 = _engine.FastForward(_testCharacter, plan, 30.0);
        
        // 验证战斗状态被保存
        Assert.NotNull(plan.BattleStateJson);
        Assert.NotEmpty(plan.BattleStateJson);
        
        // 保存战斗状态以便后续验证
        var savedBattleState = plan.BattleStateJson;
        
        // Act - 第二次离线结算（应该从保存的状态继续）
        var result2 = _engine.FastForward(_testCharacter, plan, 30.0);

        // Assert
        Assert.NotNull(plan.BattleStateJson);
        Assert.Equal(30.0, result1.SimulatedSeconds);
        Assert.Equal(30.0, result2.SimulatedSeconds);
        Assert.Equal(60.0, result2.UpdatedExecutedSeconds); // 累计时间
        Assert.False(result2.PlanCompleted);
    }

    [Fact]
    public void OfflineSettlement_InfiniteTask_VsDurationTask_DifferentBehavior()
    {
        // Arrange
        var infinitePlan = CreateInfiniteCombatPlan();
        var durationPlan = CreateCombatPlan(
            limitType: LimitType.Duration,
            limitValue: 100.0, // 100秒限制
            executedSeconds: 0
        );
        
        // Act - 两个计划都离线120秒
        var infiniteResult = _engine.FastForward(_testCharacter, infinitePlan, 120.0);
        var durationResult = _engine.FastForward(_testCharacter, durationPlan, 120.0);

        // Assert
        // 无限任务：模拟120秒，不完成
        Assert.False(infiniteResult.PlanCompleted);
        Assert.Equal(120.0, infiniteResult.SimulatedSeconds);
        Assert.Equal(ActivityState.Running, infinitePlan.State);
        
        // 有限任务：只模拟100秒（剩余时长），标记为完成
        Assert.True(durationResult.PlanCompleted);
        Assert.Equal(100.0, durationResult.SimulatedSeconds);
        Assert.Equal(ActivityState.Completed, durationPlan.State);
    }

    private ActivityPlan CreateInfiniteCombatPlan()
    {
        return CreateCombatPlan(LimitType.Infinite, null, 0);
    }

    private ActivityPlan CreateCombatPlan(
        LimitType limitType,
        double? limitValue,
        double executedSeconds)
    {
        var payload = new
        {
            EnemyId = "dummy",
            EnemyCount = 1,
            RespawnDelay = (double?)null,
            Seed = (ulong?)null
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
            StartedAt = DateTime.UtcNow,
            PayloadJson = JsonSerializer.Serialize(payload),
            ExecutedSeconds = executedSeconds
        };
    }
}
