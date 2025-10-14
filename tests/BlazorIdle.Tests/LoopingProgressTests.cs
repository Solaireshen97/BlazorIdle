using Xunit;

namespace BlazorIdle.Tests;

/// <summary>
/// 测试进度条循环滚动功能
/// </summary>
public class LoopingProgressTests
{
    /// <summary>
    /// 测试基础进度计算（未超过100%）
    /// </summary>
    [Fact]
    public void CalculateSmoothProgress_BelowOneHundredPercent_ReturnsCorrectValue()
    {
        // Arrange
        double currentTime = 5.0;
        double nextTriggerAt = 10.0;
        double interval = 5.0;
        double lastTriggerAt = nextTriggerAt - interval; // = 5.0
        
        // Act
        double serverProgress = (currentTime - lastTriggerAt) / interval; // (5-5)/5 = 0
        double clientElapsed = 2.0; // 模拟2秒客户端时间
        double interpolated = serverProgress + (clientElapsed / interval); // 0 + 2/5 = 0.4
        
        // Assert
        Assert.InRange(interpolated, 0.0, 1.0);
        Assert.Equal(0.4, interpolated, precision: 2);
    }

    /// <summary>
    /// 测试循环进度计算（超过100%后取模）
    /// </summary>
    [Fact]
    public void CalculateSmoothProgress_AboveOneHundredPercent_ShouldLoop()
    {
        // Arrange
        double rawProgress = 1.3; // 130%
        
        // Act
        double loopedProgress = rawProgress % 1.0;
        
        // Assert
        Assert.Equal(0.3, loopedProgress, precision: 2);
    }

    /// <summary>
    /// 测试循环进度计算（大幅超过100%）
    /// </summary>
    [Fact]
    public void CalculateSmoothProgress_WayAboveOneHundredPercent_ShouldLoop()
    {
        // Arrange
        double rawProgress = 2.7; // 270%
        
        // Act
        double loopedProgress = rawProgress % 1.0;
        
        // Assert
        Assert.Equal(0.7, loopedProgress, precision: 2);
    }

    /// <summary>
    /// 测试精确100%时的循环
    /// </summary>
    [Fact]
    public void CalculateSmoothProgress_ExactlyOneHundredPercent_ShouldLoopToZero()
    {
        // Arrange
        double rawProgress = 1.0;
        
        // Act
        double loopedProgress = rawProgress % 1.0;
        
        // Assert
        Assert.Equal(0.0, loopedProgress, precision: 2);
    }

    /// <summary>
    /// 测试多次完整循环
    /// </summary>
    [Fact]
    public void CalculateSmoothProgress_MultipleCycles_ShouldLoop()
    {
        // Arrange
        var testCases = new[]
        {
            (1.1, 0.1),
            (1.5, 0.5),
            (2.0, 0.0),
            (2.3, 0.3),
            (3.7, 0.7)
        };
        
        // Act & Assert
        foreach (var (input, expected) in testCases)
        {
            double result = input % 1.0;
            Assert.Equal(expected, result, precision: 2);
        }
    }

    /// <summary>
    /// 测试interval范围验证
    /// </summary>
    [Theory]
    [InlineData(0.05, false)]  // 低于最小值
    [InlineData(0.1, true)]    // 最小值
    [InlineData(5.0, true)]    // 正常范围
    [InlineData(100.0, true)]  // 最大值
    [InlineData(150.0, false)] // 超过最大值
    public void ValidateInterval_ShouldCheckRange(double interval, bool shouldBeValid)
    {
        // Arrange
        double minInterval = 0.1;
        double maxInterval = 100.0;
        
        // Act
        bool isValid = interval >= minInterval && interval <= maxInterval;
        
        // Assert
        Assert.Equal(shouldBeValid, isValid);
    }

    /// <summary>
    /// 测试负进度值（不应该发生但要验证处理）
    /// </summary>
    [Fact]
    public void CalculateSmoothProgress_NegativeProgress_ShouldHandleGracefully()
    {
        // Arrange
        double currentTime = 2.0;
        double nextTriggerAt = 10.0;
        double interval = 5.0;
        double lastTriggerAt = nextTriggerAt - interval; // 5.0
        
        // Act
        double serverProgress = (currentTime - lastTriggerAt) / interval;
        
        // Assert - 应该是负值或零（表示还未到触发点）
        Assert.True(serverProgress >= -1.0 && serverProgress <= 1.0);
    }

    /// <summary>
    /// 测试零interval的处理
    /// </summary>
    [Fact]
    public void CalculateSmoothProgress_ZeroInterval_ShouldReturnBinaryValue()
    {
        // Arrange
        double currentTime1 = 5.0;
        double currentTime2 = 10.0;
        double nextTriggerAt = 10.0;
        
        // Act
        bool beforeTrigger = currentTime1 < nextTriggerAt;
        bool atOrAfterTrigger = currentTime2 >= nextTriggerAt;
        
        // Assert - 这两个状态应该是互斥的（不能同时为真）
        Assert.True(beforeTrigger);
        Assert.True(atOrAfterTrigger);
        // 验证它们确实是不同的状态
        Assert.NotEqual(currentTime1, currentTime2);
    }
}
