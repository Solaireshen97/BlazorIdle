using BlazorIdle.Server.Domain.Characters;
using BlazorIdle.Server.Domain.Combat;
using BlazorIdle.Server.Domain.Combat.Combatants;
using BlazorIdle.Server.Domain.Combat.Damage;
using BlazorIdle.Server.Domain.Combat.Enemies;
using BlazorIdle.Server.Domain.Combat.Engine;
using BlazorIdle.Server.Domain.Combat.Professions;
using BlazorIdle.Server.Domain.Combat.Rng;
using BlazorIdle.Shared.Models;
using BlazorWebGame.Domain.Combat;
using System;
using Xunit;

namespace BlazorIdle.Tests;

/// <summary>
/// Phase 4 单元测试：怪物攻击能力
/// 测试怪物可以攻击玩家、玩家受伤、玩家死亡后怪物暂停攻击、玩家复活后怪物恢复攻击
/// </summary>
public class EnemyAttackTests
{
    #region EnemyDefinition Extension Tests

    [Fact]
    public void EnemyDefinition_WithAttackProperties_ShouldStoreCorrectly()
    {
        // Arrange & Act
        var enemy = new EnemyDefinition(
            id: "test_enemy",
            name: "Test Enemy",
            level: 5,
            maxHp: 100,
            baseDamage: 10,
            attackDamageType: DamageType.Physical,
            attackIntervalSeconds: 2.5
        );

        // Assert
        Assert.Equal(10, enemy.BaseDamage);
        Assert.Equal(DamageType.Physical, enemy.AttackDamageType);
        Assert.Equal(2.5, enemy.AttackIntervalSeconds);
    }

    [Fact]
    public void EnemyDefinition_DefaultAttackProperties_ShouldBeZeroAndPhysical()
    {
        // Arrange & Act
        var enemy = new EnemyDefinition(
            id: "test_enemy",
            name: "Test Enemy",
            level: 5,
            maxHp: 100
        );

        // Assert
        Assert.Equal(0, enemy.BaseDamage);
        Assert.Equal(DamageType.Physical, enemy.AttackDamageType);
        Assert.Equal(2.0, enemy.AttackIntervalSeconds);
    }

    #endregion

    #region EnemyAttackEvent Tests

    [Fact]
    public void EnemyAttackEvent_ShouldDamagePlayer()
    {
        // Arrange
        var enemy = new EnemyDefinition("enemy1", "Goblin", 5, 100, baseDamage: 15);
        var encounter = new Encounter(enemy);
        var enemyCombatant = new EnemyCombatant("enemy_0", encounter);
        
        var stats = new CharacterStats();
        var player = new PlayerCombatant("p1", "Player", stats, stamina: 10);
        
        var context = CreateTestBattleContext(player, encounter);
        var track = new TrackState(TrackType.EnemyAttack, 2.0, 2.0);
        var attackEvent = new EnemyAttackEvent(2.0, enemyCombatant, track);

        // Act
        attackEvent.Execute(context);

        // Assert
        Assert.Equal(85, player.CurrentHp); // 100 - 15 = 85
        Assert.False(player.IsDead);
        Assert.Equal(CombatantState.Alive, player.State);
    }

    [Fact]
    public void EnemyAttackEvent_WhenPlayerDead_ShouldNotDamage()
    {
        // Arrange
        var enemy = new EnemyDefinition("enemy1", "Goblin", 5, 100, baseDamage: 15);
        var encounter = new Encounter(enemy);
        var enemyCombatant = new EnemyCombatant("enemy_0", encounter);
        
        var stats = new CharacterStats();
        var player = new PlayerCombatant("p1", "Player", stats, stamina: 10);
        player.ReceiveDamage(150, DamageType.Physical, 1.0); // Kill player
        
        var context = CreateTestBattleContext(player, encounter);
        var track = new TrackState(TrackType.EnemyAttack, 2.0, 2.0);
        var attackEvent = new EnemyAttackEvent(3.0, enemyCombatant, track);

        // Act
        attackEvent.Execute(context);

        // Assert
        Assert.Equal(0, player.CurrentHp); // Still dead, no additional damage
        Assert.True(player.IsDead);
    }

