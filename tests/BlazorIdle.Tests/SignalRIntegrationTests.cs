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
    }

    [Fact]
    public void BattleNotificationService_IsAvailable_RespectsConfiguration()
    {
        // Arrange
        var hubContextMock = new Mock<IHubContext<BattleNotificationHub>>();
        var loggerMock = new Mock<ILogger<BattleNotificationService>>();
        var metrics = new BlazorIdle.Server.Services.SignalRMetrics();
        
        var enabledOptions = Options.Create(new SignalROptions { EnableSignalR = true });
        var disabledOptions = Options.Create(new SignalROptions { EnableSignalR = false });

        // Act
        var enabledService = new BattleNotificationService(hubContextMock.Object, loggerMock.Object, enabledOptions, metrics);
        var disabledService = new BattleNotificationService(hubContextMock.Object, loggerMock.Object, disabledOptions, metrics);

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
        var metrics = new BlazorIdle.Server.Services.SignalRMetrics();
        
        var service = new BattleNotificationService(hubContextMock.Object, loggerMock.Object, options, metrics);
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
        var metrics = new BlazorIdle.Server.Services.SignalRMetrics();
        
        var service = new BattleNotificationService(hubContextMock.Object, loggerMock.Object, options, metrics);
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
        var metrics = new BlazorIdle.Server.Services.SignalRMetrics();
        
        var service = new BattleNotificationService(hubContextMock.Object, loggerMock.Object, options, metrics);
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
        var metrics = new BlazorIdle.Server.Services.SignalRMetrics();
        
        var notificationService = new BattleNotificationService(hubContextMock.Object, loggerMock.Object, options, metrics);
        
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
        var metrics = new BlazorIdle.Server.Services.SignalRMetrics();
        
        var service = new BattleNotificationService(hubContextMock.Object, loggerMock.Object, options, metrics);
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
    public void SignalRMetrics_RecordsNotificationsSent()
    {
        // Arrange
        var metrics = new BlazorIdle.Server.Services.SignalRMetrics();

        // Act
        metrics.RecordNotificationSent(100);
        metrics.RecordNotificationSent(200);
        metrics.RecordNotificationSent(150);

        // Assert
        Assert.Equal(3, metrics.TotalNotificationsSent);
        Assert.True(metrics.AverageLatencyMs > 0);
        Assert.Equal(100, metrics.SuccessRate);
    }

    [Fact]
    public void SignalRMetrics_RecordsNotificationsFailed()
    {
        // Arrange
        var metrics = new BlazorIdle.Server.Services.SignalRMetrics();

        // Act
        metrics.RecordNotificationSent(100);
        metrics.RecordNotificationFailed();

        // Assert
        Assert.Equal(1, metrics.TotalNotificationsSent);
        Assert.Equal(1, metrics.TotalNotificationsFailed);
        Assert.Equal(50, metrics.SuccessRate);
    }

    [Fact]
    public void SignalRMetrics_RecordsNotificationsSkipped()
    {
        // Arrange
        var metrics = new BlazorIdle.Server.Services.SignalRMetrics();

        // Act
        metrics.RecordNotificationSkipped();
        metrics.RecordNotificationSkipped();

        // Assert
        Assert.Equal(2, metrics.TotalNotificationsSkipped);
    }

    [Fact]
    public void SignalRMetrics_CalculatesPercentiles()
    {
        // Arrange
        var metrics = new BlazorIdle.Server.Services.SignalRMetrics();

        // Act - 记录 100 个延迟值
        for (int i = 1; i <= 100; i++)
        {
            metrics.RecordNotificationSent(i);
        }

        // Assert
        Assert.True(metrics.P95LatencyMs >= 90);
        Assert.True(metrics.P99LatencyMs >= 98);
    }

    [Fact]
    public async Task BattleNotificationService_GetMetrics_ReturnsCorrectSummary()
    {
        // Arrange
        var clientProxyMock = new Mock<IClientProxy>();
        var groupManagerMock = new Mock<IHubClients>();
        groupManagerMock.Setup(x => x.Group(It.IsAny<string>())).Returns(clientProxyMock.Object);
        
        var hubContextMock = new Mock<IHubContext<BattleNotificationHub>>();
        hubContextMock.Setup(x => x.Clients).Returns(groupManagerMock.Object);
        
        var loggerMock = new Mock<ILogger<BattleNotificationService>>();
        var options = Options.Create(new SignalROptions { EnableSignalR = true });
        var metrics = new BlazorIdle.Server.Services.SignalRMetrics();
        
        var service = new BattleNotificationService(hubContextMock.Object, loggerMock.Object, options, metrics);
        var battleId = Guid.NewGuid();

        // Act
        await service.NotifyStateChangeAsync(battleId, "PlayerDeath");
        var summary = service.GetMetrics();

        // Assert
        Assert.Equal(1, summary.TotalSent);
        Assert.Equal(0, summary.TotalFailed);
        Assert.Equal(100, summary.SuccessRate);
    }

    [Fact]
    public void SignalROptionsValidator_ValidatesCorrectly()
    {
        // Arrange
        var validator = new BlazorIdle.Server.Config.SignalROptionsValidator();
        var validOptions = new SignalROptions
        {
            HubEndpoint = "/hubs/battle",
            EnableSignalR = true,
            MaxReconnectAttempts = 5,
            ReconnectBaseDelayMs = 1000,
            MaxReconnectDelayMs = 30000,
            ConnectionTimeoutSeconds = 30,
            KeepAliveIntervalSeconds = 15,
            ServerTimeoutSeconds = 30,
            Notification = new NotificationOptions(),
            Performance = new PerformanceOptions()
        };

        // Act
        var result = validator.Validate(null, validOptions);

        // Assert
        Assert.True(result.Succeeded);
    }

    [Fact]
    public void SignalROptionsValidator_FailsForInvalidOptions()
    {
        // Arrange
        var validator = new BlazorIdle.Server.Config.SignalROptionsValidator();
        var invalidOptions = new SignalROptions
        {
            HubEndpoint = "hubs/battle", // 缺少前导斜杠
            MaxReconnectAttempts = -1, // 负数
            ReconnectBaseDelayMs = 50, // 太小
            Notification = new NotificationOptions(),
            Performance = new PerformanceOptions()
        };

        // Act
        var result = validator.Validate(null, invalidOptions);

        // Assert
        Assert.True(result.Failed);
    }
}
