using Xunit;
using BlazorIdle.Models;

namespace BlazorIdle.Tests;

/// <summary>
/// 测试进度条配置功能
/// </summary>
public class ProgressBarConfigTests
{
    /// <summary>
    /// 测试默认配置创建
    /// </summary>
    [Fact]
    public void DefaultConfig_ShouldHaveCorrectValues()
    {
        // Arrange & Act
        var config = new ProgressBarConfig();
        
        // Assert
        Assert.NotNull(config.ProgressBar);
        Assert.NotNull(config.JITPolling);
        Assert.NotNull(config.HPAnimation);
        Assert.NotNull(config.Debug);
        
        // 验证默认值
        Assert.True(config.ProgressBar.EnableLoopingProgress);
        Assert.Equal(100, config.ProgressBar.AnimationIntervalMs);
        Assert.Equal(0.1, config.ProgressBar.MinIntervalForLooping);
        Assert.Equal(100.0, config.ProgressBar.MaxIntervalForLooping);
    }

    /// <summary>
    /// 测试JIT轮询配置默认值
    /// </summary>
    [Fact]
    public void JITPollingConfig_ShouldHaveCorrectDefaults()
    {
        // Arrange & Act
        var config = new ProgressBarConfig();
        
        // Assert
        Assert.True(config.JITPolling.EnableJITPolling);
        Assert.Equal(150, config.JITPolling.TriggerWindowMs);
        Assert.Equal(100, config.JITPolling.MinPredictionTimeMs);
        Assert.Equal(1, config.JITPolling.MaxJITAttemptsPerCycle);
        Assert.True(config.JITPolling.AdaptivePollingEnabled);
    }

    /// <summary>
    /// 测试自适应轮询阈值
    /// </summary>
    [Theory]
    [InlineData(0.2, 500)]   // Critical: HP < 30%
    [InlineData(0.3, 500)]   // Critical: HP = 30%
    [InlineData(0.4, 1000)]  // Low: 30% < HP <= 50%
    [InlineData(0.5, 1000)]  // Low: HP = 50%
    [InlineData(0.6, 2000)]  // Normal: HP > 50%
    [InlineData(1.0, 2000)]  // Normal: Full HP
    public void AdaptivePolling_ShouldCalculateCorrectInterval(double hpPercent, int expectedInterval)
    {
        // Arrange
        var config = new ProgressBarConfig();
        int actualInterval;
        
        // Act
        if (hpPercent <= config.JITPolling.HealthCriticalThreshold)
        {
            actualInterval = config.JITPolling.CriticalHealthPollingMs;
        }
        else if (hpPercent <= config.JITPolling.HealthLowThreshold)
        {
            actualInterval = config.JITPolling.LowHealthPollingMs;
        }
        else
        {
            actualInterval = config.JITPolling.NormalPollingMs;
        }
        
        // Assert
        Assert.Equal(expectedInterval, actualInterval);
    }

    /// <summary>
    /// 测试HP动画配置
    /// </summary>
    [Fact]
    public void HPAnimationConfig_ShouldHaveCorrectDefaults()
    {
        // Arrange & Act
        var config = new ProgressBarConfig();
        
        // Assert
        Assert.Equal(120, config.HPAnimation.TransitionDurationMs);
        Assert.Equal("linear", config.HPAnimation.TransitionTimingFunction);
        Assert.True(config.HPAnimation.EnableSmoothTransition);
        Assert.Equal(120, config.HPAnimation.PlayerHPTransitionMs);
        Assert.Equal(120, config.HPAnimation.EnemyHPTransitionMs);
    }

    /// <summary>
    /// 测试调试配置默认关闭
    /// </summary>
    [Fact]
    public void DebugConfig_ShouldBeDisabledByDefault()
    {
        // Arrange & Act
        var config = new ProgressBarConfig();
        
        // Assert
        Assert.False(config.Debug.LogProgressCalculations);
        Assert.False(config.Debug.LogJITPollingEvents);
        Assert.False(config.Debug.ShowProgressDebugInfo);
    }

    /// <summary>
    /// 测试配置范围验证
    /// </summary>
    [Theory]
    [InlineData(50, true)]    // 有效的动画间隔
    [InlineData(100, true)]   // 默认值
    [InlineData(200, true)]   // 较大的值
    [InlineData(0, false)]    // 无效：太小
    [InlineData(-1, false)]   // 无效：负数
    public void AnimationInterval_ShouldValidateRange(int intervalMs, bool isValid)
    {
        // Arrange & Act
        bool actualValid = intervalMs > 0 && intervalMs <= 1000;
        
        // Assert
        Assert.Equal(isValid, actualValid);
    }

    /// <summary>
    /// 测试轮询间隔范围
    /// </summary>
    [Fact]
    public void PollingIntervals_ShouldBeInValidRange()
    {
        // Arrange
        var config = new ProgressBarConfig();
        
        // Act & Assert
        Assert.InRange(config.JITPolling.CriticalHealthPollingMs, 
            config.JITPolling.MinPollingIntervalMs, 
            config.JITPolling.MaxPollingIntervalMs);
            
        Assert.InRange(config.JITPolling.LowHealthPollingMs, 
            config.JITPolling.MinPollingIntervalMs, 
            config.JITPolling.MaxPollingIntervalMs);
            
        Assert.InRange(config.JITPolling.NormalPollingMs, 
            config.JITPolling.MinPollingIntervalMs, 
            config.JITPolling.MaxPollingIntervalMs);
    }

    /// <summary>
    /// 测试健康阈值顺序
    /// </summary>
    [Fact]
    public void HealthThresholds_ShouldBeInCorrectOrder()
    {
        // Arrange
        var config = new ProgressBarConfig();
        
        // Act & Assert
        Assert.True(config.JITPolling.HealthCriticalThreshold < config.JITPolling.HealthLowThreshold);
        Assert.True(config.JITPolling.HealthLowThreshold <= 1.0);
        Assert.True(config.JITPolling.HealthCriticalThreshold >= 0.0);
    }

    /// <summary>
    /// 测试JIT触发窗口验证
    /// </summary>
    [Fact]
    public void JITTriggerWindow_ShouldBeReasonable()
    {
        // Arrange
        var config = new ProgressBarConfig();
        
        // Act & Assert
        // 触发窗口应该大于最小预测时间
        Assert.True(config.JITPolling.TriggerWindowMs > config.JITPolling.MinPredictionTimeMs);
        // 触发窗口不应该太大（避免过早触发）
        Assert.True(config.JITPolling.TriggerWindowMs < 500);
    }

    /// <summary>
    /// 测试interval循环范围合理性
    /// </summary>
    [Fact]
    public void IntervalLoopingRange_ShouldBeSane()
    {
        // Arrange
        var config = new ProgressBarConfig();
        
        // Act & Assert
        Assert.True(config.ProgressBar.MinIntervalForLooping > 0);
        Assert.True(config.ProgressBar.MaxIntervalForLooping > config.ProgressBar.MinIntervalForLooping);
        Assert.True(config.ProgressBar.MinIntervalForLooping >= 0.01); // 至少10ms
    }
}
