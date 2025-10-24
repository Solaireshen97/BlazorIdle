using BlazorIdle.Server.Application.Battles.Step;
using BlazorIdle.Server.Domain.Characters;
using BlazorIdle.Server.Domain.Combat.Enemies;
using BlazorIdle.Server.Domain.Combat.Professions;
using BlazorIdle.Shared.Messages.Battle;
using BlazorIdle.Shared.Models;
using Xunit;

namespace BlazorIdle.Tests.SignalR;

/// <summary>
/// RunningBattleExtensions的单元测试
/// 测试战斗帧生成和快照生成功能
/// </summary>
public class RunningBattleExtensionsTests
{
    /// <summary>
    /// 创建测试用的战斗实例
    /// </summary>
    private RunningBattle CreateTestBattle(double duration = 60.0)
    {
        var battleId = Guid.NewGuid();
        var characterId = Guid.NewGuid();
        var profession = Profession.Warrior;
        ulong seed = 12345;

        // 创建敌人定义
        var enemyDef = new EnemyDefinition(
            id: "test_enemy",
            name: "Test Enemy",
            level: 1,
            maxHp: 1000,
            armor: 100,
            magicResist: 0.5
        );

        // 创建角色属性
        var stats = new CharacterStats
        {
            AttackPower = 100,
            SpellPower = 50,
            CritChance = 0.25,
            CritMultiplier = 2.0,
            HastePercent = 0.1
        };

        return new RunningBattle(
            id: battleId,
            characterId: characterId,
            profession: profession,
            seed: seed,
            targetSeconds: duration,
            enemyDef: enemyDef,
            enemyCount: 1,
            stats: stats,
            mode: StepBattleMode.Duration
        );
    }

    [Fact]
    public void GenerateFrameTick_ShouldReturnValidFrame()
    {
        // Arrange
        var battle = CreateTestBattle();

        // Act
        var frame = battle.GenerateFrameTick();

        // Assert
        Assert.NotNull(frame);
        Assert.Equal(1, frame.Version); // 第一帧版本号应该为1
        Assert.Equal(battle.Id.ToString(), frame.BattleId);
        Assert.True(frame.ServerTime > 0);
        Assert.Equal(BattlePhase.Active, frame.Phase);
        Assert.NotNull(frame.Metrics);
    }

    [Fact]
    public void GenerateFrameTick_ShouldIncrementVersion()
    {
        // Arrange
        var battle = CreateTestBattle();

        // Act
        var frame1 = battle.GenerateFrameTick();
        var frame2 = battle.GenerateFrameTick();
        var frame3 = battle.GenerateFrameTick();

        // Assert
        Assert.Equal(1, frame1.Version);
        Assert.Equal(2, frame2.Version);
        Assert.Equal(3, frame3.Version);
    }

    [Fact]
    public void GenerateFrameTick_AfterAdvance_ShouldHaveUpdatedMetrics()
    {
        // Arrange
        var battle = CreateTestBattle();
        
        // Act - 生成第一帧
        var frame1 = battle.GenerateFrameTick();
        
        // 推进战斗
        battle.Advance(maxEvents: 1000, maxSimSecondsSlice: 0.5);
        
        // 生成第二帧
        var frame2 = battle.GenerateFrameTick();

        // Assert
        Assert.True(frame2.Version > frame1.Version);
        Assert.NotNull(frame2.Metrics);
        Assert.NotNull(frame2.Metrics.Dps);
        Assert.NotNull(frame2.Metrics.Health);
    }

    [Fact]
    public void GenerateFrameTick_CompletedBattle_ShouldShowEndedPhase()
    {
        // Arrange
        var battle = CreateTestBattle(duration: 1.0);
        
        // Act - 推进到战斗结束
        battle.FastForwardTo(2.0);
        var frame = battle.GenerateFrameTick();

        // Assert
        Assert.True(battle.Completed);
        Assert.Equal(BattlePhase.Ended, frame.Phase);
    }

