using Xunit;
using BlazorIdle.Server.Application.Abstractions;
using BlazorIdle.Server.Config;
using BlazorIdle.Server.Services;
using BlazorIdle.Server.Hubs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.SignalR;
using Moq;

namespace BlazorIdle.Tests;

/// <summary>
/// SignalR 集成测试
/// 验证 SignalR 基础架构的基本功能
/// </summary>
public sealed class SignalRIntegrationTests
{
    [Fact]
    public void SignalROptions_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var options = new SignalROptions();

        // Assert - 基础配置
        Assert.Equal("/hubs/battle", options.HubEndpoint);
        Assert.True(options.EnableSignalR);
        Assert.Equal(5, options.MaxReconnectAttempts);
        Assert.Equal(1000, options.ReconnectBaseDelayMs);
        Assert.Equal(30000, options.MaxReconnectDelayMs);
        Assert.False(options.EnableDetailedLogging);
        Assert.Equal(30, options.ConnectionTimeoutSeconds);
        Assert.Equal(15, options.KeepAliveIntervalSeconds);
        Assert.Equal(30, options.ServerTimeoutSeconds);
        
        // Assert - 通知配置
        Assert.NotNull(options.Notification);
        Assert.True(options.Notification.EnablePlayerDeathNotification);
        Assert.True(options.Notification.EnablePlayerReviveNotification);
        Assert.True(options.Notification.EnableEnemyKilledNotification);
        Assert.True(options.Notification.EnableTargetSwitchedNotification);
        Assert.False(options.Notification.EnableWaveSpawnNotification);
        Assert.False(options.Notification.EnableSkillCastNotification);
        Assert.False(options.Notification.EnableBuffChangeNotification);
        
        // Assert - 性能配置
        Assert.NotNull(options.Performance);
        Assert.False(options.Performance.EnableThrottling);
        Assert.Equal(1000, options.Performance.ThrottleWindowMs);
        Assert.False(options.Performance.EnableBatching);
        Assert.Equal(100, options.Performance.BatchDelayMs);
        Assert.False(options.Performance.AutoDegradeOnMobile);
        
        // Assert - 监控配置
        Assert.NotNull(options.Monitoring);
        Assert.False(options.Monitoring.EnableMetrics);
        Assert.Equal(60, options.Monitoring.MetricsIntervalSeconds);
        Assert.False(options.Monitoring.EnableConnectionTracking);
        Assert.False(options.Monitoring.EnableLatencyMeasurement);
        Assert.Equal(1000, options.Monitoring.SlowNotificationThresholdMs);
        
