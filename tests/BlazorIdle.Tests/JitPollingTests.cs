using Xunit;

namespace BlazorIdle.Tests;

/// <summary>
/// 测试 JIT (Just-in-time) 自适应轮询功能
/// Tests for JIT adaptive polling functionality
/// </summary>
public class JitPollingTests
{
    /// <summary>
    /// 测试触发预测计算
    /// 应该正确计算到下一个触发点的时间
    /// </summary>
    [Fact]
    public void TriggerPrediction_ShouldCalculateCorrectTime_ToNextTrigger()
    {
        // Arrange
        double interval = 2.0;           // 2秒间隔
        double currentProgress = 0.75;   // 当前进度 75%
        
        // Act
        double timeToNextTrigger = interval * (1.0 - currentProgress);
        
        // Assert
        Assert.Equal(0.5, timeToNextTrigger, precision: 2); // 还需要 0.5秒
    }
    
    /// <summary>
    /// 测试触发窗口判断
    /// 当进度在触发窗口内时，应该返回 true
    /// </summary>
    [Fact]
    public void TriggerWindow_ShouldReturnTrue_WhenWithinLeadTime()
    {
        // Arrange
        double timeToNextTrigger = 0.12;  // 距离触发还有 120ms
        double triggerLeadTimeSeconds = 0.15; // 提前量 150ms
        
        // Act
        bool shouldTriggerJit = timeToNextTrigger > 0 && 
                                timeToNextTrigger <= triggerLeadTimeSeconds + 0.1;
        
        // Assert
        Assert.True(shouldTriggerJit);
    }
    
    /// <summary>
    /// 测试触发窗口判断 - 太远
    /// 当距离触发点太远时，不应该触发 JIT 轮询
    /// </summary>
    [Fact]
    public void TriggerWindow_ShouldReturnFalse_WhenTooFar()
    {
        // Arrange
        double timeToNextTrigger = 1.5;  // 距离触发还有 1.5秒
        double triggerLeadTimeSeconds = 0.15;
        
        // Act
        bool shouldTriggerJit = timeToNextTrigger > 0 && 
                                timeToNextTrigger <= triggerLeadTimeSeconds + 0.1;
        
        // Assert
        Assert.False(shouldTriggerJit);
    }
    
    /// <summary>
    /// 测试触发窗口判断 - 已过期
    /// 当已经过了触发点时，不应该触发 JIT 轮询
    /// </summary>
    [Fact]
    public void TriggerWindow_ShouldReturnFalse_WhenAlreadyPassed()
    {
        // Arrange
        double timeToNextTrigger = -0.1;  // 已经过了触发点
        double triggerLeadTimeSeconds = 0.15;
        
        // Act
        bool shouldTriggerJit = timeToNextTrigger > 0 && 
                                timeToNextTrigger <= triggerLeadTimeSeconds + 0.1;
        
        // Assert
        Assert.False(shouldTriggerJit);
    }
    
    /// <summary>
    /// 测试 JIT 延迟计算
    /// 应该根据预测时间和提前量计算延迟
    /// </summary>
    [Fact]
    public void JitDelay_ShouldCalculateCorrectly_BasedOnPrediction()
    {
        // Arrange
        double predictedTriggerTimeSeconds = 0.3;  // 预测 300ms 后触发
        int triggerLeadTimeMs = 150;               // 提前 150ms
        
        // Act
        int delayMs = Math.Max(0, (int)((predictedTriggerTimeSeconds * 1000) - triggerLeadTimeMs));
        
        // Assert
        Assert.Equal(150, delayMs); // 300ms - 150ms = 150ms
    }
    
    /// <summary>
    /// 测试 JIT 延迟计算 - 负值处理
    /// 当计算出负延迟时，应该返回 0
    /// </summary>
    [Fact]
    public void JitDelay_ShouldBeZero_WhenCalculationIsNegative()
    {
        // Arrange
        double predictedTriggerTimeSeconds = 0.1;  // 预测 100ms 后触发
        int triggerLeadTimeMs = 150;               // 提前 150ms
        
        // Act
        int delayMs = Math.Max(0, (int)((predictedTriggerTimeSeconds * 1000) - triggerLeadTimeMs));
        
        // Assert
        Assert.Equal(0, delayMs); // Max(0, -50) = 0
    }
    
