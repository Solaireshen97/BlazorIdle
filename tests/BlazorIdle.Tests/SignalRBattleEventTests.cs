using Xunit;
using BlazorIdle.Server.Application.Abstractions;
using BlazorIdle.Server.Domain.Combat;
using BlazorIdle.Server.Domain.Combat.Damage;
using BlazorIdle.Server.Domain.Combat.Engine;
using BlazorIdle.Server.Domain.Combat.Professions;
using BlazorIdle.Server.Domain.Combat.Rng;
using BlazorIdle.Server.Domain.Combat.Enemies;
using BlazorIdle.Server.Domain.Characters;
using BlazorIdle.Shared.Models;
using BlazorWebGame.Domain.Combat;
using Moq;
using System;

namespace BlazorIdle.Tests;

/// <summary>
/// SignalR 战斗事件集成测试
/// 验证战斗事件会触发 SignalR 通知
/// </summary>
public sealed class SignalRBattleEventTests
{
    [Fact]
    public void BattleContext_WithNotificationService_CanBeSet()
    {
        // Arrange
        var notificationServiceMock = new Mock<IBattleNotificationService>();
        notificationServiceMock.Setup(x => x.IsAvailable).Returns(true);
        
        var context = CreateTestBattleContext();
        
        // Act
        context.NotificationService = notificationServiceMock.Object;
        
        // Assert
        Assert.NotNull(context.NotificationService);
        Assert.True(context.NotificationService.IsAvailable);
    }
    
    [Fact]
    public void PlayerDeathEvent_WithNotificationService_SendsNotification()
    {
        // Arrange
        var notificationServiceMock = new Mock<IBattleNotificationService>();
        notificationServiceMock.Setup(x => x.IsAvailable).Returns(true);
        
        var context = CreateTestBattleContext();
        context.NotificationService = notificationServiceMock.Object;
        
        // 设置玩家为死亡状态 - 使用正确的参数
        context.Player.ReceiveDamage(context.Player.MaxHp, DamageType.Physical, 0.0);
        
        var deathEvent = new PlayerDeathEvent(0.5);
        
        // Act
        deathEvent.Execute(context);
        
        // Assert
        notificationServiceMock.Verify(
            x => x.NotifyStateChangeAsync(context.Battle.Id, "PlayerDeath"),
            Times.Once);
    }
    
    [Fact]
    public void PlayerReviveEvent_WithNotificationService_SendsNotification()
    {
        // Arrange
        var notificationServiceMock = new Mock<IBattleNotificationService>();
        notificationServiceMock.Setup(x => x.IsAvailable).Returns(true);
        
        var context = CreateTestBattleContext();
        context.NotificationService = notificationServiceMock.Object;
        
        // 设置玩家为死亡状态后再复活
        context.Player.ReceiveDamage(context.Player.MaxHp, DamageType.Physical, 0.0);
        
        var reviveEvent = new PlayerReviveEvent(1.0);
        
        // Act
        reviveEvent.Execute(context);
        
        // Assert
        notificationServiceMock.Verify(
            x => x.NotifyStateChangeAsync(context.Battle.Id, "PlayerRevive"),
            Times.Once);
    }
    
    [Fact]
    public void BattleEngine_WithNotificationService_CanAdvance()
    {
        // Arrange
        var notificationServiceMock = new Mock<IBattleNotificationService>();
        notificationServiceMock.Setup(x => x.IsAvailable).Returns(true);
        
        var enemy = EnemyRegistry.Resolve("goblin");
        var stats = new CharacterStats
        {
            AttackPower = 1000, // 高攻击力确保能击杀
            CritChance = 0.0
        };
        
        var engine = new BattleEngine(
            battleId: Guid.NewGuid(),
            characterId: Guid.NewGuid(),
            profession: Profession.Warrior,
            stats: stats,
            rng: new RngContext(12345),
            enemyDef: enemy,
            enemyCount: 1
        );
        
        engine.Context.NotificationService = notificationServiceMock.Object;
        
        // Act - 推进战斗
        engine.AdvanceTo(10.0, maxEvents: 1000);
        
        // Assert - 战斗应该成功推进
        Assert.True(engine.Clock.CurrentTime > 0);
    }
    
    [Fact]
    public void BattleEngine_WithoutNotificationService_DoesNotCrash()
    {
        // Arrange - 没有设置 NotificationService
        var enemy = EnemyRegistry.Resolve("goblin");
        var stats = new CharacterStats
        {
            AttackPower = 100,
            CritChance = 0.0
        };
        
        var engine = new BattleEngine(
            battleId: Guid.NewGuid(),
            characterId: Guid.NewGuid(),
            profession: Profession.Warrior,
            stats: stats,
            rng: new RngContext(12345),
            enemyDef: enemy,
            enemyCount: 1
        );
        
        // Act & Assert - 应该不会崩溃
        var exception = Record.Exception(() => engine.AdvanceTo(5.0, maxEvents: 100));
        Assert.Null(exception);
    }
    
    [Fact]
    public void BattleEngine_DisabledNotificationService_DoesNotSendNotification()
    {
        // Arrange
        var notificationServiceMock = new Mock<IBattleNotificationService>();
        notificationServiceMock.Setup(x => x.IsAvailable).Returns(false);
        
        var enemy = EnemyRegistry.Resolve("goblin");
        var stats = new CharacterStats
        {
            AttackPower = 1000,
            CritChance = 0.0
        };
        
        var engine = new BattleEngine(
            battleId: Guid.NewGuid(),
            characterId: Guid.NewGuid(),
            profession: Profession.Warrior,
            stats: stats,
            rng: new RngContext(12345),
            enemyDef: enemy,
            enemyCount: 1
        );
        
        engine.Context.NotificationService = notificationServiceMock.Object;
        
        // Act
        engine.AdvanceTo(10.0, maxEvents: 1000);
        
        // Assert - 禁用时不应发送通知
        notificationServiceMock.Verify(
            x => x.NotifyStateChangeAsync(It.IsAny<Guid>(), It.IsAny<string>()),
            Times.Never);
    }
    
    private BattleContext CreateTestBattleContext()
    {
        var battle = new Battle
        {
            Id = Guid.NewGuid(),
            CharacterId = Guid.NewGuid(),
            StartedAt = 0
        };
        
        var clock = new GameClock();
        var scheduler = new EventScheduler();
        var collector = new SegmentCollector();
        var rng = new RngContext(12345);
        var module = ProfessionRegistry.Resolve(Profession.Warrior);
        
        var enemy = EnemyRegistry.Resolve("goblin");
        var encounter = new Encounter(enemy);
        
        return new BattleContext(
            battle: battle,
            clock: clock,
            scheduler: scheduler,
            collector: collector,
            professionModule: module,
            profession: Profession.Warrior,
            rng: rng,
            encounter: encounter,
            stats: new CharacterStats()
        );
    }
}
