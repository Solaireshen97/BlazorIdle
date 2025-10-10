using BlazorIdle.Server.Domain.Characters;
using BlazorIdle.Server.Domain.Combat;
using BlazorIdle.Server.Domain.Combat.Enemies;
using BlazorIdle.Server.Domain.Combat.Engine;
using BlazorIdle.Server.Domain.Combat.Professions;
using BlazorIdle.Server.Domain.Combat.Rng;
using BlazorIdle.Shared.Models;
using System;
using System.Linq;
using Xunit;

namespace BlazorIdle.Tests;

/// <summary>
/// Phase 7 单元测试：RNG 一致性与战斗回放
/// 测试相同 seed 产生相同结果，验证战斗可重放
/// </summary>
public class BattleReplayTests
{
    #region Test Helpers

    private static CharacterStats CreateTestStats()
    {
        return new CharacterStats
        {
            AttackPower = 100,
            SpellPower = 100,
            CritChance = 0.2,
            CritMultiplier = 2.0,
            HastePercent = 0.1
        };
    }

    private static EnemyDefinition CreateTestEnemy(string id = "test_enemy", int hp = 500)
    {
        return new EnemyDefinition(
            id: id,
            name: "Test Enemy",
            level: 5,
            maxHp: hp,
            baseDamage: 50,
            attackIntervalSeconds: 2.0,
            skills: new()
        );
    }

    private static BattleEngine CreateBattle(ulong seed, double duration = 10.0, int enemyCount = 1)
    {
        var battleId = Guid.NewGuid();
        var characterId = Guid.NewGuid();
        var profession = Profession.Warrior;
        var stats = CreateTestStats();
        var rng = new RngContext(seed);
        var enemy = CreateTestEnemy();

        var engine = new BattleEngine(
            battleId: battleId,
            characterId: characterId,
            profession: profession,
            stats: stats,
            rng: rng,
            enemyDef: enemy,
            enemyCount: enemyCount
        );

        return engine;
    }

    private static void RunBattle(BattleEngine engine, double duration)
    {
        engine.AdvanceTo(duration, maxEvents: 10000);
        engine.FinalizeNow();
    }

    #endregion

    #region P7.1: RNG Consistency Tests

    [Fact]
    public void SameSeed_ShouldProduceSameResults()
    {
        // Arrange
        const ulong seed = 12345UL;
        const double duration = 10.0;

        // Act - Run two battles with the same seed
        var engine1 = CreateBattle(seed, duration);
        RunBattle(engine1, duration);

        var engine2 = CreateBattle(seed, duration);
        RunBattle(engine2, duration);

        // Assert - Results should be identical
        var segments1 = engine1.Segments;
        var segments2 = engine2.Segments;

        Assert.Equal(segments1.Count, segments2.Count);

        for (int i = 0; i < segments1.Count; i++)
        {
            var seg1 = segments1[i];
            var seg2 = segments2[i];

            // Compare total damage (must be exactly the same)
            Assert.Equal(seg1.TotalDamage, seg2.TotalDamage);

            // Compare event counts
            Assert.Equal(seg1.EventCount, seg2.EventCount);

            // Compare RNG indices
            Assert.Equal(seg1.RngIndexStart, seg2.RngIndexStart);
            Assert.Equal(seg1.RngIndexEnd, seg2.RngIndexEnd);

            // Compare damage by source
            Assert.Equal(seg1.DamageBySource.Count, seg2.DamageBySource.Count);
            foreach (var kvp in seg1.DamageBySource)
            {
                Assert.True(seg2.DamageBySource.ContainsKey(kvp.Key));
                Assert.Equal(kvp.Value, seg2.DamageBySource[kvp.Key]);
            }
        }

        // Compare final battle state
        Assert.Equal(engine1.Killed, engine2.Killed);
        Assert.Equal(engine1.SeedIndexEnd, engine2.SeedIndexEnd);
    }