    /// <summary>
    /// 测试频率限制 - 每秒最大次数
    /// 应该限制每秒内的 JIT 轮询次数
    /// </summary>
    [Fact]
    public void RateLimit_ShouldPrevent_ExcessiveJitPolls()
    {
        // Arrange
        int maxJitPollsPerSecond = 5;
        int currentJitPollsThisSecond = 5;
        
        // Act
        bool shouldAllow = currentJitPollsThisSecond < maxJitPollsPerSecond;
        
        // Assert
        Assert.False(shouldAllow);
    }
    
    /// <summary>
    /// 测试冷却时间检查
    /// 在冷却时间内不应该触发新的 JIT 轮询
    /// </summary>
    [Fact]
    public void Cooldown_ShouldPrevent_TooFrequentJitPolls()
    {
        // Arrange
        DateTime lastJitPoll = DateTime.UtcNow.AddMilliseconds(-200); // 200ms 前
        int cooldownMs = 300;
        
        // Act
        bool isInCooldown = (DateTime.UtcNow - lastJitPoll).TotalMilliseconds < cooldownMs;
        
        // Assert
        Assert.True(isInCooldown);
    }
    
    /// <summary>
    /// 测试冷却时间检查 - 已过期
    /// 冷却时间过后应该允许新的 JIT 轮询
    /// </summary>
    [Fact]
    public void Cooldown_ShouldAllow_AfterCooldownPeriod()
    {
        // Arrange
        DateTime lastJitPoll = DateTime.UtcNow.AddMilliseconds(-400); // 400ms 前
        int cooldownMs = 300;
        
        // Act
        bool isInCooldown = (DateTime.UtcNow - lastJitPoll).TotalMilliseconds < cooldownMs;
        
        // Assert
        Assert.False(isInCooldown);
    }
    
    /// <summary>
    /// 测试每秒计数器重置
    /// 当进入新的一秒时，应该重置计数器
    /// </summary>
    [Fact]
    public void SecondCounter_ShouldReset_WhenNewSecondStarts()
    {
        // Arrange
        DateTime jitPollSecondStart = DateTime.UtcNow.AddSeconds(-1.5); // 1.5秒前
        
        // Act
        bool shouldReset = (DateTime.UtcNow - jitPollSecondStart).TotalSeconds >= 1.0;
        
        // Assert
        Assert.True(shouldReset);
    }
    
    /// <summary>
    /// 测试配置参数范围限制
    /// 提前时间应该在最小和最大值之间
    /// </summary>
    [Fact]
    public void LeadTime_ShouldBeClampedToRange()
    {
        // Arrange
        int triggerLeadTimeMs = 200;
        int minLeadTimeMs = 50;
        int maxLeadTimeMs = 500;
        
        // Act
        int clampedLeadTime = Math.Clamp(triggerLeadTimeMs, minLeadTimeMs, maxLeadTimeMs);
        
        // Assert
        Assert.Equal(200, clampedLeadTime);
        
        // Test lower bound
        clampedLeadTime = Math.Clamp(30, minLeadTimeMs, maxLeadTimeMs);
        Assert.Equal(50, clampedLeadTime);
        
        // Test upper bound
        clampedLeadTime = Math.Clamp(600, minLeadTimeMs, maxLeadTimeMs);
        Assert.Equal(500, clampedLeadTime);
    }
    
    /// <summary>
    /// 测试循环进度下的触发预测
    /// 当进度循环时，应该正确预测下一个触发点
    /// </summary>
    [Fact]
    public void TriggerPrediction_ShouldWork_WithCyclingProgress()
    {
        // Arrange
        double interval = 1.5;           // 1.5秒间隔
        double rawProgress = 2.3;        // 原始进度 230%
        
        // Act
        double cycledProgress = rawProgress % 1.0;  // 0.3 (30%)
        double timeToNextTrigger = interval * (1.0 - cycledProgress);
        
        // Assert
        Assert.Equal(0.3, cycledProgress, precision: 2);
        Assert.Equal(1.05, timeToNextTrigger, precision: 2); // 70% * 1.5s = 1.05s
    }
}
