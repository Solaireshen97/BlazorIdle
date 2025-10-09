using BlazorIdle.Server.Domain.Characters;
using BlazorIdle.Server.Domain.Combat;
using BlazorIdle.Server.Domain.Combat.Combatants;
using BlazorIdle.Server.Domain.Combat.Enemies;
using BlazorIdle.Server.Domain.Combat.Rng;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace BlazorIdle.Tests;

/// <summary>
/// Phase 2 单元测试：TargetSelector 目标选取系统
/// 验证基于权重的随机目标选择逻辑
/// </summary>
public class TargetSelectorTests
{
    #region Basic Selection Tests

    [Fact]
    public void SelectTarget_WithNoTargets_ShouldReturnNull()
    {
        // Arrange
        var rng = new RngContext(12345);
        var selector = new TargetSelector(rng);
        var emptyList = new List<ICombatant>();

        // Act
        var result = selector.SelectTarget(emptyList);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void SelectTarget_WithSingleTarget_ShouldReturnThatTarget()
    {
        // Arrange
        var rng = new RngContext(12345);
        var selector = new TargetSelector(rng);
        var enemyDef = new EnemyDefinition("goblin", "Goblin", 5, 100, 10, 0.1, 1.0, 1.0, 1.0);
        var encounter = new Encounter(enemyDef);
        var enemy = new EnemyCombatant("enemy1", encounter);
        var targets = new List<ICombatant> { enemy };

        // Act
        var result = selector.SelectTarget(targets);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("enemy1", result.Id);
    }

    [Fact]
    public void SelectTarget_WithDeadTargets_ShouldReturnNull()
    {
        // Arrange
        var rng = new RngContext(12345);
        var selector = new TargetSelector(rng);
        var enemyDef = new EnemyDefinition("goblin", "Goblin", 5, 100, 10, 0.1, 1.0, 1.0, 1.0);
        var encounter = new Encounter(enemyDef);
        encounter.ApplyDamage(100, 0.0); // Kill the enemy
        var enemy = new EnemyCombatant("enemy1", encounter);
        var targets = new List<ICombatant> { enemy };

        // Act
        var result = selector.SelectTarget(targets);

        // Assert
        Assert.Null(result); // Dead enemies cannot be targeted
    }

    #endregion

    #region Weight Distribution Tests

    [Fact]
    public void SelectTarget_WithEqualWeights_ShouldDistributeEvenly()
    {
        // Arrange
        var rng = new RngContext(12345);
        var selector = new TargetSelector(rng);
        
        var enemy1 = CreateAliveEnemy("enemy1");
        var enemy2 = CreateAliveEnemy("enemy2");
        var enemy3 = CreateAliveEnemy("enemy3");
        
        enemy1.ThreatWeight = 1.0;
        enemy2.ThreatWeight = 1.0;
        enemy3.ThreatWeight = 1.0;
        
        var targets = new List<ICombatant> { enemy1, enemy2, enemy3 };

        // Act - 进行多次选择以验证分布
        var counts = new Dictionary<string, int>
        {
            ["enemy1"] = 0,
            ["enemy2"] = 0,
            ["enemy3"] = 0
        };
        
        const int iterations = 3000;
        for (int i = 0; i < iterations; i++)
        {
            var selected = selector.SelectTarget(targets);
            if (selected != null)
            {
                counts[selected.Id]++;
            }
        }

        // Assert - 每个目标应该被选中约 1000 次 (±15% 允许随机波动)
        foreach (var count in counts.Values)
        {
            double expected = iterations / 3.0;
            double tolerance = expected * 0.15; // 15% 容差
            Assert.InRange(count, expected - tolerance, expected + tolerance);
        }
    }

    [Fact]
    public void SelectTarget_WithDifferentWeights_ShouldDistributeProportionally()
    {
        // Arrange
        var rng = new RngContext(54321);
        var selector = new TargetSelector(rng);
        
        var enemy1 = CreateAliveEnemy("enemy1");
        var enemy2 = CreateAliveEnemy("enemy2");
        
        // enemy1 的权重是 enemy2 的 3 倍
        enemy1.ThreatWeight = 3.0;
        enemy2.ThreatWeight = 1.0;
        
        var targets = new List<ICombatant> { enemy1, enemy2 };

        // Act - 进行多次选择以验证分布
        var counts = new Dictionary<string, int>
        {
            ["enemy1"] = 0,
            ["enemy2"] = 0
        };
        
        const int iterations = 4000;
        for (int i = 0; i < iterations; i++)
        {
            var selected = selector.SelectTarget(targets);
            if (selected != null)
            {
                counts[selected.Id]++;
            }
        }

        // Assert - enemy1 应该被选中约 3000 次，enemy2 约 1000 次 (±15%)
        double expected1 = iterations * 0.75; // 75% 应该选择 enemy1
        double expected2 = iterations * 0.25; // 25% 应该选择 enemy2
        double tolerance = iterations * 0.15;
        
        Assert.InRange(counts["enemy1"], expected1 - tolerance, expected1 + tolerance);
        Assert.InRange(counts["enemy2"], expected2 - tolerance, expected2 + tolerance);
    }

    [Fact]
    public void SelectTarget_WithHighThreatWeight_ShouldBeSelectedMore()
    {
        // Arrange
        var rng = new RngContext(99999);
        var selector = new TargetSelector(rng);
        
        var normalEnemy = CreateAliveEnemy("normal");
        var taunter = CreateAliveEnemy("taunter");
        
        normalEnemy.ThreatWeight = 1.0;
        taunter.ThreatWeight = 5.0; // 嘲讽提高 5 倍仇恨
        
        var targets = new List<ICombatant> { normalEnemy, taunter };

        // Act
        var counts = new Dictionary<string, int>
        {
            ["normal"] = 0,
            ["taunter"] = 0
        };
        
        const int iterations = 3000;
        for (int i = 0; i < iterations; i++)
        {
            var selected = selector.SelectTarget(targets);
            if (selected != null)
            {
                counts[selected.Id]++;
            }
        }

        // Assert - taunter 应该被选中约 5 倍于 normal
        // Total weight = 6.0, taunter = 5/6 ≈ 83.3%, normal = 1/6 ≈ 16.7%
        double expectedTaunter = iterations * (5.0 / 6.0);
        double expectedNormal = iterations * (1.0 / 6.0);
        double tolerance = iterations * 0.15;
        
        Assert.InRange(counts["taunter"], expectedTaunter - tolerance, expectedTaunter + tolerance);
        Assert.InRange(counts["normal"], expectedNormal - tolerance, expectedNormal + tolerance);
    }

    #endregion

    #region RNG Reproducibility Tests

    [Fact]
    public void SelectTarget_WithSameSeed_ShouldProduceSameSequence()
    {
        // Arrange
        const ulong seed = 42;
        var rng1 = new RngContext(seed);
        var rng2 = new RngContext(seed);
        var selector1 = new TargetSelector(rng1);
        var selector2 = new TargetSelector(rng2);
        
        var enemy1 = CreateAliveEnemy("enemy1");
        var enemy2 = CreateAliveEnemy("enemy2");
        var enemy3 = CreateAliveEnemy("enemy3");
        var targets = new List<ICombatant> { enemy1, enemy2, enemy3 };

        // Act - 进行多次选择
        var sequence1 = new List<string>();
        var sequence2 = new List<string>();
        
        for (int i = 0; i < 100; i++)
        {
            var selected1 = selector1.SelectTarget(targets);
            var selected2 = selector2.SelectTarget(targets);
            
            sequence1.Add(selected1?.Id ?? "null");
            sequence2.Add(selected2?.Id ?? "null");
        }

        // Assert - 两个序列应该完全相同
        Assert.Equal(sequence1, sequence2);
    }

    [Fact]
    public void SelectTarget_WithDifferentSeeds_ShouldProduceDifferentSequences()
    {
        // Arrange
        var rng1 = new RngContext(42);
        var rng2 = new RngContext(12345);
        var selector1 = new TargetSelector(rng1);
        var selector2 = new TargetSelector(rng2);
        
        var enemy1 = CreateAliveEnemy("enemy1");
        var enemy2 = CreateAliveEnemy("enemy2");
        var enemy3 = CreateAliveEnemy("enemy3");
        var targets = new List<ICombatant> { enemy1, enemy2, enemy3 };

        // Act
        var sequence1 = new List<string>();
        var sequence2 = new List<string>();
        
        for (int i = 0; i < 100; i++)
        {
            var selected1 = selector1.SelectTarget(targets);
            var selected2 = selector2.SelectTarget(targets);
            
            sequence1.Add(selected1?.Id ?? "null");
            sequence2.Add(selected2?.Id ?? "null");
        }

        // Assert - 两个序列应该不同（虽然理论上可能相同，但概率极低）
        Assert.NotEqual(sequence1, sequence2);
    }

    #endregion

    #region Mixed Alive/Dead Targets Tests

    [Fact]
    public void SelectTarget_WithMixedAliveAndDead_ShouldOnlySelectAlive()
    {
        // Arrange
        var rng = new RngContext(12345);
        var selector = new TargetSelector(rng);
        
        var aliveEnemy = CreateAliveEnemy("alive");
        var deadEnemy = CreateDeadEnemy("dead");
        
        var targets = new List<ICombatant> { aliveEnemy, deadEnemy };

        // Act - 进行多次选择
        var selectedIds = new HashSet<string>();
        for (int i = 0; i < 100; i++)
        {
            var selected = selector.SelectTarget(targets);
            if (selected != null)
            {
                selectedIds.Add(selected.Id);
            }
        }

        // Assert - 应该只选择到 alive
        Assert.Contains("alive", selectedIds);
        Assert.DoesNotContain("dead", selectedIds);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void SelectTarget_WithZeroWeight_ShouldNeverBeSelected()
    {
        // Arrange
        var rng = new RngContext(12345);
        var selector = new TargetSelector(rng);
        
        var enemy1 = CreateAliveEnemy("enemy1");
        var enemy2 = CreateAliveEnemy("enemy2");
        
        enemy1.ThreatWeight = 1.0;
        enemy2.ThreatWeight = 0.0; // 零权重
        
        var targets = new List<ICombatant> { enemy1, enemy2 };

        // Act
        var selectedIds = new HashSet<string>();
        for (int i = 0; i < 100; i++)
        {
            var selected = selector.SelectTarget(targets);
            if (selected != null)
            {
                selectedIds.Add(selected.Id);
            }
        }

        // Assert - enemy2 永远不应该被选中
        Assert.Contains("enemy1", selectedIds);
        Assert.DoesNotContain("enemy2", selectedIds);
    }

    [Fact]
    public void SelectTarget_WithAllDeadTargets_ShouldReturnNull()
    {
        // Arrange
        var rng = new RngContext(12345);
        var selector = new TargetSelector(rng);
        
        var dead1 = CreateDeadEnemy("dead1");
        var dead2 = CreateDeadEnemy("dead2");
        
        var targets = new List<ICombatant> { dead1, dead2 };

        // Act
        var result = selector.SelectTarget(targets);

        // Assert
        Assert.Null(result);
    }

    #endregion

    #region Helper Methods

    private EnemyCombatant CreateAliveEnemy(string id)
    {
        var enemyDef = new EnemyDefinition($"type_{id}", $"Enemy {id}", 5, 100, 10, 0.1, 1.0, 1.0, 1.0);
        var encounter = new Encounter(enemyDef);
        return new EnemyCombatant(id, encounter);
    }

    private EnemyCombatant CreateDeadEnemy(string id)
    {
        var enemyDef = new EnemyDefinition($"type_{id}", $"Enemy {id}", 5, 100, 10, 0.1, 1.0, 1.0, 1.0);
        var encounter = new Encounter(enemyDef);
        encounter.ApplyDamage(100, 0.0); // Kill the enemy
        return new EnemyCombatant(id, encounter);
    }

    #endregion
}
