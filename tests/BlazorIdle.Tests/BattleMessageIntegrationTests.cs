using BlazorIdle.Server.Application.Abstractions;
using BlazorIdle.Server.Config;
using BlazorIdle.Server.Domain.Characters;
using BlazorIdle.Server.Domain.Combat;
using BlazorIdle.Server.Domain.Combat.Enemies;
using BlazorIdle.Server.Domain.Combat.Engine;
using BlazorIdle.Server.Domain.Combat.Professions;
using BlazorIdle.Server.Domain.Combat.Rng;
using BlazorIdle.Server.Services;
using BlazorIdle.Shared.Models;
using Microsoft.Extensions.Options;
using Xunit;

namespace BlazorIdle.Tests;

/// <summary>
/// 战斗消息系统集成测试
/// 验证在实际战斗中消息事件是否正确触发
/// </summary>
public class BattleMessageIntegrationTests
{
    private class TestNotificationService : IBattleNotificationService
    {
        public List<object> ReceivedEvents { get; } = new();
        public bool IsAvailable => true;

        public Task NotifyStateChangeAsync(Guid battleId, string eventType)
        {
            ReceivedEvents.Add(new { BattleId = battleId, EventType = eventType });
            return Task.CompletedTask;
        }

        public Task NotifyEventAsync(Guid battleId, object eventData)
        {
            ReceivedEvents.Add(eventData);
            return Task.CompletedTask;
        }
    }

    [Fact]
    public void BattleEngine_WithMessageFormatter_SendsAttackStartedEvent()
    {
        // Arrange
        var notificationService = new TestNotificationService();
        var messageOptions = Options.Create(new BattleMessageOptions
        {
            AttackStartedTemplate = "{attacker} 开始攻击 {target}",
            EnableAttackStartedEvent = true,
            PlayerName = "测试玩家"
        });
        var messageFormatter = new BattleMessageFormatter(messageOptions);

        var battleId = Guid.NewGuid();
        var characterId = Guid.NewGuid();
        var rng = new RngContext(12345);
        var stats = new CharacterStats { AttackPower = 50 };
        var enemy = new EnemyDefinition(
            id: "test_enemy",
            name: "测试敌人",
            level: 1,
            maxHp: 500
        );

        var engine = new BattleEngine(
            battleId: battleId,
            characterId: characterId,
            profession: Profession.Warrior,
            stats: stats,
            rng: rng,
            enemyDef: enemy,
            enemyCount: 1,
            notificationService: notificationService,
            messageFormatter: messageFormatter
        );

        // Act - Advance the battle to trigger an attack
        engine.AdvanceUntil(3.0);

        // Assert - Check that attack started events were sent
        var attackStartedEvents = notificationService.ReceivedEvents
            .OfType<AttackStartedEventDto>()
            .ToList();

        Assert.NotEmpty(attackStartedEvents);
        var firstEvent = attackStartedEvents.First();
        Assert.Equal(battleId, firstEvent.BattleId);
        Assert.Equal("AttackStarted", firstEvent.EventType);
        Assert.Equal("测试玩家", firstEvent.AttackerName);
        Assert.Equal("测试敌人", firstEvent.TargetName);
        Assert.Contains("测试玩家", firstEvent.Message);
        Assert.Contains("测试敌人", firstEvent.Message);
    }

    [Fact]
    public void BattleEngine_WithMessageFormatter_SendsDamageAppliedEvent()
    {
        // Arrange
        var notificationService = new TestNotificationService();
        var messageOptions = Options.Create(new BattleMessageOptions
        {
            DamageDealtTemplate = "{attacker} 对 {target} 造成 {damage} 点伤害{critSuffix}",
            CritSuffix = "（暴击）",
            EnableDamageDealtEvent = true,
            PlayerName = "测试玩家"
        });
        var messageFormatter = new BattleMessageFormatter(messageOptions);

        var battleId = Guid.NewGuid();
        var characterId = Guid.NewGuid();
        var rng = new RngContext(12345);
        var stats = new CharacterStats { AttackPower = 50 };
        var enemy = new EnemyDefinition(
            id: "test_enemy",
            name: "测试敌人",
            level: 1,
            maxHp: 500
        );

        var engine = new BattleEngine(
            battleId: battleId,
            characterId: characterId,
            profession: Profession.Warrior,
            stats: stats,
            rng: rng,
            enemyDef: enemy,
            enemyCount: 1,
            notificationService: notificationService,
            messageFormatter: messageFormatter
        );

        // Act - Advance the battle to trigger damage
        engine.AdvanceUntil(3.0);

        // Assert - Check that damage applied events were sent
        var damageEvents = notificationService.ReceivedEvents
            .OfType<DamageAppliedEventDto>()
            .ToList();

        Assert.NotEmpty(damageEvents);
        var firstEvent = damageEvents.First();
        Assert.Equal(battleId, firstEvent.BattleId);
        Assert.Equal("DamageApplied", firstEvent.EventType);
        Assert.Equal("测试玩家", firstEvent.AttackerName);
        Assert.Equal("测试敌人", firstEvent.TargetName);
        Assert.True(firstEvent.Damage > 0);
        Assert.Contains("测试玩家", firstEvent.Message);
        Assert.Contains("测试敌人", firstEvent.Message);
        Assert.Contains("伤害", firstEvent.Message);
    }

