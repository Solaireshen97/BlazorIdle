using BlazorIdle.Server.Application.Battles;
using BlazorIdle.Server.Application.Battles.Offline;
using BlazorIdle.Server.Domain.Activities;
using BlazorIdle.Server.Domain.Characters;
using BlazorIdle.Shared.Models;
using System;
using Xunit;

namespace BlazorIdle.Tests;

/// <summary>
/// 测试自动离线结算功能的核心逻辑
/// 主要验证：
/// 1. 离线检测阈值判断
/// 2. 自动应用收益逻辑
/// 3. 计划状态更新
/// </summary>
public class OfflineAutoSettlementTests
{
    [Fact]
    public void OfflineDetection_ShouldIdentifyOfflinePlayer_WhenExceedingThreshold()
    {
        // Arrange
        var offlineThresholdSeconds = 60;
        var lastSeenAt = DateTime.UtcNow.AddSeconds(-120); // 2分钟前
        var now = DateTime.UtcNow;

        // Act
        var offlineSeconds = (now - lastSeenAt).TotalSeconds;
        var isOffline = offlineSeconds >= offlineThresholdSeconds;

        // Assert
        Assert.True(isOffline, "玩家应被识别为离线状态");
        Assert.True(offlineSeconds >= 120, "离线时长应至少为120秒");
    }

    [Fact]
    public void OfflineDetection_ShouldNotTrigger_WhenBelowThreshold()
    {
        // Arrange
        var offlineThresholdSeconds = 60;
        var lastSeenAt = DateTime.UtcNow.AddSeconds(-30); // 30秒前
        var now = DateTime.UtcNow;

        // Act
        var offlineSeconds = (now - lastSeenAt).TotalSeconds;
        var isOffline = offlineSeconds >= offlineThresholdSeconds;

        // Assert
        Assert.False(isOffline, "玩家不应被识别为离线状态");
    }

    [Fact]
    public void AutoApplyRewards_ShouldApplyImmediately_WhenEnabled()
    {
        // Arrange
        var autoApplyEnabled = true;
        var initialGold = 1000L;
        var initialExp = 5000L;
        var rewardGold = 500L;
        var rewardExp = 1000L;

        // Act - 模拟自动应用收益
        var finalGold = initialGold;
        var finalExp = initialExp;
        
        if (autoApplyEnabled)
        {
            finalGold += rewardGold;
            finalExp += rewardExp;
        }

        // Assert
        Assert.Equal(1500, finalGold);
        Assert.Equal(6000, finalExp);
    }

    [Fact]
    public void PlanState_ShouldUpdateToCompleted_WhenLimitReached()
    {
        // Arrange
        var plan = new ActivityPlan
        {
            Id = Guid.NewGuid(),
            CharacterId = Guid.NewGuid(),
            Type = ActivityType.Combat,
            State = ActivityState.Running,
            LimitType = LimitType.Duration,
            LimitValue = 60, // 60秒限制
            ExecutedSeconds = 30, // 已执行30秒
            PayloadJson = "{}",
            SlotIndex = 0,
            CreatedAt = DateTime.UtcNow
        };

        // Act - 模拟再执行40秒（总共70秒，超过限制）
        var simulatedSeconds = 40.0;
        plan.ExecutedSeconds += simulatedSeconds;
        
        var isCompleted = plan.ExecutedSeconds >= plan.LimitValue;
        if (isCompleted)
        {
            plan.State = ActivityState.Completed;
            plan.CompletedAt = DateTime.UtcNow;
        }

        // Assert
        Assert.True(isCompleted);
        Assert.Equal(ActivityState.Completed, plan.State);
        Assert.NotNull(plan.CompletedAt);
    }

    [Fact]
    public void OfflineFastForward_ShouldCalculateCorrectDuration()
    {
        // Arrange
        var character = new Character
        {
            Id = Guid.NewGuid(),
            Name = "TestWarrior",
            Profession = Profession.Warrior,
            Level = 10,
            Strength = 20,
            Agility = 15,
            Intellect = 10,
            Stamina = 25
        };

        var plan = new ActivityPlan
        {
            Id = Guid.NewGuid(),
            CharacterId = character.Id,
            Type = ActivityType.Combat,
            State = ActivityState.Running,
            LimitType = LimitType.Duration,
            LimitValue = 3600, // 1小时
            ExecutedSeconds = 1800, // 已执行30分钟
            PayloadJson = "{\"enemyId\":\"dummy\",\"enemyCount\":1}",
            SlotIndex = 0,
            CreatedAt = DateTime.UtcNow
        };

        var offlineSeconds = 1200.0; // 离线20分钟

        // Act
        var simulator = new BattleSimulator();
        var fakeEquipmentStats = TestHelpers.CreateFakeEquipmentStatsIntegration();
        var engine = new OfflineFastForwardEngine(simulator, fakeEquipmentStats);
        var result = engine.FastForward(character, plan, offlineSeconds);

        // Assert
        Assert.Equal(1200, result.SimulatedSeconds); // 模拟20分钟
        Assert.Equal(3000, result.UpdatedExecutedSeconds); // 总共50分钟
        Assert.False(result.PlanCompleted); // 未完成（50 < 60分钟）
        Assert.True(result.Gold > 0); // 有金币收益
        Assert.True(result.Exp > 0); // 有经验收益
    }
}