    [Fact]
    public void EnemyAttackEvent_WhenEnemyDead_ShouldNotAttack()
    {
        // Arrange
        var enemy = new EnemyDefinition("enemy1", "Goblin", 5, 100, baseDamage: 15);
        var encounter = new Encounter(enemy);
        encounter.ApplyDamage(150, 1.0); // Kill enemy
        var enemyCombatant = new EnemyCombatant("enemy_0", encounter);
        
        var stats = new CharacterStats();
        var player = new PlayerCombatant("p1", "Player", stats, stamina: 10);
        
        var context = CreateTestBattleContext(player, encounter);
        var track = new TrackState(TrackType.EnemyAttack, 2.0, 2.0);
        var attackEvent = new EnemyAttackEvent(2.0, enemyCombatant, track);

        // Act
        attackEvent.Execute(context);

        // Assert
        Assert.Equal(100, player.CurrentHp); // No damage dealt
    }

    [Fact]
    public void EnemyAttackEvent_FatalDamage_ShouldTriggerPlayerDeath()
    {
        // Arrange
        var enemy = new EnemyDefinition("enemy1", "Goblin", 5, 100, baseDamage: 120);
        var encounter = new Encounter(enemy);
        var enemyCombatant = new EnemyCombatant("enemy_0", encounter);
        
        var stats = new CharacterStats();
        var player = new PlayerCombatant("p1", "Player", stats, stamina: 10); // 100 HP
        
        var context = CreateTestBattleContext(player, encounter);
        var track = new TrackState(TrackType.EnemyAttack, 2.0, 2.0);
        var attackEvent = new EnemyAttackEvent(2.0, enemyCombatant, track);

        // Act
        attackEvent.Execute(context);

        // Assert
        Assert.Equal(0, player.CurrentHp);
        Assert.True(player.IsDead);
        Assert.Equal(CombatantState.Dead, player.State);
        
        // Verify death event was scheduled
        var nextEvent = context.Scheduler.PeekNext();
        Assert.NotNull(nextEvent);
        Assert.Equal("PlayerDeath", nextEvent.EventType);
    }

    [Fact]
    public void EnemyAttackEvent_WithZeroDamage_ShouldScheduleNextAttack()
    {
        // Arrange
        var enemy = new EnemyDefinition("enemy1", "Goblin", 5, 100, baseDamage: 0);
        var encounter = new Encounter(enemy);
        var enemyCombatant = new EnemyCombatant("enemy_0", encounter);
        
        var stats = new CharacterStats();
        var player = new PlayerCombatant("p1", "Player", stats, stamina: 10);
        
        var context = CreateTestBattleContext(player, encounter);
        var track = new TrackState(TrackType.EnemyAttack, 2.0, 2.0);
        var attackEvent = new EnemyAttackEvent(2.0, enemyCombatant, track);

        // Act
        attackEvent.Execute(context);

        // Assert
        Assert.Equal(100, player.CurrentHp); // No damage
        Assert.Equal(4.0, track.NextTriggerAt); // Next attack scheduled at 2.0 + 2.0
    }

    #endregion

    #region TrackState Pause/Resume Tests

    [Fact]
    public void TrackState_Pause_ShouldSetNextTriggerToMaxValue()
    {
        // Arrange
        var track = new TrackState(TrackType.EnemyAttack, 2.0, 5.0);
        track.NextTriggerAt = 10.0;

        // Act
        track.Pause(8.0);

        // Assert
        Assert.Equal(double.MaxValue, track.NextTriggerAt);
    }

