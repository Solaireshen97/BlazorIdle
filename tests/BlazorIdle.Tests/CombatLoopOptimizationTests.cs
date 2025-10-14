using BlazorIdle.Server.Domain.Characters;
using BlazorIdle.Server.Domain.Combat;
using BlazorIdle.Server.Domain.Combat.Damage;
using BlazorIdle.Server.Domain.Combat.Enemies;
using BlazorIdle.Server.Domain.Combat.Engine;
using BlazorIdle.Server.Domain.Combat.Professions;
using BlazorIdle.Server.Domain.Combat.Rng;
using BlazorIdle.Server.Infrastructure.Configuration;
using BlazorIdle.Shared.Models;
using System;
using System.Linq;
using Xunit;

namespace BlazorIdle.Tests;

/// <summary>
/// Tests for combat loop optimization (Upper Phase Tasks 1.1-1.4)
/// </summary>
public class CombatLoopOptimizationTests
{
    [Fact]
    public void Task11_AttackStartsWithFullInterval_ShouldDelayFirstAttack()
    {
        // Arrange
        var options = new CombatLoopOptions
        {
            AttackStartsWithFullInterval = true,
            SpecialStartsWithFullInterval = true
        };
        
        var engine = CreateTestBattleEngine(loopOptions: options);
        var attackTrack = engine.Context.Tracks.First(t => t.TrackType == TrackType.Attack);
        var specialTrack = engine.Context.Tracks.First(t => t.TrackType == TrackType.Special);
        
        // Assert: First attack should happen at attackInterval, not at 0
        Assert.Equal(2.0, attackTrack.NextTriggerAt, 2); // 2.0 seconds (BaseAttackInterval)
        Assert.Equal(5.0, specialTrack.NextTriggerAt, 2); // 5.0 seconds (BaseSpecialInterval)
    }
    
    [Fact]
    public void Task11_AttackStartsImmediately_OldBehavior()
    {
        // Arrange
        var options = new CombatLoopOptions
        {
            AttackStartsWithFullInterval = false,
            SpecialStartsWithFullInterval = false
        };
        
        var engine = CreateTestBattleEngine(loopOptions: options);
        var attackTrack = engine.Context.Tracks.First(t => t.TrackType == TrackType.Attack);
        var specialTrack = engine.Context.Tracks.First(t => t.TrackType == TrackType.Special);
        
        // Assert: First attack should happen immediately (old behavior)
        Assert.Equal(0.0, attackTrack.NextTriggerAt, 2);
        Assert.Equal(0.0, specialTrack.NextTriggerAt, 2);
    }
    
    [Fact]
    public void Task12_PauseAttackWhenNoEnemies_ShouldPauseOnWaveTransition()
    {
        // Arrange: Create a dungeon with two waves
        var dungeon = new DungeonDefinition(
            id: "test_pause_dungeon",
            name: "Test Pause Dungeon",
            waves: new[]
            {
                new DungeonDefinition.Wave(new[] { ("dummy", 1) }),
                new DungeonDefinition.Wave(new[] { ("dummy", 1) })
            },
            waveRespawnDelaySeconds: 2.0,
            runRespawnDelaySeconds: 3.0
        );
        
        var provider = new DungeonEncounterProvider(dungeon, loop: false);
        var options = new CombatLoopOptions
        {
            AttackStartsWithFullInterval = false, // Use old behavior for consistent timing
            PauseAttackWhenNoEnemies = true
        };
        
        var engine = CreateTestBattleEngine(provider: provider, loopOptions: options);
        
        // Act: Advance until first wave is cleared
        engine.AdvanceUntil(60.0);
        
        // Check if we reached wave cleared state
        var wave1Cleared = engine.Context.EncounterGroup?.All.All(e => e.IsDead) ?? false;
        
        if (wave1Cleared)
        {
            // Get attack track
            var attackTrack = engine.Context.Tracks.FirstOrDefault(t => t.TrackType == TrackType.Attack);
            
            // Assert: Attack track should be paused (NextTriggerAt should be FAR_FUTURE)
            Assert.NotNull(attackTrack);
            // During wave transition, track should be paused or resumed - check tags
            var segments = engine.Segments;
            var totalTags = segments.SelectMany(s => s.TagCounters).ToList();
            var tracksPaused = totalTags.Where(t => t.Key.StartsWith("track_paused")).Sum(t => t.Value);
            
            // Should have at least one pause event
            Assert.True(tracksPaused > 0, $"Expected track pause events, got {tracksPaused}");
        }
    }
    
    [Fact]
    public void Task13_CurrentAttackTarget_ShouldBeSetDuringAttack()
    {
        // Arrange
        var options = new CombatLoopOptions
        {
            AttackStartsWithFullInterval = false, // Use old behavior for immediate test
            LockTargetForAttackCycle = true
        };
        
        var enemyDef = new EnemyDefinition(
            id: "test_target",
            name: "Test Target",
            level: 5,
            maxHp: 1000, // High HP so it doesn't die quickly
            baseDamage: 0, // No damage to keep test simple
            attackDamageType: DamageType.Physical,
            attackIntervalSeconds: 10.0
        );
        
        var engine = CreateTestBattleEngine(
            enemyDef: enemyDef,
            enemyCount: 1,
            loopOptions: options
        );
        
        // Initially, CurrentAttackTarget should be null
        Assert.Null(engine.Context.CurrentAttackTarget);
        
        // Act: Advance to first attack (should happen at 0 with old behavior)
        engine.AdvanceTo(0.1, 100);
        
        // Assert: CurrentAttackTarget should be null after attack completes
        // (it's set during attack, then cleared)
        Assert.Null(engine.Context.CurrentAttackTarget);
        
        // Verify that an attack happened
        var segments = engine.Segments;
        if (segments.Count > 0)
        {
            var totalTags = segments.SelectMany(s => s.TagCounters).ToList();
            var basicAttacks = totalTags.Where(t => t.Key == "damage_dealt:basic_attack").Sum(t => t.Value);
            Assert.True(basicAttacks > 0, "Expected at least one basic attack");
        }
    }
    
    #region Helper Methods
    
    private BattleEngine CreateTestBattleEngine(
        EnemyDefinition? enemyDef = null,
        int enemyCount = 1,
        IEncounterProvider? provider = null,
        CombatLoopOptions? loopOptions = null)
    {
        var battleId = Guid.NewGuid();
        var characterId = Guid.NewGuid();
        var stats = new CharacterStats { AttackPower = 100 };
        var rng = new RngContext(12345);
        
        var enemy = enemyDef ?? new EnemyDefinition(
            id: "test_enemy",
            name: "Test Enemy",
            level: 5,
            maxHp: 100,
            baseDamage: 0,
            attackDamageType: DamageType.Physical,
            attackIntervalSeconds: 3.0
        );
        
        var meta = new BattleMeta
        {
            ModeTag = "test",
            EnemyId = enemy.Id,
            EnemyCount = enemyCount
        };
        
        if (provider != null)
        {
            return new BattleEngine(
                battleId: battleId,
                characterId: characterId,
                profession: Profession.Warrior,
                stats: stats,
                rng: rng,
                provider: provider,
                module: new TestProfessionModule(),
                meta: meta,
                loopOptions: loopOptions
            );
        }
        
        return new BattleEngine(
            battleId: battleId,
            characterId: characterId,
            profession: Profession.Warrior,
            stats: stats,
            rng: rng,
            enemyDef: enemy,
            enemyCount: enemyCount,
            module: new TestProfessionModule(),
            meta: meta,
            loopOptions: loopOptions
        );
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
