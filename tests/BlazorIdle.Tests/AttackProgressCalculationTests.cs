using System;
using Xunit;

namespace BlazorIdle.Tests;

/// <summary>
/// 测试攻击进度条的计算逻辑
/// </summary>
public class AttackProgressCalculationTests
{
    /// <summary>
    /// 模拟客户端进度计算逻辑
    /// </summary>
    private (double progress, double timeRemaining) CalculateProgress(
        DateTime lastUpdateTime,
        double lastServerCurrentTime,
        double nextAttackAt,
        double attackInterval,
        DateTime now)
    {
        // 计算从上次服务器更新到现在经过的客户端时间
        var clientElapsed = (now - lastUpdateTime).TotalSeconds;
        
        // 估算当前服务器时间（基于客户端时间推进）
        var estimatedServerTime = lastServerCurrentTime + clientElapsed;
        
        // 计算攻击进度起始时间（NextAttackAt - AttackInterval）
        var attackStartTime = nextAttackAt - attackInterval;
        
        // 计算进度（0.0 到 1.0）
        var elapsedSinceStart = estimatedServerTime - attackStartTime;
        var progress = Math.Clamp(elapsedSinceStart / attackInterval, 0.0, 1.0);
        
        // 计算剩余时间
        var timeRemaining = Math.Max(0, nextAttackAt - estimatedServerTime);
        
        return (progress, timeRemaining);
    }

    [Fact]
    public void ProgressShouldStartAtZeroAtBeginningOfCycle()
    {
        // Arrange: 攻击周期刚开始
        var lastUpdateTime = DateTime.UtcNow;
        var lastServerCurrentTime = 10.0;  // 服务器时间 10 秒
        var nextAttackAt = 12.0;            // 下次攻击在 12 秒
        var attackInterval = 2.0;           // 攻击间隔 2 秒
        var now = lastUpdateTime;           // 当前时间（刚更新）

        // Act
        var (progress, timeRemaining) = CalculateProgress(
            lastUpdateTime, lastServerCurrentTime, nextAttackAt, attackInterval, now);

        // Assert
        Assert.InRange(progress, 0.0, 0.1);  // 应该接近 0%
        Assert.InRange(timeRemaining, 1.9, 2.1);  // 应该剩余约 2 秒
    }

    [Fact]
    public void ProgressShouldBeFiftyPercentAtHalfway()
    {
        // Arrange: 攻击周期进行到一半
        var lastUpdateTime = DateTime.UtcNow;
        var lastServerCurrentTime = 10.0;
        var nextAttackAt = 12.0;
        var attackInterval = 2.0;
        var now = lastUpdateTime.AddSeconds(1.0);  // 客户端时间过去了 1 秒

        // Act
        var (progress, timeRemaining) = CalculateProgress(
            lastUpdateTime, lastServerCurrentTime, nextAttackAt, attackInterval, now);

        // Assert
        Assert.InRange(progress, 0.45, 0.55);  // 应该约 50%
        Assert.InRange(timeRemaining, 0.9, 1.1);  // 应该剩余约 1 秒
    }

    [Fact]
    public void ProgressShouldBeFullAtEndOfCycle()
    {
        // Arrange: 攻击周期即将结束
        var lastUpdateTime = DateTime.UtcNow;
        var lastServerCurrentTime = 10.0;
        var nextAttackAt = 12.0;
        var attackInterval = 2.0;
        var now = lastUpdateTime.AddSeconds(2.0);  // 客户端时间过去了 2 秒

        // Act
        var (progress, timeRemaining) = CalculateProgress(
            lastUpdateTime, lastServerCurrentTime, nextAttackAt, attackInterval, now);

        // Assert
        Assert.InRange(progress, 0.95, 1.0);  // 应该接近 100%
        Assert.InRange(timeRemaining, 0.0, 0.1);  // 应该剩余约 0 秒
    }

    [Fact]
    public void ProgressShouldClampAt100Percent()
    {
        // Arrange: 超过攻击时间（客户端时间快于服务器）
        var lastUpdateTime = DateTime.UtcNow;
        var lastServerCurrentTime = 10.0;
        var nextAttackAt = 12.0;
        var attackInterval = 2.0;
        var now = lastUpdateTime.AddSeconds(3.0);  // 客户端时间过去了 3 秒（超过间隔）

        // Act
        var (progress, timeRemaining) = CalculateProgress(
            lastUpdateTime, lastServerCurrentTime, nextAttackAt, attackInterval, now);

        // Assert
        Assert.Equal(1.0, progress);  // 应该被限制在 100%
        Assert.Equal(0.0, timeRemaining);  // 剩余时间应该是 0
    }