    [Fact]
    public void TrackState_Resume_ShouldRestoreRemainingTime()
    {
        // Arrange
        var track = new TrackState(TrackType.EnemyAttack, 2.0, 5.0);
        track.NextTriggerAt = 10.0;
        track.Pause(8.0); // Remaining: 10.0 - 8.0 = 2.0

        // Act
        track.Resume(15.0); // Resume at 15.0, should trigger at 15.0 + 2.0 = 17.0

        // Assert
        Assert.Equal(17.0, track.NextTriggerAt);
    }

    [Fact]
    public void TrackState_PauseTwice_ShouldNotChangePauseState()
    {
        // Arrange
        var track = new TrackState(TrackType.EnemyAttack, 2.0, 5.0);
        track.NextTriggerAt = 10.0;

        // Act
        track.Pause(8.0);
        var firstTrigger = track.NextTriggerAt;
        track.Pause(9.0); // Second pause should be ignored

        // Assert
        Assert.Equal(firstTrigger, track.NextTriggerAt);
    }

    [Fact]
    public void TrackState_ResumeWithoutPause_ShouldNotChange()
    {
        // Arrange
        var track = new TrackState(TrackType.EnemyAttack, 2.0, 5.0);
        track.NextTriggerAt = 10.0;

        // Act
        track.Resume(8.0);

        // Assert
        Assert.Equal(10.0, track.NextTriggerAt); // Should not change
    }

    #endregion

    #region Integration Tests

    [Fact]
    public void BattleEngine_WithEnemyAttack_ShouldInitializeEnemyTracks()
    {
        // Arrange
        var enemy = new EnemyDefinition("enemy1", "Goblin", 5, 100, baseDamage: 10, attackIntervalSeconds: 2.0);
        var stats = new CharacterStats { AttackPower = 100 };
        var rng = new RngContext(12345);

        // Act
        var engine = new BattleEngine(
            battleId: Guid.NewGuid(),
            characterId: Guid.NewGuid(),
            profession: Profession.Warrior,
            stats: stats,
            rng: rng,
            enemyDef: enemy,
            enemyCount: 1
        );

        // Assert
        Assert.Single(engine.Context.EnemyAttackTracks);
        Assert.True(engine.Context.EnemyAttackTracks.ContainsKey("enemy_0"));
    }

    [Fact]
    public void BattleEngine_PlayerTakesEnemyDamage_Integration()
    {
        // Arrange
        var enemy = new EnemyDefinition("enemy1", "Goblin", 5, 200, baseDamage: 10, attackIntervalSeconds: 1.0);
        var stats = new CharacterStats { AttackPower = 50 };
        var rng = new RngContext(12345);

        var engine = new BattleEngine(
            battleId: Guid.NewGuid(),
            characterId: Guid.NewGuid(),
            profession: Profession.Warrior,
            stats: stats,
            rng: rng,
            enemyDef: enemy,
            enemyCount: 1
        );

        var initialPlayerHp = engine.Context.Player.CurrentHp;

        // Act - Advance to first enemy attack at 1.0 second
        engine.AdvanceUntil(2.0);

        // Assert
        Assert.True(engine.Context.Player.CurrentHp < initialPlayerHp, "Player should have taken damage from enemy");
    }

    [Fact]
    public void BattleEngine_PlayerDeathPausesEnemyAttacks_Integration()
    {
        // Arrange - Create a strong enemy that can kill the player
        var enemy = new EnemyDefinition("enemy1", "Dragon", 10, 1000, baseDamage: 150, attackIntervalSeconds: 0.5);
        var stats = new CharacterStats { AttackPower = 10 };
        var rng = new RngContext(12345);

        var engine = new BattleEngine(
            battleId: Guid.NewGuid(),
            characterId: Guid.NewGuid(),
            profession: Profession.Warrior,
            stats: stats,
            rng: rng,
            enemyDef: enemy,
            enemyCount: 1
        );

        // Act - Advance enough for enemy to kill player
        engine.AdvanceUntil(1.5); // Enemy attacks at 0.5s, should kill player

        // Assert
        Assert.True(engine.Context.Player.IsDead, "Player should be dead");
        
        // Enemy attack tracks should be paused
        var enemyTrack = engine.Context.EnemyAttackTracks["enemy_0"];
        Assert.Equal(double.MaxValue, enemyTrack.NextTriggerAt);
    }