    [Fact]
    public void GenerateSnapshot_ShouldReturnValidSnapshot()
    {
        // Arrange
        var battle = CreateTestBattle();
        battle.Advance(maxEvents: 100, maxSimSecondsSlice: 0.1);

        // Act
        var snapshot = battle.GenerateSnapshot();

        // Assert
        Assert.NotNull(snapshot);
        Assert.Equal(battle.Id.ToString(), snapshot.BattleId);
        Assert.True(snapshot.ServerTime > 0);
        Assert.NotNull(snapshot.State);
        Assert.NotNull(snapshot.State.Player);
        Assert.NotNull(snapshot.State.Enemies);
        Assert.NotNull(snapshot.State.Statistics);
    }

    [Fact]
    public void GenerateSnapshot_ShouldIncludePlayerState()
    {
        // Arrange
        var battle = CreateTestBattle();
        battle.Advance(maxEvents: 100, maxSimSecondsSlice: 0.1);

        // Act
        var snapshot = battle.GenerateSnapshot();

        // Assert
        var player = snapshot.State.Player;
        Assert.NotNull(player);
        Assert.NotNull(player.Health);
        Assert.True(player.Health.Current > 0);
        Assert.True(player.Health.Max > 0);
        Assert.NotNull(player.Resources);
        Assert.NotNull(player.Buffs);
        Assert.NotNull(player.Debuffs);
    }

    [Fact]
    public void GenerateSnapshot_ShouldIncludeEnemyStates()
    {
        // Arrange
        var battle = CreateTestBattle();
        battle.Advance(maxEvents: 100, maxSimSecondsSlice: 0.1);

        // Act
        var snapshot = battle.GenerateSnapshot();

        // Assert
        var enemies = snapshot.State.Enemies;
        Assert.NotNull(enemies);
        Assert.NotEmpty(enemies);
        
        var enemy = enemies[0];
        Assert.NotNull(enemy.Id);
        Assert.NotNull(enemy.Name);
        Assert.NotNull(enemy.Health);
        Assert.True(enemy.Health.Max > 0);
    }

    [Fact]
    public void GenerateSnapshot_ShouldIncludeStatistics()
    {
        // Arrange
        var battle = CreateTestBattle();
        battle.Advance(maxEvents: 100, maxSimSecondsSlice: 1.0);

        // Act
        var snapshot = battle.GenerateSnapshot();

        // Assert
        var stats = snapshot.State.Statistics;
        Assert.NotNull(stats);
        // 统计数据应该存在（即使为0）
        Assert.True(stats.TotalDamage >= 0);
        Assert.True(stats.TotalHealing >= 0);
        Assert.True(stats.TotalHits >= 0);
        Assert.True(stats.EnemiesKilled >= 0);
    }

    [Fact]
    public void GetCurrentVersion_ShouldReturnCorrectVersion()
    {
        // Arrange
        var battle = CreateTestBattle();

        // Act
        var version1 = battle.GetCurrentVersion();
        battle.GenerateFrameTick();
        var version2 = battle.GetCurrentVersion();
        battle.GenerateFrameTick();
        var version3 = battle.GetCurrentVersion();

        // Assert
        Assert.Equal(0, version1); // 初始版本
        Assert.Equal(1, version2); // 生成一帧后
        Assert.Equal(2, version3); // 生成两帧后
    }

    [Fact]
    public void CleanupFrameState_ShouldNotThrow()
    {
        // Arrange
        var battle = CreateTestBattle();
        battle.GenerateFrameTick();

        // Act & Assert - 应该不抛出异常
        battle.CleanupFrameState();
        
        // 清理后应该可以继续生成帧（版本号会重置）
        var frame = battle.GenerateFrameTick();
        Assert.NotNull(frame);
    }