    [Fact]
    public void ProgressShouldResetAfterServerUpdate()
    {
        // Arrange: 模拟服务器返回新的攻击周期（刚重置）
        var lastUpdateTime = DateTime.UtcNow;
        var lastServerCurrentTime = 12.5;  // 服务器时间已经到了 12.5 秒（攻击已触发）
        var nextAttackAt = 14.5;            // 下次攻击在 14.5 秒（新周期）
        var attackInterval = 2.0;
        var now = lastUpdateTime;           // 刚收到服务器更新

        // Act
        var (progress, timeRemaining) = CalculateProgress(
            lastUpdateTime, lastServerCurrentTime, nextAttackAt, attackInterval, now);

        // Assert
        // 攻击起始时间 = 14.5 - 2.0 = 12.5
        // 当前服务器时间 = 12.5
        // 进度 = (12.5 - 12.5) / 2.0 = 0%（刚开始新周期）
        Assert.InRange(progress, 0.0, 0.1);  // 应该约 0%（新周期刚开始）
        Assert.InRange(timeRemaining, 1.9, 2.1);  // 应该剩余约 2 秒
    }

    [Fact]
    public void ProgressCalculationWithFastAttackSpeed()
    {
        // Arrange: 快速攻击（0.5 秒间隔）
        var lastUpdateTime = DateTime.UtcNow;
        var lastServerCurrentTime = 10.0;
        var nextAttackAt = 10.5;
        var attackInterval = 0.5;
        var now = lastUpdateTime.AddSeconds(0.25);  // 客户端时间过去了 0.25 秒

        // Act
        var (progress, timeRemaining) = CalculateProgress(
            lastUpdateTime, lastServerCurrentTime, nextAttackAt, attackInterval, now);

        // Assert
        Assert.InRange(progress, 0.45, 0.55);  // 应该约 50%
        Assert.InRange(timeRemaining, 0.2, 0.3);  // 应该剩余约 0.25 秒
    }

    [Fact]
    public void ProgressCalculationWithSlowAttackSpeed()
    {
        // Arrange: 缓慢攻击（5 秒间隔）
        var lastUpdateTime = DateTime.UtcNow;
        var lastServerCurrentTime = 10.0;
        var nextAttackAt = 15.0;
        var attackInterval = 5.0;
        var now = lastUpdateTime.AddSeconds(2.5);  // 客户端时间过去了 2.5 秒

        // Act
        var (progress, timeRemaining) = CalculateProgress(
            lastUpdateTime, lastServerCurrentTime, nextAttackAt, attackInterval, now);

        // Assert
        Assert.InRange(progress, 0.45, 0.55);  // 应该约 50%
        Assert.InRange(timeRemaining, 2.4, 2.6);  // 应该剩余约 2.5 秒
    }

    [Fact]
    public void ProgressShouldHandleVerySmallTimeInterval()
    {
        // Arrange: 测试精度，客户端时间仅过去 0.01 秒
        var lastUpdateTime = DateTime.UtcNow;
        var lastServerCurrentTime = 10.0;
        var nextAttackAt = 12.0;
        var attackInterval = 2.0;
        var now = lastUpdateTime.AddSeconds(0.01);

        // Act
        var (progress, timeRemaining) = CalculateProgress(
            lastUpdateTime, lastServerCurrentTime, nextAttackAt, attackInterval, now);

        // Assert
        Assert.InRange(progress, 0.0, 0.02);  // 应该约 0.5%
        Assert.InRange(timeRemaining, 1.98, 2.0);
    }

    [Fact]
    public void ProgressCalculationWithHasteEffect()
    {
        // Arrange: 模拟急速效果（攻击间隔从 2.0 秒减少到 1.5 秒）
        var lastUpdateTime = DateTime.UtcNow;
        var lastServerCurrentTime = 10.0;
        var nextAttackAt = 11.5;            // 下次攻击在 11.5 秒
        var attackInterval = 1.5;           // 急速后的间隔
        var now = lastUpdateTime.AddSeconds(0.75);  // 过去了 0.75 秒

        // Act
        var (progress, timeRemaining) = CalculateProgress(
            lastUpdateTime, lastServerCurrentTime, nextAttackAt, attackInterval, now);

        // Assert
        Assert.InRange(progress, 0.45, 0.55);  // 应该约 50%
        Assert.InRange(timeRemaining, 0.7, 0.8);  // 应该剩余约 0.75 秒
    }
}