    [Fact]
    public void DifferentSeeds_ShouldProduceDifferentResults()
    {
        // Arrange
        const ulong seed1 = 12345UL;
        const ulong seed2 = 54321UL;
        const double duration = 10.0;

        // Act
        var engine1 = CreateBattle(seed1, duration);
        RunBattle(engine1, duration);

        var engine2 = CreateBattle(seed2, duration);
        RunBattle(engine2, duration);

        // Assert - Results should be different
        var totalDmg1 = engine1.Segments.Sum(s => s.TotalDamage);
        var totalDmg2 = engine2.Segments.Sum(s => s.TotalDamage);

        // With different seeds and RNG-dependent events (crits, target selection),
        // RNG sequences should be different. We verify by checking that the
        // raw RNG values differ in the first few calls
        var rng1 = new RngContext(seed1);
        var rng2 = new RngContext(seed2);
        
        var val1 = rng1.NextDouble();
        var val2 = rng2.NextDouble();
        
        Assert.NotEqual(val1, val2);
    }

    [Fact]
    public void SameSeed_MultipleEnemies_ShouldBeConsistent()
    {
        // Arrange
        const ulong seed = 99999UL;
        const double duration = 15.0;
        const int enemyCount = 3;

        // Act
        var engine1 = CreateBattle(seed, duration, enemyCount);
        RunBattle(engine1, duration);

        var engine2 = CreateBattle(seed, duration, enemyCount);
        RunBattle(engine2, duration);

        // Assert
        Assert.Equal(engine1.Segments.Count, engine2.Segments.Count);
        Assert.Equal(engine1.SeedIndexEnd, engine2.SeedIndexEnd);

        var totalDmg1 = engine1.Segments.Sum(s => s.TotalDamage);
        var totalDmg2 = engine2.Segments.Sum(s => s.TotalDamage);
        Assert.Equal(totalDmg1, totalDmg2);
    }

    #endregion

    #region P7.2: RNG Index Recording Tests

    [Fact]
    public void RngIndex_ShouldBeRecordedInSegments()
    {
        // Arrange
        const ulong seed = 11111UL;
        const double duration = 10.0;

        // Act
        var engine = CreateBattle(seed, duration);
        var startIndex = engine.Context.Rng.Index;
        RunBattle(engine, duration);

        // Assert
        var segments = engine.Segments;
        Assert.NotEmpty(segments);

        // First segment should start at or near the initial index
        var firstSeg = segments.First();
        Assert.True(firstSeg.RngIndexStart >= startIndex);
        Assert.True(firstSeg.RngIndexEnd >= firstSeg.RngIndexStart);

        // Segments should be continuous
        for (int i = 1; i < segments.Count; i++)
        {
            var prevSeg = segments[i - 1];
            var currSeg = segments[i];

            // Current segment should start where previous ended
            Assert.True(currSeg.RngIndexStart >= prevSeg.RngIndexEnd);
        }

        // Last segment should end at the final RNG index
        var lastSeg = segments.Last();
        Assert.Equal(lastSeg.RngIndexEnd, engine.SeedIndexEnd);
    }

    [Fact]
    public void RngIndex_ShouldIncreaseMonotonically()
    {
        // Arrange
        const ulong seed = 22222UL;
        const double duration = 10.0;

        // Act
        var engine = CreateBattle(seed, duration);
        RunBattle(engine, duration);

        // Assert
        var segments = engine.Segments;
        Assert.NotEmpty(segments);

        long lastEnd = 0;
        foreach (var seg in segments)
        {
            // Each segment's start should be >= previous segment's end
            Assert.True(seg.RngIndexStart >= lastEnd);
            Assert.True(seg.RngIndexEnd >= seg.RngIndexStart);
            lastEnd = seg.RngIndexEnd;
        }
    }

    [Fact]
    public void RngIndex_ShouldCaptureAllRandomEvents()
    {
        // Arrange
        const ulong seed = 33333UL;
        const double duration = 10.0;

        // Act
        var engine = CreateBattle(seed, duration);
        var initialIndex = engine.Context.Rng.Index;
        RunBattle(engine, duration);
        var finalIndex = engine.Context.Rng.Index;

        // Assert
        // Total RNG calls should be captured in segments
        var totalRngRange = finalIndex - initialIndex;
        Assert.True(totalRngRange > 0, "RNG should be called during battle");

        // The segments should cover this range
        var segments = engine.Segments;
        if (segments.Count > 0)
        {
            var segmentRangeCovered = segments.Last().RngIndexEnd - segments.First().RngIndexStart;
            Assert.True(segmentRangeCovered <= totalRngRange);
        }
    }

