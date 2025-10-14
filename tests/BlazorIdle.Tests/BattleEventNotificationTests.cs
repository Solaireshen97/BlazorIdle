using BlazorIdle.Server.Application.Abstractions;
using BlazorIdle.Server.Config;
using BlazorIdle.Server.Domain.Characters;
using BlazorIdle.Server.Domain.Combat;
using BlazorIdle.Server.Domain.Combat.Combatants;
using BlazorIdle.Server.Domain.Combat.Damage;
using BlazorIdle.Server.Domain.Combat.Enemies;
using BlazorIdle.Server.Domain.Combat.Engine;
using BlazorIdle.Server.Domain.Combat.Professions;
using BlazorIdle.Server.Domain.Combat.Rng;
using BlazorIdle.Server.Services;
using BlazorIdle.Shared.Models;
using BlazorWebGame.Domain.Combat;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace BlazorIdle.Tests;

/// <summary>
/// 战斗事件通知功能单元测试
/// 测试新增的战斗事件消息通知功能
/// </summary>
public class BattleEventNotificationTests
{
    #region AttackStartEvent Tests

    [Fact]
    public void PlayerAttack_ShouldSendAttackStartEvent()
    {
        // Arrange
        var notifications = new List<object>();
        var mockNotificationService = CreateMockNotificationService(notifications);
        var context = CreateTestBattleContext(mockNotificationService.Object);

        var enemy = context.EnemyCombatants[0];
        var attackTrack = new TrackState(TrackType.Attack, 2.0, 1.0);
        var attackEvent = new AttackTickEvent(1.0, attackTrack);

        // Act
        attackEvent.Execute(context);

        // Assert
        var attackStartEvents = notifications.OfType<AttackStartEventDto>().ToList();
        Assert.NotEmpty(attackStartEvents);
        
        var evt = attackStartEvents.First();
        Assert.Equal("AttackStart", evt.EventType);
        Assert.Equal(context.Player.Name, evt.AttackerName);
        Assert.Equal("Player", evt.AttackerType);
        Assert.NotEmpty(evt.TargetName);
        Assert.Equal("Enemy", evt.TargetType);
    }

    [Fact]
    public void EnemyAttack_ShouldSendAttackStartEvent()
    {
        // Arrange
        var notifications = new List<object>();
        var mockNotificationService = CreateMockNotificationService(notifications);
        var context = CreateTestBattleContext(mockNotificationService.Object, enemyBaseDamage: 10);

        var enemy = context.EnemyCombatants[0];
        var attackEvent = new EnemyAttackEvent(1.0, enemy);

        // Act
        attackEvent.Execute(context);

        // Assert
        var attackStartEvents = notifications.OfType<AttackStartEventDto>().ToList();
        Assert.NotEmpty(attackStartEvents);
        
        var evt = attackStartEvents.First();
        Assert.Equal("AttackStart", evt.EventType);
        Assert.Equal(enemy.Name, evt.AttackerName);
        Assert.Equal("Enemy", evt.AttackerType);
        Assert.Equal(context.Player.Name, evt.TargetName);
        Assert.Equal("Player", evt.TargetType);
    }

    #endregion

    #region DamageDealtEvent Tests

    [Fact]
    public void PlayerAttack_ShouldSendDamageDealtEvent()
    {
        // Arrange
        var notifications = new List<object>();
        var mockNotificationService = CreateMockNotificationService(notifications);
        var context = CreateTestBattleContext(mockNotificationService.Object);

        var attackTrack = new TrackState(TrackType.Attack, 2.0, 1.0);
        var attackEvent = new AttackTickEvent(1.0, attackTrack);

        // Act
        attackEvent.Execute(context);

        // Assert
        var damageDealtEvents = notifications.OfType<DamageDealtEventDto>().ToList();
        Assert.NotEmpty(damageDealtEvents);
        
        var evt = damageDealtEvents.First();
        Assert.Equal("DamageDealt", evt.EventType);
        Assert.Equal(context.Player.Name, evt.AttackerName);
        Assert.True(evt.Damage > 0);
        Assert.Equal("Physical", evt.DamageType);
    }

