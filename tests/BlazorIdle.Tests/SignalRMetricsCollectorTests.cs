using BlazorIdle.Server.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace BlazorIdle.Tests;

/// <summary>
/// SignalR 指标收集器测试
/// </summary>
public class SignalRMetricsCollectorTests
{
    private readonly Mock<ILogger<SignalRMetricsCollector>> _loggerMock;

    public SignalRMetricsCollectorTests()
    {
        _loggerMock = new Mock<ILogger<SignalRMetricsCollector>>();
    }

    [Fact]
    public void RecordNotificationSent_WithoutThrottle_IncrementsCount()
    {
        // Arrange
        var collector = new SignalRMetricsCollector(_loggerMock.Object);

        // Act
        collector.RecordNotificationSent("PlayerDeath", throttled: false);
        var metrics = collector.GetEventMetrics("PlayerDeath");

        // Assert
        Assert.NotNull(metrics);
        Assert.Equal(1, metrics.SentCount);
        Assert.Equal(0, metrics.ThrottledCount);
        Assert.Equal(1, metrics.TotalAttempts);
    }

    [Fact]
    public void RecordNotificationSent_WithThrottle_IncrementsThrottledCount()
    {
        // Arrange
        var collector = new SignalRMetricsCollector(_loggerMock.Object);

        // Act
        collector.RecordNotificationSent("PlayerDeath", throttled: true);
        var metrics = collector.GetEventMetrics("PlayerDeath");

        // Assert
        Assert.NotNull(metrics);
        Assert.Equal(0, metrics.SentCount);
        Assert.Equal(1, metrics.ThrottledCount);
        Assert.Equal(1, metrics.TotalAttempts);
    }

    [Fact]
    public void RecordNotificationFailed_IncrementsFailedCount()
    {
        // Arrange
        var collector = new SignalRMetricsCollector(_loggerMock.Object);
        var exception = new Exception("Test error");

        // Act
        collector.RecordNotificationFailed("PlayerDeath", exception);
        var metrics = collector.GetEventMetrics("PlayerDeath");

        // Assert
        Assert.NotNull(metrics);
        Assert.Equal(1, metrics.FailedCount);
        Assert.Equal("Test error", metrics.LastError);
        Assert.NotNull(metrics.LastErrorTime);
    }

    [Fact]
    public void RecordNotificationSent_MultipleEvents_TracksEachSeparately()
    {
        // Arrange
        var collector = new SignalRMetricsCollector(_loggerMock.Object);

        // Act
        collector.RecordNotificationSent("PlayerDeath", throttled: false);
        collector.RecordNotificationSent("EnemyKilled", throttled: false);
        collector.RecordNotificationSent("PlayerDeath", throttled: false);

        // Assert
        var deathMetrics = collector.GetEventMetrics("PlayerDeath");
        var killedMetrics = collector.GetEventMetrics("EnemyKilled");
        
        Assert.Equal(2, deathMetrics!.SentCount);
        Assert.Equal(1, killedMetrics!.SentCount);
    }

    [Fact]
    public void IncrementCounter_AddsToCounter()
    {
        // Arrange
        var collector = new SignalRMetricsCollector(_loggerMock.Object);

        // Act
        collector.IncrementCounter("test_counter", 5);
        collector.IncrementCounter("test_counter", 3);

        // Assert
        Assert.Equal(8, collector.GetCounter("test_counter"));
    }

    [Fact]
    public void GetCounter_WithNonExistentCounter_ReturnsZero()
    {
        // Arrange
        var collector = new SignalRMetricsCollector(_loggerMock.Object);

        // Act
        var value = collector.GetCounter("non_existent");

        // Assert
        Assert.Equal(0, value);
    }

    [Fact]
    public void GetEventMetrics_WithNonExistentEvent_ReturnsNull()
    {
        // Arrange
        var collector = new SignalRMetricsCollector(_loggerMock.Object);

        // Act
        var metrics = collector.GetEventMetrics("NonExistent");

        // Assert
        Assert.Null(metrics);
    }