    [Fact]
    public void BattleEngine_WithEnemyAttack_SendsDamageReceivedEvent()
    {
        // Arrange
        var notificationService = new TestNotificationService();
        var messageOptions = Options.Create(new BattleMessageOptions
        {
            DamageReceivedTemplate = "{target} 受到来自 {attacker} 的 {damage} 点伤害",
            EnableDamageReceivedEvent = true,
            EnableEnemyAttackStartedEvent = true,
            PlayerName = "测试玩家"
        });
        var messageFormatter = new BattleMessageFormatter(messageOptions);

        var battleId = Guid.NewGuid();
        var characterId = Guid.NewGuid();
        var rng = new RngContext(12345);
        var stats = new CharacterStats { AttackPower = 50 };
        
        // Create enemy that can attack
        var enemy = new EnemyDefinition(
            id: "test_enemy",
            name: "攻击型敌人",
            level: 1,
            maxHp: 1000,
            baseDamage: 20,
            attackIntervalSeconds: 2.0
        );

        var engine = new BattleEngine(
            battleId: battleId,
            characterId: characterId,
            profession: Profession.Warrior,
            stats: stats,
            rng: rng,
            enemyDef: enemy,
            enemyCount: 1,
            notificationService: notificationService,
            messageFormatter: messageFormatter
        );

        // Act - Advance the battle to trigger enemy attacks
        engine.AdvanceUntil(5.0);

        // Assert - Check that enemy attack events were sent
        var enemyAttackEvents = notificationService.ReceivedEvents
            .OfType<AttackStartedEventDto>()
            .Where(e => e.EventType == "EnemyAttackStarted")
            .ToList();

        var damageReceivedEvents = notificationService.ReceivedEvents
            .OfType<DamageReceivedEventDto>()
            .ToList();

        // Enemy should have attacked at least once
        Assert.NotEmpty(enemyAttackEvents);
        var firstAttack = enemyAttackEvents.First();
        Assert.Equal("攻击型敌人", firstAttack.AttackerName);
        Assert.Equal("测试玩家", firstAttack.TargetName);

        // Player should have received damage
        Assert.NotEmpty(damageReceivedEvents);
        var firstDamage = damageReceivedEvents.First();
        Assert.Equal("攻击型敌人", firstDamage.AttackerName);
        Assert.Equal("测试玩家", firstDamage.TargetName);
        Assert.True(firstDamage.Damage > 0);
        Assert.Contains("受到", firstDamage.Message);
    }

    [Fact]
    public void BattleEngine_WithDisabledEvents_DoesNotSendMessages()
    {
        // Arrange
        var notificationService = new TestNotificationService();
        var messageOptions = Options.Create(new BattleMessageOptions
        {
            EnableAttackStartedEvent = false,
            EnableDamageDealtEvent = false,
            EnableDamageReceivedEvent = false,
            EnableEnemyAttackStartedEvent = false
        });
        var messageFormatter = new BattleMessageFormatter(messageOptions);

        var battleId = Guid.NewGuid();
        var characterId = Guid.NewGuid();
        var rng = new RngContext(12345);
        var stats = new CharacterStats { AttackPower = 50 };
        var enemy = new EnemyDefinition(
            id: "test_enemy",
            name: "测试敌人",
            level: 1,
            maxHp: 500,
            baseDamage: 20,
            attackIntervalSeconds: 2.0
        );

        var engine = new BattleEngine(
            battleId: battleId,
            characterId: characterId,
            profession: Profession.Warrior,
            stats: stats,
            rng: rng,
            enemyDef: enemy,
            enemyCount: 1,
            notificationService: notificationService,
            messageFormatter: messageFormatter
        );

        // Act - Advance the battle
        engine.AdvanceUntil(5.0);

        // Assert - No attack started or damage events should be sent (only progress events)
        var attackStartedEvents = notificationService.ReceivedEvents
            .OfType<AttackStartedEventDto>()
            .ToList();
        var damageEvents = notificationService.ReceivedEvents
            .OfType<DamageAppliedEventDto>()
            .ToList();
        var damageReceivedEvents = notificationService.ReceivedEvents
            .OfType<DamageReceivedEventDto>()
            .ToList();

        Assert.Empty(attackStartedEvents);
        Assert.Empty(damageEvents);
        Assert.Empty(damageReceivedEvents);
    }
}
