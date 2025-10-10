using BlazorIdle.Server.Domain.Characters;
using BlazorIdle.Server.Domain.Combat.Combatants;
using BlazorIdle.Server.Domain.Combat.Enemies;
using BlazorIdle.Server.Domain.Combat.Engine;
using BlazorIdle.Server.Domain.Combat.Rng;
using BlazorIdle.Shared.Models;
using BlazorWebGame.Domain.Combat;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace BlazorIdle.Tests;

/// <summary>
/// Phase 6 单元测试：强化型地下城预留系统
/// 测试禁用自动复活、玩家死亡触发重置、强化掉落倍率等功能
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
            waves: new List<DungeonDefinition.Wave>
            {
                new DungeonDefinition.Wave(new List<(string, int)> { ("slime", 1) })
            }
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
            waves: new List<DungeonDefinition.Wave>
            {
                new DungeonDefinition.Wave(new List<(string, int)> { ("slime", 2) })
            },
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
    public void DungeonDefinition_NegativeEnhancedDropMultiplier_ShouldDefaultToOne()
    {
        // Arrange & Act
        var dungeon = new DungeonDefinition(
            id: "test_dungeon",
            name: "Test Dungeon",
            waves: new List<DungeonDefinition.Wave>
            {
                new DungeonDefinition.Wave(new List<(string, int)> { ("slime", 1) })
            },
            enhancedDropMultiplier: -1.0
        );

        // Assert
        Assert.Equal(1.0, dungeon.EnhancedDropMultiplier);
    }

    #endregion

    #region PlayerCombatant AutoReviveAllowed Tests

    [Fact]
    public void PlayerCombatant_AutoReviveAllowed_DefaultTrue()
    {
        // Arrange & Act
        var player = new PlayerCombatant("p1", "Player", new CharacterStats(), stamina: 10);

        // Assert
        Assert.True(player.AutoReviveAllowed);
    }

    [Fact]
    public void PlayerCombatant_AutoReviveAllowed_CanBeSetToFalse()
    {
        // Arrange
        var player = new PlayerCombatant("p1", "Player", new CharacterStats(), stamina: 10)
        {
            AutoReviveAllowed = false
        };

        // Act
        var actualDamage = player.ReceiveDamage(120, Server.Domain.Combat.Damage.DamageType.Physical, 1.0);

        // Assert
        Assert.Equal(100, actualDamage);
        Assert.Equal(0, player.CurrentHp);
        Assert.True(player.IsDead);
        Assert.False(player.ReviveAt.HasValue); // No revive time set because AutoReviveAllowed is false
    }

    [Fact]
    public void PlayerCombatant_BothAutoReviveEnabledAndAllowed_ShouldSetReviveTime()
    {
        // Arrange
        var player = new PlayerCombatant("p1", "Player", new CharacterStats(), stamina: 10)
        {
            AutoReviveEnabled = true,
            AutoReviveAllowed = true
        };

        // Act
        player.ReceiveDamage(120, Server.Domain.Combat.Damage.DamageType.Physical, 1.0);

        // Assert
        Assert.True(player.ReviveAt.HasValue);
        Assert.Equal(11.0, player.ReviveAt.Value);
    }

    [Fact]
    public void PlayerCombatant_AutoReviveEnabledButNotAllowed_ShouldNotSetReviveTime()
    {
        // Arrange
        var player = new PlayerCombatant("p1", "Player", new CharacterStats(), stamina: 10)
        {
            AutoReviveEnabled = true,
            AutoReviveAllowed = false
        };

        // Act
        player.ReceiveDamage(120, Server.Domain.Combat.Damage.DamageType.Physical, 1.0);

        // Assert
        Assert.False(player.ReviveAt.HasValue);
    }

    #endregion

    #region BattleEngine Integration Tests

    [Fact]
    public void BattleEngine_NormalDungeon_ShouldAllowAutoRevive()
    {
        // Arrange
        var battleId = Guid.NewGuid();
        var characterId = Guid.NewGuid();
        var stats = new CharacterStats();
        var rng = new RngContext(12345);

        var dungeon = new DungeonDefinition(
            id: "normal_dungeon",
            name: "Normal Dungeon",
            waves: new List<DungeonDefinition.Wave>
            {
                new DungeonDefinition.Wave(new List<(string, int)> { ("slime", 1) })
            }
        );

        var provider = new DungeonEncounterProvider(dungeon, loop: false);

        // Act
        var engine = new BattleEngine(
            battleId: battleId,
            characterId: characterId,
            profession: Profession.Warrior,
            stats: stats,
            rng: rng,
            provider: provider
        );

        // Assert
        Assert.NotNull(engine.Context.Player);
        Assert.True(engine.Context.Player.AutoReviveAllowed);
        Assert.False(engine.ResetTriggered);
    }

    [Fact]
    public void BattleEngine_EnhancedDungeon_ShouldDisableAutoRevive()
    {
        // Arrange
        var battleId = Guid.NewGuid();
        var characterId = Guid.NewGuid();
        var stats = new CharacterStats();
        var rng = new RngContext(12345);

        var dungeon = new DungeonDefinition(
            id: "enhanced_dungeon",
            name: "Enhanced Dungeon",
            waves: new List<DungeonDefinition.Wave>
            {
                new DungeonDefinition.Wave(new List<(string, int)> { ("slime", 1) })
            },
            allowAutoRevive: false,
            resetOnPlayerDeath: true
        );

        var provider = new DungeonEncounterProvider(dungeon, loop: false);

        // Act
        var engine = new BattleEngine(
            battleId: battleId,
            characterId: characterId,
            profession: Profession.Warrior,
            stats: stats,
            rng: rng,
            provider: provider
        );

        // Assert
        Assert.NotNull(engine.Context.Player);
        Assert.False(engine.Context.Player.AutoReviveAllowed);
        Assert.False(engine.ResetTriggered); // Not triggered yet
    }

    [Fact]
    public void BattleEngine_EnhancedDungeon_PlayerDeathShouldNotScheduleRevive()
    {
        // Arrange
        var battleId = Guid.NewGuid();
        var characterId = Guid.NewGuid();
        var stats = new CharacterStats();
        var rng = new RngContext(12345);

        var dungeon = new DungeonDefinition(
            id: "enhanced_dungeon",
            name: "Enhanced Dungeon",
            waves: new List<DungeonDefinition.Wave>
            {
                new DungeonDefinition.Wave(new List<(string, int)> { ("slime", 1) })
            },
            allowAutoRevive: false,
            resetOnPlayerDeath: true
        );

        var provider = new DungeonEncounterProvider(dungeon, loop: false);

        var engine = new BattleEngine(
            battleId: battleId,
            characterId: characterId,
            profession: Profession.Warrior,
            stats: stats,
            rng: rng,
            provider: provider
        );

        // Act - Kill player directly
        engine.Context.Player.ReceiveDamage(100, Server.Domain.Combat.Damage.DamageType.Physical, 1.0);

        // Assert
        Assert.True(engine.Context.Player.IsDead);
        Assert.False(engine.Context.Player.ReviveAt.HasValue); // No revive scheduled because AutoReviveAllowed is false
        Assert.Equal(CombatantState.Dead, engine.Context.Player.State);
    }

    [Fact]
    public void BattleEngine_NormalDungeon_PlayerDeathShouldScheduleRevive()
    {
        // Arrange
        var battleId = Guid.NewGuid();
        var characterId = Guid.NewGuid();
        var stats = new CharacterStats();
        var rng = new RngContext(12345);

        var dungeon = new DungeonDefinition(
            id: "normal_dungeon",
            name: "Normal Dungeon",
            waves: new List<DungeonDefinition.Wave>
            {
                new DungeonDefinition.Wave(new List<(string, int)> { ("slime", 1) })
            }
        );

        var provider = new DungeonEncounterProvider(dungeon, loop: false);

        var engine = new BattleEngine(
            battleId: battleId,
            characterId: characterId,
            profession: Profession.Warrior,
            stats: stats,
            rng: rng,
            provider: provider
        );

        // Act - Kill player directly
        engine.Context.Player.ReceiveDamage(100, Server.Domain.Combat.Damage.DamageType.Physical, 1.0);

        // Assert
        Assert.True(engine.Context.Player.IsDead);
        Assert.True(engine.Context.Player.ReviveAt.HasValue); // Revive scheduled for normal dungeon
        Assert.Equal(CombatantState.Dead, engine.Context.Player.State);
    }

    [Fact]
    public void BattleEngine_SingleEnemy_ShouldNotApplyDungeonConfig()
    {
        // Arrange - Test backward compatibility with single enemy battles
        var battleId = Guid.NewGuid();
        var characterId = Guid.NewGuid();
        var stats = new CharacterStats();
        var rng = new RngContext(12345);
        var enemyDef = EnemyRegistry.Resolve("slime");

        // Act
        var engine = new BattleEngine(
            battleId: battleId,
            characterId: characterId,
            profession: Profession.Warrior,
            stats: stats,
            rng: rng,
            enemyDef: enemyDef,
            enemyCount: 1
        );

        // Assert - Should use default values
        Assert.True(engine.Context.Player.AutoReviveAllowed);
        Assert.False(engine.ResetTriggered);
    }

    #endregion

    #region Stats Collection Tests

    [Fact]
    public void BattleEngine_EnhancedDungeon_ShouldRecordContextTags()
    {
        // Arrange
        var battleId = Guid.NewGuid();
        var characterId = Guid.NewGuid();
        var stats = new CharacterStats();
        var rng = new RngContext(12345);

        var dungeon = new DungeonDefinition(
            id: "enhanced_dungeon",
            name: "Enhanced Dungeon",
            waves: new List<DungeonDefinition.Wave>
            {
                new DungeonDefinition.Wave(new List<(string, int)> { ("slime", 1) })
            },
            allowAutoRevive: false,
            enhancedDropMultiplier: 2.0,
            resetOnPlayerDeath: true
        );

        var provider = new DungeonEncounterProvider(dungeon, loop: false);

        // Act
        var engine = new BattleEngine(
            battleId: battleId,
            characterId: characterId,
            profession: Profession.Warrior,
            stats: stats,
            rng: rng,
            provider: provider
        );

        // Advance battle a bit to generate at least one segment
        engine.AdvanceTo(1.0, 100);
        engine.FinalizeNow();
        
        var allTags = engine.Segments.SelectMany(s => s.TagCounters.Keys).Distinct().ToList();

        // Assert - Check for enhanced dungeon tags
        Assert.Contains("ctx.enhanced_dungeon", allTags);
        Assert.Contains("ctx.auto_revive_disabled", allTags);
        Assert.Contains("ctx.reset_on_death", allTags);
        Assert.Contains("ctx.enhanced_drops", allTags);
    }

    [Fact]
    public void BattleEngine_EnhancedDungeon_PlayerDeathWithoutRevive()
    {
        // Arrange
        var battleId = Guid.NewGuid();
        var characterId = Guid.NewGuid();
        var stats = new CharacterStats();
        var rng = new RngContext(12345);

        var dungeon = new DungeonDefinition(
            id: "enhanced_dungeon",
            name: "Enhanced Dungeon",
            waves: new List<DungeonDefinition.Wave>
            {
                new DungeonDefinition.Wave(new List<(string, int)> { ("slime", 1) })
            },
            allowAutoRevive: false,
            resetOnPlayerDeath: true
        );

        var provider = new DungeonEncounterProvider(dungeon, loop: false);

        var engine = new BattleEngine(
            battleId: battleId,
            characterId: characterId,
            profession: Profession.Warrior,
            stats: stats,
            rng: rng,
            provider: provider
        );

        // Act - Kill player directly (simulating enemy damage)
        var player = engine.Context.Player;
        player.ReceiveDamage(100, Server.Domain.Combat.Damage.DamageType.Physical, 1.0);

        // Assert - Player should be dead without revive scheduled
        Assert.True(player.IsDead);
        Assert.False(player.ReviveAt.HasValue);
        Assert.Equal(CombatantState.Dead, player.State);
        
        // Verify that AutoReviveAllowed is false due to dungeon configuration
        Assert.False(player.AutoReviveAllowed);
    }

    #endregion
}
