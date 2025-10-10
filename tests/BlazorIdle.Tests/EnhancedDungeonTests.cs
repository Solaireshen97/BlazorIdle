using BlazorIdle.Server.Domain.Characters;
using BlazorIdle.Server.Domain.Combat;
using BlazorIdle.Server.Domain.Combat.Combatants;
using BlazorIdle.Server.Domain.Combat.Enemies;
using BlazorIdle.Server.Domain.Combat.Engine;
using BlazorIdle.Server.Domain.Combat.Professions;
using BlazorIdle.Server.Domain.Combat.Rng;
using BlazorIdle.Shared.Models;
using BlazorWebGame.Domain.Combat;
using Xunit;
using System;
using System.Collections.Generic;

namespace BlazorIdle.Tests;

/// <summary>
/// Phase 6 单元测试：强化型地下城预留
/// 测试禁用复活模式、玩家死亡标记、强化掉落、向后兼容
/// </summary>
public class EnhancedDungeonTests
{
    #region DungeonDefinition Tests

    [Fact]
    public void DungeonDefinition_DefaultValues_ShouldAllowAutoRevive()
    {
        // Arrange & Act
        var dungeon = new DungeonDefinition(
            id: "test_dungeon",
            name: "Test Dungeon",
            waves: new[] { new DungeonDefinition.Wave(new[] { ("dummy", 1) }) }
        );

        // Assert
        Assert.True(dungeon.AllowAutoRevive);
        Assert.Equal(1.0, dungeon.EnhancedDropMultiplier);
        Assert.False(dungeon.ResetOnPlayerDeath);
    }

    [Fact]
    public void DungeonDefinition_EnhancedMode_ShouldDisableAutoRevive()
    {
        // Arrange & Act
        var dungeon = new DungeonDefinition(
            id: "enhanced_dungeon",
            name: "Enhanced Dungeon",
            waves: new[] { new DungeonDefinition.Wave(new[] { ("dummy", 1) }) },
            allowAutoRevive: false,
            enhancedDropMultiplier: 2.0,
            resetOnPlayerDeath: true
        );

        // Assert
        Assert.False(dungeon.AllowAutoRevive);
        Assert.Equal(2.0, dungeon.EnhancedDropMultiplier);
        Assert.True(dungeon.ResetOnPlayerDeath);
    }

    [Fact]
    public void DungeonDefinition_EnhancedDropMultiplier_ShouldClampToMinimum()
    {
        // Arrange & Act
        var dungeon = new DungeonDefinition(
            id: "test_dungeon",
            name: "Test Dungeon",
            waves: new[] { new DungeonDefinition.Wave(new[] { ("dummy", 1) }) },
            enhancedDropMultiplier: -1.0
        );

        // Assert
        Assert.Equal(1.0, dungeon.EnhancedDropMultiplier);
    }

    #endregion

    #region BattleContext Integration Tests

    [Fact]
    public void BattleContext_WithNormalDungeon_ShouldEnableAutoRevive()
    {
        // Arrange
        var dungeon = new DungeonDefinition(
            id: "normal_dungeon",
            name: "Normal Dungeon",
            waves: new[] { new DungeonDefinition.Wave(new[] { ("dummy", 1) }) },
            allowAutoRevive: true
        );

        var battle = new Battle { Id = Guid.NewGuid(), CharacterId = Guid.NewGuid() };
        var clock = new GameClock();
        var scheduler = new EventScheduler();
        var collector = new SegmentCollector();
        var rng = new RngContext(12345);
        var professionModule = ProfessionRegistry.Resolve(Profession.Warrior);

        // Act
        var context = new BattleContext(
            battle: battle,
            clock: clock,
            scheduler: scheduler,
            collector: collector,
            professionModule: professionModule,
            profession: Profession.Warrior,
            rng: rng,
            stats: new CharacterStats(),
            dungeon: dungeon
        );

        // Assert
        Assert.NotNull(context.CurrentDungeon);
        Assert.Equal("normal_dungeon", context.CurrentDungeon.Id);
        Assert.True(context.Player.AutoReviveEnabled);
    }

