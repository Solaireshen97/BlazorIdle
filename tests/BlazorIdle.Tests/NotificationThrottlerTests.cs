using Xunit;
using BlazorIdle.Server.Services;

namespace BlazorIdle.Tests;

/// <summary>
/// 通知节流器测试
/// </summary>
public sealed class NotificationThrottlerTests
{
    [Fact]
    public void Throttler_FirstCall_ReturnsTrue()
    {
        // Arrange
        var throttler = new NotificationThrottler(1000);
        var eventKey = "test_event";

        // Act
        var shouldSend = throttler.ShouldSend(eventKey);

        // Assert
        Assert.True(shouldSend);
    }

    [Fact]
    public void Throttler_SecondCallWithinWindow_ReturnsFalse()
    {
        // Arrange
        var throttler = new NotificationThrottler(1000);
        var eventKey = "test_event";

        // Act
        throttler.ShouldSend(eventKey); // 第一次调用
        var shouldSend = throttler.ShouldSend(eventKey); // 第二次调用（立即）

        // Assert
        Assert.False(shouldSend);
    }

    [Fact]
    public async Task Throttler_CallAfterWindow_ReturnsTrue()
    {
        // Arrange
        var throttler = new NotificationThrottler(100); // 100ms 窗口
        var eventKey = "test_event";

        // Act
        throttler.ShouldSend(eventKey); // 第一次调用
        await Task.Delay(150); // 等待超过窗口时间
        var shouldSend = throttler.ShouldSend(eventKey); // 第二次调用

        // Assert
        Assert.True(shouldSend);
    }

    [Fact]
    public void Throttler_DifferentEventKeys_DoNotInterfere()
    {
        // Arrange
        var throttler = new NotificationThrottler(1000);
        var eventKey1 = "event_1";
        var eventKey2 = "event_2";

        // Act
        throttler.ShouldSend(eventKey1); // 第一个事件
        var shouldSend2 = throttler.ShouldSend(eventKey2); // 第二个事件（不同的键）

        // Assert
        Assert.True(shouldSend2); // 不同事件不应被节流
    }

    [Fact]
    public void Throttler_GetSuppressedCount_TracksCorrectly()
    {
        // Arrange
        var throttler = new NotificationThrottler(1000);
        var eventKey = "test_event";

        // Act
        throttler.ShouldSend(eventKey); // 第一次
        throttler.ShouldSend(eventKey); // 被抑制
        throttler.ShouldSend(eventKey); // 被抑制
        throttler.ShouldSend(eventKey); // 被抑制

        // Assert
        Assert.Equal(3, throttler.GetSuppressedCount(eventKey));
    }

    [Fact]
    public async Task Throttler_SuppressedCountResets_AfterSuccessfulSend()
    {
        // Arrange
        var throttler = new NotificationThrottler(100);
        var eventKey = "test_event";

        // Act
        throttler.ShouldSend(eventKey); // 第一次
        throttler.ShouldSend(eventKey); // 被抑制
        throttler.ShouldSend(eventKey); // 被抑制
        
        await Task.Delay(150); // 等待超过窗口
        throttler.ShouldSend(eventKey); // 再次成功发送
        
        var count = throttler.GetSuppressedCount(eventKey);

        // Assert
        Assert.Equal(0, count); // 计数应该重置
    }

    [Fact]
    public void Throttler_CustomWindow_Works()
    {
        // Arrange
        var throttler = new NotificationThrottler(1000); // 默认 1000ms
        var eventKey = "test_event";
        var customWindow = TimeSpan.FromMilliseconds(500);

        // Act
        throttler.ShouldSend(eventKey, customWindow); // 第一次
        var shouldSend = throttler.ShouldSend(eventKey, customWindow); // 第二次（立即）

        // Assert
        Assert.False(shouldSend); // 应该被节流
    }

    [Fact]
    public void Throttler_GetStateCount_ReturnsCorrectCount()
    {
        // Arrange
        var throttler = new NotificationThrottler(1000);

        // Act
        throttler.ShouldSend("event_1");
        throttler.ShouldSend("event_2");
        throttler.ShouldSend("event_3");

        // Assert
        Assert.Equal(3, throttler.GetStateCount());
    }

    [Fact]
    public void Throttler_Clear_RemovesAllStates()
    {
        // Arrange
        var throttler = new NotificationThrottler(1000);
        throttler.ShouldSend("event_1");
        throttler.ShouldSend("event_2");

        // Act
        throttler.Clear();

        // Assert
        Assert.Equal(0, throttler.GetStateCount());
        Assert.True(throttler.ShouldSend("event_1")); // 清除后第一次调用应返回 true
    }

    [Fact]
    public async Task Throttler_CleanupExpiredStates_RemovesOldEntries()
    {
        // Arrange
        var throttler = new NotificationThrottler(100);
        throttler.ShouldSend("event_1");
        throttler.ShouldSend("event_2");

        // Act
        await Task.Delay(200); // 等待一段时间
        throttler.CleanupExpiredStates(0); // 清理 0 分钟（立即清理所有）

        // Assert
        Assert.Equal(0, throttler.GetStateCount());
    }

    [Fact]
    public async Task Throttler_ThreadSafety_MultipleThreads()
    {
        // Arrange
        var throttler = new NotificationThrottler(100);
        var eventKey = "test_event";
        var successCount = 0;
        var tasks = new List<Task>();

        // Act - 10 个并发任务尝试发送通知
        for (int i = 0; i < 10; i++)
        {
            tasks.Add(Task.Run(() =>
            {
                if (throttler.ShouldSend(eventKey))
                {
                    Interlocked.Increment(ref successCount);
                }
            }));
        }

        await Task.WhenAll(tasks);

        // Assert - 应该只有一个成功
        Assert.Equal(1, successCount);
    }
}