        // Assert - 新增配置
        Assert.Equal("battle_", options.BattleGroupPrefix);
        Assert.Equal(0, options.MaxConcurrentConnections);
        Assert.Equal(300, options.ConnectionIdleTimeoutSeconds);
    }

    [Fact]
    public void BattleNotificationService_IsAvailable_RespectsConfiguration()
    {
        // Arrange
        var hubContextMock = new Mock<IHubContext<BattleNotificationHub>>();
        var loggerMock = new Mock<ILogger<BattleNotificationService>>();
        
        var enabledOptions = Options.Create(new SignalROptions { EnableSignalR = true });
        var disabledOptions = Options.Create(new SignalROptions { EnableSignalR = false });

        // Act
        var enabledService = new BattleNotificationService(hubContextMock.Object, loggerMock.Object, enabledOptions);
        var disabledService = new BattleNotificationService(hubContextMock.Object, loggerMock.Object, disabledOptions);

        // Assert
        Assert.True(enabledService.IsAvailable);
        Assert.False(disabledService.IsAvailable);
    }

    [Fact]
    public async Task BattleNotificationService_NotifyStateChange_DoesNotThrow()
    {
        // Arrange
        var clientProxyMock = new Mock<IClientProxy>();
        var groupManagerMock = new Mock<IHubClients>();
        groupManagerMock.Setup(x => x.Group(It.IsAny<string>())).Returns(clientProxyMock.Object);
        
        var hubContextMock = new Mock<IHubContext<BattleNotificationHub>>();
        hubContextMock.Setup(x => x.Clients).Returns(groupManagerMock.Object);
        
        var loggerMock = new Mock<ILogger<BattleNotificationService>>();
        var options = Options.Create(new SignalROptions { EnableSignalR = true });
        
        var service = new BattleNotificationService(hubContextMock.Object, loggerMock.Object, options);
        var battleId = Guid.NewGuid();

        // Act & Assert - should not throw
        await service.NotifyStateChangeAsync(battleId, "PlayerDeath");
        
        // Verify SendAsync was called
        clientProxyMock.Verify(
            x => x.SendCoreAsync(
                "StateChanged",
                It.IsAny<object[]>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task BattleNotificationService_WithDisabledSignalR_DoesNotSendNotification()
    {
        // Arrange
        var clientProxyMock = new Mock<IClientProxy>();
        var groupManagerMock = new Mock<IHubClients>();
        groupManagerMock.Setup(x => x.Group(It.IsAny<string>())).Returns(clientProxyMock.Object);
        
        var hubContextMock = new Mock<IHubContext<BattleNotificationHub>>();
        hubContextMock.Setup(x => x.Clients).Returns(groupManagerMock.Object);
        
        var loggerMock = new Mock<ILogger<BattleNotificationService>>();
        var options = Options.Create(new SignalROptions { EnableSignalR = false });
        
        var service = new BattleNotificationService(hubContextMock.Object, loggerMock.Object, options);
        var battleId = Guid.NewGuid();

        // Act
        await service.NotifyStateChangeAsync(battleId, "PlayerDeath");

        // Assert - should not call SendAsync
        clientProxyMock.Verify(
            x => x.SendCoreAsync(
                It.IsAny<string>(),
                It.IsAny<object[]>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Theory]
    [InlineData("PlayerDeath")]
    [InlineData("EnemyKilled")]
    [InlineData("TargetSwitched")]
    [InlineData("PlayerRevive")]
    public async Task BattleNotificationService_SupportsAllEventTypes(string eventType)
    {
        // Arrange
        var clientProxyMock = new Mock<IClientProxy>();
        var groupManagerMock = new Mock<IHubClients>();
        groupManagerMock.Setup(x => x.Group(It.IsAny<string>())).Returns(clientProxyMock.Object);
        
        var hubContextMock = new Mock<IHubContext<BattleNotificationHub>>();
        hubContextMock.Setup(x => x.Clients).Returns(groupManagerMock.Object);
        
        var loggerMock = new Mock<ILogger<BattleNotificationService>>();
        var options = Options.Create(new SignalROptions { EnableSignalR = true });
        
        var service = new BattleNotificationService(hubContextMock.Object, loggerMock.Object, options);
        var battleId = Guid.NewGuid();

        // Act
        await service.NotifyStateChangeAsync(battleId, eventType);

        // Assert
        clientProxyMock.Verify(
            x => x.SendCoreAsync(
                "StateChanged",
                It.IsAny<object[]>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public void BattleContext_WithNotificationService_IsInjected()
    {
        // Arrange
        var clientProxyMock = new Mock<IClientProxy>();
        var groupManagerMock = new Mock<IHubClients>();
        groupManagerMock.Setup(x => x.Group(It.IsAny<string>())).Returns(clientProxyMock.Object);
        
        var hubContextMock = new Mock<IHubContext<BattleNotificationHub>>();
        hubContextMock.Setup(x => x.Clients).Returns(groupManagerMock.Object);
        
        var loggerMock = new Mock<ILogger<BattleNotificationService>>();
        var options = Options.Create(new SignalROptions { EnableSignalR = true });
        
        var notificationService = new BattleNotificationService(hubContextMock.Object, loggerMock.Object, options);
        
        // Act
        var context = new BlazorIdle.Server.Domain.Combat.BattleContext(
            battle: new BlazorIdle.Server.Domain.Combat.Battle { Id = Guid.NewGuid(), CharacterId = Guid.NewGuid() },
            clock: new BlazorWebGame.Domain.Combat.GameClock(),
            scheduler: new BlazorWebGame.Domain.Combat.EventScheduler(),
            collector: new BlazorIdle.Server.Domain.Combat.SegmentCollector(),
            professionModule: BlazorIdle.Server.Domain.Combat.Professions.ProfessionRegistry.Resolve(BlazorIdle.Shared.Models.Profession.Warrior),
            profession: BlazorIdle.Shared.Models.Profession.Warrior,
            rng: new BlazorIdle.Server.Domain.Combat.Rng.RngContext(12345),
            notificationService: notificationService
        );

        // Assert
        Assert.NotNull(context.NotificationService);
        Assert.True(context.NotificationService.IsAvailable);
    }

    [Fact]
    public async Task BattleNotificationService_WithDisabledEventType_DoesNotSendNotification()
    {
        // Arrange
        var clientProxyMock = new Mock<IClientProxy>();
        var groupManagerMock = new Mock<IHubClients>();
        groupManagerMock.Setup(x => x.Group(It.IsAny<string>())).Returns(clientProxyMock.Object);
        
        var hubContextMock = new Mock<IHubContext<BattleNotificationHub>>();
        hubContextMock.Setup(x => x.Clients).Returns(groupManagerMock.Object);
        
        var loggerMock = new Mock<ILogger<BattleNotificationService>>();
        var options = Options.Create(new SignalROptions 
        { 
            EnableSignalR = true,
            Notification = new NotificationOptions
            {
                EnablePlayerDeathNotification = false // 禁用玩家死亡通知
            }
        });
        
        var service = new BattleNotificationService(hubContextMock.Object, loggerMock.Object, options);
        var battleId = Guid.NewGuid();

        // Act
        await service.NotifyStateChangeAsync(battleId, "PlayerDeath");

        // Assert - 不应发送通知
        clientProxyMock.Verify(
            x => x.SendCoreAsync(
                It.IsAny<string>(),
                It.IsAny<object[]>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public void SignalROptions_SectionName_IsCorrect()
    {
        // Assert
        Assert.Equal("SignalR", SignalROptions.SectionName);
    }
    
    [Fact]
    public void SignalROptionsValidator_ValidOptions_Passes()
    {
        // Arrange
        var validator = new SignalROptionsValidator();
        var validOptions = new SignalROptions
        {
            HubEndpoint = "/hubs/battle",
            MaxReconnectAttempts = 5,
            ReconnectBaseDelayMs = 1000,
            MaxReconnectDelayMs = 30000,
            ConnectionTimeoutSeconds = 30,
            KeepAliveIntervalSeconds = 15,
            ServerTimeoutSeconds = 30,
            BattleGroupPrefix = "battle_",
            MaxConcurrentConnections = 100,
            ConnectionIdleTimeoutSeconds = 300
        };
        
        // Act
        var result = validator.Validate(null, validOptions);
        
        // Assert
        Assert.True(result.Succeeded);
    }
    
    [Theory]
    [InlineData("", "HubEndpoint cannot be null or empty")]
    [InlineData("hubs/battle", "HubEndpoint must start with '/'")]
    public void SignalROptionsValidator_InvalidHubEndpoint_Fails(string hubEndpoint, string expectedError)
    {
        // Arrange
        var validator = new SignalROptionsValidator();
        var options = new SignalROptions { HubEndpoint = hubEndpoint };
        
        // Act
        var result = validator.Validate(null, options);
        
        // Assert
        Assert.False(result.Succeeded);
        Assert.Contains(expectedError, result.FailureMessage);
    }
    
    [Fact]
    public void SignalROptionsValidator_InvalidDelays_Fails()
    {
        // Arrange
        var validator = new SignalROptionsValidator();
        var options = new SignalROptions
        {
            ReconnectBaseDelayMs = 50,  // Too small
            MaxReconnectDelayMs = 100   // Less than base
        };
        
        // Act
        var result = validator.Validate(null, options);
        
        // Assert
        Assert.False(result.Succeeded);
        Assert.Contains("ReconnectBaseDelayMs must be >= 100", result.FailureMessage);
    }
    
    [Fact]
    public void SignalROptionsValidator_EmptyBattleGroupPrefix_Fails()
    {
        // Arrange
        var validator = new SignalROptionsValidator();
        var options = new SignalROptions { BattleGroupPrefix = "" };
        
        // Act
        var result = validator.Validate(null, options);
        
        // Assert
        Assert.False(result.Succeeded);
        Assert.Contains("BattleGroupPrefix cannot be null or empty", result.FailureMessage);
    }
    
    [Fact]
    public async Task BattleGroupPrefix_IsConfigurable()
    {
        // Arrange
        var clientProxyMock = new Mock<IClientProxy>();
        var groupManagerMock = new Mock<IHubClients>();
        var capturedGroupName = "";
        groupManagerMock.Setup(x => x.Group(It.IsAny<string>()))
            .Callback<string>(name => capturedGroupName = name)
            .Returns(clientProxyMock.Object);
        
        var hubContextMock = new Mock<IHubContext<BattleNotificationHub>>();
        hubContextMock.Setup(x => x.Clients).Returns(groupManagerMock.Object);
        
        var loggerMock = new Mock<ILogger<BattleNotificationService>>();
        var options = Options.Create(new SignalROptions 
        { 
            EnableSignalR = true,
            BattleGroupPrefix = "custom_prefix_"
        });
        
        var service = new BattleNotificationService(hubContextMock.Object, loggerMock.Object, options);
        var battleId = Guid.Parse("12345678-1234-1234-1234-123456789012");
        
        // Act
        await service.NotifyStateChangeAsync(battleId, "TestEvent");
        
        // Assert
        Assert.Equal("custom_prefix_12345678-1234-1234-1234-123456789012", capturedGroupName);
    }
}