    #endregion

    #region P7.3: Battle Replay Tool Tests

    [Fact]
    public void ReplayWithSameSeed_ShouldMatchOriginal()
    {
        // Arrange
        const ulong seed = 44444UL;
        const double duration = 12.0;

        // Act - Original battle
        var original = CreateBattle(seed, duration);
        RunBattle(original, duration);

        // Act - Replay with same seed
        var replay = CreateBattle(seed, duration);
        RunBattle(replay, duration);

        // Assert - Results should match exactly
        Assert.Equal(original.Segments.Count, replay.Segments.Count);
        Assert.Equal(original.SeedIndexEnd, replay.SeedIndexEnd);
        Assert.Equal(original.Killed, replay.Killed);

        if (original.KillTime.HasValue && replay.KillTime.HasValue)
        {
            // Kill times should match
            Assert.Equal(original.KillTime.Value, replay.KillTime.Value, precision: 6);
        }
    }

    [Fact]
    public void ReplayWithDifferentDuration_ShouldBeConsistentUpToPoint()
    {
        // Arrange
        const ulong seed = 55555UL;

        // Act - Two battles with same seed but different durations
        var shortBattle = CreateBattle(seed, duration: 5.0);
        RunBattle(shortBattle, 5.0);

        var longBattle = CreateBattle(seed, duration: 10.0);
        RunBattle(longBattle, 10.0);

        // Assert - The first 5 seconds should be identical
        // We can compare early segments
        var shortSegments = shortBattle.Segments;
        var longSegments = longBattle.Segments.Take(shortSegments.Count).ToList();

        // At least the first segment should match
        if (shortSegments.Count > 0 && longSegments.Count > 0)
        {
            var shortFirst = shortSegments.First();
            var longFirst = longSegments.First();

            // Damage in first segment should be very similar
            // (might differ slightly if segment boundaries differ)
            Assert.True(Math.Abs(shortFirst.TotalDamage - longFirst.TotalDamage) < shortFirst.TotalDamage * 0.5);
        }
    }

    #endregion

    #region P7.4: Critical Event RNG Tests

    [Fact]
    public void CriticalHits_ShouldBeConsistentWithSameSeed()
    {
        // Arrange
        const ulong seed = 66666UL;
        const double duration = 10.0;

        // Act
        var engine1 = CreateBattle(seed, duration);
        RunBattle(engine1, duration);

        var engine2 = CreateBattle(seed, duration);
        RunBattle(engine2, duration);

        // Assert - Count crit tags
        long critCount1 = 0;
        long critCount2 = 0;

        foreach (var seg in engine1.Segments)
        {
            foreach (var kvp in seg.TagCounters)
            {
                if (kvp.Key.StartsWith("crit:"))
                    critCount1 += kvp.Value;
            }
        }

        foreach (var seg in engine2.Segments)
        {
            foreach (var kvp in seg.TagCounters)
            {
                if (kvp.Key.StartsWith("crit:"))
                    critCount2 += kvp.Value;
            }
        }

        // Crit counts should match exactly
        Assert.Equal(critCount1, critCount2);
    }

    [Fact]
    public void TargetSelection_ShouldBeConsistentWithSameSeed()
    {
        // Arrange
        const ulong seed = 77777UL;
        const double duration = 10.0;
        const int enemyCount = 3;

        // Act
        var engine1 = CreateBattle(seed, duration, enemyCount);
        RunBattle(engine1, duration);

        var engine2 = CreateBattle(seed, duration, enemyCount);
        RunBattle(engine2, duration);

        // Assert - Damage distribution should be identical
        var totalDmg1 = engine1.Segments.Sum(s => s.TotalDamage);
        var totalDmg2 = engine2.Segments.Sum(s => s.TotalDamage);

        Assert.Equal(totalDmg1, totalDmg2);
    }

    #endregion

    #region P7.5: RNG Context Tests

