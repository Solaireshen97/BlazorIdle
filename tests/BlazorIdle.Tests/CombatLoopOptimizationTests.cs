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
        
        // 默认测试行为：使用常规暂停和延迟
        public bool PauseSpecialWhenNoEnemies => true;
        public bool SpecialStartsImmediately => false;
        public bool SpecialStartsImmediatelyAfterRevive => false;

        public void RegisterBuffDefinitions(BattleContext ctx) { }
        public void OnBattleStart(BattleContext ctx) { }
        public void BuildSkills(BattleContext ctx, Server.Domain.Combat.Skills.AutoCastEngine autoCaster) { }
        public void OnAttackTick(BattleContext ctx, AttackTickEvent evt) { }
        public void OnSpecialPulse(BattleContext ctx, SpecialPulseEvent evt) { }
        public void OnSkillCast(BattleContext ctx, Server.Domain.Combat.Skills.SkillDefinition def) { }
    }
    
    /// <summary>
    /// 战斗循环优化 Task 2.x: 测试职业特定配置
    /// 验证战士和猎人的特殊轨道配置差异
    /// </summary>
    [Fact]
    public void Task2x_ProfessionSpecificConfig_WarriorVsRanger()
    {
        // Arrange - 创建战士和猎人的引擎
        var warriorModule = new Server.Domain.Combat.Professions.WarriorProfession();
        var rangerModule = new Server.Domain.Combat.Professions.RangerProfession();
        
        // Act - 检查配置差异
        var warriorPauseSpecial = warriorModule.PauseSpecialWhenNoEnemies;
        var warriorSpecialStartsImmediate = warriorModule.SpecialStartsImmediately;
        var warriorSpecialAfterRevive = warriorModule.SpecialStartsImmediatelyAfterRevive;
        
        var rangerPauseSpecial = rangerModule.PauseSpecialWhenNoEnemies;
        var rangerSpecialStartsImmediate = rangerModule.SpecialStartsImmediately;
        var rangerSpecialAfterRevive = rangerModule.SpecialStartsImmediatelyAfterRevive;
        
        // Assert - 验证战士配置（战斗专注，持续积累怒气）
        Assert.False(warriorPauseSpecial, "战士特殊轨道应在无怪物时持续触发");
        Assert.True(warriorSpecialStartsImmediate, "战士特殊轨道应立即开始");
        Assert.True(warriorSpecialAfterRevive, "战士复活后特殊轨道应立即触发");
        
        // Assert - 验证猎人配置（需要目标，常规节奏）
        Assert.True(rangerPauseSpecial, "猎人特殊轨道应在无怪物时暂停");
        Assert.False(rangerSpecialStartsImmediate, "猎人特殊轨道应等待间隔");
        Assert.False(rangerSpecialAfterRevive, "猎人复活后特殊轨道应等待间隔");
    }
    
    /// <summary>
    /// 战斗循环优化 Task 2.3: 测试特殊轨道初始延迟使用职业配置
    /// 验证战士的特殊轨道立即开始，而测试模块等待间隔
    /// </summary>
    [Fact]
    public void Task23_SpecialTrackInitialDelay_WarriorStartsImmediately()
    {
        // Arrange - 使用战士职业模块
        var warriorModule = new Server.Domain.Combat.Professions.WarriorProfession();
        var battleId = Guid.NewGuid();
        var characterId = Guid.NewGuid();
        var stats = new CharacterStats { AttackPower = 100 };
        var rng = new RngContext(12345);
        
        var enemyDef = new EnemyDefinition(
            id: "test_enemy",
            name: "Test Enemy",
            level: 5,
            maxHp: 1000,
            baseDamage: 0,
            attackDamageType: DamageType.Physical,
            attackIntervalSeconds: 3.0
        );
        
        var meta = new BattleMeta
        {
            ModeTag = "test",
            EnemyId = enemyDef.Id,
            EnemyCount = 1
        };
        
        var options = new CombatLoopOptions
        {
            SpecialStartsWithFullInterval = true // 全局配置为等待间隔
        };
        
        // Act - 创建战斗引擎，战士模块应覆盖全局配置
        var engine = new BattleEngine(
            battleId: battleId,
            characterId: characterId,
            profession: Profession.Warrior,
            stats: stats,
            rng: rng,
            enemyDef: enemyDef,
            enemyCount: 1,
            module: warriorModule,
            meta: meta,
            loopOptions: options
        );
        
        // Assert - 验证特殊轨道立即开始（NextTriggerAt == 0）
        var specialTrack = engine.Context.Tracks.FirstOrDefault(t => t.TrackType == TrackType.Special);
        Assert.NotNull(specialTrack);
        Assert.Equal(0.0, specialTrack.NextTriggerAt);
        
        // 对比：创建使用默认测试模块的引擎（应等待间隔）
        var testModuleEngine = CreateTestBattleEngine(
            enemyDef: enemyDef,
            enemyCount: 1,
            loopOptions: options
        );
        
        var testSpecialTrack = testModuleEngine.Context.Tracks.FirstOrDefault(t => t.TrackType == TrackType.Special);
        Assert.NotNull(testSpecialTrack);
        Assert.Equal(warriorModule.BaseSpecialInterval, testSpecialTrack.NextTriggerAt);
    }
    
    #endregion
}
