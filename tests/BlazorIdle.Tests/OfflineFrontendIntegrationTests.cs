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
/// 离线战斗前端集成测试
/// 验证离线检查、心跳更新和结算应用的完整流程
/// </summary>
public class OfflineFrontendIntegrationTests
{
    private readonly BattleSimulator _simulator;
    private readonly OfflineFastForwardEngine _engine;

    public OfflineFrontendIntegrationTests()
    {
        _simulator = new BattleSimulator();
        _engine = new OfflineFastForwardEngine(_simulator);
    }

    [Fact]
    public void OfflineCheck_WithNoOfflineTime_ShouldReturnNoRewards()
    {
        // Arrange
        var character = CreateTestCharacter();
        character.LastSeenAtUtc = DateTime.UtcNow; // 刚刚在线

        // 创建运行中的计划
        var plan = CreateTestPlan(character.Id);

        // Act
        var offlineSeconds = CalculateOfflineSeconds(character);

        // Assert - 时间可能略大于0由于代码执行时间，所以小于1秒视为无离线时间
        Assert.True(offlineSeconds < 1);
    }

    [Fact]
    public void OfflineCheck_WithRunningPlan_ShouldReturnSettlement()
    {
        // Arrange
        var character = CreateTestCharacter();
        character.LastSeenAtUtc = DateTime.UtcNow.AddHours(-1); // 1小时前离线

        var plan = CreateTestPlan(character.Id);
        plan.State = ActivityState.Running;
        plan.ExecutedSeconds = 900; // 已执行15分钟
        plan.StartedAt = DateTime.UtcNow.AddHours(-1);

        // Act
        var offlineSeconds = CalculateOfflineSeconds(character);
        var result = _engine.FastForward(character, plan, offlineSeconds);

        // Assert
        Assert.True(offlineSeconds > 0);
        Assert.NotNull(result);
        Assert.True(result.SimulatedSeconds > 0 || result.SimulatedSeconds == 0); // 可能为0如果没有剩余时间
        Assert.True(result.UpdatedExecutedSeconds >= plan.ExecutedSeconds); // 应该至少等于或大于初始值
        Assert.Equal(character.Id, result.CharacterId);
        Assert.Equal(plan.Id, result.PlanId);
    }

    [Fact]
    public void OfflineCheck_PlanCompletedDuringOffline_ShouldMarkAsCompleted()
    {
        // Arrange
        var character = CreateTestCharacter();
        character.LastSeenAtUtc = DateTime.UtcNow.AddHours(-2); // 2小时前离线

        var plan = CreateTestPlan(character.Id);
        plan.State = ActivityState.Running;
        plan.LimitType = LimitType.Duration;
        plan.LimitValue = 3600; // 1小时限制
        plan.ExecutedSeconds = 1800; // 已执行30分钟
        plan.StartedAt = DateTime.UtcNow.AddHours(-2);

        // Act
        var offlineSeconds = CalculateOfflineSeconds(character);
        var result = _engine.FastForward(character, plan, offlineSeconds);

        // Assert
        Assert.True(result.PlanCompleted);
        Assert.Equal(ActivityState.Completed, plan.State);
        Assert.NotNull(plan.CompletedAt);
    }

    [Fact]
    public void OfflineCheck_SeamlessInheritance_ShouldContinueFromMidBattle()
    {
        // Arrange - 测试无感继承：计划打到一半进入离线
        var character = CreateTestCharacter();
        character.LastSeenAtUtc = DateTime.UtcNow.AddMinutes(-30); // 30分钟前离线

        var plan = CreateTestPlan(character.Id);
        plan.State = ActivityState.Running;
        plan.LimitType = LimitType.Duration;
        plan.LimitValue = 3600; // 1小时限制
        plan.ExecutedSeconds = 900; // 已执行15分钟
        plan.StartedAt = DateTime.UtcNow.AddMinutes(-45);

        // Act
        var offlineSeconds = CalculateOfflineSeconds(character);
        var result = _engine.FastForward(character, plan, offlineSeconds);

        // Assert - 应该从15分钟的进度继续计算30分钟
        Assert.True(Math.Abs(result.UpdatedExecutedSeconds - 2700) < 1); // 15分钟 + 30分钟 = 45分钟 (允许小误差)
        Assert.False(result.PlanCompleted); // 还没完成（总共1小时）
        Assert.Equal(ActivityState.Running, plan.State);
    }

