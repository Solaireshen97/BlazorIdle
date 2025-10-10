using BlazorIdle.Server.Application.Battles;
using BlazorIdle.Server.Domain.Characters;
using BlazorIdle.Server.Domain.Combat;
using BlazorIdle.Server.Domain.Combat.Enemies;
using BlazorIdle.Server.Domain.Combat.Rng;
using BlazorIdle.Shared.Models;
using System;
using System.Diagnostics;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace BlazorIdle.Tests;

/// <summary>
/// Phase 8: 性能基准测试
/// 验证战斗系统扩展后的性能表现
/// </summary>
public class Phase8PerformanceTests
{
    private readonly ITestOutputHelper _output;
    private readonly BattleSimulator _simulator = new();

    public Phase8PerformanceTests(ITestOutputHelper output)
    {
        _output = output;
    }

    #region 基础战斗性能测试

    [Fact]
    public void Perf_SimpleBattle_60Seconds_CompletesInReasonableTime()
    {
        // Arrange
        var config = new BattleSimulator.BattleConfig
        {
            BattleId = Guid.NewGuid(),
            CharacterId = Guid.NewGuid(),
            Profession = Profession.Warrior,
            Stats = new CharacterStats(),
            Seed = 12345UL,
            EnemyDef = EnemyRegistry.Resolve("dummy"),
            EnemyCount = 1,
            Mode = "duration"
        };

        // Act
        var sw = Stopwatch.StartNew();
        var result = _simulator.RunForDuration(config, 60.0);
        sw.Stop();

        // Assert
        _output.WriteLine($"60秒简单战斗耗时: {sw.ElapsedMilliseconds}ms");
        Assert.True(sw.ElapsedMilliseconds < 10000, 
            $"60秒战斗应在10秒内完成，实际: {sw.ElapsedMilliseconds}ms");
        Assert.NotEmpty(result.Segments);
    }

    [Fact]
    public void Perf_MultiEnemyBattle_60Seconds_CompletesInReasonableTime()
    {
        // Arrange
        var config = new BattleSimulator.BattleConfig
        {
            BattleId = Guid.NewGuid(),
            CharacterId = Guid.NewGuid(),
            Profession = Profession.Warrior,
            Stats = new CharacterStats(),
            Seed = 54321UL,
            EnemyDef = EnemyRegistry.Resolve("dummy"),
            EnemyCount = 5,
            Mode = "duration"
        };

        // Act
        var sw = Stopwatch.StartNew();
        var result = _simulator.RunForDuration(config, 60.0);
        sw.Stop();

        // Assert
        _output.WriteLine($"60秒多敌人战斗耗时: {sw.ElapsedMilliseconds}ms");
        Assert.True(sw.ElapsedMilliseconds < 15000, 
            $"60秒5敌人战斗应在15秒内完成，实际: {sw.ElapsedMilliseconds}ms");
    }

    #endregion

    #region 长时间战斗性能测试

    [Fact]
    public void Perf_LongDurationBattle_5Minutes_NoMemoryLeak()
    {
        // Arrange
        var config = new BattleSimulator.BattleConfig
        {
            BattleId = Guid.NewGuid(),
            CharacterId = Guid.NewGuid(),
            Profession = Profession.Warrior,
            Stats = new CharacterStats(),
            Seed = 77777UL,
            EnemyDef = EnemyRegistry.Resolve("dummy"),
            EnemyCount = 1,
            Mode = "duration"
        };

        // Act
        var memBefore = GC.GetTotalMemory(true);
        var sw = Stopwatch.StartNew();
        var result = _simulator.RunForDuration(config, 300.0); // 5分钟
        sw.Stop();
        var memAfter = GC.GetTotalMemory(true);
        var memDiff = (memAfter - memBefore) / 1024.0 / 1024.0; // MB

        // Assert
        _output.WriteLine($"5分钟战斗耗时: {sw.ElapsedMilliseconds}ms");
        _output.WriteLine($"内存增长: {memDiff:F2} MB");
        
        Assert.True(sw.ElapsedMilliseconds < 60000, 
            $"5分钟战斗应在60秒内完成，实际: {sw.ElapsedMilliseconds}ms");
        Assert.True(memDiff < 100, 
            $"5分钟战斗内存增长应小于100MB，实际: {memDiff:F2}MB");
    }

    #endregion

    #region RNG性能测试

    [Fact]
    public void Perf_RngContext_1MillionCalls_CompletesQuickly()
    {
        // Arrange
        var rng = new RngContext(12345UL);
        const int iterations = 1_000_000;

        // Act
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < iterations; i++)
        {
            rng.NextDouble();
        }
        sw.Stop();

        // Assert
        _output.WriteLine($"100万次RNG调用耗时: {sw.ElapsedMilliseconds}ms");
        _output.WriteLine($"平均每次: {sw.ElapsedMilliseconds / 1000000.0 * 1000:F3}μs");
        
        Assert.True(sw.ElapsedMilliseconds < 2000, 
            $"100万次RNG调用应在2秒内完成，实际: {sw.ElapsedMilliseconds}ms");
    }

    #endregion

    #region Segment收集性能测试

    [Fact]
    public void Perf_SegmentCollection_LargeNumberOfEvents_HandlesEfficiently()
    {
        // Arrange
        var collector = new SegmentCollector();
        const int eventCount = 10000;

        // Act
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < eventCount; i++)
        {
            collector.OnDamage("player", 100);
            
            if (i % 100 == 0)
            {
                collector.OnRngIndex(i);
            }
        }
        sw.Stop();

        // Assert
        _output.WriteLine($"收集{eventCount}个事件耗时: {sw.ElapsedMilliseconds}ms");
        
        Assert.True(sw.ElapsedMilliseconds < 3000, 
            $"收集{eventCount}个事件应在3秒内完成，实际: {sw.ElapsedMilliseconds}ms");
    }

    #endregion
}
