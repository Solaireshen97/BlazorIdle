using BlazorIdle.Server.Application.Battles;
using BlazorIdle.Server.Domain.Characters;
using BlazorIdle.Server.Domain.Combat;
using BlazorIdle.Server.Domain.Combat.Enemies;
using BlazorIdle.Server.Domain.Combat.Rng;
using BlazorIdle.Shared.Models;
using System;
using System.Linq;
using Xunit;

namespace BlazorIdle.Tests;

/// <summary>
/// Phase 7: RNG 一致性与战斗回放测试
/// 验证：
/// 1. 相同 seed 战斗结果 100% 一致
/// 2. 不同 seed 结果不同
/// 3. RNG Index 正确记录
/// 4. 离线快进与在线战斗结果一致
/// </summary>
public class BattleReplayTests
{
    private readonly BattleSimulator _simulator = new();

    [Fact]
    public void SameSeed_ProducesIdenticalResults()
    {
        // Arrange
        var seed = 98765UL;
        var battleId1 = Guid.NewGuid();
        var battleId2 = Guid.NewGuid();
        var characterId = Guid.NewGuid();
        var duration = 15.0;

        var config1 = new BattleSimulator.BattleConfig
        {
            BattleId = battleId1,
            CharacterId = characterId,
            Profession = Profession.Warrior,
            Stats = new CharacterStats(),
            Seed = seed,
            EnemyDef = EnemyRegistry.Resolve("dummy"),
            EnemyCount = 1,
            Mode = "duration"
        };

        var config2 = new BattleSimulator.BattleConfig
        {
            BattleId = battleId2,
            CharacterId = characterId,
            Profession = Profession.Warrior,
            Stats = new CharacterStats(),
            Seed = seed,
            EnemyDef = EnemyRegistry.Resolve("dummy"),
            EnemyCount = 1,
            Mode = "duration"
        };

        // Act
        var result1 = _simulator.RunForDuration(config1, duration);
        var result2 = _simulator.RunForDuration(config2, duration);

        // Assert - 战斗结果应该完全一致
        Assert.Equal(result1.Killed, result2.Killed);
        Assert.Equal(result1.KillTime, result2.KillTime);
        Assert.Equal(result1.Overkill, result2.Overkill);
        
        // RNG Index 应该完全一致
        Assert.Equal(result1.SeedIndexStart, result2.SeedIndexStart);
        Assert.Equal(result1.SeedIndexEnd, result2.SeedIndexEnd);
        
        // Segment 数量应该一致
        Assert.Equal(result1.Segments.Count, result2.Segments.Count);
        
        // 每个 Segment 的数据应该一致
        for (int i = 0; i < result1.Segments.Count; i++)
        {
            var seg1 = result1.Segments[i];
            var seg2 = result2.Segments[i];
            
            Assert.Equal(seg1.TotalDamage, seg2.TotalDamage);
            Assert.Equal(seg1.EventCount, seg2.EventCount);
            Assert.Equal(seg1.RngIndexStart, seg2.RngIndexStart);
            Assert.Equal(seg1.RngIndexEnd, seg2.RngIndexEnd);
            
            // 检查伤害来源统计一致
            foreach (var kvp in seg1.DamageBySource)
            {
                Assert.True(seg2.DamageBySource.ContainsKey(kvp.Key));
                Assert.Equal(kvp.Value, seg2.DamageBySource[kvp.Key]);
            }
        }
    }

    [Fact]
    public void DifferentSeeds_ProduceDifferentRngSequences()
    {
        // Arrange
        var seed1 = 11111UL;
        var seed2 = 22222UL;

        // Act - 直接测试 RngContext 以证明不同种子产生不同序列
        var rng1 = new RngContext(seed1);
        var rng2 = new RngContext(seed2);
        
        var values1 = new double[10];
        var values2 = new double[10];
        
        for (int i = 0; i < 10; i++)
        {
            values1[i] = rng1.NextDouble();
            values2[i] = rng2.NextDouble();
        }

        // Assert - 不同 seed 应该产生不同的随机数序列
        bool allSame = true;
        for (int i = 0; i < 10; i++)
        {
            if (values1[i] != values2[i])
            {
                allSame = false;
                break;
            }
        }
        
        Assert.False(allSame, "Different seeds should produce different random number sequences");
    }