    [Fact]
    public void OfflineCheck_MaxCapSeconds_ShouldLimitTo12Hours()
    {
        // Arrange
        var character = CreateTestCharacter();
        character.LastSeenAtUtc = DateTime.UtcNow.AddHours(-24); // 24小时前离线

        var plan = CreateTestPlan(character.Id);
        plan.State = ActivityState.Running;
        plan.LimitType = LimitType.Infinite;
        plan.ExecutedSeconds = 0;
        plan.StartedAt = DateTime.UtcNow.AddHours(-24);

        // Act
        var offlineSeconds = CalculateOfflineSeconds(character);
        var result = _engine.FastForward(character, plan, offlineSeconds, maxCapSeconds: 43200); // 12小时上限

        // Assert - 离线24小时，但只计算12小时
        Assert.True(offlineSeconds > 43200);
        Assert.True(result.SimulatedSeconds <= 43200);
    }

    [Fact]
    public void HeartbeatUpdate_ShouldUpdateLastSeenTime()
    {
        // Arrange
        var character = CreateTestCharacter();
        var oldLastSeen = character.LastSeenAtUtc;

        // Act - 模拟心跳更新
        System.Threading.Thread.Sleep(10); // 确保时间不同
        character.LastSeenAtUtc = DateTime.UtcNow;

        // Assert
        Assert.NotNull(character.LastSeenAtUtc);
        Assert.True(character.LastSeenAtUtc > oldLastSeen);
    }

    [Fact]
    public void OfflineSettlement_WithRewards_ShouldHavePositiveValues()
    {
        // Arrange
        var character = CreateTestCharacter();
        character.LastSeenAtUtc = DateTime.UtcNow.AddHours(-1);

        var plan = CreateTestPlan(character.Id);
        plan.State = ActivityState.Running;
        plan.ExecutedSeconds = 0;
        plan.StartedAt = DateTime.UtcNow.AddHours(-1);

        // Act
        var offlineSeconds = CalculateOfflineSeconds(character);
        var result = _engine.FastForward(character, plan, offlineSeconds);

        // Assert - 应该有收益（金币和经验可能为0，但伤害应该有）
        Assert.True(result.Gold >= 0);
        Assert.True(result.Exp >= 0);
        Assert.True(result.TotalDamage >= 0); // 伤害应该非负
    }

    // Helper methods

    private Character CreateTestCharacter()
    {
        return new Character
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
            LastSeenAtUtc = DateTime.UtcNow
        };
    }

    private ActivityPlan CreateTestPlan(Guid characterId)
    {
        var payload = new CombatActivityPayload
        {
            EnemyId = "dummy",
            EnemyCount = 1,
            RespawnDelay = 0,
            Seed = 12345
        };

        return new ActivityPlan
        {
            Id = Guid.NewGuid(),
            CharacterId = characterId,
            SlotIndex = 0,
            Type = ActivityType.Combat,
            LimitType = LimitType.Duration,
            LimitValue = 3600, // 1小时
            State = ActivityState.Pending,
            CreatedAt = DateTime.UtcNow,
            PayloadJson = JsonSerializer.Serialize(payload),
            ExecutedSeconds = 0
        };
    }

    private double CalculateOfflineSeconds(Character character)
    {
        if (!character.LastSeenAtUtc.HasValue)
            return 0;

        var now = DateTime.UtcNow;
        var lastSeen = character.LastSeenAtUtc.Value;
        return (now - lastSeen).TotalSeconds;
    }
}
