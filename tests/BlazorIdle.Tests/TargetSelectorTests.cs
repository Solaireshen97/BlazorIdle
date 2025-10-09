using BlazorIdle.Server.Domain.Characters;
using BlazorIdle.Server.Domain.Combat;
using BlazorIdle.Server.Domain.Combat.Combatants;
using BlazorIdle.Server.Domain.Combat.Damage;
using BlazorIdle.Server.Domain.Combat.Enemies;
using BlazorIdle.Server.Domain.Combat.Rng;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace BlazorIdle.Tests;

/// <summary>
/// Phase 2 单元测试：TargetSelector 加权随机目标选择
/// </summary>
public class TargetSelectorTests
{
    #region Basic Functionality Tests

    [Fact]
    public void TargetSelector_SelectTarget_WithNoCandidates_ShouldReturnNull()
    {
        // Arrange
        var rng = new RngContext(12345);
        var selector = new TargetSelector(rng);
        var candidates = new List<ICombatant>();

        // Act
        var selected = selector.SelectTarget(candidates);

        // Assert
        Assert.Null(selected);
    }

    [Fact]
    public void TargetSelector_SelectTarget_WithOnlyDeadCandidates_ShouldReturnNull()
    {
        // Arrange
        var rng = new RngContext(12345);
        var selector = new TargetSelector(rng);
        var enemyDef = new EnemyDefinition("goblin", "Goblin", 5, 100, 10, 0.1, 1.0, 1.0, 1.0);
        var encounter = new Encounter(enemyDef);
        encounter.ApplyDamage(1000, 0.0); // Kill it
        var candidates = new List<ICombatant>
        {
            new EnemyCombatant("e1", encounter)
        };

        // Act
        var selected = selector.SelectTarget(candidates);

        // Assert
        Assert.Null(selected);
    }

    [Fact]
    public void TargetSelector_SelectTarget_WithSingleCandidate_ShouldSelectThatCandidate()
    {
        // Arrange
        var rng = new RngContext(12345);
        var selector = new TargetSelector(rng);
        var enemyDef = new EnemyDefinition("goblin", "Goblin", 5, 100, 10, 0.1, 1.0, 1.0, 1.0);
        var encounter = new Encounter(enemyDef);
        var candidate = new EnemyCombatant("e1", encounter);
        var candidates = new List<ICombatant> { candidate };

        // Act
        var selected = selector.SelectTarget(candidates);

        // Assert
        Assert.NotNull(selected);
        Assert.Equal("e1", selected.Id);
    }

    [Fact]
    public void TargetSelector_SelectTarget_WithEqualWeights_ShouldDistributeEvenly()
    {
        // Arrange
        var rng = new RngContext(54321);
        var selector = new TargetSelector(rng);
        
        var candidates = new List<ICombatant>();
        for (int i = 0; i < 3; i++)
        {
            var enemyDef = new EnemyDefinition($"enemy{i}", $"Enemy{i}", 5, 100, 10, 0.1, 1.0, 1.0, 1.0);
            var encounter = new Encounter(enemyDef);
            candidates.Add(new EnemyCombatant($"e{i}", encounter));
        }

        // Act - 多次选择统计分布
        var counts = new Dictionary<string, int>
        {
            ["e0"] = 0,
            ["e1"] = 0,
            ["e2"] = 0
        };

        const int iterations = 3000;
        for (int i = 0; i < iterations; i++)
        {
            var selected = selector.SelectTarget(candidates);
            Assert.NotNull(selected);
            counts[selected!.Id]++;
        }

        // Assert - 每个目标应该被选中约 33%，允许 ±5% 误差
        double expectedRatio = 1.0 / 3.0;
        double tolerance = 0.05;
        
        foreach (var kvp in counts)
        {
            double actualRatio = (double)kvp.Value / iterations;
            Assert.InRange(actualRatio, expectedRatio - tolerance, expectedRatio + tolerance);
        }
    }

    #endregion

    #region Weight-Based Selection Tests

