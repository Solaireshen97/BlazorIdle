using BlazorIdle.Server.Application.Battles.Offline;
using System;
using System.Collections.Generic;
using System.Text.Json;
using Xunit;

namespace BlazorIdle.Tests;

/// <summary>
/// 离线战斗前端集成测试
/// 验证DTOs序列化和基本功能
/// </summary>
public class OfflineFrontendIntegrationTests
{
    [Fact]
    public void OfflineCheckResult_Serialization_ShouldWork()
    {
        // Arrange
        var settlement = new OfflineFastForwardResult
        {
            CharacterId = Guid.NewGuid(),
            PlanId = Guid.NewGuid(),
            SimulatedSeconds = 3600,
            PlanCompleted = false,
            TotalDamage = 100000,
            TotalKills = 50,
            Gold = 2000,
            Exp = 3000,
            LootExpected = new Dictionary<string, double> { ["item1"] = 1.5 },
            LootSampled = new Dictionary<string, int> { ["item2"] = 2 },
            UpdatedExecutedSeconds = 3600,
            DropMode = "expected"
        };

        var result = new OfflineCheckResult
        {
            HasOfflineTime = true,
            OfflineSeconds = 3600,
            HasRunningPlan = true,
            Settlement = settlement,
            PlanCompleted = false,
            NextPlanStarted = false,
            NextPlanId = null
        };

        // Act
        var json = JsonSerializer.Serialize(result);
        var deserialized = JsonSerializer.Deserialize<OfflineCheckResult>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.True(deserialized.HasOfflineTime);
        Assert.Equal(3600, deserialized.OfflineSeconds);
        Assert.True(deserialized.HasRunningPlan);
        Assert.NotNull(deserialized.Settlement);
        Assert.Equal(2000, deserialized.Settlement.Gold);
        Assert.Equal(3000, deserialized.Settlement.Exp);
        Assert.Equal(50, deserialized.Settlement.TotalKills);
    }

    [Fact]
    public void OfflineCheckResult_NoOfflineTime_ShouldWork()
    {
        // Arrange
        var result = new OfflineCheckResult
        {
            HasOfflineTime = false,
            OfflineSeconds = 0,
            HasRunningPlan = false,
            Settlement = null,
            PlanCompleted = false,
            NextPlanStarted = false,
            NextPlanId = null
        };

        // Act
        var json = JsonSerializer.Serialize(result);
        var deserialized = JsonSerializer.Deserialize<OfflineCheckResult>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.False(deserialized.HasOfflineTime);
        Assert.Equal(0, deserialized.OfflineSeconds);
        Assert.False(deserialized.HasRunningPlan);
        Assert.Null(deserialized.Settlement);
    }

    [Fact]
    public void OfflineFastForwardResult_WithLoot_ShouldSerialize()
    {
        // Arrange
        var result = new OfflineFastForwardResult
        {
            CharacterId = Guid.NewGuid(),
            PlanId = Guid.NewGuid(),
            SimulatedSeconds = 1800,
            PlanCompleted = true,
            TotalDamage = 50000,
            TotalKills = 25,
            Gold = 1000,
            Exp = 1500,
            LootExpected = new Dictionary<string, double>
            {
                ["sword"] = 0.5,
                ["potion"] = 3.2
            },
            LootSampled = new Dictionary<string, int>
            {
                ["sword"] = 1,
                ["potion"] = 3
            },
            UpdatedExecutedSeconds = 1800,
            DropMode = "sampled"
        };

        // Act
        var json = JsonSerializer.Serialize(result);
        var deserialized = JsonSerializer.Deserialize<OfflineFastForwardResult>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.True(deserialized.PlanCompleted);
        Assert.Equal(1000, deserialized.Gold);
        Assert.Equal(1500, deserialized.Exp);
        Assert.Equal(2, deserialized.LootExpected.Count);
        Assert.Equal(2, deserialized.LootSampled.Count);
        Assert.Equal("sampled", deserialized.DropMode);
    }

    [Fact]
    public void ApplyOfflineSettlementRequest_Serialization_ShouldWork()
    {
        // Arrange
        var characterId = Guid.NewGuid();
        var settlement = new OfflineFastForwardResult
        {
            CharacterId = characterId,
            PlanId = Guid.NewGuid(),
            SimulatedSeconds = 600,
            PlanCompleted = false,
            TotalDamage = 10000,
            TotalKills = 10,
            Gold = 500,
            Exp = 800,
            LootExpected = new Dictionary<string, double>(),
            LootSampled = new Dictionary<string, int>(),
            UpdatedExecutedSeconds = 600,
            DropMode = "expected"
        };

        var request = new { CharacterId = characterId, Settlement = settlement };

        // Act - 验证可以序列化为JSON
        var json = JsonSerializer.Serialize(request);

        // Assert
        Assert.NotNull(json);
        Assert.Contains("CharacterId", json);
        Assert.Contains("Settlement", json);
        Assert.Contains("\"Gold\":500", json);
        Assert.Contains("\"Exp\":800", json);
    }

    [Fact]
    public void OfflineCheckResult_WithNextPlanStarted_ShouldSerialize()
    {
        // Arrange
        var nextPlanId = Guid.NewGuid();
        var settlement = new OfflineFastForwardResult
        {
            CharacterId = Guid.NewGuid(),
            PlanId = Guid.NewGuid(),
            SimulatedSeconds = 7200,
            PlanCompleted = true,
            TotalDamage = 200000,
            TotalKills = 100,
            Gold = 5000,
            Exp = 8000,
            LootExpected = new Dictionary<string, double>(),
            LootSampled = new Dictionary<string, int>(),
            UpdatedExecutedSeconds = 7200,
            DropMode = "expected"
        };

        var result = new OfflineCheckResult
        {
            HasOfflineTime = true,
            OfflineSeconds = 10800, // 3 hours
            HasRunningPlan = true,
            Settlement = settlement,
            PlanCompleted = true,
            NextPlanStarted = true,
            NextPlanId = nextPlanId
        };

        // Act
        var json = JsonSerializer.Serialize(result);
        var deserialized = JsonSerializer.Deserialize<OfflineCheckResult>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.True(deserialized.HasOfflineTime);
        Assert.True(deserialized.PlanCompleted);
        Assert.True(deserialized.NextPlanStarted);
        Assert.Equal(nextPlanId, deserialized.NextPlanId);
    }
}
