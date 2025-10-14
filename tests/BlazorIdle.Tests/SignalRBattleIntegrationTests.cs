using Xunit;
using BlazorIdle.Server.Application.Abstractions;
using BlazorIdle.Server.Config;
using BlazorIdle.Server.Domain.Combat;
using BlazorIdle.Server.Domain.Combat.Combatants;
using BlazorIdle.Server.Domain.Combat.Damage;
using BlazorIdle.Server.Domain.Combat.Enemies;
using BlazorIdle.Server.Domain.Combat.Professions;
using BlazorIdle.Server.Domain.Combat.Rng;
using BlazorIdle.Server.Domain.Combat.Skills;
using BlazorIdle.Server.Hubs;
using BlazorIdle.Server.Services;
using BlazorIdle.Shared.Models;
using BlazorWebGame.Domain.Combat;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace BlazorIdle.Tests;

/// <summary>
/// SignalR 战斗集成测试
/// 验证 SignalR 通知在实际战斗场景中的工作情况
/// </summary>
public sealed class SignalRBattleIntegrationTests
{
    /// <summary>
    /// 测试玩家死亡时是否发送 SignalR 通知
    /// </summary>
    [Fact]
    public void PlayerDeathEvent_SendsSignalRNotification()
    {
        // Arrange
        var battleId = Guid.NewGuid();
        var characterId = Guid.NewGuid();
        
        // Mock SignalR Hub Context
        var clientProxyMock = new Mock<IClientProxy>();
        var groupManagerMock = new Mock<IHubClients>();
        groupManagerMock.Setup(x => x.Group($"battle_{battleId}")).Returns(clientProxyMock.Object);
        
        var hubContextMock = new Mock<IHubContext<BattleNotificationHub>>();
        hubContextMock.Setup(x => x.Clients).Returns(groupManagerMock.Object);
        
        var loggerMock = new Mock<ILogger<BattleNotificationService>>();
        var options = Options.Create(new SignalROptions 
        { 
            EnableSignalR = true,
            Notification = new NotificationOptions
            {
                EnablePlayerDeathNotification = true
            }
        });
        
        var notificationService = new BattleNotificationService(hubContextMock.Object, loggerMock.Object, options);
        
        // Create battle context
        var battle = new Battle { Id = battleId, CharacterId = characterId };
        var clock = new GameClock();
        var scheduler = new EventScheduler();
        var collector = new SegmentCollector();
        var professionModule = ProfessionRegistry.Resolve(Profession.Warrior);
        var rng = new RngContext(12345);
        
        var context = new BattleContext(
            battle: battle,
            clock: clock,
            scheduler: scheduler,
            collector: collector,
            professionModule: professionModule,
            profession: Profession.Warrior,
            rng: rng,
            notificationService: notificationService
        );
        
        // Set player to dead state
        context.Player.ReceiveDamage(context.Player.CurrentHp, DamageType.Physical, 0.0);
        
        // Act - Execute player death event
        var deathEvent = new PlayerDeathEvent(1.0);
        deathEvent.Execute(context);
        
        // Assert - Verify notification was sent
        clientProxyMock.Verify(
            x => x.SendCoreAsync(
                "StateChanged",
                It.Is<object[]>(args => args.Length > 0),
                It.IsAny<CancellationToken>()),
            Times.Once,
            "PlayerDeath 事件应该触发 SignalR 通知");
    }

    /// <summary>
    /// 测试玩家复活时是否发送 SignalR 通知
    /// </summary>
    [Fact]
    public void PlayerReviveEvent_SendsSignalRNotification()
    {
        // Arrange
        var battleId = Guid.NewGuid();
        var characterId = Guid.NewGuid();
        
        var clientProxyMock = new Mock<IClientProxy>();
        var groupManagerMock = new Mock<IHubClients>();
        groupManagerMock.Setup(x => x.Group($"battle_{battleId}")).Returns(clientProxyMock.Object);
        
        var hubContextMock = new Mock<IHubContext<BattleNotificationHub>>();
        hubContextMock.Setup(x => x.Clients).Returns(groupManagerMock.Object);
        
        var loggerMock = new Mock<ILogger<BattleNotificationService>>();
        var options = Options.Create(new SignalROptions 
        { 
            EnableSignalR = true,
            Notification = new NotificationOptions
            {
                EnablePlayerReviveNotification = true
            }
        });
        
        var notificationService = new BattleNotificationService(hubContextMock.Object, loggerMock.Object, options);
        
        var battle = new Battle { Id = battleId, CharacterId = characterId };
        var clock = new GameClock();
        var scheduler = new EventScheduler();
        var collector = new SegmentCollector();
        var professionModule = ProfessionRegistry.Resolve(Profession.Warrior);
        var rng = new RngContext(12345);
        
        var context = new BattleContext(
            battle: battle,
            clock: clock,
            scheduler: scheduler,
            collector: collector,
            professionModule: professionModule,
            profession: Profession.Warrior,
            rng: rng,
            notificationService: notificationService
        );
        
        // Set player to dead state first
        context.Player.ReceiveDamage(context.Player.CurrentHp, DamageType.Physical, 0.0);
        
        // Act - Execute player revive event
        var reviveEvent = new PlayerReviveEvent(5.0);
        reviveEvent.Execute(context);
        
        // Assert
        clientProxyMock.Verify(
            x => x.SendCoreAsync(
                "StateChanged",
                It.Is<object[]>(args => args.Length > 0),
                It.IsAny<CancellationToken>()),
            Times.Once,
            "PlayerRevive 事件应该触发 SignalR 通知");
    }