    [Fact]
    public void EnemyAttack_ShouldSendDamageDealtEvent()
    {
        // Arrange
        var notifications = new List<object>();
        var mockNotificationService = CreateMockNotificationService(notifications);
        var context = CreateTestBattleContext(mockNotificationService.Object, enemyBaseDamage: 10);

        var enemy = context.EnemyCombatants[0];
        var attackEvent = new EnemyAttackEvent(1.0, enemy);

        // Act
        attackEvent.Execute(context);

        // Assert
        var damageDealtEvents = notifications.OfType<DamageDealtEventDto>().ToList();
        Assert.NotEmpty(damageDealtEvents);
        
        var evt = damageDealtEvents.First();
        Assert.Equal("DamageDealt", evt.EventType);
        Assert.Equal(enemy.Name, evt.AttackerName);
        Assert.Equal(context.Player.Name, evt.TargetName);
        Assert.True(evt.Damage > 0);
    }

    #endregion

    #region DamageReceivedEvent Tests

    [Fact]
    public void EnemyAttack_ShouldSendDamageReceivedEvent()
    {
        // Arrange
        var notifications = new List<object>();
        var mockNotificationService = CreateMockNotificationService(notifications);
        var context = CreateTestBattleContext(mockNotificationService.Object, enemyBaseDamage: 10);

        var enemy = context.EnemyCombatants[0];
        var attackEvent = new EnemyAttackEvent(1.0, enemy);

        // Act
        attackEvent.Execute(context);

        // Assert
        var damageReceivedEvents = notifications.OfType<DamageReceivedEventDto>().ToList();
        Assert.NotEmpty(damageReceivedEvents);
        
        var evt = damageReceivedEvents.First();
        Assert.Equal("DamageReceived", evt.EventType);
        Assert.Equal(context.Player.Name, evt.ReceiverName);
        Assert.Equal(enemy.Name, evt.AttackerName);
        Assert.True(evt.Damage > 0);
        Assert.True(evt.CurrentHp < evt.MaxHp);
    }

    #endregion

    #region BattleEventMessageFormatter Tests

    [Fact]
    public void MessageFormatter_ShouldFormatAttackStartMessage()
    {
        // Arrange
        var options = Options.Create(new BattleEventsOptions
        {
            EnableBattleEventMessages = true,
            Messages = new BattleEventMessages
            {
                AttackStart = new AttackStartMessages
                {
                    Enabled = true,
                    PlayerAttacksEnemy = "{attacker} 开始攻击 {target}"
                }
            }
        });
        var formatter = new BattleEventMessageFormatter(options);

        var evt = new AttackStartEventDto
        {
            AttackerName = "玩家",
            AttackerType = "Player",
            TargetName = "史莱姆",
            TargetType = "Enemy"
        };

        // Act
        var message = formatter.FormatAttackStartMessage(evt);

        // Assert
        Assert.Equal("玩家 开始攻击 史莱姆", message);
    }

    [Fact]
    public void MessageFormatter_ShouldFormatDamageDealtMessage()
    {
        // Arrange
        var options = Options.Create(new BattleEventsOptions
        {
            EnableBattleEventMessages = true,
            Messages = new BattleEventMessages
            {
                DamageDealt = new DamageDealtMessages
                {
                    Enabled = true,
                    Normal = "{attacker} 对 {target} 造成 {damage} 点{damageType}伤害"
                }
            },
            DamageTypeNames = new Dictionary<string, string>
            {
                { "Physical", "物理" }
            }
        });
        var formatter = new BattleEventMessageFormatter(options);

        var evt = new DamageDealtEventDto
        {
            AttackerName = "玩家",
            TargetName = "史莱姆",
            Damage = 50,
            IsCrit = false,
            DamageType = "Physical"
        };

        // Act
        var message = formatter.FormatDamageDealtMessage(evt);

        // Assert
        Assert.Equal("玩家 对 史莱姆 造成 50 点物理伤害", message);
    }

    [Fact]
    public void MessageFormatter_ShouldFormatCriticalDamageMessage()
    {
        // Arrange
        var options = Options.Create(new BattleEventsOptions
        {
            EnableBattleEventMessages = true,
            Messages = new BattleEventMessages
            {
                DamageDealt = new DamageDealtMessages
                {
                    Enabled = true,
                    Critical = "{attacker} 对 {target} 造成 {damage} 点{damageType}暴击伤害！"
                }
            },
            DamageTypeNames = new Dictionary<string, string>
            {
                { "Physical", "物理" }
            }
        });
        var formatter = new BattleEventMessageFormatter(options);

        var evt = new DamageDealtEventDto
        {
            AttackerName = "玩家",
            TargetName = "史莱姆",
            Damage = 100,
            IsCrit = true,
            DamageType = "Physical"
        };

        // Act
        var message = formatter.FormatDamageDealtMessage(evt);

        // Assert
        Assert.Equal("玩家 对 史莱姆 造成 100 点物理暴击伤害！", message);
    }