    [Fact]
    public void TargetSelector_SelectTarget_WithDifferentWeights_ShouldRespectWeights()
    {
        // Arrange
        var rng = new RngContext(99999);
        var selector = new TargetSelector(rng);
        
        var candidates = new List<ICombatant>();
        var weights = new[] { 1.0, 5.0, 1.0 }; // 中间目标权重为5，应该被选中更多
        
        for (int i = 0; i < 3; i++)
        {
            var enemyDef = new EnemyDefinition($"enemy{i}", $"Enemy{i}", 5, 100, 10, 0.1, 1.0, 1.0, 1.0);
            var encounter = new Encounter(enemyDef);
            var enemy = new EnemyCombatant($"e{i}", encounter)
            {
                ThreatWeight = weights[i]
            };
            candidates.Add(enemy);
        }

        // Act
        var counts = new Dictionary<string, int>
        {
            ["e0"] = 0,
            ["e1"] = 0,
            ["e2"] = 0
        };

        const int iterations = 7000;
        for (int i = 0; i < iterations; i++)
        {
            var selected = selector.SelectTarget(candidates);
            Assert.NotNull(selected);
            counts[selected!.Id]++;
        }

        // Assert - 权重比例 1:5:1，总权重 7
        // e0: ~14.3%, e1: ~71.4%, e2: ~14.3%
        double totalWeight = 7.0;
        double tolerance = 0.05;
        
        Assert.InRange((double)counts["e0"] / iterations, 1.0 / totalWeight - tolerance, 1.0 / totalWeight + tolerance);
        Assert.InRange((double)counts["e1"] / iterations, 5.0 / totalWeight - tolerance, 5.0 / totalWeight + tolerance);
        Assert.InRange((double)counts["e2"] / iterations, 1.0 / totalWeight - tolerance, 1.0 / totalWeight + tolerance);
    }

    [Fact]
    public void TargetSelector_SelectTarget_WithHighThreatWeight_ShouldBeSelectedMore()
    {
        // Arrange
        var rng = new RngContext(42);
        var selector = new TargetSelector(rng);
        
        var candidates = new List<ICombatant>();
        
        // 创建两个目标，一个权重高
        var normalEnemy = new EnemyDefinition("normal", "Normal", 5, 100, 10, 0.1, 1.0, 1.0, 1.0);
        var normalEncounter = new Encounter(normalEnemy);
        var normal = new EnemyCombatant("normal", normalEncounter) { ThreatWeight = 1.0 };
        
        var taunterEnemy = new EnemyDefinition("taunter", "Taunter", 5, 100, 10, 0.1, 1.0, 1.0, 1.0);
        var taunterEncounter = new Encounter(taunterEnemy);
        var taunter = new EnemyCombatant("taunter", taunterEncounter) { ThreatWeight = 10.0 };
        
        candidates.Add(normal);
        candidates.Add(taunter);

        // Act
        int normalCount = 0;
        int taunterCount = 0;
        const int iterations = 1100;
        
        for (int i = 0; i < iterations; i++)
        {
            var selected = selector.SelectTarget(candidates);
            Assert.NotNull(selected);
            if (selected!.Id == "normal") normalCount++;
            else if (selected.Id == "taunter") taunterCount++;
        }

        // Assert - 权重比例 1:10，taunter 应该被选中约 90.9%
        double taunterRatio = (double)taunterCount / iterations;
        Assert.InRange(taunterRatio, 0.85, 0.95); // 允许 ±5% 误差
    }

    #endregion

    #region RNG Determinism Tests

    [Fact]
    public void TargetSelector_SelectTarget_WithSameSeed_ShouldProduceSameResults()
    {
        // Arrange
        const ulong seed = 777;
        var candidates = CreateTestCandidates(3);

        // Act - 使用相同种子进行两次选择序列
        var results1 = new List<string>();
        var rng1 = new RngContext(seed);
        var selector1 = new TargetSelector(rng1);
        for (int i = 0; i < 10; i++)
        {
            var selected = selector1.SelectTarget(candidates);
            results1.Add(selected!.Id);
        }

        var results2 = new List<string>();
        var rng2 = new RngContext(seed);
        var selector2 = new TargetSelector(rng2);
        for (int i = 0; i < 10; i++)
        {
            var selected = selector2.SelectTarget(candidates);
            results2.Add(selected!.Id);
        }

        // Assert - 序列应该完全相同
        Assert.Equal(results1, results2);
    }

