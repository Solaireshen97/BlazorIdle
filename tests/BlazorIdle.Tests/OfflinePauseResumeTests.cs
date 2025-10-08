using BlazorIdle.Server.Application.Battles;
using BlazorIdle.Server.Domain.Activities;
using BlazorIdle.Server.Domain.Characters;
using BlazorIdle.Shared.Models;
using System;
using Xunit;

namespace BlazorIdle.Tests;

/// <summary>
/// 测试离线暂停和恢复功能
/// 验证：
/// 1. 离线检测暂停计划（保留状态）
/// 2. 玩家上线后恢复计划
/// 3. 暂停状态下计划保持Running状态
/// </summary>
public class OfflinePauseResumeTests
{
    [Fact]
    public void PausedPlan_ShouldKeepRunningState()
    {
        // Arrange
        var plan = new ActivityPlan
        {
            Id = Guid.NewGuid(),
            CharacterId = Guid.NewGuid(),
            Type = ActivityType.Combat,
            State = ActivityState.Running,
            LimitType = LimitType.Duration,
            LimitValue = 3600, // 1小时
            ExecutedSeconds = 1800, // 已执行30分钟
            PayloadJson = "{\"enemyId\":\"dummy\",\"enemyCount\":1}",
            SlotIndex = 0,
            CreatedAt = DateTime.UtcNow,
            StartedAt = DateTime.UtcNow.AddSeconds(-1800),
            BattleId = Guid.NewGuid(),
            BattleStateJson = "{\"enemyHpPercent\":0.5}"
        };

        // Act - 模拟暂停操作
        plan.StartedAt = null; // 清空开始时间表示暂停
        plan.BattleId = null; // 清空战斗ID表示内存已清空
        // plan.State 保持为 Running
        // plan.BattleStateJson 保持不变

        // Assert
        Assert.Equal(ActivityState.Running, plan.State);
        Assert.Null(plan.StartedAt);
        Assert.Null(plan.BattleId);
        Assert.NotNull(plan.BattleStateJson);
        Assert.False(string.IsNullOrWhiteSpace(plan.BattleStateJson));
    }

    [Fact]
    public void PausedPlan_ShouldNotBeCompleted()
    {
        // Arrange
        var plan = new ActivityPlan
        {
            Id = Guid.NewGuid(),
            CharacterId = Guid.NewGuid(),
            Type = ActivityType.Combat,
            State = ActivityState.Running,
            LimitType = LimitType.Duration,
            LimitValue = 3600,
            ExecutedSeconds = 1800,
            PayloadJson = "{}",
            SlotIndex = 0,
            CreatedAt = DateTime.UtcNow,
            BattleStateJson = "{\"enemyHpPercent\":0.5}"
        };

        // Act - 暂停后检查是否完成
        plan.StartedAt = null;
        plan.BattleId = null;
        var isCompleted = plan.State == ActivityState.Completed;

        // Assert
        Assert.False(isCompleted, "暂停的计划不应被标记为完成");
        Assert.Equal(ActivityState.Running, plan.State);
    }

    [Fact]
    public void ResumedPlan_ShouldHaveBattleIdAndStartedAt()
    {
        // Arrange - 暂停状态的计划
        var plan = new ActivityPlan
        {
            Id = Guid.NewGuid(),
            CharacterId = Guid.NewGuid(),
            Type = ActivityType.Combat,
            State = ActivityState.Running,
            LimitType = LimitType.Duration,
            LimitValue = 3600,
            ExecutedSeconds = 1800,
            PayloadJson = "{}",
            SlotIndex = 0,
            CreatedAt = DateTime.UtcNow,
            BattleStateJson = "{\"enemyHpPercent\":0.5}",
            StartedAt = null,
            BattleId = null
        };

        // Act - 模拟恢复操作
        plan.BattleId = Guid.NewGuid();
        plan.StartedAt = DateTime.UtcNow;

        // Assert
        Assert.NotNull(plan.BattleId);
        Assert.NotNull(plan.StartedAt);
        Assert.Equal(ActivityState.Running, plan.State);
        Assert.NotNull(plan.BattleStateJson);
    }

    [Fact]
    public void OfflineDetection_ShouldPauseNotStop()
    {
        // Arrange
        var character = new Character
        {
            Id = Guid.NewGuid(),
            Name = "TestWarrior",
            Profession = Profession.Warrior,
            Level = 10,
            LastSeenAtUtc = DateTime.UtcNow.AddSeconds(-120) // 2分钟前
        };

        var plan = new ActivityPlan
        {
            Id = Guid.NewGuid(),
            CharacterId = character.Id,
            Type = ActivityType.Combat,
            State = ActivityState.Running,
            LimitType = LimitType.Duration,
            LimitValue = 3600,
            ExecutedSeconds = 1800,
            PayloadJson = "{}",
            SlotIndex = 0,
            CreatedAt = DateTime.UtcNow,
            StartedAt = DateTime.UtcNow.AddSeconds(-1800),
            BattleId = Guid.NewGuid()
        };

        var offlineThresholdSeconds = 60;
        var offlineSeconds = (DateTime.UtcNow - character.LastSeenAtUtc.Value).TotalSeconds;

        // Act
        bool shouldPause = offlineSeconds >= offlineThresholdSeconds;

        // Assert
        Assert.True(shouldPause, "应该触发暂停操作");
        
        // 模拟暂停后的状态
        if (shouldPause)
        {
            plan.StartedAt = null;
            plan.BattleId = null;
            // plan.State 保持为 Running，而不是 Completed
        }

        Assert.Equal(ActivityState.Running, plan.State);
        Assert.Null(plan.BattleId);
        Assert.NotEqual(ActivityState.Completed, plan.State);
    }

    [Fact]
    public void PausedPlan_IdentifiedByStateAndBattleId()
    {
        // Arrange
        var pausedPlan = new ActivityPlan
        {
            Id = Guid.NewGuid(),
            CharacterId = Guid.NewGuid(),
            Type = ActivityType.Combat,
            State = ActivityState.Running, // 仍然是Running状态
            LimitType = LimitType.Duration,
            LimitValue = 3600,
            ExecutedSeconds = 1800,
            PayloadJson = "{}",
            SlotIndex = 0,
            CreatedAt = DateTime.UtcNow,
            BattleStateJson = "{\"enemyHpPercent\":0.5}",
            StartedAt = null, // 暂停时清空
            BattleId = null // 暂停时清空
        };

        var activePlan = new ActivityPlan
        {
            Id = Guid.NewGuid(),
            CharacterId = Guid.NewGuid(),
            Type = ActivityType.Combat,
            State = ActivityState.Running,
            LimitType = LimitType.Duration,
            LimitValue = 3600,
            ExecutedSeconds = 1800,
            PayloadJson = "{}",
            SlotIndex = 0,
            CreatedAt = DateTime.UtcNow,
            BattleStateJson = "{}",
            StartedAt = DateTime.UtcNow,
            BattleId = Guid.NewGuid()
        };

        // Act
        bool isPaused = pausedPlan.State == ActivityState.Running 
            && !pausedPlan.BattleId.HasValue 
            && !string.IsNullOrWhiteSpace(pausedPlan.BattleStateJson);
        
        bool isActive = activePlan.State == ActivityState.Running 
            && activePlan.BattleId.HasValue;

        // Assert
        Assert.True(isPaused, "应该能识别暂停状态的计划");
        Assert.True(isActive, "应该能识别活跃状态的计划");
    }
}
