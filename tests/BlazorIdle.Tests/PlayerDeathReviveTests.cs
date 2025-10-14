using BlazorIdle.Server.Domain.Characters;
using BlazorIdle.Server.Domain.Combat;
using BlazorIdle.Server.Domain.Combat.Combatants;
using BlazorIdle.Server.Domain.Combat.Damage;
using BlazorIdle.Shared.Models;
using BlazorWebGame.Domain.Combat;
using Xunit;

namespace BlazorIdle.Tests;

/// <summary>
/// Phase 3 单元测试：玩家死亡与复活系统
/// 测试玩家受伤、死亡、复活流程，以及死亡时攻击暂停、复活后恢复
/// </summary>
public class PlayerDeathReviveTests
{
    #region PlayerCombatant Death Tests

    [Fact]
    public void PlayerCombatant_ReceiveDamage_ShouldReduceHp()
    {
        // Arrange
        var player = new PlayerCombatant("p1", "Player", new CharacterStats(), stamina: 10);
        var initialHp = player.CurrentHp; // 100 HP

        // Act
        var actualDamage = player.ReceiveDamage(30, DamageType.Physical, 1.0);

        // Assert
        Assert.Equal(30, actualDamage);
        Assert.Equal(70, player.CurrentHp);
        Assert.False(player.IsDead);
        Assert.Equal(CombatantState.Alive, player.State);
    }

    [Fact]
    public void PlayerCombatant_ReceiveDamage_WhenFatal_ShouldTransitionToDead()
    {
        // Arrange
        var player = new PlayerCombatant("p1", "Player", new CharacterStats(), stamina: 10);

        // Act
        var actualDamage = player.ReceiveDamage(120, DamageType.Physical, 5.0);

        // Assert
        Assert.Equal(100, actualDamage); // Can only deal remaining HP
        Assert.Equal(0, player.CurrentHp);
        Assert.True(player.IsDead);
        Assert.Equal(CombatantState.Dead, player.State);
        Assert.Equal(5.0, player.DeathTime);
        Assert.True(player.ReviveAt.HasValue);
        Assert.Equal(15.0, player.ReviveAt.Value); // 5.0 + 10.0 default revive duration
    }

    [Fact]
    public void PlayerCombatant_ReceiveDamage_WhenDead_ShouldNotTakeDamage()
    {
        // Arrange
        var player = new PlayerCombatant("p1", "Player", new CharacterStats(), stamina: 10);
        player.ReceiveDamage(120, DamageType.Physical, 1.0);

        // Act - try to damage again after death
        var actualDamage = player.ReceiveDamage(50, DamageType.Physical, 2.0);

        // Assert
        Assert.Equal(0, actualDamage);
        Assert.Equal(0, player.CurrentHp);
        Assert.True(player.IsDead);
    }

    [Fact]
    public void PlayerCombatant_ReceiveDamage_WithAutoReviveDisabled_ShouldNotSetReviveTime()
    {
        // Arrange
        var player = new PlayerCombatant("p1", "Player", new CharacterStats(), stamina: 10)
        {
            AutoReviveEnabled = false
        };

        // Act
        player.ReceiveDamage(120, DamageType.Physical, 1.0);

        // Assert
        Assert.True(player.IsDead);
        Assert.Null(player.ReviveAt);
    }

    [Fact]
    public void PlayerCombatant_Revive_ShouldRestoreFullHpAndAliveState()
    {
        // Arrange
        var player = new PlayerCombatant("p1", "Player", new CharacterStats(), stamina: 10);
        player.ReceiveDamage(120, DamageType.Physical, 1.0);
        Assert.True(player.IsDead);

        // Act
        player.Revive(11.0);

        // Assert
        Assert.Equal(100, player.CurrentHp);
        Assert.Equal(player.MaxHp, player.CurrentHp);
        Assert.False(player.IsDead);
        Assert.Equal(CombatantState.Alive, player.State);
        Assert.Null(player.DeathTime);
        Assert.Null(player.ReviveAt);
        Assert.True(player.CanAct());
        Assert.True(player.CanBeTargeted());
    }

