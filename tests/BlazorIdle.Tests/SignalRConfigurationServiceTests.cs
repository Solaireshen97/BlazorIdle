using BlazorIdle.Server.Config;
using BlazorIdle.Server.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace BlazorIdle.Tests;

/// <summary>
/// SignalR 配置服务测试
/// </summary>
public class SignalRConfigurationServiceTests
{
    private readonly Mock<ILogger<SignalRConfigurationService>> _loggerMock;

    public SignalRConfigurationServiceTests()
    {
        _loggerMock = new Mock<ILogger<SignalRConfigurationService>>();
    }

    [Fact]
    public void Constructor_WithValidOptions_Initializes()
    {
        // Arrange
        var options = CreateDefaultOptions();

        // Act
        var service = new SignalRConfigurationService(options, _loggerMock.Object);

        // Assert
        Assert.NotNull(service);
        Assert.NotNull(service.Options);
    }

    [Fact]
    public void Options_WhenAccessed_IncrementsAccessCount()
    {
        // Arrange
        var options = CreateDefaultOptions();
        var service = new SignalRConfigurationService(options, _loggerMock.Object);

        // Act
        var stats1 = service.GetStatistics();
        _ = service.Options;
        _ = service.Options;
        var stats2 = service.GetStatistics();

        // Assert
        Assert.Equal(0, stats1.AccessCount);
        Assert.Equal(2, stats2.AccessCount);
    }

    [Fact]
    public void ValidateConfiguration_WithValidOptions_ReturnsSuccess()
    {
        // Arrange
        var options = CreateDefaultOptions();
        var service = new SignalRConfigurationService(options, _loggerMock.Object);

        // Act
        var result = service.ValidateConfiguration();

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void ValidateConfiguration_WithInvalidOptions_ReturnsFalse()
    {
        // Arrange
        var options = Options.Create(new SignalROptions
        {
            HubEndpoint = "", // Invalid
            MaxReconnectAttempts = -1 // Invalid
        });
        var service = new SignalRConfigurationService(options, _loggerMock.Object);

        // Act
        var result = service.ValidateConfiguration();

        // Assert
        Assert.False(result.IsValid);
        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public void IsEventTypeEnabled_WithPlayerDeath_ReturnsCorrectValue()
    {
        // Arrange
        var options = CreateDefaultOptions();
        var service = new SignalRConfigurationService(options, _loggerMock.Object);

        // Act
        var enabled = service.IsEventTypeEnabled("PlayerDeath");

        // Assert
        Assert.True(enabled);
    }

    [Fact]
    public void IsEventTypeEnabled_WithDisabledType_ReturnsFalse()
    {
        // Arrange
        var options = CreateDefaultOptions();
        options.Value.Notification.EnableWaveSpawnNotification = false;
        var service = new SignalRConfigurationService(options, _loggerMock.Object);

        // Act
        var enabled = service.IsEventTypeEnabled("WaveSpawn");

        // Assert
        Assert.False(enabled);
    }

    [Fact]
    public void IsEventTypeEnabled_WithUnknownType_ReturnsTrue()
    {
        // Arrange
        var options = CreateDefaultOptions();
        var service = new SignalRConfigurationService(options, _loggerMock.Object);

        // Act
        var enabled = service.IsEventTypeEnabled("UnknownEventType");

        // Assert
        Assert.True(enabled); // 默认启用未知类型
    }

    [Fact]
    public void GetStatistics_ReturnsValidStats()
    {
        // Arrange
        var options = CreateDefaultOptions();
        var service = new SignalRConfigurationService(options, _loggerMock.Object);

        // Act
        var stats = service.GetStatistics();

        // Assert
        Assert.NotNull(stats);
        Assert.True(stats.IsSignalREnabled);
        Assert.NotEmpty(stats.EnabledNotificationTypes);
        Assert.Contains("PlayerDeath", stats.EnabledNotificationTypes);
    }

    [Fact]
    public void GetStatistics_WithThrottlingEnabled_ReflectsInStats()
    {
        // Arrange
        var options = CreateDefaultOptions();
        options.Value.Performance.EnableThrottling = true;
        var service = new SignalRConfigurationService(options, _loggerMock.Object);

        // Act
        var stats = service.GetStatistics();

        // Assert
        Assert.True(stats.IsThrottlingEnabled);
    }

    [Fact]
    public void LogConfigurationUsage_DoesNotThrow()
    {
        // Arrange
        var options = CreateDefaultOptions();
        var service = new SignalRConfigurationService(options, _loggerMock.Object);

        // Act & Assert
        var exception = Record.Exception(() => service.LogConfigurationUsage());
        Assert.Null(exception);
    }

    private IOptions<SignalROptions> CreateDefaultOptions()
    {
        return Options.Create(new SignalROptions
        {
            HubEndpoint = "/hubs/battle",
            EnableSignalR = true,
            MaxReconnectAttempts = 5,
            ReconnectBaseDelayMs = 1000,
            MaxReconnectDelayMs = 30000,
            EnableDetailedLogging = false,
            ConnectionTimeoutSeconds = 30,
            KeepAliveIntervalSeconds = 15,
            ServerTimeoutSeconds = 30,
            Notification = new NotificationOptions
            {
                EnablePlayerDeathNotification = true,
                EnablePlayerReviveNotification = true,
                EnableEnemyKilledNotification = true,
                EnableTargetSwitchedNotification = true,
                EnableWaveSpawnNotification = false,
                EnableSkillCastNotification = false,
                EnableBuffChangeNotification = false
            },
            Performance = new PerformanceOptions
            {
                EnableThrottling = false,
                ThrottleWindowMs = 1000,
                EnableBatching = false,
                BatchDelayMs = 100,
                AutoDegradeOnMobile = false
            }
        });
    }
}