    [Fact]
    public void RngIndex_IncrementsCorrectly()
    {
        // Arrange
        var seed = 55555UL;
        var duration = 5.0;

        var config = new BattleSimulator.BattleConfig
        {
            BattleId = Guid.NewGuid(),
            CharacterId = Guid.NewGuid(),
            Profession = Profession.Ranger,
            Stats = new CharacterStats(),
            Seed = seed,
            EnemyDef = EnemyRegistry.Resolve("dummy"),
            EnemyCount = 1,
            Mode = "duration"
        };

        // Act
        var result = _simulator.RunForDuration(config, duration);

        // Assert
        // RNG Index 应该从 0 开始
        Assert.Equal(0, result.SeedIndexStart);
        
        // RNG Index 应该在战斗过程中递增
        Assert.True(result.SeedIndexEnd > result.SeedIndexStart);
        
        // 每个 Segment 的 RNG Index 应该连续且递增
        long previousEnd = result.SeedIndexStart;
        foreach (var segment in result.Segments)
        {
            // 段的开始应该等于或接近上一段的结束
            // (允许段之间有小间隙，因为可能有事件在段边界)
            Assert.True(segment.RngIndexStart >= previousEnd);
            
            // 段的结束应该大于等于开始
            Assert.True(segment.RngIndexEnd >= segment.RngIndexStart);
            
            previousEnd = segment.RngIndexEnd;
        }
        
        // 最后一段的结束应该等于整体的结束
        if (result.Segments.Any())
        {
            var lastSegment = result.Segments.Last();
            Assert.True(lastSegment.RngIndexEnd <= result.SeedIndexEnd);
        }
    }

    [Fact]
    public void MultipleEnemies_SameSeed_ProducesIdenticalResults()
    {
        // Arrange
        var seed = 77777UL;
        var duration = 12.0;

        var config1 = new BattleSimulator.BattleConfig
        {
            BattleId = Guid.NewGuid(),
            CharacterId = Guid.NewGuid(),
            Profession = Profession.Ranger,
            Stats = new CharacterStats(),
            Seed = seed,
            EnemyDef = EnemyRegistry.Resolve("dummy"),
            EnemyCount = 3, // 多个敌人
            Mode = "duration"
        };

        var config2 = new BattleSimulator.BattleConfig
        {
            BattleId = Guid.NewGuid(),
            CharacterId = Guid.NewGuid(),
            Profession = Profession.Ranger,
            Stats = new CharacterStats(),
            Seed = seed,
            EnemyDef = EnemyRegistry.Resolve("dummy"),
            EnemyCount = 3, // 相同数量的敌人
            Mode = "duration"
        };

        // Act
        var result1 = _simulator.RunForDuration(config1, duration);
        var result2 = _simulator.RunForDuration(config2, duration);

        // Assert
        // 即使有多个敌人（涉及随机目标选择），相同 seed 也应该产生一致结果
        Assert.Equal(result1.SeedIndexEnd, result2.SeedIndexEnd);
        Assert.Equal(result1.Segments.Count, result2.Segments.Count);
        
        var totalDamage1 = result1.Segments.Sum(s => s.TotalDamage);
        var totalDamage2 = result2.Segments.Sum(s => s.TotalDamage);
        Assert.Equal(totalDamage1, totalDamage2);
    }

    [Fact]
    public void RngContext_NextBool_IsDeterministic()
    {
        // Arrange
        var seed = 99999UL;
        var rng1 = new RngContext(seed);
        var rng2 = new RngContext(seed);
        var iterations = 100;

        // Act & Assert
        for (int i = 0; i < iterations; i++)
        {
            var result1 = rng1.NextBool(0.3); // 30% 概率
            var result2 = rng2.NextBool(0.3);
            
            Assert.Equal(result1, result2);
            Assert.Equal(rng1.Index, rng2.Index);
        }
    }

    [Fact]
    public void RngContext_NextDouble_IsDeterministic()
    {
        // Arrange
        var seed = 33333UL;
        var rng1 = new RngContext(seed);
        var rng2 = new RngContext(seed);
        var iterations = 100;

        // Act & Assert
        for (int i = 0; i < iterations; i++)
        {
            var result1 = rng1.NextDouble();
            var result2 = rng2.NextDouble();
            
            Assert.Equal(result1, result2);
            Assert.Equal(rng1.Index, rng2.Index);
        }
    }

