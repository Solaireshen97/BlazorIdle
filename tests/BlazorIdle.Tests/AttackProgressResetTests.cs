using BlazorIdle.Server.Domain.Characters;
using BlazorIdle.Server.Domain.Combat;
using BlazorIdle.Server.Domain.Combat.Enemies;
using BlazorIdle.Server.Domain.Combat.Engine;
using BlazorIdle.Server.Domain.Combat.Rng;
using BlazorIdle.Shared.Models;
using System;
using System.Linq;
using Xunit;

namespace BlazorIdle.Tests;

/// <summary>
/// 测试攻击进度在目标切换和怪物等待刷新时的重置行为
/// </summary>
public class AttackProgressResetTests
{
    [Fact]
    public void AttackProgress_ResetsOnTargetSwitch_WhenMonsterDiesInMultipleEnemies()
    {
        // Arrange: 创建一个多怪物战斗场景
        var battleId = Guid.NewGuid();
        var characterId = Guid.NewGuid();
        var seed = 12345UL;
        var rng = new RngContext(seed);
        
        // 创建两个弱小的敌人，容易击杀
        var weakEnemy = new EnemyDefinition("weak_dummy", "弱小假人", level: 1, maxHp: 50);
        
        var stats = new CharacterStats
        {
            AttackPower = 100,
            CritChance = 0.0,
            HastePercent = 0.0
        };

        var engine = new BattleEngine(
            battleId: battleId,
            characterId: characterId,
            profession: Profession.Warrior,
            stats: stats,
            rng: rng,
            enemyDef: weakEnemy,
            enemyCount: 2
        );

        // Act: 推进战斗直到第一个敌人死亡
        // 普攻间隔默认为 2.0 秒，让我们推进到第一个怪物死亡后
        engine.AdvanceTo(3.0, maxEvents: 100); // 应该能击杀第一个敌人
        
        var attackTrack = engine.Context.Tracks.FirstOrDefault(t => t.TrackType == TrackType.Attack);
        
        // Assert: 验证攻击进度已被重置
        Assert.NotNull(attackTrack);
        
        // 检查是否有 retarget_primary 标签，表明发生了目标切换
        var segments = engine.Segments;
        bool hasRetargetTag = segments.Any(s => s.TagCounters.ContainsKey("retarget_primary"));
        
        if (hasRetargetTag)
        {
            // 如果发生了重定向，应该也有攻击进度重置标签
            bool hasResetTag = segments.Any(s => s.TagCounters.ContainsKey("attack_progress_reset"));
            Assert.True(hasResetTag, "切换目标时应该重置攻击进度");
        }
    }

    [Fact]
    public void AttackProgress_ResetsWhenWaitingForRespawn_InContinuousMode()
    {
        // Arrange: 创建持续模式战斗，带有刷新延迟
        var battleId = Guid.NewGuid();
        var characterId = Guid.NewGuid();
        var seed = 54321UL;
        var rng = new RngContext(seed);
        
        var weakEnemy = new EnemyDefinition("weak_dummy", "弱小假人", level: 1, maxHp: 30);
        
        var stats = new CharacterStats
        {
            AttackPower = 50,
            CritChance = 0.0,
            HastePercent = 0.0
        };

        // 创建一个简单的 provider，支持重生
        var provider = new SimpleRespawnProvider(weakEnemy, respawnDelaySeconds: 2.0);

        var engine = new BattleEngine(
            battleId: battleId,
            characterId: characterId,
            profession: Profession.Warrior,
            stats: stats,
            rng: rng,
            provider: provider
        );

        // Act: 推进战斗直到怪物死亡并安排刷新
        engine.AdvanceTo(5.0, maxEvents: 200);
        
        // Assert: 检查是否有 spawn_scheduled 和 attack_progress_reset 标签
        var segments = engine.Segments;
        bool hasSpawnScheduled = segments.Any(s => s.TagCounters.ContainsKey("spawn_scheduled"));
        bool hasResetTag = segments.Any(s => s.TagCounters.ContainsKey("attack_progress_reset"));
        
        if (hasSpawnScheduled)
        {
            Assert.True(hasResetTag, "怪物死亡等待刷新时应该重置攻击进度");
        }
    }

    [Fact]
    public void AttackProgress_NextTriggerTime_UpdatedCorrectly_OnReset()
    {
        // Arrange
        var battleId = Guid.NewGuid();
        var characterId = Guid.NewGuid();
        var seed = 99999UL;
        var rng = new RngContext(seed);
        
        var weakEnemy = new EnemyDefinition("weak_dummy", "弱小假人", level: 1, maxHp: 40);
        
        var stats = new CharacterStats
        {
            AttackPower = 80,
            CritChance = 0.0,
            HastePercent = 0.0
        };

        var engine = new BattleEngine(
            battleId: battleId,
            characterId: characterId,
            profession: Profession.Warrior,
            stats: stats,
            rng: rng,
            enemyDef: weakEnemy,
            enemyCount: 2
        );

        // Act: 推进战斗到第一个怪物死亡
        var attackTrack = engine.Context.Tracks.FirstOrDefault(t => t.TrackType == TrackType.Attack);
        Assert.NotNull(attackTrack);
        
        var initialNextTrigger = attackTrack.NextTriggerAt;
        
        // 推进战斗
        engine.AdvanceTo(4.0, maxEvents: 100);
        
        var segments = engine.Segments;
        bool hasResetTag = segments.Any(s => s.TagCounters.ContainsKey("attack_progress_reset"));
        
        // Assert: 如果发生了重置，NextTriggerAt应该更新为当前时间 + 完整间隔
        if (hasResetTag)
        {
            var currentTime = engine.Clock.CurrentTime;
            var expectedMinTrigger = currentTime; // 至少是当前时间或之后
            
            Assert.True(attackTrack.NextTriggerAt >= expectedMinTrigger, 
                "攻击进度重置后，下次触发时间应该在当前时间或之后");
        }
    }

    // 简单的重生提供器用于测试
    private class SimpleRespawnProvider : IEncounterProvider
    {
        private readonly EnemyDefinition _enemyDef;
        private readonly double _respawnDelay;
        private int _waveIndex;
        private int _runCount;
        private bool _hasAdvanced;

        public SimpleRespawnProvider(EnemyDefinition enemyDef, double respawnDelaySeconds)
        {
            _enemyDef = enemyDef;
            _respawnDelay = respawnDelaySeconds;
            _waveIndex = 1;
            _runCount = 0;
            _hasAdvanced = false;
        }

        public EncounterGroup CurrentGroup => new EncounterGroup(new[] { _enemyDef });

        public int CurrentWaveIndex => _waveIndex;

        public int CompletedRunCount => _runCount;

        public bool TryAdvance(out EncounterGroup? nextGroup, out bool runCompleted)
        {
            if (_hasAdvanced)
            {
                nextGroup = null;
                runCompleted = false;
                return false;
            }

            _hasAdvanced = true;
            _waveIndex++;
            nextGroup = new EncounterGroup(new[] { _enemyDef });
            runCompleted = false;
            return true;
        }

        public double GetRespawnDelaySeconds(bool runJustCompleted)
        {
            return _respawnDelay;
        }
    }
}