    /// <summary>
    /// 测试 SignalR 禁用时不发送通知
    /// </summary>
    [Fact]
    public void PlayerDeathEvent_WhenSignalRDisabled_DoesNotSendNotification()
    {
        // Arrange
        var battleId = Guid.NewGuid();
        var characterId = Guid.NewGuid();
        
        var clientProxyMock = new Mock<IClientProxy>();
        var groupManagerMock = new Mock<IHubClients>();
        groupManagerMock.Setup(x => x.Group($"battle_{battleId}")).Returns(clientProxyMock.Object);
        
        var hubContextMock = new Mock<IHubContext<BattleNotificationHub>>();
        hubContextMock.Setup(x => x.Clients).Returns(groupManagerMock.Object);
        
        var loggerMock = new Mock<ILogger<BattleNotificationService>>();
        var options = Options.Create(new SignalROptions 
        { 
            EnableSignalR = false  // Disabled
        });
        
        var notificationService = new BattleNotificationService(hubContextMock.Object, loggerMock.Object, options);
        
        var battle = new Battle { Id = battleId, CharacterId = characterId };
        var clock = new GameClock();
        var scheduler = new EventScheduler();
        var collector = new SegmentCollector();
        var professionModule = ProfessionRegistry.Resolve(Profession.Warrior);
        var rng = new RngContext(12345);
        
        var context = new BattleContext(
            battle: battle,
            clock: clock,
            scheduler: scheduler,
            collector: collector,
            professionModule: professionModule,
            profession: Profession.Warrior,
            rng: rng,
            notificationService: notificationService
        );
        
        context.Player.ReceiveDamage(context.Player.CurrentHp, DamageType.Physical, 0.0);
        
        // Act
        var deathEvent = new PlayerDeathEvent(1.0);
        deathEvent.Execute(context);
        
        // Assert - No notification should be sent
        clientProxyMock.Verify(
            x => x.SendCoreAsync(
                It.IsAny<string>(),
                It.IsAny<object[]>(),
                It.IsAny<CancellationToken>()),
            Times.Never,
            "SignalR 禁用时不应发送通知");
    }

    /// <summary>
    /// 测试特定事件类型禁用时不发送通知
    /// </summary>
    [Fact]
    public void PlayerDeathEvent_WhenNotificationTypeDisabled_DoesNotSendNotification()
    {
        // Arrange
        var battleId = Guid.NewGuid();
        var characterId = Guid.NewGuid();
        
        var clientProxyMock = new Mock<IClientProxy>();
        var groupManagerMock = new Mock<IHubClients>();
        groupManagerMock.Setup(x => x.Group($"battle_{battleId}")).Returns(clientProxyMock.Object);
        
        var hubContextMock = new Mock<IHubContext<BattleNotificationHub>>();
        hubContextMock.Setup(x => x.Clients).Returns(groupManagerMock.Object);
        
        var loggerMock = new Mock<ILogger<BattleNotificationService>>();
        var options = Options.Create(new SignalROptions 
        { 
            EnableSignalR = true,
            Notification = new NotificationOptions
            {
                EnablePlayerDeathNotification = false  // This specific type disabled
            }
        });
        
        var notificationService = new BattleNotificationService(hubContextMock.Object, loggerMock.Object, options);
        
        var battle = new Battle { Id = battleId, CharacterId = characterId };
        var clock = new GameClock();
        var scheduler = new EventScheduler();
        var collector = new SegmentCollector();
        var professionModule = ProfessionRegistry.Resolve(Profession.Warrior);
        var rng = new RngContext(12345);
        
        var context = new BattleContext(
            battle: battle,
            clock: clock,
            scheduler: scheduler,
            collector: collector,
            professionModule: professionModule,
            profession: Profession.Warrior,
            rng: rng,
            notificationService: notificationService
        );
        
        context.Player.ReceiveDamage(context.Player.CurrentHp, DamageType.Physical, 0.0);
        
        // Act
        var deathEvent = new PlayerDeathEvent(1.0);
        deathEvent.Execute(context);
        
        // Assert
        clientProxyMock.Verify(
            x => x.SendCoreAsync(
                It.IsAny<string>(),
                It.IsAny<object[]>(),
                It.IsAny<CancellationToken>()),
            Times.Never,
            "PlayerDeath 通知禁用时不应发送通知");
    }

    /// <summary>
    /// 测试通知服务为 null 时不会抛出异常
    /// </summary>
    [Fact]
    public void PlayerDeathEvent_WhenNotificationServiceIsNull_DoesNotThrow()
    {
        // Arrange
        var battleId = Guid.NewGuid();
        var characterId = Guid.NewGuid();
        
        var battle = new Battle { Id = battleId, CharacterId = characterId };
        var clock = new GameClock();
        var scheduler = new EventScheduler();
        var collector = new SegmentCollector();
        var professionModule = ProfessionRegistry.Resolve(Profession.Warrior);
        var rng = new RngContext(12345);
        
        var context = new BattleContext(
            battle: battle,
            clock: clock,
            scheduler: scheduler,
            collector: collector,
            professionModule: professionModule,
            profession: Profession.Warrior,
            rng: rng,
            notificationService: null  // No notification service
        );
        
        context.Player.ReceiveDamage(context.Player.CurrentHp, DamageType.Physical, 0.0);
        
        // Act & Assert - Should not throw
        var deathEvent = new PlayerDeathEvent(1.0);
        var exception = Record.Exception(() => deathEvent.Execute(context));
        
        Assert.Null(exception);
    }
}