    [Fact]
    public void PlayerCombatant_CanAct_WhenDead_ShouldReturnFalse()
    {
        // Arrange
        var player = new PlayerCombatant("p1", "Player", new CharacterStats(), stamina: 10);
        player.ReceiveDamage(120, DamageType.Physical, 1.0);

        // Act & Assert
        Assert.False(player.CanAct());
        Assert.False(player.CanBeTargeted());
    }

    #endregion

    #region Event Integration Tests

    [Fact]
    public void PlayerDeathEvent_ShouldPauseTracksAndScheduleRevive()
    {
        // Arrange
        var context = CreateTestBattleContext();
        var player = context.Player;
        
        // Kill the player
        player.ReceiveDamage(200, DamageType.Physical, 5.0);
        Assert.True(player.IsDead);

        // Act - execute death event
        var deathEvent = new PlayerDeathEvent(5.0);
        deathEvent.Execute(context);

        // Assert - tracks should be paused
        foreach (var track in context.Tracks)
        {
            Assert.True(track.NextTriggerAt > 1e9); // Paused to far future
        }

        // Assert - revive event should be scheduled
        var scheduledEvents = GetScheduledEvents(context.Scheduler);
        Assert.Contains(scheduledEvents, e => e is PlayerReviveEvent);
    }

    [Fact]
    public void PlayerReviveEvent_ShouldRestoreHpAndResumeTracks()
    {
        // Arrange
        var context = CreateTestBattleContext();
        var player = context.Player;
        
        // Kill and trigger death event
        player.ReceiveDamage(200, DamageType.Physical, 5.0);
        var deathEvent = new PlayerDeathEvent(5.0);
        deathEvent.Execute(context);
        
        // Verify tracks are paused
        var attackTrack = context.Tracks.Find(t => t.TrackType == TrackType.Attack);
        Assert.NotNull(attackTrack);
        Assert.True(attackTrack.NextTriggerAt > 1e9);

        // Act - execute revive event
        var reviveEvent = new PlayerReviveEvent(15.0);
        reviveEvent.Execute(context);

        // Assert - player should be alive
        Assert.False(player.IsDead);
        Assert.Equal(player.MaxHp, player.CurrentHp);
        Assert.True(player.CanAct());

        // Assert - tracks should be resumed
        Assert.True(attackTrack.NextTriggerAt < 1e9); // No longer paused
        Assert.True(attackTrack.NextTriggerAt > 15.0); // Scheduled after revive time
    }

    [Fact]
    public void AttackTickEvent_WhenPlayerDead_ShouldNotExecuteAttack()
    {
        // Arrange
        var context = CreateTestBattleContext();
        var player = context.Player;
        var attackTrack = context.Tracks.Find(t => t.TrackType == TrackType.Attack);
        Assert.NotNull(attackTrack);

        // Kill the player
        player.ReceiveDamage(200, DamageType.Physical, 5.0);
        Assert.True(player.IsDead);

        var initialEventCount = context.SegmentCollector.EventCount;

        // Act - try to execute attack while dead
        var attackEvent = new AttackTickEvent(6.0, attackTrack);
        attackEvent.Execute(context);

        // Assert - attack should be skipped (no new events)
        // The event count should not increase because the attack is blocked
        Assert.Equal(initialEventCount, context.SegmentCollector.EventCount);
    }

    [Fact]
    public void SpecialPulseEvent_WhenPlayerDead_ShouldNotExecuteSpecial()
    {
        // Arrange
        var context = CreateTestBattleContext();
        var player = context.Player;
        var specialTrack = context.Tracks.Find(t => t.TrackType == TrackType.Special);
        Assert.NotNull(specialTrack);

        // Kill the player
        player.ReceiveDamage(200, DamageType.Physical, 5.0);
        Assert.True(player.IsDead);

        var initialEventCount = context.SegmentCollector.EventCount;

        // Act - try to execute special while dead
        var specialEvent = new SpecialPulseEvent(6.0, specialTrack);
        specialEvent.Execute(context);

        // Assert - special should be skipped (no new events)
        // The event count should not increase because the special is blocked
        Assert.Equal(initialEventCount, context.SegmentCollector.EventCount);
    }