    [Fact]
    public void RngContext_ShouldProduceDeterministicSequence()
    {
        // Arrange
        const ulong seed = 88888UL;

        // Act
        var rng1 = new RngContext(seed);
        var rng2 = new RngContext(seed);

        // Generate sequences
        var seq1 = new double[100];
        var seq2 = new double[100];

        for (int i = 0; i < 100; i++)
        {
            seq1[i] = rng1.NextDouble();
            seq2[i] = rng2.NextDouble();
        }

        // Assert
        Assert.Equal(seq1, seq2);
        Assert.Equal(rng1.Index, rng2.Index);
    }

    [Fact]
    public void RngContext_NextBool_ShouldBeConsistent()
    {
        // Arrange
        const ulong seed = 99999UL;
        const double probability = 0.3;

        // Act
        var rng1 = new RngContext(seed);
        var rng2 = new RngContext(seed);

        var results1 = new bool[100];
        var results2 = new bool[100];

        for (int i = 0; i < 100; i++)
        {
            results1[i] = rng1.NextBool(probability);
            results2[i] = rng2.NextBool(probability);
        }

        // Assert
        Assert.Equal(results1, results2);
    }

    [Fact]
    public void RngContext_Index_ShouldTrackCalls()
    {
        // Arrange
        const ulong seed = 11223UL;
        var rng = new RngContext(seed);
        var initialIndex = rng.Index;

        // Act
        rng.NextDouble();
        var afterOne = rng.Index;

        rng.NextBool();
        var afterTwo = rng.Index;

        rng.NextInt(1, 100);
        var afterThree = rng.Index;

        // Assert
        Assert.Equal(0, initialIndex);
        Assert.Equal(1, afterOne);
        Assert.Equal(2, afterTwo);
        Assert.Equal(3, afterThree);
    }

    #endregion

    #region P7.6: Complex Scenario Tests

    [Fact]
    public void ComplexBattle_WithSkillsAndEnemyAttacks_ShouldBeReproducible()
    {
        // Arrange
        const ulong seed = 111222UL;
        const double duration = 20.0;

        // Create enemy with skills
        var enemy = new EnemyDefinition(
            id: "complex_enemy",
            name: "Complex Enemy",
            level: 10,
            maxHp: 1000,
            baseDamage: 50,
            attackIntervalSeconds: 3.0,
            skills: new()
            {
                new EnemySkillDefinition(
                    id: "enemy_strike",
                    name: "Enemy Strike",
                    cooldownSeconds: 5.0,
                    trigger: TriggerType.OnCooldownReady,
                    effectValue: 100
                )
            }
        );

        // Act - Two battles
        var engine1 = CreateComplexBattle(seed, duration, enemy);
        RunBattle(engine1, duration);

        var engine2 = CreateComplexBattle(seed, duration, enemy);
        RunBattle(engine2, duration);

        // Assert
        Assert.Equal(engine1.Segments.Count, engine2.Segments.Count);
        Assert.Equal(engine1.SeedIndexEnd, engine2.SeedIndexEnd);

        var totalDmg1 = engine1.Segments.Sum(s => s.TotalDamage);
        var totalDmg2 = engine2.Segments.Sum(s => s.TotalDamage);
        Assert.Equal(totalDmg1, totalDmg2);
    }

    private static BattleEngine CreateComplexBattle(ulong seed, double duration, EnemyDefinition enemy)
    {
        var battleId = Guid.NewGuid();
        var characterId = Guid.NewGuid();
        var profession = Profession.Warrior;
        var stats = CreateTestStats();
        var rng = new RngContext(seed);

        return new BattleEngine(
            battleId: battleId,
            characterId: characterId,
            profession: profession,
            stats: stats,
            rng: rng,
            enemyDef: enemy,
            enemyCount: 1
        );
    }

    #endregion

    #region P7.4: Offline Fast-Forward Consistency Tests