    [Fact]
    public void MessageFormatter_ShouldFormatDamageReceivedMessage()
    {
        // Arrange
        var options = Options.Create(new BattleEventsOptions
        {
            EnableBattleEventMessages = true,
            Messages = new BattleEventMessages
            {
                DamageReceived = new DamageReceivedMessages
                {
                    Enabled = true,
                    Player = "{receiver} 受到 {attacker} 的 {damage} 点{damageType}伤害（剩余 {currentHp}/{maxHp}）"
                }
            },
            DamageTypeNames = new Dictionary<string, string>
            {
                { "Physical", "物理" }
            }
        });
        var formatter = new BattleEventMessageFormatter(options);

        var evt = new DamageReceivedEventDto
        {
            ReceiverName = "玩家",
            AttackerName = "哥布林",
            Damage = 15,
            DamageType = "Physical",
            CurrentHp = 85,
            MaxHp = 100
        };

        // Act
        var message = formatter.FormatDamageReceivedMessage(evt, isPlayer: true);

        // Assert
        Assert.Equal("玩家 受到 哥布林 的 15 点物理伤害（剩余 85/100）", message);
    }

    #endregion

    #region Helper Methods

    private class TestProfessionModule : IProfessionModule
    {
        public string Id => "test_profession";
        public double BaseAttackInterval => 2.0;
        public double BaseSpecialInterval => 5.0;

        public void RegisterBuffDefinitions(BattleContext ctx) { }
        public void OnBattleStart(BattleContext ctx) { }
        public void BuildSkills(BattleContext ctx, Server.Domain.Combat.Skills.AutoCastEngine autoCaster) { }
        public void OnAttackTick(BattleContext ctx, AttackTickEvent evt) { }
        public void OnSpecialPulse(BattleContext ctx, SpecialPulseEvent evt) { }
        public void OnSkillCast(BattleContext ctx, Server.Domain.Combat.Skills.SkillDefinition def) { }
    }

    private Mock<IBattleNotificationService> CreateMockNotificationService(List<object> notifications)
    {
        var mock = new Mock<IBattleNotificationService>();
        mock.SetupGet(x => x.IsAvailable).Returns(true);
        mock.Setup(x => x.NotifyEventAsync(It.IsAny<Guid>(), It.IsAny<object>()))
            .Callback<Guid, object>((battleId, eventData) => notifications.Add(eventData))
            .Returns(Task.CompletedTask);
        return mock;
    }

    private BattleContext CreateTestBattleContext(
        IBattleNotificationService? notificationService = null,
        int enemyBaseDamage = 0)
    {
        var battleId = Guid.NewGuid();
        var characterId = Guid.NewGuid();
        var stats = new CharacterStats { AttackPower = 10 };
        
        var enemyDef = new EnemyDefinition(
            id: "test_enemy",
            name: "测试敌人",
            level: 1,
            maxHp: 100,
            baseDamage: enemyBaseDamage,
            attackDamageType: DamageType.Physical,
            attackIntervalSeconds: 2.0
        );
        
        var encounter = new Encounter(enemyDef);
        var encounterGroup = EncounterGroup.FromSingle(encounter);
        
        var battle = new Battle
        {
            Id = battleId,
            CharacterId = characterId,
            AttackIntervalSeconds = 2.0,
            SpecialIntervalSeconds = 5.0
        };

        var context = new BattleContext(
            battle: battle,
            clock: new GameClock(),
            scheduler: new EventScheduler(),
            collector: new SegmentCollector(),
            professionModule: new TestProfessionModule(),
            profession: Profession.Warrior,
            rng: new RngContext(12345),
            stats: stats,
            stamina: 10,
            encounterGroup: encounterGroup
        );

        // 设置通知服务
        if (notificationService != null)
        {
            typeof(BattleContext)
                .GetProperty(nameof(BattleContext.NotificationService))!
                .SetValue(context, notificationService);
        }

        // 创建敌人战斗单位列表
        var enemyCombatant = new EnemyCombatant("enemy_0", encounter);
        
        // 如果敌人有攻击能力，设置攻击轨道
        if (enemyBaseDamage > 0)
        {
            var attackTrack = new TrackState(TrackType.Attack, 2.0, 2.0);
            enemyCombatant.AttackTrack = attackTrack;
        }
        
        context.EnemyCombatants.Add(enemyCombatant);

        return context;
    }

    #endregion
}
