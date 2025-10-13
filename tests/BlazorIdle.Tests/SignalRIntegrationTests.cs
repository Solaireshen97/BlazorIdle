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
        Assert.False(options.EnableDetailedLogging);
        Assert.Equal(30, options.ConnectionTimeoutSeconds);
        Assert.Equal(15, options.KeepAliveIntervalSeconds);
        Assert.Equal(30, options.ServerTimeoutSeconds);
        
        // Assert - Phase 2.5 增强配置
        Assert.Equal("battle_", options.GroupNamePrefix);
        Assert.NotNull(options.Methods);
        Assert.Equal("StateChanged", options.Methods.StateChanged);
        Assert.Equal("BattleEvent", options.Methods.BattleEvent);
        
        Assert.NotNull(options.Throttling);
        Assert.False(options.Throttling.EnableThrottling);
        Assert.Equal(100, options.Throttling.MinNotificationIntervalMs);
        Assert.Equal(500, options.Throttling.MaxBatchDelayMs);
        Assert.Equal(10, options.Throttling.MaxEventsPerBatch);
        
        Assert.NotNull(options.Monitoring);
        Assert.False(options.Monitoring.EnableMetrics);
        Assert.True(options.Monitoring.LogConnectionEvents);
        Assert.False(options.Monitoring.LogNotificationDetails);
        Assert.Equal(1000, options.Monitoring.SlowNotificationThresholdMs);
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
    public async Task BattleNotificationService_UsesCustomGroupNamePrefix()
    {
        // Arrange
        var customPrefix = "custom_battle_";
        var battleId = Guid.NewGuid();
        var expectedGroupName = $"{customPrefix}{battleId}";
        
        var clientProxyMock = new Mock<IClientProxy>();
        var groupManagerMock = new Mock<IHubClients>();
        groupManagerMock.Setup(x => x.Group(expectedGroupName)).Returns(clientProxyMock.Object);
        
        var hubContextMock = new Mock<IHubContext<BattleNotificationHub>>();
        hubContextMock.Setup(x => x.Clients).Returns(groupManagerMock.Object);
        
        var loggerMock = new Mock<ILogger<BattleNotificationService>>();
        var options = Options.Create(new SignalROptions 
        { 
            EnableSignalR = true,
            GroupNamePrefix = customPrefix
        });
        
        var service = new BattleNotificationService(hubContextMock.Object, loggerMock.Object, options);

        // Act
        await service.NotifyStateChangeAsync(battleId, "PlayerDeath");

        // Assert - 验证使用了正确的组名
        groupManagerMock.Verify(x => x.Group(expectedGroupName), Times.Once);
    }
    
    [Fact]
    public async Task BattleNotificationService_UsesCustomMethodNames()
    {
        // Arrange
        var customMethodName = "CustomStateChanged";
        var battleId = Guid.NewGuid();
        
        var clientProxyMock = new Mock<IClientProxy>();
        var groupManagerMock = new Mock<IHubClients>();
        groupManagerMock.Setup(x => x.Group(It.IsAny<string>())).Returns(clientProxyMock.Object);
        
        var hubContextMock = new Mock<IHubContext<BattleNotificationHub>>();
        hubContextMock.Setup(x => x.Clients).Returns(groupManagerMock.Object);
        
        var loggerMock = new Mock<ILogger<BattleNotificationService>>();
        var options = Options.Create(new SignalROptions 
        { 
            EnableSignalR = true,
            Methods = new MethodNames { StateChanged = customMethodName }
        });
        
        var service = new BattleNotificationService(hubContextMock.Object, loggerMock.Object, options);

        // Act
        await service.NotifyStateChangeAsync(battleId, "PlayerDeath");

        // Assert - 验证使用了自定义方法名
        clientProxyMock.Verify(
            x => x.SendCoreAsync(
                customMethodName,
                It.IsAny<object[]>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