    [Fact]
    public void GetSummary_CalculatesTotalsCorrectly()
    {
        // Arrange
        var collector = new SignalRMetricsCollector(_loggerMock.Object);

        // Act
        collector.RecordNotificationSent("PlayerDeath", throttled: false);
        collector.RecordNotificationSent("PlayerDeath", throttled: true);
        collector.RecordNotificationSent("EnemyKilled", throttled: false);
        collector.RecordNotificationFailed("TargetSwitched");

        var summary = collector.GetSummary();

        // Assert
        Assert.Equal(2, summary.TotalNotificationsSent);
        Assert.Equal(1, summary.TotalNotificationsThrottled);
        Assert.Equal(1, summary.TotalNotificationsFailed);
        // RecordNotificationFailed doesn't increment TotalAttempts, only RecordNotificationSent does
        Assert.Equal(3, summary.TotalAttempts);
    }

    [Fact]
    public void GetSummary_CalculatesRatesCorrectly()
    {
        // Arrange
        var collector = new SignalRMetricsCollector(_loggerMock.Object);

        // Act
        collector.RecordNotificationSent("PlayerDeath", throttled: false);
        collector.RecordNotificationSent("PlayerDeath", throttled: true);
        collector.RecordNotificationSent("PlayerDeath", throttled: false);
        collector.RecordNotificationSent("PlayerDeath", throttled: true);

        var summary = collector.GetSummary();

        // Assert
        Assert.Equal(0.5, summary.ThrottleRate);
        Assert.Equal(0.0, summary.FailureRate);
    }

    [Fact]
    public void GetSummary_WithNoData_ReturnsZeroRates()
    {
        // Arrange
        var collector = new SignalRMetricsCollector(_loggerMock.Object);

        // Act
        var summary = collector.GetSummary();

        // Assert
        Assert.Equal(0.0, summary.ThrottleRate);
        Assert.Equal(0.0, summary.FailureRate);
    }

    [Fact]
    public void Reset_ClearsAllMetrics()
    {
        // Arrange
        var collector = new SignalRMetricsCollector(_loggerMock.Object);
        collector.RecordNotificationSent("PlayerDeath", throttled: false);
        collector.IncrementCounter("test_counter", 10);

        // Act
        collector.Reset();

        // Assert
        var metrics = collector.GetEventMetrics("PlayerDeath");
        var counter = collector.GetCounter("test_counter");
        
        Assert.Null(metrics);
        Assert.Equal(0, counter);
    }

    [Fact]
    public void LogMetrics_DoesNotThrow()
    {
        // Arrange
        var collector = new SignalRMetricsCollector(_loggerMock.Object);
        collector.RecordNotificationSent("PlayerDeath", throttled: false);

        // Act & Assert
        var exception = Record.Exception(() => collector.LogMetrics());
        Assert.Null(exception);
    }

    [Fact]
    public void EventMetrics_Clone_CreatesIndependentCopy()
    {
        // Arrange
        var collector = new SignalRMetricsCollector(_loggerMock.Object);
        collector.RecordNotificationSent("PlayerDeath", throttled: false);
        
        var original = collector.GetEventMetrics("PlayerDeath");
        var clone = original!.Clone();

        // Act
        collector.RecordNotificationSent("PlayerDeath", throttled: false);

        // Assert
        Assert.Equal(1, clone.SentCount); // Clone should not change
        Assert.Equal(2, original.SentCount); // Original should change
    }

    [Fact]
    public void GetSummary_IncludesCollectionDuration()
    {
        // Arrange
        var collector = new SignalRMetricsCollector(_loggerMock.Object);
        Thread.Sleep(100); // Wait a bit

        // Act
        var summary = collector.GetSummary();

        // Assert
        Assert.True(summary.CollectionDuration.TotalMilliseconds >= 100);
    }
}
