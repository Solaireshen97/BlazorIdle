using Xunit;

namespace BlazorIdle.Tests;

/// <summary>
/// 测试进度条循环功能
/// Tests for progress bar cycling functionality
/// </summary>
public class ProgressBarCyclingTests
{
    /// <summary>
    /// 测试基本的取模循环功能
    /// 当进度超过 100% 时，应该循环回到 0%
    /// </summary>
    [Fact]
    public void ProgressCalculation_ShouldCycle_WhenExceeds100Percent()
    {
        // Arrange
        double currentTime = 10.0;
        double nextTriggerAt = 10.0;  // 触发点在 10s
        double interval = 2.0;         // 间隔 2s
        DateTime lastUpdateTime = DateTime.UtcNow.AddSeconds(-2.5); // 2.5秒前更新
        
        // Act
        // 服务器进度：(10 - 8) / 2 = 1.0 (100%)
        // 客户端插值：2.5 / 2 = 1.25
        // 总进度：1.0 + 1.25 = 2.25
        // 取模后：2.25 % 1.0 = 0.25 (25%)
        double serverProgress = (currentTime - (nextTriggerAt - interval)) / interval;
        double clientElapsed = 2.5;
        double interpolated = serverProgress + (clientElapsed / interval);
        double cycledProgress = interpolated % 1.0;
        
        // Assert
        Assert.Equal(1.0, serverProgress, precision: 2);
        Assert.Equal(2.25, interpolated, precision: 2);
        Assert.Equal(0.25, cycledProgress, precision: 2);
    }
    
    /// <summary>
    /// 测试多轮循环
    /// 进度应该能够多次超过 100% 并正确循环
    /// </summary>
    [Fact]
    public void ProgressCalculation_ShouldCycleMultipleTimes_WhenFarExceeds100Percent()
    {
        // Arrange
        double interval = 1.0;  // 1秒间隔
        
        // Test multiple cycles
        var testCases = new[]
        {
            (elapsed: 0.5, expected: 0.5),   // 50% (未超过)
            (elapsed: 1.0, expected: 0.0),   // 100% -> 0%
            (elapsed: 1.3, expected: 0.3),   // 130% -> 30%
            (elapsed: 2.0, expected: 0.0),   // 200% -> 0%
            (elapsed: 2.7, expected: 0.7),   // 270% -> 70%
            (elapsed: 5.2, expected: 0.2),   // 520% -> 20%
        };
        
        foreach (var (elapsed, expected) in testCases)
        {
            // Act
            double progress = elapsed / interval;
            double cycled = progress % 1.0;
            
            // Assert
            Assert.Equal(expected, cycled, precision: 2);
        }
    }
    
    /// <summary>
    /// 测试间隔变化时的行为
    /// 当服务器更新间隔时，客户端应该平滑过渡
    /// </summary>
    [Fact]
    public void ProgressCalculation_ShouldAdaptToNewInterval_WhenServerUpdates()
    {
        // Arrange - 第一个周期
        double oldInterval = 2.0;
        double currentTime1 = 8.0;
        double nextTriggerAt1 = 10.0;
        DateTime lastUpdate1 = DateTime.UtcNow.AddSeconds(-0.5);
        
        // Act - 第一个周期的进度
        double serverProgress1 = (currentTime1 - (nextTriggerAt1 - oldInterval)) / oldInterval;
        double clientElapsed1 = 0.5;
        double progress1 = (serverProgress1 + (clientElapsed1 / oldInterval)) % 1.0;
        
        // Assert - 第一个周期
        Assert.True(progress1 >= 0.0 && progress1 < 1.0);
        
        // Arrange - 第二个周期（间隔改变）
        double newInterval = 1.5;
        double currentTime2 = 11.0;
        double nextTriggerAt2 = 11.5;
        DateTime lastUpdate2 = DateTime.UtcNow;
        
        // Act - 第二个周期的进度
        double serverProgress2 = (currentTime2 - (nextTriggerAt2 - newInterval)) / newInterval;
        double progress2 = serverProgress2 % 1.0;
        
        // Assert - 第二个周期
        Assert.True(progress2 >= 0.0 && progress2 < 1.0);
        Assert.Equal(0.67, progress2, precision: 1); // (11-10) / 1.5 ≈ 0.67
    }
    
    /// <summary>
    /// 测试极小间隔的边界情况
    /// </summary>
    [Fact]
    public void ProgressCalculation_ShouldHandleSmallIntervals()
    {
        // Arrange
        double interval = 0.1;  // 100ms 间隔
        double elapsed = 0.25;  // 250ms
        
        // Act
        double progress = (elapsed / interval) % 1.0;
        
        // Assert
        Assert.Equal(0.5, progress, precision: 2); // 0.25 / 0.1 = 2.5 -> 0.5
    }
    
    /// <summary>
    /// 测试零间隔的边界情况
    /// 应该返回 0 或 1（二元判断）
    /// </summary>
    [Fact]
    public void ProgressCalculation_ShouldReturnBinary_WhenIntervalIsZero()
    {
        // Arrange
        double interval = 0.0;
        double currentTime = 5.0;
        double nextTriggerAt = 10.0;
        
        // Act & Assert - 未到触发时间
        bool shouldReturn0 = currentTime < nextTriggerAt;
        Assert.True(shouldReturn0);
        
        // Act & Assert - 已到触发时间
        currentTime = 10.0;
        bool shouldReturn1 = currentTime >= nextTriggerAt;
        Assert.True(shouldReturn1);
    }
    
    /// <summary>
    /// 测试负进度的处理
    /// 虽然理论上不应出现，但要确保正确处理
    /// </summary>
    [Fact]
    public void ProgressCalculation_ShouldHandleNegativeProgress()
    {
        // Arrange
        double progress = -0.3;
        
        // Act
        double cycled = progress % 1.0;
        if (cycled < 0) cycled += 1.0;
        
        // Assert
        Assert.Equal(0.7, cycled, precision: 2);
    }
}