    [Fact]
    public void BattleEngine_PlayerReviveResumesEnemyAttacks_Integration()
    {
        // Arrange - Create an enemy with reasonable damage
        var enemy = new EnemyDefinition("enemy1", "Orc", 8, 100, baseDamage: 15, attackIntervalSeconds: 1.0);
        var stats = new CharacterStats { AttackPower = 500 }; // Strong player to kill enemy quickly
        var rng = new RngContext(12345);

        var engine = new BattleEngine(
            battleId: Guid.NewGuid(),
            characterId: Guid.NewGuid(),
            profession: Profession.Warrior,
            stats: stats,
            rng: rng,
            enemyDef: enemy,
            enemyCount: 1
        );

        var player = engine.Context.Player;
        player.ReviveDurationSeconds = 1.0; // Very short revive for testing

        // Advance to time 2.0 first
        engine.AdvanceUntil(2.0);
        
        // Now manually kill the player to test revive mechanism
        player.ReceiveDamage(150, DamageType.Physical, engine.Clock.CurrentTime);
        Assert.True(player.IsDead, "Player should be dead after taking fatal damage");
        
        // Manually trigger death event
        var deathEvent = new PlayerDeathEvent(engine.Clock.CurrentTime);
        deathEvent.Execute(engine.Context);
        
        // Verify enemy attack track is paused
        var enemyTrack = engine.Context.EnemyAttackTracks["enemy_0"];
        Assert.Equal(double.MaxValue, enemyTrack.NextTriggerAt);
        
        // Advance past the revive time (death at 2.0 + 1.0 revive = 3.0)
        engine.AdvanceUntil(4.0);

        // Assert - player should be alive after revive
        Assert.False(player.IsDead, "Player should be alive after revive");
        
        // Note: Player HP might not be MaxHp if enemy attacked after revive
        // The important thing is that player is alive and enemy attacks resumed
        Assert.True(player.CurrentHp > 0 && player.CurrentHp <= player.MaxHp);
        
        // Enemy attack track should be resumed (not at MaxValue)
        Assert.NotEqual(double.MaxValue, enemyTrack.NextTriggerAt);
    }

    #endregion

    #region Helper Methods

    private BattleContext CreateTestBattleContext(PlayerCombatant player, Encounter encounter)
    {
        var battle = new Battle
        {
            Id = Guid.NewGuid(),
            CharacterId = Guid.NewGuid(),
            AttackIntervalSeconds = 1.0,
            SpecialIntervalSeconds = 5.0,
            StartedAt = 0
        };

        var clock = new GameClock();
        var scheduler = new EventScheduler();
        var collector = new SegmentCollector();
        var rng = new RngContext(12345);
        var professionModule = ProfessionRegistry.Resolve(Profession.Warrior);

        var context = new BattleContext(
            battle: battle,
            clock: clock,
            scheduler: scheduler,
            collector: collector,
            professionModule: professionModule,
            profession: Profession.Warrior,
            rng: rng,
            encounter: encounter,
            encounterGroup: null,
            stats: player.Stats,
            stamina: 10,
            characterId: player.Id,
            characterName: player.Name
        );

        // Replace the player in context with our test player
        var field = typeof(BattleContext).GetField("Player", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance) 
            ?? typeof(BattleContext).GetProperty("Player")?.GetBackingField();
        
        if (field == null)
        {
            // Use reflection to set the property
            var prop = typeof(BattleContext).GetProperty("Player");
            prop?.SetValue(context, player);
        }
        else
        {
            field.SetValue(context, player);
        }

        return context;
    }

    #endregion
}

internal static class ReflectionExtensions
{
    public static System.Reflection.FieldInfo? GetBackingField(this System.Reflection.PropertyInfo property)
    {
        return property.DeclaringType?.GetField($"<{property.Name}>k__BackingField",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
    }
}