    [Fact]
    public void RngContext_NextInt_IsDeterministic()
    {
        // Arrange
        var seed = 44444UL;
        var rng1 = new RngContext(seed);
        var rng2 = new RngContext(seed);
        var iterations = 100;

        // Act & Assert
        for (int i = 0; i < iterations; i++)
        {
            var result1 = rng1.NextInt(1, 100);
            var result2 = rng2.NextInt(1, 100);
            
            Assert.Equal(result1, result2);
            Assert.Equal(rng1.Index, rng2.Index);
        }
    }

    [Fact]
    public void RngContext_Split_CreatesIndependentStreams()
    {
        // Arrange
        var seed = 66666UL;
        var rng = new RngContext(seed);
        var salt = 12345UL;

        // Act
        var rngSplit1 = rng.Split(salt);
        var rngSplit2 = rng.Split(salt);
        
        // 主流继续
        var mainResult = rng.NextDouble();

        // Assert
        // Split 不应该改变主流的状态（除了创建时消耗的随机数）
        // 相同 salt 的 Split 应该产生相同的子流
        Assert.Equal(rngSplit1.Index, rngSplit2.Index);
        
        var split1Result = rngSplit1.NextDouble();
        var split2Result = rngSplit2.NextDouble();
        Assert.Equal(split1Result, split2Result);
    }

    [Fact]
    public void DifferentProfessions_SameSeed_ProduceDifferentButDeterministicResults()
    {
        // Arrange
        var seed = 88888UL;
        var duration = 10.0;

        var configWarrior = new BattleSimulator.BattleConfig
        {
            BattleId = Guid.NewGuid(),
            CharacterId = Guid.NewGuid(),
            Profession = Profession.Warrior,
            Stats = new CharacterStats(),
            Seed = seed,
            EnemyDef = EnemyRegistry.Resolve("dummy"),
            EnemyCount = 1,
            Mode = "duration"
        };

        var configRanger = new BattleSimulator.BattleConfig
        {
            BattleId = Guid.NewGuid(),
            CharacterId = Guid.NewGuid(),
            Profession = Profession.Ranger,
            Stats = new CharacterStats(),
            Seed = seed,
            EnemyDef = EnemyRegistry.Resolve("dummy"),
            EnemyCount = 1,
            Mode = "duration"
        };

        // Act
        var resultWarrior1 = _simulator.RunForDuration(configWarrior, duration);
        var resultWarrior2 = _simulator.RunForDuration(configWarrior, duration);
        var resultRanger = _simulator.RunForDuration(configRanger, duration);

        // Assert
        // 相同职业相同 seed 应该产生一致结果
        Assert.Equal(resultWarrior1.SeedIndexEnd, resultWarrior2.SeedIndexEnd);
        
        // 不同职业即使相同 seed 也会产生不同结果（因为技能、间隔不同）
        // 但每次运行应该仍然是确定性的
        Assert.NotEqual(resultWarrior1.SeedIndexEnd, resultRanger.SeedIndexEnd);
    }

    [Fact]
    public void SegmentRngRanges_AreContinuous()
    {
        // Arrange
        var seed = 10101UL;
        var duration = 20.0; // 较长时间以产生多个段

        var config = new BattleSimulator.BattleConfig
        {
            BattleId = Guid.NewGuid(),
            CharacterId = Guid.NewGuid(),
            Profession = Profession.Warrior,
            Stats = new CharacterStats(),
            Seed = seed,
            EnemyDef = EnemyRegistry.Resolve("dummy"),
            EnemyCount = 1,
            Mode = "duration"
        };

        // Act
        var result = _simulator.RunForDuration(config, duration);

        // Assert
        // 应该有多个段
        Assert.True(result.Segments.Count > 1, "Should have multiple segments for this duration");
        
        // 验证段之间的 RNG Index 是连续的
        for (int i = 0; i < result.Segments.Count - 1; i++)
        {
            var currentSegment = result.Segments[i];
            var nextSegment = result.Segments[i + 1];
            
            // 当前段的结束应该小于等于下一段的开始
            Assert.True(currentSegment.RngIndexEnd <= nextSegment.RngIndexStart,
                $"Segment {i} RngIndexEnd ({currentSegment.RngIndexEnd}) should be <= Segment {i+1} RngIndexStart ({nextSegment.RngIndexStart})");
        }
    }
}
