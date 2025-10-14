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
    public async Task BattleNotificationService_WithThrottlingEnabled_SuppressesFrequentNotifications()
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
            Performance = new PerformanceOptions
            {
                EnableThrottling = true,
                ThrottleWindowMs = 1000
            }
        });
        
        var service = new BattleNotificationService(hubContextMock.Object, loggerMock.Object, options);
        var battleId = Guid.NewGuid();

        // Act - 快速发送 5 次相同通知
        await service.NotifyStateChangeAsync(battleId, "EnemyKilled");
        await service.NotifyStateChangeAsync(battleId, "EnemyKilled");
        await service.NotifyStateChangeAsync(battleId, "EnemyKilled");
        await service.NotifyStateChangeAsync(battleId, "EnemyKilled");
        await service.NotifyStateChangeAsync(battleId, "EnemyKilled");

        // Assert - 由于节流，应该只发送一次
        clientProxyMock.Verify(
            x => x.SendCoreAsync(
                "StateChanged",
                It.IsAny<object[]>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task BattleNotificationService_WithThrottlingDisabled_SendsAllNotifications()
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
            Performance = new PerformanceOptions
            {
                EnableThrottling = false
            }
        });
        
        var service = new BattleNotificationService(hubContextMock.Object, loggerMock.Object, options);
        var battleId = Guid.NewGuid();

        // Act - 快速发送 3 次通知
        await service.NotifyStateChangeAsync(battleId, "EnemyKilled");
        await service.NotifyStateChangeAsync(battleId, "EnemyKilled");
        await service.NotifyStateChangeAsync(battleId, "EnemyKilled");

        // Assert - 没有节流，应该发送 3 次
        clientProxyMock.Verify(
            x => x.SendCoreAsync(
                "StateChanged",
                It.IsAny<object[]>(),
                It.IsAny<CancellationToken>()),
            Times.Exactly(3));
    }

    [Fact]
    public async Task BattleNotificationService_NotifyEventAsync_WithEnabledDamageApplied_SendsEvent()
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
                EnableDamageAppliedNotification = true // 启用伤害应用通知
            }
        });
        
        var service = new BattleNotificationService(hubContextMock.Object, loggerMock.Object, options);
        var battleId = Guid.NewGuid();

        var damageEvent = new BlazorIdle.Shared.Models.DamageAppliedEventDto
        {
            BattleId = battleId,
            EventType = "DamageApplied",
            Source = "basic_attack",
            Damage = 100,
            IsCrit = false,
            AttackerName = "Player",
            TargetName = "Enemy",
            Message = "Player dealt 100 damage to Enemy"
        };

        // Act
        await service.NotifyEventAsync(battleId, damageEvent);

        // Assert - 应该发送事件
        clientProxyMock.Verify(
            x => x.SendCoreAsync(
                "BattleEvent",
                It.IsAny<object[]>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task BattleNotificationService_NotifyEventAsync_WithDisabledDamageApplied_DoesNotSendEvent()
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
                EnableDamageAppliedNotification = false // 禁用伤害应用通知
            }
        });
        
        var service = new BattleNotificationService(hubContextMock.Object, loggerMock.Object, options);
        var battleId = Guid.NewGuid();

        var damageEvent = new BlazorIdle.Shared.Models.DamageAppliedEventDto
        {
            BattleId = battleId,
            EventType = "DamageApplied",
            Source = "basic_attack",
            Damage = 100,
            IsCrit = false,
            AttackerName = "Player",
            TargetName = "Enemy",
            Message = "Player dealt 100 damage to Enemy"
        };

        // Act
        await service.NotifyEventAsync(battleId, damageEvent);

        // Assert - 不应发送事件
        clientProxyMock.Verify(
            x => x.SendCoreAsync(
                It.IsAny<string>(),
                It.IsAny<object[]>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task BattleNotificationService_NotifyEventAsync_WithNonBattleEventDto_SendsEvent()
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
            EnableSignalR = true
        });
        
        var service = new BattleNotificationService(hubContextMock.Object, loggerMock.Object, options);
        var battleId = Guid.NewGuid();

        var customEvent = new { Message = "Custom event" };

        // Act
        await service.NotifyEventAsync(battleId, customEvent);

        // Assert - 应该发送事件（不会被过滤）
        clientProxyMock.Verify(
            x => x.SendCoreAsync(
                "BattleEvent",
                It.IsAny<object[]>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