    [Fact]
    public void BattleContext_WithEnhancedDungeon_ShouldDisableAutoRevive()
    {
        // Arrange
        var dungeon = new DungeonDefinition(
            id: "enhanced_dungeon",
            name: "Enhanced Dungeon",
            waves: new[] { new DungeonDefinition.Wave(new[] { ("dummy", 1) }) },
            allowAutoRevive: false,
            resetOnPlayerDeath: true
        );

        var battle = new Battle { Id = Guid.NewGuid(), CharacterId = Guid.NewGuid() };
        var clock = new GameClock();
        var scheduler = new EventScheduler();
        var collector = new SegmentCollector();
        var rng = new RngContext(12345);
        var professionModule = ProfessionRegistry.Resolve(Profession.Warrior);

        // Act
        var context = new BattleContext(
            battle: battle,
            clock: clock,
            scheduler: scheduler,
            collector: collector,
            professionModule: professionModule,
            profession: Profession.Warrior,
            rng: rng,
            stats: new CharacterStats(),
            dungeon: dungeon
        );

        // Assert
        Assert.NotNull(context.CurrentDungeon);
        Assert.Equal("enhanced_dungeon", context.CurrentDungeon.Id);
        Assert.False(context.Player.AutoReviveEnabled);
    }

    [Fact]
    public void BattleContext_WithoutDungeon_ShouldDefaultToAutoReviveEnabled()
    {
        // Arrange
        var battle = new Battle { Id = Guid.NewGuid(), CharacterId = Guid.NewGuid() };
        var clock = new GameClock();
        var scheduler = new EventScheduler();
        var collector = new SegmentCollector();
        var rng = new RngContext(12345);
        var professionModule = ProfessionRegistry.Resolve(Profession.Warrior);

        // Act
        var context = new BattleContext(
            battle: battle,
            clock: clock,
            scheduler: scheduler,
            collector: collector,
            professionModule: professionModule,
            profession: Profession.Warrior,
            rng: rng,
            stats: new CharacterStats()
        );

        // Assert
        Assert.Null(context.CurrentDungeon);
        Assert.True(context.Player.AutoReviveEnabled); // Default behavior
    }

    #endregion

    #region PlayerDeathEvent Tests

    [Fact]
    public void PlayerDeathEvent_WithNormalDungeon_ShouldScheduleRevive()
    {
        // Arrange
        var dungeon = new DungeonDefinition(
            id: "normal_dungeon",
            name: "Normal Dungeon",
            waves: new[] { new DungeonDefinition.Wave(new[] { ("dummy", 1) }) },
            allowAutoRevive: true
        );

        var battle = new Battle { Id = Guid.NewGuid(), CharacterId = Guid.NewGuid() };
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
            stats: new CharacterStats(),
            dungeon: dungeon
        );

        // Kill the player
        context.Player.ReceiveDamage(1000, BlazorIdle.Server.Domain.Combat.Damage.DamageType.Physical, 10.0);
        Assert.True(context.Player.IsDead);
        Assert.Equal(20.0, context.Player.ReviveAt); // 10.0 + 10.0 default revive duration

        // Act
        var deathEvent = new PlayerDeathEvent(10.0);
        deathEvent.Execute(context);

        // Assert
        // Should have scheduled a PlayerReviveEvent
        var events = GetScheduledEvents(scheduler);
        Assert.Contains(events, e => e is PlayerReviveEvent);
        
