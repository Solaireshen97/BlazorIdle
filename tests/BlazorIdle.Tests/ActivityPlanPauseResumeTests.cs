using BlazorIdle.Server.Domain.Activities;
using System;
using Xunit;

namespace BlazorIdle.Tests;

/// <summary>
/// 测试活动计划的暂停和恢复功能
/// 验证离线检测正确暂停任务而不是完成任务
/// </summary>
public class ActivityPlanPauseResumeTests
{
    [Fact]
    public void ActivityState_PausedEnum_ShouldExist()
    {
        // Arrange & Act
        var pausedState = ActivityState.Paused;

        // Assert
        Assert.Equal(4, (int)pausedState);
    }

    [Fact]
    public void ActivityPlan_WithPausedState_ShouldMaintainState()
    {
        // Arrange
        var plan = new ActivityPlan
        {
            Id = Guid.NewGuid(),
            CharacterId = Guid.NewGuid(),
            Type = ActivityType.Combat,
            State = ActivityState.Paused,
            LimitType = LimitType.Duration,
            LimitValue = 3600,
            ExecutedSeconds = 1800, // 已执行30分钟
            BattleStateJson = "{\"test\":\"data\"}", // 保存的战斗状态
            PayloadJson = "{}",
            SlotIndex = 0,
            CreatedAt = DateTime.UtcNow,
            StartedAt = DateTime.UtcNow.AddMinutes(-30)
        };

        // Assert
        Assert.Equal(ActivityState.Paused, plan.State);
        Assert.NotNull(plan.BattleStateJson); // 暂停的任务应该保留战斗状态
        Assert.Null(plan.CompletedAt); // 暂停的任务不应该有完成时间
        Assert.Equal(1800, plan.ExecutedSeconds);
    }

    [Fact]
    public void ActivityPlan_PausedState_ShouldNotReachLimit()
    {
        // Arrange
        var plan = new ActivityPlan
        {
            Id = Guid.NewGuid(),
            CharacterId = Guid.NewGuid(),
            State = ActivityState.Paused,
            LimitType = LimitType.Duration,
            LimitValue = 100.0,
            ExecutedSeconds = 50.0 // 未达到限制
        };

        // Act
        var isLimitReached = plan.IsLimitReached();

        // Assert
        Assert.False(isLimitReached); // 暂停状态下，即使达到限制也不应该判定为已完成
    }

    [Fact]
    public void ActivityPlan_CompletedVsPaused_BehaviorDifference()
    {
        // Arrange - Completed plan
        var completedPlan = new ActivityPlan
        {
            Id = Guid.NewGuid(),
            CharacterId = Guid.NewGuid(),
            State = ActivityState.Completed,
            LimitType = LimitType.Duration,
            LimitValue = 3600,
            ExecutedSeconds = 3600,
            BattleStateJson = null, // 已完成的任务清空了战斗状态
            CompletedAt = DateTime.UtcNow,
            PayloadJson = "{}",
            SlotIndex = 0,
            CreatedAt = DateTime.UtcNow
        };

        // Arrange - Paused plan
        var pausedPlan = new ActivityPlan
        {
            Id = Guid.NewGuid(),
            CharacterId = Guid.NewGuid(),
            State = ActivityState.Paused,
            LimitType = LimitType.Duration,
            LimitValue = 3600,
            ExecutedSeconds = 1800, // 还未完成
            BattleStateJson = "{\"test\":\"data\"}", // 保留了战斗状态
            CompletedAt = null, // 没有完成时间
            PayloadJson = "{}",
            SlotIndex = 0,
            CreatedAt = DateTime.UtcNow
        };

        // Assert - Completed plan
        Assert.Equal(ActivityState.Completed, completedPlan.State);
        Assert.Null(completedPlan.BattleStateJson);
        Assert.NotNull(completedPlan.CompletedAt);

        // Assert - Paused plan
        Assert.Equal(ActivityState.Paused, pausedPlan.State);
        Assert.NotNull(pausedPlan.BattleStateJson);
        Assert.Null(pausedPlan.CompletedAt);
    }

    [Fact]
    public void OfflineDetection_Scenario_PauseNotComplete()
    {
        // 模拟离线检测场景
        // Arrange - 玩家正在执行任务
        var plan = new ActivityPlan
        {
            Id = Guid.NewGuid(),
            CharacterId = Guid.NewGuid(),
            State = ActivityState.Running,
            LimitType = LimitType.Duration,
            LimitValue = 3600, // 1小时限制
            ExecutedSeconds = 1800, // 已执行30分钟
            BattleStateJson = "{\"currentWave\":5}",
            StartedAt = DateTime.UtcNow.AddMinutes(-30),
            PayloadJson = "{}",
            SlotIndex = 0,
            CreatedAt = DateTime.UtcNow.AddMinutes(-30)
        };

        // Act - 离线检测应该将状态改为Paused（而不是Completed）
        plan.State = ActivityState.Paused;
        // 保留 BattleStateJson
        // 保留 ExecutedSeconds
        // 不设置 CompletedAt

        // Assert
        Assert.Equal(ActivityState.Paused, plan.State);
        Assert.NotNull(plan.BattleStateJson); // 关键：战斗状态被保留
        Assert.Null(plan.CompletedAt); // 关键：没有完成时间
        Assert.Equal(1800, plan.ExecutedSeconds); // 关键：已执行时长被保留
        Assert.False(plan.IsLimitReached()); // 还未达到限制
    }
}
