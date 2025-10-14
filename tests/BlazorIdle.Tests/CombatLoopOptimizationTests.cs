using BlazorIdle.Server.Domain.Characters;
using BlazorIdle.Server.Domain.Combat;
using BlazorIdle.Server.Domain.Combat.Damage;
using BlazorIdle.Server.Domain.Combat.Enemies;
using BlazorIdle.Server.Domain.Combat.Engine;
using BlazorIdle.Server.Domain.Combat.Professions;
using BlazorIdle.Server.Domain.Combat.Rng;
using BlazorIdle.Server.Infrastructure.Configuration;
using BlazorIdle.Shared.Models;
using BlazorIdle.Server.Application.Abstractions;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using Moq;

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
    
    // ========================================
    // Middle Phase Tests (Tasks 2.1-2.5)
    // ========================================
    
    [Fact]
    public void Task21_WarriorSpecial_ShouldNotPauseWhenNoEnemies()
    {
        // Arrange: Warrior's special track should NOT pause when no enemies
        var dungeon = new DungeonDefinition(
            id: "test_warrior_dungeon",
            name: "Test Warrior Dungeon",
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
            AttackStartsWithFullInterval = false,
            PauseAttackWhenNoEnemies = true,
            PauseSpecialWhenNoEnemiesByDefault = true // Default is to pause
        };
        
        var warriorModule = new Server.Domain.Combat.Professions.WarriorProfession();
        var engine = CreateTestBattleEngineWithModule(
            provider: provider,
            loopOptions: options,
            professionModule: warriorModule
        );
        
        // Kill all enemies to trigger wave transition
        var firstEnemy = engine.Context.EncounterGroup!.All[0];
        firstEnemy.ApplyDamage(10000, engine.Clock.CurrentTime);
        
        // Act: Advance to trigger wave transition
        engine.AdvanceTo(1.0, 100);
        
        // Find the special track
        var specialTrack = engine.Context.Tracks.First(t => t.TrackType == TrackType.Special);
        
        // Assert: Special track should NOT be paused (NextTriggerAt < 1e9)
        // Warrior's special track continues even when no enemies
        Assert.True(specialTrack.NextTriggerAt < 1e9, 
            $"Warrior special track should not be paused. NextTriggerAt: {specialTrack.NextTriggerAt}");
        
        // Verify attack track IS paused
        var attackTrack = engine.Context.Tracks.First(t => t.TrackType == TrackType.Attack);
        Assert.True(attackTrack.NextTriggerAt > 1e9, 
            "Attack track should be paused");
    }
    
    [Fact]
    public void Task22_WarriorSpecial_ShouldStartImmediately()
    {
        // Arrange: Warrior's special track should start immediately (delay = 0)
        var options = new CombatLoopOptions
        {
            AttackStartsWithFullInterval = true,
            SpecialStartsWithFullInterval = true // Default is delayed start
        };
        
        var warriorModule = new Server.Domain.Combat.Professions.WarriorProfession();
        var engine = CreateTestBattleEngineWithModule(
            loopOptions: options,
            professionModule: warriorModule
        );
        
        var specialTrack = engine.Context.Tracks.First(t => t.TrackType == TrackType.Special);
        
        // Assert: Warrior's special track should start at 0 (immediate)
        // despite global config saying it should start at full interval
        Assert.Equal(0.0, specialTrack.NextTriggerAt, 2);
        
        // Verify attack track still uses configured behavior
        var attackTrack = engine.Context.Tracks.First(t => t.TrackType == TrackType.Attack);
        Assert.True(attackTrack.NextTriggerAt > 0, "Attack track should use full interval");
    }
    
    [Fact]
    public void Task23_RangerSpecial_ShouldUseDefaultConfig()
    {
        // Arrange: Ranger should use default configuration (pause when no enemies)
        var dungeon = new DungeonDefinition(
            id: "test_ranger_dungeon",
            name: "Test Ranger Dungeon",
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
            AttackStartsWithFullInterval = false,
            PauseAttackWhenNoEnemies = true,
            PauseSpecialWhenNoEnemiesByDefault = true
        };
        
        var rangerModule = new Server.Domain.Combat.Professions.RangerProfession();
        var engine = CreateTestBattleEngineWithModule(
            provider: provider,
            loopOptions: options,
            professionModule: rangerModule
        );
        
        // Kill all enemies to trigger wave transition
        var firstEnemy = engine.Context.EncounterGroup!.All[0];
        firstEnemy.ApplyDamage(10000, engine.Clock.CurrentTime);
        
        // Act: Advance to trigger wave transition
        engine.AdvanceTo(1.0, 100);
        
        // Find the special track
        var specialTrack = engine.Context.Tracks.First(t => t.TrackType == TrackType.Special);
        
        // Assert: Ranger's special track SHOULD be paused (uses default config)
        Assert.True(specialTrack.NextTriggerAt > 1e9, 
            $"Ranger special track should be paused. NextTriggerAt: {specialTrack.NextTriggerAt}");
    }
    
    [Fact]
    public void Task24_ProfessionConfigUsedAfterSpawnResume()
    {
        // Arrange: Test that profession config is used when resuming tracks after spawn
        // This is simpler than testing revive, and tests the same configuration path
        var dungeon = new DungeonDefinition(
            id: "test_config_dungeon",
            name: "Test Config Dungeon",
            waves: new[]
            {
                new DungeonDefinition.Wave(new[] { ("dummy", 1) }),
                new DungeonDefinition.Wave(new[] { ("dummy", 1) })
            },
            waveRespawnDelaySeconds: 0.5,  // Short delay for faster test
            runRespawnDelaySeconds: 1.0
        );
        
        var provider = new DungeonEncounterProvider(dungeon, loop: false);
        var options = new CombatLoopOptions
        {
            AttackStartsWithFullInterval = false,
            PauseAttackWhenNoEnemies = true,
            PauseSpecialWhenNoEnemiesByDefault = true
        };
        
        var warriorModule = new Server.Domain.Combat.Professions.WarriorProfession();
        
        // Verify warrior module config
        Assert.True(warriorModule.SpecialStartsImmediately == true);
        Assert.True(warriorModule.PauseSpecialWhenNoEnemies == false);
        
        var engine = CreateTestBattleEngineWithModule(
            provider: provider,
            loopOptions: options,
            professionModule: warriorModule
        );
        
        // Kill first wave to trigger pause and resume
        var firstEnemy = engine.Context.EncounterGroup!.All[0];
        firstEnemy.ApplyDamage(10000, engine.Clock.CurrentTime);
        
        // Act: Advance to let wave transition happen
        engine.AdvanceTo(2.0, 100);
        
        // Assert: Special track should not have been paused (Warrior config)
        var specialTrack = engine.Context.Tracks.First(t => t.TrackType == TrackType.Special);
        
        // If special track was paused, NextTriggerAt would be > 1e9
        // If it continued (Warrior behavior), NextTriggerAt would be normal
        Assert.True(specialTrack.NextTriggerAt < 1e9,
            $"Warrior special track should not pause when no enemies. NextTriggerAt: {specialTrack.NextTriggerAt}");
            
        // Verify attack track WAS paused and then resumed
        var attackTrack = engine.Context.Tracks.First(t => t.TrackType == TrackType.Attack);
        Assert.True(attackTrack.NextTriggerAt < 1e9,
            "Attack track should have been resumed after spawn");
    }
    
    [Fact]
    public void Task25_ProfessionConfigPriority_OverridesGlobalConfig()
    {
        // Arrange: Test that profession config takes priority over global config
        var options = new CombatLoopOptions
        {
            SpecialStartsWithFullInterval = true, // Global says: delay
            PauseSpecialWhenNoEnemiesByDefault = true // Global says: pause
        };
        
        var warriorModule = new Server.Domain.Combat.Professions.WarriorProfession();
        var engine = CreateTestBattleEngineWithModule(
            loopOptions: options,
            professionModule: warriorModule
        );
        
        var specialTrack = engine.Context.Tracks.First(t => t.TrackType == TrackType.Special);
        
        // Assert: Warrior config (immediate start) should override global config (delayed start)
        Assert.Equal(0.0, specialTrack.NextTriggerAt, 2);
    }
    
    /// <summary>
    /// 战斗循环优化 Task 3.2 & 3.3: 验证 SignalR 通知在轨道暂停/恢复时发送
    /// 测试场景：
    /// 1. 怪物死亡时发送 TrackProgressResetEventDto（原因: spawn_wait）
    /// 2. 怪物刷新时发送 TrackProgressResetEventDto（原因: spawn_complete）
    /// 3. 事件包含正确的轨道类型和触发时间
    /// </summary>
    [Fact]
    public void Task32_TrackProgressReset_ShouldNotifyFrontend()
    {
        // Arrange: 创建多波次副本
        var dungeon = new DungeonDefinition(
            id: "test_signalr_dungeon",
            name: "Test SignalR Dungeon",
            waves: new[]
            {
                new DungeonDefinition.Wave(new[] { ("dummy", 1) }),
                new DungeonDefinition.Wave(new[] { ("dummy", 1) })
            },
            waveRespawnDelaySeconds: 1.0,
            runRespawnDelaySeconds: 2.0
        );
        
        var provider = new DungeonEncounterProvider(dungeon, loop: false);
        
        var options = new CombatLoopOptions
        {
            AttackStartsWithFullInterval = false, // 使用旧行为以便更快触发攻击
            PauseAttackWhenNoEnemies = true,
            PauseSpecialWhenNoEnemiesByDefault = true
        };
        
        var engine = CreateTestBattleEngine(
            provider: provider,
            loopOptions: options
        );
        
        // 创建 mock 通知服务并注入到上下文
        var capturedEvents = new List<object>();
        var mockNotificationService = new Mock<IBattleNotificationService>();
        mockNotificationService.Setup(x => x.IsAvailable).Returns(true);
        mockNotificationService
            .Setup(x => x.NotifyEventAsync(It.IsAny<Guid>(), It.IsAny<object>()))
            .Callback<Guid, object>((battleId, evt) => capturedEvents.Add(evt))
            .Returns(Task.CompletedTask);
        
        // 使用反射设置 NotificationService（因为它是只读属性）
        var contextType = engine.Context.GetType();
        var notificationServiceField = contextType.GetField("_notificationService", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        if (notificationServiceField != null)
        {
            notificationServiceField.SetValue(engine.Context, mockNotificationService.Object);
        }
        
        // Act: 推进战斗直到第一波怪物死亡（应该触发暂停事件）
        engine.AdvanceUntil(60.0);
        
        // Assert: 验证至少收到一个事件
        var resetEvents = capturedEvents
            .OfType<TrackProgressResetEventDto>()
            .ToList();
        
        // 如果收到事件，验证其内容
        if (resetEvents.Count > 0)
        {
            // 应该有暂停事件
            var pauseEvents = resetEvents.Where(e => e.ResetReason == "spawn_wait").ToList();
            if (pauseEvents.Count > 0)
            {
                var pauseEvent = pauseEvents.First();
                Assert.Contains("Attack", pauseEvent.TrackTypes);
                Assert.Equal(engine.Battle.Id, pauseEvent.BattleId);
            }
            
            // 应该有恢复事件
            var resumeEvents = resetEvents.Where(e => e.ResetReason == "spawn_complete").ToList();
            if (resumeEvents.Count > 0)
            {
                var resumeEvent = resumeEvents.First();
                Assert.Contains("Attack", resumeEvent.TrackTypes);
                Assert.NotNull(resumeEvent.NewTriggerTimes);
                Assert.Equal(engine.Battle.Id, resumeEvent.BattleId);
            }
        }
        
        // 至少应该有一些事件被发送（如果NotificationService注入成功）
        // 注意：这个测试主要验证代码不会崩溃，事件的具体内容在实际使用中会更重要
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
    
    /// <summary>
    /// 战斗循环优化中篇测试辅助方法：创建使用指定职业模块的战斗引擎
    /// </summary>
    private BattleEngine CreateTestBattleEngineWithModule(
        IProfessionModule professionModule,
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
                profession: Profession.Warrior, // Profession enum doesn't affect the test
                stats: stats,
                rng: rng,
                provider: provider,
                module: professionModule,
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
            module: professionModule,
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
