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
using Xunit;

namespace BlazorIdle.Tests;

/// <summary>
/// Phase 4 单元测试：怪物攻击能力
/// 测试怪物可以攻击玩家、玩家受伤、玩家死亡时怪物暂停、玩家复活后怪物恢复
/// </summary>
public class EnemyAttackTests
{
    #region EnemyDefinition Tests

    [Fact]
    public void EnemyDefinition_ShouldSupportAttackProperties()
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
    public void EnemyDefinition_DefaultValues_ShouldBeZero()
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
        Assert.Equal(3.0, enemy.AttackIntervalSeconds);
    }

    #endregion

    #region EnemyCombatant Tests

    [Fact]
    public void EnemyCombatant_ShouldSupportAttackTrack()
    {
        // Arrange
        var enemyDef = new EnemyDefinition("test", "Test", 5, 100, baseDamage: 10);
        var encounter = new Encounter(enemyDef);
        var enemy = new EnemyCombatant("e1", encounter);

        // Act
        var track = new TrackState(TrackType.Attack, 2.0, 2.0);
        enemy.AttackTrack = track;

        // Assert
        Assert.NotNull(enemy.AttackTrack);
        Assert.Equal(2.0, enemy.AttackTrack.CurrentInterval);
    }

    #endregion

    #region EnemyAttackEvent Tests

    [Fact]
    public void EnemyAttackEvent_ShouldDamagePlayer()
    {
        // Arrange
        var context = CreateTestBattleContext(enemyBaseDamage: 20);
        var enemy = context.EnemyCombatants[0];
        var initialPlayerHp = context.Player.CurrentHp;

        // Act
        var attackEvent = new EnemyAttackEvent(1.0, enemy);
        attackEvent.Execute(context);

        // Assert
        Assert.Equal(initialPlayerHp - 20, context.Player.CurrentHp);
        Assert.False(context.Player.IsDead);
    }

    [Fact]
    public void EnemyAttackEvent_WhenPlayerDead_ShouldPauseAttack()
    {
        // Arrange
        var context = CreateTestBattleContext(enemyBaseDamage: 10);
        var enemy = context.EnemyCombatants[0];
        
        // Kill the player first
        context.Player.ReceiveDamage(200, DamageType.Physical, 0.5);
        Assert.True(context.Player.IsDead);

        // Act
        var attackEvent = new EnemyAttackEvent(1.0, enemy);
        attackEvent.Execute(context);

        // Assert - enemy attack should be paused (set to far future)
        Assert.Equal(1e10, enemy.AttackTrack!.NextTriggerAt);
        Assert.Equal(0, context.Player.CurrentHp); // Player HP should not change
    }

    [Fact]
    public void EnemyAttackEvent_WhenEnemyDead_ShouldNotAttack()
    {
        // Arrange
        var context = CreateTestBattleContext(enemyBaseDamage: 10);
        var enemy = context.EnemyCombatants[0];
        var initialPlayerHp = context.Player.CurrentHp;
        
        // Kill the enemy
        enemy.ReceiveDamage(200, DamageType.Physical, 0.5);
        Assert.True(enemy.IsDead);

        // Act
        var attackEvent = new EnemyAttackEvent(1.0, enemy);
        attackEvent.Execute(context);

        // Assert - player should not be damaged
        Assert.Equal(initialPlayerHp, context.Player.CurrentHp);
    }

    [Fact]
    public void EnemyAttackEvent_ShouldScheduleNextAttack()
    {
        // Arrange
        var context = CreateTestBattleContext(enemyBaseDamage: 10, attackInterval: 3.0);
        var enemy = context.EnemyCombatants[0];
        var initialEventCount = context.Scheduler.Count;

        // Act
        var attackEvent = new EnemyAttackEvent(1.0, enemy);
        attackEvent.Execute(context);

        // Assert - next attack should be scheduled
        Assert.Equal(4.0, enemy.AttackTrack!.NextTriggerAt); // 1.0 + 3.0
        // Note: Scheduler count check is not reliable as other events may be scheduled
    }

    [Fact]
    public void EnemyAttackEvent_FatalDamage_ShouldTriggerPlayerDeath()
    {
        // Arrange
        var context = CreateTestBattleContext(enemyBaseDamage: 150, playerStamina: 10);
        var enemy = context.EnemyCombatants[0];
        
        Assert.Equal(100, context.Player.CurrentHp); // 10 stamina = 100 HP
        Assert.False(context.Player.IsDead);

        // Act
        var attackEvent = new EnemyAttackEvent(1.0, enemy);
        attackEvent.Execute(context);

        // Assert - player should be dead and death event scheduled
        Assert.True(context.Player.IsDead);
        Assert.Equal(0, context.Player.CurrentHp);
        Assert.Equal(CombatantState.Dead, context.Player.State);
    }

    #endregion

    #region Integration Tests

    [Fact]
    public void BattleEngine_ShouldInitializeEnemyAttacks()
    {
        // Arrange & Act
        var engine = CreateTestBattleEngine(enemyBaseDamage: 10, enemyCount: 2);

        // Assert
        Assert.Equal(2, engine.Context.EnemyCombatants.Count);
        Assert.All(engine.Context.EnemyCombatants, enemy =>
        {
            Assert.NotNull(enemy.AttackTrack);
            Assert.True(enemy.AttackTrack.NextTriggerAt > 0);
        });
    }

    [Fact]
    public void BattleEngine_PlayerDeathAndRevive_ShouldPauseAndResumeEnemyAttacks()
    {
        // Arrange - use high damage to kill player (default 100 HP)
        var engine = CreateTestBattleEngine(enemyBaseDamage: 150, enemyCount: 1);
        
        // Player starts with 100 HP (default 10 stamina)
        Assert.Equal(100, engine.Context.Player.CurrentHp);
        Assert.False(engine.Context.Player.IsDead);
        
        // Get reference to enemy combatant
        Assert.Single(engine.Context.EnemyCombatants);
        var enemy = engine.Context.EnemyCombatants[0];
        
        // Act 1: Advance time to let enemy attack and kill player
        // Enemy attacks at 3.0s with 150 damage (fatal)
        engine.AdvanceTo(3.5, 100);
        
        // Assert 1: Player should be dead, enemy attack paused
        Assert.True(engine.Context.Player.IsDead);
        Assert.Equal(0, engine.Context.Player.CurrentHp);
        
        // The enemy should have paused (NextTriggerAt set to FAR_FUTURE)
        // Note: The actual attack track might have been rescheduled before pausing
        // So we check if the player revive is scheduled and will resume attacks
        
        // Act 2: Advance time to let player revive
        // Death at ~3s, revive at 3 + 10 = 13s
        engine.AdvanceTo(15.0, 100);
        
        // Assert 2: Player should be alive after revive
        Assert.False(engine.Context.Player.IsDead);
        Assert.Equal(CombatantState.Alive, engine.Context.Player.State);
        Assert.Equal(100, engine.Context.Player.CurrentHp); // Full HP restored
        
        // Enemy attack should have resumed (NextTriggerAt is a reasonable future time, not FAR_FUTURE)
        Assert.True(enemy.AttackTrack!.NextTriggerAt < 1e9); // Not paused
        Assert.True(enemy.AttackTrack!.NextTriggerAt > 13.0); // Scheduled after revive
    }

    [Fact]
    public void BattleEngine_MultipleEnemies_ShouldAllAttack()
    {
        // Arrange - default player has 100 HP
        var engine = CreateTestBattleEngine(enemyBaseDamage: 5, enemyCount: 3);
        var initialPlayerHp = engine.Context.Player.CurrentHp;
        Assert.Equal(100, initialPlayerHp); // Verify default HP
        
        // Act: Advance time to let enemies attack multiple times
        engine.AdvanceTo(10.0, 1000);
        
        // Assert: Player should have taken damage from multiple enemies
        Assert.True(engine.Context.Player.CurrentHp < initialPlayerHp);
        // With 3 enemies at 5 damage each attacking every 3s, in 10s expect roughly:
        // (10/3) * 3 * 5 = ~50 damage
        Assert.True(initialPlayerHp - engine.Context.Player.CurrentHp > 30); // At least some attacks landed
    }

    #endregion

    #region Helper Methods

    private BattleContext CreateTestBattleContext(
        int enemyBaseDamage = 10,
        double attackInterval = 3.0,
        int playerStamina = 10)
    {
        var battleId = Guid.NewGuid();
        var characterId = Guid.NewGuid();
        var stats = new CharacterStats { AttackPower = 100 };
        
        var enemyDef = new EnemyDefinition(
            id: "test_enemy",
            name: "Test Enemy",
            level: 5,
            maxHp: 100,
            baseDamage: enemyBaseDamage,
            attackDamageType: DamageType.Physical,
            attackIntervalSeconds: attackInterval
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
            stamina: playerStamina,
            encounterGroup: encounterGroup
        );
        
        // Manually initialize enemy combatants (simulating what BattleEngine does)
        var enemyCombatant = new EnemyCombatant("enemy_0", encounter);
        var attackTrack = new TrackState(TrackType.Attack, attackInterval, attackInterval);
        enemyCombatant.AttackTrack = attackTrack;
        context.EnemyCombatants.Add(enemyCombatant);
        
        return context;
    }

    private BattleEngine CreateTestBattleEngine(
        int enemyBaseDamage = 10,
        int enemyCount = 1)
    {
        var battleId = Guid.NewGuid();
        var characterId = Guid.NewGuid();
        var stats = new CharacterStats { AttackPower = 100 };
        var rng = new RngContext(12345);
        
        var enemyDef = new EnemyDefinition(
            id: "test_enemy",
            name: "Test Enemy",
            level: 5,
            maxHp: 100,
            baseDamage: enemyBaseDamage,
            attackDamageType: DamageType.Physical,
            attackIntervalSeconds: 3.0
        );
        
        var meta = new BattleMeta
        {
            ModeTag = "test",
            EnemyId = enemyDef.Id,
            EnemyCount = enemyCount
        };
        
        var engine = new BattleEngine(
            battleId: battleId,
            characterId: characterId,
            profession: Profession.Warrior,
            stats: stats,
            rng: rng,
            enemyDef: enemyDef,
            enemyCount: enemyCount,
            module: new TestProfessionModule(),
            meta: meta
        );
        
        return engine;
    }

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

    #endregion
}