    [Fact]
    public void MultipleFrames_ShouldMaintainConsistency()
    {
        // Arrange
        var battle = CreateTestBattle();

        // Act - 生成多个帧并推进战斗
        var frames = new List<FrameTick>();
        for (int i = 0; i < 10; i++)
        {
            battle.Advance(maxEvents: 100, maxSimSecondsSlice: 0.1);
            frames.Add(battle.GenerateFrameTick());
        }

        // Assert - 验证版本号连续性
        for (int i = 0; i < frames.Count; i++)
        {
            Assert.Equal(i + 1, frames[i].Version);
            Assert.Equal(battle.Id.ToString(), frames[i].BattleId);
        }
    }

    [Fact]
    public void GenerateFrameTick_WithMetrics_ShouldHaveValidDpsData()
    {
        // Arrange
        var battle = CreateTestBattle();
        battle.Advance(maxEvents: 100, maxSimSecondsSlice: 0.5);

        // Act
        var frame = battle.GenerateFrameTick();

        // Assert
        Assert.NotNull(frame.Metrics.Dps);
        Assert.True(frame.Metrics.Dps.Player >= 0);
        Assert.True(frame.Metrics.Dps.Received >= 0);
    }

    [Fact]
    public void GenerateFrameTick_WithAggregates_ShouldHaveValidWindowData()
    {
        // Arrange
        var battle = CreateTestBattle();
        battle.Advance(maxEvents: 100, maxSimSecondsSlice: 0.5);

        // Act
        var frame = battle.GenerateFrameTick();

        // Assert
        if (frame.Aggregates != null)
        {
            Assert.True(frame.Aggregates.WindowEnd >= frame.Aggregates.WindowStart);
            Assert.True(frame.Aggregates.Damage >= 0);
            Assert.True(frame.Aggregates.Healing >= 0);
            Assert.True(frame.Aggregates.Hits >= 0);
        }
    }

    [Fact]
    public void GenerateSnapshot_AfterDamage_ShouldReflectEnemyHealth()
    {
        // Arrange
        var battle = CreateTestBattle();
        
        // Act - 推进战斗造成一些伤害
        battle.Advance(maxEvents: 500, maxSimSecondsSlice: 1.0);
        var snapshot = battle.GenerateSnapshot();

        // Assert
        var enemy = snapshot.State.Enemies.FirstOrDefault();
        if (enemy != null)
        {
            // 敌人生命值应该在合理范围内
            Assert.True(enemy.Health.Current >= 0);
            Assert.True(enemy.Health.Current <= enemy.Health.Max);
        }
    }

    [Fact]
    public void FrameTick_Metrics_Health_ShouldBeValid()
    {
        // Arrange
        var battle = CreateTestBattle();

        // Act
        var frame = battle.GenerateFrameTick();

        // Assert
        var health = frame.Metrics.Health;
        Assert.NotNull(health);
        Assert.True(health.Current > 0);
        Assert.True(health.Max > 0);
        Assert.True(health.Current <= health.Max);
    }

    [Fact]
    public void FrameTick_Metrics_Shield_ShouldBeValid()
    {
        // Arrange
        var battle = CreateTestBattle();

        // Act
        var frame = battle.GenerateFrameTick();

        // Assert
        var shield = frame.Metrics.Shield;
        Assert.NotNull(shield);
        Assert.True(shield.Current >= 0);
    }

    [Fact]
    public void ConcurrentFrameGeneration_ShouldMaintainVersionIntegrity()
    {
        // Arrange
        var battle = CreateTestBattle();
        var versions = new System.Collections.Concurrent.ConcurrentBag<long>();

        // Act - 并发生成多个帧
        Parallel.For(0, 100, _ =>
        {
            var frame = battle.GenerateFrameTick();
            versions.Add(frame.Version);
        });

        // Assert - 所有版本号应该是唯一的
        var uniqueVersions = versions.Distinct().Count();
        Assert.Equal(100, uniqueVersions);
    }
}