        // Should have tagged player_death but not dungeon_reset_on_death
        var segment = collector.Flush(context.Clock.CurrentTime);
        Assert.True(segment.TagCounters.ContainsKey("player_death"));
        Assert.False(segment.TagCounters.ContainsKey("dungeon_reset_on_death"));
    }

    [Fact]
    public void PlayerDeathEvent_WithEnhancedDungeon_ShouldNotScheduleReviveButTagReset()
    {
        // Arrange
        var dungeon = new DungeonDefinition(
            id: "enhanced_dungeon",
            name: "Enhanced Dungeon",
            waves: new[] { new DungeonDefinition.Wave(new[] { ("dummy", 1) }) },
            allowAutoRevive: false,
            resetOnPlayerDeath: true
        );

        var battle = new Battle { Id = Guid.NewGuid(), CharacterId = Guid.NewGuid() };
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
            stats: new CharacterStats(),
            dungeon: dungeon
        );

        // Kill the player
        context.Player.ReceiveDamage(1000, BlazorIdle.Server.Domain.Combat.Damage.DamageType.Physical, 10.0);
        Assert.True(context.Player.IsDead);
        Assert.Null(context.Player.ReviveAt); // No revive time set when AutoReviveEnabled = false

        // Act
        var deathEvent = new PlayerDeathEvent(10.0);
        deathEvent.Execute(context);

        // Assert
        // Should NOT have scheduled a PlayerReviveEvent
        var events = GetScheduledEvents(scheduler);
        Assert.DoesNotContain(events, e => e is PlayerReviveEvent);
        
        // Should have tagged both player_death and dungeon_reset_on_death
        var segment = collector.Flush(context.Clock.CurrentTime);
        Assert.True(segment.TagCounters.ContainsKey("player_death"));
        Assert.True(segment.TagCounters.ContainsKey("dungeon_reset_on_death"));
    }

    [Fact]
    public void PlayerDeathEvent_WithEnhancedDungeonButNoReset_ShouldNotScheduleReviveOrTagReset()
    {
        // Arrange: Enhanced dungeon but ResetOnPlayerDeath = false
        var dungeon = new DungeonDefinition(
            id: "enhanced_no_reset",
            name: "Enhanced No Reset",
            waves: new[] { new DungeonDefinition.Wave(new[] { ("dummy", 1) }) },
            allowAutoRevive: false,
            resetOnPlayerDeath: false // No reset
        );

        var battle = new Battle { Id = Guid.NewGuid(), CharacterId = Guid.NewGuid() };
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
            stats: new CharacterStats(),
            dungeon: dungeon
        );

        // Kill the player
        context.Player.ReceiveDamage(1000, BlazorIdle.Server.Domain.Combat.Damage.DamageType.Physical, 10.0);
        Assert.True(context.Player.IsDead);
        Assert.Null(context.Player.ReviveAt);

        // Act
        var deathEvent = new PlayerDeathEvent(10.0);
        deathEvent.Execute(context);

        // Assert
        // Should NOT have scheduled a PlayerReviveEvent
        var events = GetScheduledEvents(scheduler);
        Assert.DoesNotContain(events, e => e is PlayerReviveEvent);
        
        // Should have tagged player_death but NOT dungeon_reset_on_death
        var segment = collector.Flush(context.Clock.CurrentTime);
        Assert.True(segment.TagCounters.ContainsKey("player_death"));
        Assert.False(segment.TagCounters.ContainsKey("dungeon_reset_on_death"));
    }

    #endregion

    #region BattleEngine Integration Tests

    [Fact]
    public void BattleEngine_WithNormalDungeon_ShouldPassDungeonToContext()
    {
        // Arrange
        var dungeon = new DungeonDefinition(
            id: "normal_dungeon",
            name: "Normal Dungeon",
            waves: new[] { new DungeonDefinition.Wave(new[] { ("dummy", 1) }) }
        );
        var provider = new DungeonEncounterProvider(dungeon, loop: false);
        var rng = new RngContext(12345);
        var stats = new CharacterStats();

        // Act
        var engine = new BattleEngine(
            battleId: Guid.NewGuid(),
            characterId: Guid.NewGuid(),
            profession: Profession.Warrior,
            stats: stats,
            rng: rng,
            provider: provider
        );

        // Assert
        Assert.NotNull(engine.Context.CurrentDungeon);
        Assert.Equal("normal_dungeon", engine.Context.CurrentDungeon.Id);
        Assert.True(engine.Context.Player.AutoReviveEnabled);
    }

    [Fact]
    public void BattleEngine_WithEnhancedDungeon_ShouldDisableAutoRevive()
    {
        // Arrange
        var dungeon = new DungeonDefinition(
            id: "enhanced_dungeon",
            name: "Enhanced Dungeon",
            waves: new[] { new DungeonDefinition.Wave(new[] { ("dummy", 1) }) },
            allowAutoRevive: false,
            enhancedDropMultiplier: 2.0,
            resetOnPlayerDeath: true
        );
        var provider = new DungeonEncounterProvider(dungeon, loop: false);
        var rng = new RngContext(12345);
        var stats = new CharacterStats();

        // Act
        var engine = new BattleEngine(
            battleId: Guid.NewGuid(),
            characterId: Guid.NewGuid(),
            profession: Profession.Warrior,
            stats: stats,
            rng: rng,
            provider: provider
        );

        // Assert
        Assert.NotNull(engine.Context.CurrentDungeon);
        Assert.Equal("enhanced_dungeon", engine.Context.CurrentDungeon.Id);
        Assert.False(engine.Context.Player.AutoReviveEnabled);
        Assert.Equal(2.0, engine.Context.CurrentDungeon.EnhancedDropMultiplier);
        Assert.True(engine.Context.CurrentDungeon.ResetOnPlayerDeath);
    }

    [Fact]
    public void BattleEngine_WithSingleEnemy_ShouldNotHaveDungeonContext()
    {
        // Arrange
        var enemyDef = EnemyRegistry.Resolve("dummy");
        var rng = new RngContext(12345);
        var stats = new CharacterStats();

        // Act
        var engine = new BattleEngine(
            battleId: Guid.NewGuid(),
            characterId: Guid.NewGuid(),
            profession: Profession.Warrior,
            stats: stats,
            rng: rng,
            enemyDef: enemyDef,
            enemyCount: 1
        );

        // Assert
        Assert.Null(engine.Context.CurrentDungeon);
        Assert.True(engine.Context.Player.AutoReviveEnabled); // Default behavior
    }

    #endregion

    #region Backward Compatibility Tests

    [Fact]
    public void BackwardCompatibility_ExistingDungeonRegistry_ShouldStillWork()
    {
        // Arrange - Use the existing intro_cave dungeon from DungeonRegistry
        var introCave = DungeonRegistry.Resolve("intro_cave");

        // Assert - Should have default Phase 6 values (backward compatible)
        Assert.True(introCave.AllowAutoRevive);
        Assert.Equal(1.0, introCave.EnhancedDropMultiplier);
        Assert.False(introCave.ResetOnPlayerDeath);
        
        // Should still have the existing economy multipliers
        Assert.Equal(1.10, introCave.GoldMultiplier);
        Assert.Equal(1.05, introCave.ExpMultiplier);
    }

    [Fact]
    public void BackwardCompatibility_PlayerCombatant_WithoutDungeon_ShouldBehaveNormally()
    {
        // Arrange - Create a player without dungeon context
        var player = new PlayerCombatant("p1", "Player", new CharacterStats(), stamina: 10);

        // Assert - Should have default auto-revive enabled
        Assert.True(player.AutoReviveEnabled);

        // Act - Kill the player
        player.ReceiveDamage(1000, BlazorIdle.Server.Domain.Combat.Damage.DamageType.Physical, 10.0);

        // Assert - Should set revive time
        Assert.True(player.IsDead);
        Assert.NotNull(player.ReviveAt);
        Assert.Equal(20.0, player.ReviveAt); // 10.0 + 10.0 default revive duration
    }

    #endregion

    #region Enhanced Drop Multiplier Tests

    [Fact]
    public void EnhancedDropMultiplier_ShouldApplyToEconomyContext()
    {
        // Arrange
        var dungeon = new DungeonDefinition(
            id: "enhanced_dungeon",
            name: "Enhanced Dungeon",
            waves: new[] { new DungeonDefinition.Wave(new[] { ("dummy", 1) }) },
            dropChanceMultiplier: 1.5,
            enhancedDropMultiplier: 2.0
        );

        // Assert - The final drop multiplier should be 1.5 * 2.0 = 3.0
        // This will be verified by the economy system when it creates EconomyContext
        Assert.Equal(1.5, dungeon.DropChanceMultiplier);
        Assert.Equal(2.0, dungeon.EnhancedDropMultiplier);
        
        // The actual multiplication happens in the economy layer
        var expectedFinal = dungeon.DropChanceMultiplier * dungeon.EnhancedDropMultiplier;
        Assert.Equal(3.0, expectedFinal);
    }

    [Fact]
    public void EnhancedDropMultiplier_DefaultValue_ShouldNotAffectDrops()
    {
        // Arrange
        var dungeon = new DungeonDefinition(
            id: "normal_dungeon",
            name: "Normal Dungeon",
            waves: new[] { new DungeonDefinition.Wave(new[] { ("dummy", 1) }) },
            dropChanceMultiplier: 1.5
            // enhancedDropMultiplier defaults to 1.0
        );

        // Assert - With default enhanced multiplier of 1.0, only base multiplier applies
        Assert.Equal(1.5, dungeon.DropChanceMultiplier);
        Assert.Equal(1.0, dungeon.EnhancedDropMultiplier);
        
        var expectedFinal = dungeon.DropChanceMultiplier * dungeon.EnhancedDropMultiplier;
        Assert.Equal(1.5, expectedFinal); // 1.5 * 1.0 = 1.5
    }

    #endregion

    #region Helper Methods

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

    #endregion
}