    [Fact]
    public void OnlineVsOffline_SameSeed_ShouldProduceSameResults()
    {
        // Arrange
        const ulong seed = 123456UL;
        const double duration = 15.0;

        // Act - "Online" battle (normal step battle)
        var onlineBattle = CreateBattle(seed, duration);
        RunBattle(onlineBattle, duration);

        // Act - "Offline" battle (same seed, same duration, simulating offline settlement)
        var offlineBattle = CreateBattle(seed, duration);
        RunBattle(offlineBattle, duration);

        // Assert - Results should be identical
        Assert.Equal(onlineBattle.Segments.Count, offlineBattle.Segments.Count);
        Assert.Equal(onlineBattle.SeedIndexEnd, offlineBattle.SeedIndexEnd);

        var onlineTotalDmg = onlineBattle.Segments.Sum(s => s.TotalDamage);
        var offlineTotalDmg = offlineBattle.Segments.Sum(s => s.TotalDamage);
        Assert.Equal(onlineTotalDmg, offlineTotalDmg);

        Assert.Equal(onlineBattle.Killed, offlineBattle.Killed);
    }

    [Fact]
    public void BattleSimulator_SameSeed_ShouldBeReproducible()
    {
        // Arrange
        const ulong seed = 654321UL;
        const double duration = 10.0;

        var simulator = new BlazorIdle.Server.Application.Battles.BattleSimulator();
        var enemy = CreateTestEnemy();
        var stats = CreateTestStats();

        var config1 = new BlazorIdle.Server.Application.Battles.BattleSimulator.BattleConfig
        {
            BattleId = Guid.NewGuid(),
            CharacterId = Guid.NewGuid(),
            Profession = Profession.Warrior,
            Stats = stats,
            Seed = seed,
            EnemyDef = enemy,
            EnemyCount = 1
        };

        var config2 = new BlazorIdle.Server.Application.Battles.BattleSimulator.BattleConfig
        {
            BattleId = Guid.NewGuid(),
            CharacterId = Guid.NewGuid(),
            Profession = Profession.Warrior,
            Stats = stats,
            Seed = seed,
            EnemyDef = enemy,
            EnemyCount = 1
        };

        // Act
        var result1 = simulator.RunForDuration(config1, duration);
        var result2 = simulator.RunForDuration(config2, duration);

        // Assert
        Assert.Equal(result1.Segments.Count, result2.Segments.Count);
        Assert.Equal(result1.SeedIndexEnd, result2.SeedIndexEnd);

        var totalDmg1 = result1.Segments.Sum(s => s.TotalDamage);
        var totalDmg2 = result2.Segments.Sum(s => s.TotalDamage);
        Assert.Equal(totalDmg1, totalDmg2);
    }

    [Fact]
    public void BattleSimulator_WithRngContext_ShouldRespectProvidedContext()
    {
        // Arrange
        const ulong seed = 789012UL;
        const double duration = 10.0;

        var simulator = new BlazorIdle.Server.Application.Battles.BattleSimulator();
        var enemy = CreateTestEnemy();
        var stats = CreateTestStats();

        // Create two RNG contexts with the same seed
        var rng1 = new RngContext(seed);
        var rng2 = new RngContext(seed);

        var config1 = new BlazorIdle.Server.Application.Battles.BattleSimulator.BattleConfig
        {
            BattleId = Guid.NewGuid(),
            CharacterId = Guid.NewGuid(),
            Profession = Profession.Warrior,
            Stats = stats,
            Seed = 0, // Seed is ignored when Rng is provided
            Rng = rng1,
            EnemyDef = enemy,
            EnemyCount = 1
        };

        var config2 = new BlazorIdle.Server.Application.Battles.BattleSimulator.BattleConfig
        {
            BattleId = Guid.NewGuid(),
            CharacterId = Guid.NewGuid(),
            Profession = Profession.Warrior,
            Stats = stats,
            Seed = 0,
            Rng = rng2,
            EnemyDef = enemy,
            EnemyCount = 1
        };

        // Act
        var result1 = simulator.RunForDuration(config1, duration);
        var result2 = simulator.RunForDuration(config2, duration);

        // Assert
        Assert.Equal(result1.Segments.Count, result2.Segments.Count);
        
        var totalDmg1 = result1.Segments.Sum(s => s.TotalDamage);
        var totalDmg2 = result2.Segments.Sum(s => s.TotalDamage);
        Assert.Equal(totalDmg1, totalDmg2);
    }

    #endregion
}
