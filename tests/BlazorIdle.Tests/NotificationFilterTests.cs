using Xunit;
using BlazorIdle.Server.Application.Abstractions;
using BlazorIdle.Server.Services;
using BlazorIdle.Server.Services.Filters;
using BlazorIdle.Server.Config;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using Moq;

namespace BlazorIdle.Tests;

/// <summary>
/// 通知过滤器测试
/// </summary>
public sealed class NotificationFilterTests
{
    [Fact]
    public void EventTypeFilter_WithEnabledEventType_ReturnsTrue()
    {
        // Arrange
        var options = Options.Create(new SignalROptions
        {
            Notification = new NotificationOptions
            {
                EnablePlayerDeathNotification = true
            }
        });
        var filter = new EventTypeFilter(options);
        var context = new NotificationFilterContext
        {
            BattleId = Guid.NewGuid(),
            EventType = "PlayerDeath"
        };

        // Act
        var result = filter.ShouldNotify(context);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void EventTypeFilter_WithDisabledEventType_ReturnsFalse()
    {
        // Arrange
        var options = Options.Create(new SignalROptions
        {
            Notification = new NotificationOptions
            {
                EnablePlayerDeathNotification = false
            }
        });
        var filter = new EventTypeFilter(options);
        var context = new NotificationFilterContext
        {
            BattleId = Guid.NewGuid(),
            EventType = "PlayerDeath"
        };

        // Act
        var result = filter.ShouldNotify(context);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void EventTypeFilter_WithUnknownEventType_ReturnsTrue()
    {
        // Arrange
        var options = Options.Create(new SignalROptions());
        var filter = new EventTypeFilter(options);
        var context = new NotificationFilterContext
        {
            BattleId = Guid.NewGuid(),
            EventType = "UnknownEvent"
        };

        // Act
        var result = filter.ShouldNotify(context);

        // Assert
        Assert.True(result); // 未知类型默认允许
    }

    [Fact]
    public void RateLimitFilter_WithThrottlingDisabled_ReturnsTrue()
    {
        // Arrange
        var options = Options.Create(new SignalROptions
        {
            Performance = new PerformanceOptions
            {
                EnableThrottling = false
            }
        });
        var filter = new RateLimitFilter(options);
        var context = new NotificationFilterContext
        {
            BattleId = Guid.NewGuid(),
            EventType = "EnemyKilled"
        };

        // Act
        var result1 = filter.ShouldNotify(context);
        var result2 = filter.ShouldNotify(context);

        // Assert
        Assert.True(result1);
        Assert.True(result2); // 未启用节流，都应返回 true
    }

    [Fact]
    public void RateLimitFilter_WithThrottlingEnabled_ThrottlesRepeatedCalls()
    {
        // Arrange
        var options = Options.Create(new SignalROptions
        {
            Performance = new PerformanceOptions
            {
                EnableThrottling = true,
                ThrottleWindowMs = 1000
            }
        });
        var filter = new RateLimitFilter(options);
        var context = new NotificationFilterContext
        {
            BattleId = Guid.NewGuid(),
            EventType = "EnemyKilled"
        };

        // Act
        var result1 = filter.ShouldNotify(context);
        var result2 = filter.ShouldNotify(context);

        // Assert
        Assert.True(result1); // 第一次应该通过
        Assert.False(result2); // 第二次应该被节流
    }

    [Fact]
    public void RateLimitFilter_AddsSuppressedCountToMetadata()
    {
        // Arrange
        var options = Options.Create(new SignalROptions
        {
            Performance = new PerformanceOptions
            {
                EnableThrottling = true,
                ThrottleWindowMs = 1000
            }
        });
        var filter = new RateLimitFilter(options);
        var context = new NotificationFilterContext
        {
            BattleId = Guid.NewGuid(),
            EventType = "EnemyKilled"
        };

        // Act
        filter.ShouldNotify(context); // 第一次
        filter.ShouldNotify(context); // 被抑制
        filter.ShouldNotify(context); // 被抑制

        var suppressedCount = context.GetMetadata<int>("SuppressedCount");

        // Assert
        Assert.True(suppressedCount > 0);
    }

    [Fact]
    public void NotificationFilterPipeline_WithMultipleFilters_ExecutesInOrder()
    {
        // Arrange
        var options = Options.Create(new SignalROptions
        {
            Notification = new NotificationOptions
            {
                EnablePlayerDeathNotification = true
            },
            Performance = new PerformanceOptions
            {
                EnableThrottling = false
            }
        });

        var filters = new List<INotificationFilter>
        {
            new RateLimitFilter(options), // Priority 20
            new EventTypeFilter(options)  // Priority 10
        };

        var loggerMock = new Mock<ILogger<NotificationFilterPipeline>>();
        var pipeline = new NotificationFilterPipeline(filters, loggerMock.Object);

        var context = new NotificationFilterContext
        {
            BattleId = Guid.NewGuid(),
            EventType = "PlayerDeath"
        };

        // Act
        var result = pipeline.Execute(context);

        // Assert
        Assert.True(result); // 所有过滤器都通过
        Assert.Equal(2, pipeline.FilterCount);
    }

    [Fact]
    public void NotificationFilterPipeline_WithBlockingFilter_ReturnsFalse()
    {
        // Arrange
        var options = Options.Create(new SignalROptions
        {
            Notification = new NotificationOptions
            {
                EnablePlayerDeathNotification = false // 禁用事件
            }
        });

        var filters = new List<INotificationFilter>
        {
            new EventTypeFilter(options)
        };

        var loggerMock = new Mock<ILogger<NotificationFilterPipeline>>();
        var pipeline = new NotificationFilterPipeline(filters, loggerMock.Object);

        var context = new NotificationFilterContext
        {
            BattleId = Guid.NewGuid(),
            EventType = "PlayerDeath"
        };

        // Act
        var result = pipeline.Execute(context);

        // Assert
        Assert.False(result); // 被 EventTypeFilter 阻止
    }

    [Fact]
    public void NotificationFilterContext_AddAndGetMetadata_Works()
    {
        // Arrange
        var context = new NotificationFilterContext();

        // Act
        context.AddMetadata("TestKey", 42);
        context.AddMetadata("TestString", "value");
        
        var intValue = context.GetMetadata<int>("TestKey");
        var stringValue = context.GetMetadata<string>("TestString");
        var missingValue = context.GetMetadata<int>("MissingKey");

        // Assert
        Assert.Equal(42, intValue);
        Assert.Equal("value", stringValue);
        Assert.Equal(0, missingValue); // 默认值
    }

    [Fact]
    public void NotificationFilterPipeline_GetFilterNames_ReturnsAllNames()
    {
        // Arrange
        var options = Options.Create(new SignalROptions());
        var filters = new List<INotificationFilter>
        {
            new EventTypeFilter(options),
            new RateLimitFilter(options)
        };

        var loggerMock = new Mock<ILogger<NotificationFilterPipeline>>();
        var pipeline = new NotificationFilterPipeline(filters, loggerMock.Object);

        // Act
        var names = pipeline.GetFilterNames().ToList();

        // Assert
        Assert.Equal(2, names.Count);
        Assert.Contains("EventTypeFilter", names);
        Assert.Contains("RateLimitFilter", names);
    }
}
