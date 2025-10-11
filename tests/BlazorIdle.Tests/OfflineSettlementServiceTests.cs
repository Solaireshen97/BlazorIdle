using BlazorIdle.Server.Application.Battles;
using BlazorIdle.Server.Application.Battles.Offline;
using BlazorIdle.Server.Domain.Activities;
using BlazorIdle.Server.Domain.Characters;
using BlazorIdle.Server.Infrastructure.Persistence;
using BlazorIdle.Shared.Models;
using System;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace BlazorIdle.Tests;

/// <summary>
/// OfflineSettlementService 单元测试
/// 测试离线结算的自动检测、计算和发放功能
/// 注意：这些测试使用简化的内存仓储，主要验证业务逻辑
/// </summary>
public class OfflineSettlementServiceTests
{
    private readonly OfflineFastForwardEngine _engine;
    private readonly Character _testCharacter;

    public OfflineSettlementServiceTests()
    {
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
            Experience = 5000,
            LastSeenAtUtc = DateTime.UtcNow.AddHours(-2) // 2小时前离线
        };

        // 创建引擎
        var simulator = new BattleSimulator();
        var fakeEquipmentStats = new TestHelpers.FakeEquipmentStatsIntegration();
        _engine = new OfflineFastForwardEngine(simulator, fakeEquipmentStats);
    }

    [Fact]
    public void FastForward_WithOfflineProgress_ShouldInheritAndContinue()
    {
        // Arrange - 测试无感继承效果：计划打到一半进入离线
        var plan = CreateCombatPlan(
            limitType: LimitType.Duration,
            limitValue: 3600, // 1小时计划
            executedSeconds: 1800 // 已在线执行30分钟
        );

        var offlineSeconds = 1200.0; // 离线20分钟

        // Act
        var result = _engine.FastForward(_testCharacter, plan, offlineSeconds);

        // Assert
        // 应该从30分钟的进度继续，再模拟20分钟，总共50分钟
        Assert.Equal(1200, result.SimulatedSeconds);
        Assert.Equal(3000, result.UpdatedExecutedSeconds); // 1800 + 1200 = 3000
        Assert.False(result.PlanCompleted); // 50分钟 < 60分钟
        
        // 验证战斗状态快照已保存（用于在线继承）
        Assert.NotNull(plan.BattleStateJson);
        Assert.NotEmpty(plan.BattleStateJson);
    }

    [Fact]
    public void FastForward_PlanCompletesDuringOffline_ShouldMarkAsCompleted()
    {
        // Arrange
        var plan = CreateCombatPlan(
            limitType: LimitType.Duration,
            limitValue: 2400, // 40分钟计划
            executedSeconds: 1200 // 已执行20分钟
        );

        var offlineSeconds = 1800.0; // 离线30分钟

        // Act
        var result = _engine.FastForward(_testCharacter, plan, offlineSeconds);

        // Assert
        // 只模拟剩余的20分钟（40-20=20）
        Assert.Equal(1200, result.SimulatedSeconds); // 只模拟剩余时长
        Assert.Equal(2400, result.UpdatedExecutedSeconds); // 达到限制
        Assert.True(result.PlanCompleted);
        Assert.Equal(ActivityState.Completed, plan.State);
        Assert.Null(plan.BattleStateJson); // 完成后应清空状态
    }

    [Fact]
    public void FastForward_MultipleOfflineSessions_ShouldMaintainContinuity()
    {
        // Arrange - 测试多次离线上线的连续性
        var plan = CreateCombatPlan(
            limitType: LimitType.Duration,
            limitValue: 100, // 100秒计划
            executedSeconds: 0
        );

        long totalGold = 0;
        long totalExp = 0;

        // Act - 模拟5次离线，每次20秒
        for (int i = 0; i < 5; i++)
        {
            var result = _engine.FastForward(_testCharacter, plan, 20.0);
            totalGold += result.Gold;
            totalExp += result.Exp;
            
            // 每次都应该有战斗状态保存（除了最后一次完成）
            if (!result.PlanCompleted)
            {
                Assert.NotNull(plan.BattleStateJson);
            }
        }

        // Assert
        Assert.True(plan.IsLimitReached());
        Assert.Equal(ActivityState.Completed, plan.State);
        Assert.Null(plan.BattleStateJson); // 完成后应清空
    }

    [Fact]
    public void FastForward_WithBattleStateSnapshot_ShouldRestoreAndContinue()
    {
        // Arrange - 测试战斗状态快照的恢复和继续
        var plan = CreateCombatPlan(
            limitType: LimitType.Duration,
            limitValue: 600, // 10分钟计划
            executedSeconds: 0
        );

        // 第一次快进：在线战斗 5 秒
        var firstResult = _engine.FastForward(_testCharacter, plan, 5.0);
        Assert.NotNull(plan.BattleStateJson);
        var firstStateJson = plan.BattleStateJson;

        // 第二次快进：离线 5 秒（应该继承第一次的战斗状态）
        var secondResult = _engine.FastForward(_testCharacter, plan, 5.0);

        // Assert
        // 验证战斗状态已更新
        Assert.NotEqual(firstStateJson, plan.BattleStateJson);
        
        // 验证累计执行时间正确
        Assert.Equal(10, secondResult.UpdatedExecutedSeconds, 0.1);
        
        // 验证击杀数据连续性
        Assert.True(secondResult.TotalKills >= firstResult.TotalKills);
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
}