    [Fact]
    public void TargetSelector_SelectTarget_WithDifferentSeeds_ShouldProduceDifferentResults()
    {
        // Arrange
        var candidates = CreateTestCandidates(3);

        // Act
        var results1 = new List<string>();
        var rng1 = new RngContext(111);
        var selector1 = new TargetSelector(rng1);
        for (int i = 0; i < 10; i++)
        {
            var selected = selector1.SelectTarget(candidates);
            results1.Add(selected!.Id);
        }

        var results2 = new List<string>();
        var rng2 = new RngContext(222);
        var selector2 = new TargetSelector(rng2);
        for (int i = 0; i < 10; i++)
        {
            var selected = selector2.SelectTarget(candidates);
            results2.Add(selected!.Id);
        }

        // Assert - 序列应该不同（概率极高）
        Assert.NotEqual(results1, results2);
    }

    #endregion

    #region Edge Cases Tests

    [Fact]
    public void TargetSelector_SelectTarget_MixedAliveAndDead_ShouldOnlySelectAlive()
    {
        // Arrange
        var rng = new RngContext(123);
        var selector = new TargetSelector(rng);
        
        var candidates = new List<ICombatant>();
        
        // 活着的敌人
        var aliveEnemy = new EnemyDefinition("alive", "Alive", 5, 100, 10, 0.1, 1.0, 1.0, 1.0);
        var aliveEncounter = new Encounter(aliveEnemy);
        candidates.Add(new EnemyCombatant("alive", aliveEncounter));
        
        // 死亡的敌人
        var deadEnemy = new EnemyDefinition("dead", "Dead", 5, 100, 10, 0.1, 1.0, 1.0, 1.0);
        var deadEncounter = new Encounter(deadEnemy);
        deadEncounter.ApplyDamage(1000, 0.0);
        candidates.Add(new EnemyCombatant("dead", deadEncounter));

        // Act - 多次选择
        for (int i = 0; i < 50; i++)
        {
            var selected = selector.SelectTarget(candidates);
            
            // Assert - 应该只选中活着的
            Assert.NotNull(selected);
            Assert.Equal("alive", selected!.Id);
        }
    }

    [Fact]
    public void TargetSelector_SelectTarget_WithZeroWeight_ShouldNeverBeSelected()
    {
        // Arrange
        var rng = new RngContext(555);
        var selector = new TargetSelector(rng);
        
        var candidates = new List<ICombatant>();
        
        var normalEnemy = new EnemyDefinition("normal", "Normal", 5, 100, 10, 0.1, 1.0, 1.0, 1.0);
        var normalEncounter = new Encounter(normalEnemy);
        var normal = new EnemyCombatant("normal", normalEncounter) { ThreatWeight = 1.0 };
        
        var zeroWeightEnemy = new EnemyDefinition("zero", "Zero", 5, 100, 10, 0.1, 1.0, 1.0, 1.0);
        var zeroWeightEncounter = new Encounter(zeroWeightEnemy);
        var zero = new EnemyCombatant("zero", zeroWeightEncounter) { ThreatWeight = 0.0 };
        
        candidates.Add(normal);
        candidates.Add(zero);

        // Act - 多次选择
        for (int i = 0; i < 100; i++)
        {
            var selected = selector.SelectTarget(candidates);
            
            // Assert - 应该只选中非零权重的
            Assert.NotNull(selected);
            Assert.Equal("normal", selected!.Id);
        }
    }

    #endregion

    #region Helper Methods

    private List<ICombatant> CreateTestCandidates(int count)
    {
        var candidates = new List<ICombatant>();
        for (int i = 0; i < count; i++)
        {
            var enemyDef = new EnemyDefinition($"enemy{i}", $"Enemy{i}", 5, 100, 10, 0.1, 1.0, 1.0, 1.0);
            var encounter = new Encounter(enemyDef);
            candidates.Add(new EnemyCombatant($"e{i}", encounter));
        }
        return candidates;
    }

    #endregion
}
