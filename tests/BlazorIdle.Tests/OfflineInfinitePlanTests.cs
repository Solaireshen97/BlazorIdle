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
/// 测试无限时长任务的离线功能
/// </summary>
public class OfflineInfinitePlanTests
{
    private readonly OfflineFastForwardEngine _engine;
    private readonly Character _testCharacter;

    public OfflineInfinitePlanTests()
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
    public void InfinitePlan_AfterOffline1Minute_ShouldNotComplete()
    {
        // Arrange - 创建无限时长的战斗计划
        var plan = CreateInfiniteCombatPlan();
        
        // Act - 模拟离线1分钟（60秒）
        var result = _engine.FastForward(_testCharacter, plan, 60.0);

        // Assert
        Assert.Equal(60, result.SimulatedSeconds);
        Assert.Equal(60, result.UpdatedExecutedSeconds);
        Assert.False(result.PlanCompleted, "无限计划不应该被标记为完成");
        Assert.Equal(ActivityState.Running, plan.State);
        Assert.Null(plan.CompletedAt);
    }

    [Fact]
    public void InfinitePlan_MultipleOfflineSessions_ShouldNeverComplete()
    {
        // Arrange
        var plan = CreateInfiniteCombatPlan();

        // Act - 模拟多次离线，每次1分钟，共10次（10分钟）
        for (int i = 0; i < 10; i++)
        {
            var result = _engine.FastForward(_testCharacter, plan, 60.0);
            
            // Assert for each iteration
            Assert.False(result.PlanCompleted, $"第{i+1}次离线后，无限计划不应该被标记为完成");
            Assert.Equal(ActivityState.Running, plan.State);
            Assert.Equal((i + 1) * 60, result.UpdatedExecutedSeconds);
        }

        // Final assertion
        Assert.Equal(600, plan.ExecutedSeconds);
        Assert.Equal(ActivityState.Running, plan.State);
        Assert.False(plan.IsLimitReached());
    }

    [Fact]
    public void InfinitePlan_LongOffline_ShouldCapAt12Hours()
    {
        // Arrange
        var plan = CreateInfiniteCombatPlan();

        // Act - 模拟离线24小时
        var result = _engine.FastForward(_testCharacter, plan, 86400.0);

        // Assert - 应该被限制在12小时（43200秒）
        Assert.Equal(43200, result.SimulatedSeconds);
        Assert.Equal(43200, result.UpdatedExecutedSeconds);
        Assert.False(result.PlanCompleted);
        Assert.Equal(ActivityState.Running, plan.State);
    }

    [Fact]
    public void InfinitePlan_ShouldContinueAccumulatingRewards()
    {
        // Arrange
        var plan = CreateInfiniteCombatPlan();
        long totalGold = 0;
        long totalExp = 0;
        int totalKills = 0;

        // Act - 模拟5次离线，每次2分钟（120秒）
        for (int i = 0; i < 5; i++)
        {
            var result = _engine.FastForward(_testCharacter, plan, 120.0);
            totalGold += result.Gold;
            totalExp += result.Exp;
            totalKills += result.TotalKills;
        }

        // Assert
        Assert.True(totalGold >= 0, "应该有金币收益（可能为0）");
        Assert.True(totalExp >= 0, "应该有经验收益（可能为0）");
        Assert.Equal(600, plan.ExecutedSeconds);
        Assert.False(plan.IsLimitReached());
        Assert.Equal(ActivityState.Running, plan.State);
    }

    [Fact]
    public void InfinitePlan_ShouldMaintainBattleState()
    {
        // Arrange
        var plan = CreateInfiniteCombatPlan();

        // Act - 第一次离线
        var result1 = _engine.FastForward(_testCharacter, plan, 30.0);
        Assert.NotNull(plan.BattleStateJson);
        var state1 = plan.BattleStateJson;

        // Act - 第二次离线
        var result2 = _engine.FastForward(_testCharacter, plan, 30.0);
        Assert.NotNull(plan.BattleStateJson);
        var state2 = plan.BattleStateJson;

        // Assert - 战斗状态应该保持并更新
        Assert.NotEqual(state1, state2);
        Assert.Equal(60, plan.ExecutedSeconds);
        Assert.False(result2.PlanCompleted);
        Assert.Equal(ActivityState.Running, plan.State);
        
        // 战斗状态不应该被清空（因为计划还在运行）
        Assert.NotNull(plan.BattleStateJson);
    }

    // 辅助方法：创建无限战斗计划
    private ActivityPlan CreateInfiniteCombatPlan()
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
            LimitType = LimitType.Infinite,
            LimitValue = null,  // 无限计划没有限制值
            State = ActivityState.Running,
            CreatedAt = DateTime.UtcNow,
            StartedAt = DateTime.UtcNow,
            PayloadJson = JsonSerializer.Serialize(payload),
            BattleId = Guid.NewGuid(),
            ExecutedSeconds = 0
        };
    }
}