    [Fact]
    public void DeathReviveCycle_ShouldWorkCorrectly()
    {
        // Arrange
        var context = CreateTestBattleContext();
        var player = context.Player;
        
        // Act - death
        player.ReceiveDamage(150, DamageType.Physical, 5.0);
        Assert.True(player.IsDead);
        Assert.Equal(5.0, player.DeathTime);
        
        var deathEvent = new PlayerDeathEvent(5.0);
        deathEvent.Execute(context);
        
        // Act - revive
        var reviveEvent = new PlayerReviveEvent(15.0);
        reviveEvent.Execute(context);
        
        // Assert - player should be fully restored
        Assert.False(player.IsDead);
        Assert.Equal(100, player.CurrentHp);
        Assert.Equal(CombatantState.Alive, player.State);
        Assert.True(player.CanAct());
        
        // Act - can take damage again after revive
        var damage = player.ReceiveDamage(30, DamageType.Physical, 20.0);
        Assert.Equal(30, damage);
        Assert.Equal(70, player.CurrentHp);
        Assert.False(player.IsDead);
    }

    [Fact]
    public void PlayerDeath_ShouldNotEndBattle()
    {
        // This test verifies that battle doesn't end when player dies
        // Battle should continue until time runs out or all enemies are dead
        
        // Arrange
        var context = CreateTestBattleContext();
        var player = context.Player;
        
        // Act - kill player
        player.ReceiveDamage(200, DamageType.Physical, 5.0);
        
        // Assert - player is dead but battle context is still valid
        Assert.True(player.IsDead);
        Assert.NotNull(context.Encounter);
        Assert.False(context.Encounter.IsDead); // Enemy is still alive
        
        // Battle should continue (this is verified by BattleEngine logic)
        // The battle only ends when:
        // 1. Target duration is reached, OR
        // 2. All enemies are dead
        // Player death does NOT end the battle
    }

    #endregion

    #region Helper Methods

    private BattleContext CreateTestBattleContext()
    {
        var battle = new Battle
        {
            Id = Guid.NewGuid(),
            CharacterId = Guid.NewGuid(),
            AttackIntervalSeconds = 2.0,
            SpecialIntervalSeconds = 5.0
        };

        var enemyDef = new Server.Domain.Combat.Enemies.EnemyDefinition(
            "test_enemy", "Test Enemy", 5, 100, 10, 0.1, 1.0, 1.0, 1.0);
        var encounter = new Server.Domain.Combat.Encounter(enemyDef);

        var context = new BattleContext(
            battle: battle,
            clock: new GameClock(),
            scheduler: new EventScheduler(),
            collector: new SegmentCollector(),
            professionModule: new TestProfessionModule(),
            profession: Profession.Warrior,
            rng: new Server.Domain.Combat.Rng.RngContext(12345),
            encounter: encounter,
            stats: new CharacterStats(),
            stamina: 10
        );

        // Initialize tracks
        var attackTrack = new TrackState(TrackType.Attack, 2.0, 0);
        var specialTrack = new TrackState(TrackType.Special, 5.0, 5.0);
        context.Tracks.Add(attackTrack);
        context.Tracks.Add(specialTrack);

        return context;
    }

    private List<IGameEvent> GetScheduledEvents(IEventScheduler scheduler)
    {
        var events = new List<IGameEvent>();
        while (scheduler.Count > 0)
        {
            var evt = scheduler.PopNext();
            if (evt != null)
            {
                events.Add(evt);
            }
        }
        return events;
    }

    /// <summary>
    /// 测试用的简单职业模块
    /// </summary>
    private class TestProfessionModule : Server.Domain.Combat.Professions.IProfessionModule
    {
        public string Id => "test_profession";
        public double BaseAttackInterval => 2.0;
        public double BaseSpecialInterval => 5.0;
        public bool PauseSpecialWhenNoEnemies => true;
        public bool SpecialStartsImmediately => false;

        public void RegisterBuffDefinitions(BattleContext ctx) { }
        public void OnBattleStart(BattleContext ctx) { }
        public void BuildSkills(BattleContext ctx, Server.Domain.Combat.Skills.AutoCastEngine autoCaster) { }
        public void OnAttackTick(BattleContext ctx, AttackTickEvent evt) { }
        public void OnSpecialPulse(BattleContext ctx, SpecialPulseEvent evt) { }
        public void OnSkillCast(BattleContext ctx, Server.Domain.Combat.Skills.SkillDefinition def) { }
    }

    #endregion
}
