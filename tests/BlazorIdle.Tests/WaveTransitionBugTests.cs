using BlazorIdle.Server.Domain.Characters;
using BlazorIdle.Server.Domain.Combat;
using BlazorIdle.Server.Domain.Combat.Damage;
using BlazorIdle.Server.Domain.Combat.Enemies;
using BlazorIdle.Server.Domain.Combat.Engine;
using BlazorIdle.Server.Domain.Combat.Professions;
using BlazorIdle.Server.Domain.Combat.Rng;
using BlazorIdle.Shared.Models;
using System;
using System.Linq;
using Xunit;

namespace BlazorIdle.Tests;

/// <summary>
/// 测试副本波次切换时的bug：玩家在第一波后变成无敌
/// Bug: 玩家在第一波战斗后，第二波怪物不再能够伤害玩家
/// </summary>
public class WaveTransitionBugTests
{
    [Fact]
    public void DungeonBattle_SecondWave_ShouldDamagePlayer()
    {
        // Arrange: 创建一个两波怪物的副本，使用 EnemyRegistry 中已有的怪物
        // 使用 "dummy" 怪物，它们有攻击能力
        var dungeon = new DungeonDefinition(
            id: "test_dungeon",
            name: "Test Dungeon",
            waves: new[]
            {
                new DungeonDefinition.Wave(new[] { ("dummy", 1) }),
                new DungeonDefinition.Wave(new[] { ("dummy", 1) })
            },
            waveRespawnDelaySeconds: 1.0,
            runRespawnDelaySeconds: 3.0
        );

        var provider = new DungeonEncounterProvider(dungeon, loop: false);
        
        var stats = new CharacterStats { AttackPower = 50 }; // 较低的攻击力以便怪物能存活更久
        var rng = new RngContext(123);
        var profession = Profession.Warrior;
        var module = new TestProfessionModule();
        
        // Use old behavior for backward compatibility in tests
        var loopOptions = new Server.Infrastructure.Configuration.CombatLoopOptions
        {
            AttackStartsWithFullInterval = false, // Old behavior: immediate attack
            SpecialStartsWithFullInterval = false
        };

        var engine = new BattleEngine(
            battleId: Guid.NewGuid(),
            characterId: Guid.NewGuid(),
            profession: profession,
            stats: stats,
            rng: rng,
            provider: provider,
            module: module,
            loopOptions: loopOptions
        );

        var initialPlayerHp = engine.Context.Player.CurrentHp;
        Assert.Equal(100, initialPlayerHp); // 10 Stamina * 10 = 100 HP (default)

        // Act: 推进战斗，让第一波怪物死亡
        engine.AdvanceTo(120.0, 2000); // 推进到 120 秒，给足够的时间

        // 验证第一波怪物已死亡
        var wave1Cleared = engine.Context.EncounterGroup!.All.All(e => e.IsDead);
        if (!wave1Cleared)
        {
            // 输出调试信息
            var enemy = engine.Context.EncounterGroup!.All[0];
            Assert.Fail($"First wave not cleared. Enemy HP: {enemy.CurrentHp}/{enemy.Enemy.MaxHp}. Player HP: {engine.Context.Player.CurrentHp}");
        }

        // 记录玩家在第一波结束时的 HP
        var hpAfterWave1 = engine.Context.Player.CurrentHp;
        Assert.True(hpAfterWave1 < initialPlayerHp, $"Player should have taken damage in wave 1. HP: {hpAfterWave1}/{initialPlayerHp}");

        // 记录第一波的 EnemyCombatants 数量和状态
        var wave1EnemyCombatantsCount = engine.Context.EnemyCombatants.Count;
        
        // 推进到第二波刚刚开始，但还没完成
        // 刷新延迟是 1 秒，所以在 121 + 1 = 122 秒时第二波应该刷新
        // 推进到 130 秒，给第二波的怪物 8 秒时间攻击玩家，但不要让玩家击杀它
        engine.AdvanceTo(130.0, 1000);

        // 检查战斗状态和波次
        var currentWaveIndex = engine.WaveIndex;
        
        // 验证已经进入或完成第二波
        Assert.True(currentWaveIndex >= 2, $"Should have reached wave 2, but at wave {currentWaveIndex}");
        
        // 记录第二波的 EnemyCombatants 数量（这里是关键：应该重新初始化）
        var wave2EnemyCombatantsCount = engine.Context.EnemyCombatants.Count;
        var wave2EnemyIds = string.Join(", ", engine.Context.EnemyCombatants.Select(e => $"{e.Id}(dead:{e.IsDead})"));
        
        // 检查 EnemyCombatants 是否指向当前的 EncounterGroup
        var currentEncounterGroup = engine.Context.EncounterGroup;
        var currentEncounters = currentEncounterGroup!.All;
        var enemyCombatantsPointToCurrentWave = engine.Context.EnemyCombatants.All(ec => 
            currentEncounters.Any(enc => ReferenceEquals(enc, ec.Encounter)));
        
        // 检查当前波次的 encounters 状态
        var currentEncounterStatus = string.Join(", ", currentEncounters.Select(e => $"HP:{e.CurrentHp}/{e.Enemy.MaxHp},Dead:{e.IsDead}"));

        // 检查 segments 中是否有波次切换的标签
        var segments = engine.Segments;
        var totalTags = segments.SelectMany(s => s.TagCounters).ToList();
        var waveTransitionCleared = totalTags.Where(t => t.Key == "wave_transition_enemy_cleared").Sum(t => t.Value);
        var waveTransitionReinitialized = totalTags.Where(t => t.Key == "wave_transition_enemy_reinitialized").Sum(t => t.Value);
        var enemyAttackInitialized = totalTags.Where(t => t.Key == "enemy_attack_initialized").Sum(t => t.Value);
        var enemyAttacks = totalTags.Where(t => t.Key == "enemy_attack").Sum(t => t.Value);
        var playerRevives = totalTags.Where(t => t.Key == "player_revive").Sum(t => t.Value);
        var damageTaken = totalTags.Where(t => t.Key == "damage_taken").Sum(t => t.Value);

        // 关键断言：验证修复是否生效
        // 如果修复生效，应该有：
        // 1. 波次切换发生（cleared=1, reinitialized=1）
        // 2. 两波怪物都初始化了攻击（initialized=2）
        // 3. 总共发生了多次敌人攻击（attacks > 10）
        // 4. 玩家受到了总伤害（damage_taken > 100）
        var hpAfterWave2 = engine.Context.Player.CurrentHp;
        
        // 验证波次切换发生
        Assert.Equal(1, waveTransitionCleared);
        Assert.Equal(1, waveTransitionReinitialized);
        
        // 验证两波怪物都初始化了攻击系统
        Assert.Equal(2, enemyAttackInitialized);
        
        // 验证敌人攻击确实发生且次数合理（至少10次）
        Assert.True(enemyAttacks >= 10, $"Expected at least 10 enemy attacks, got {enemyAttacks}");
        
        // 验证玩家受到了大量伤害（证明两波都在攻击）
        Assert.True(damageTaken > 100, $"Expected damage > 100, got {damageTaken}. This confirms enemies attacked in both waves!");
        
        // 如果玩家复活了，说明敌人确实在持续攻击
        Assert.True(playerRevives >= 1, $"Expected at least 1 player revive, got {playerRevives}");
    }

    [Fact]
    public void DungeonLoop_SecondRun_ShouldDamagePlayer()
    {
        // Arrange: 创建一个循环副本
        var enemyDef = new EnemyDefinition(
            id: "loop_enemy",
            name: "Loop Enemy",
            level: 5,
            maxHp: 30,
            baseDamage: 10,
            attackDamageType: DamageType.Physical,
            attackIntervalSeconds: 1.5
        );

        var dungeon = new DungeonDefinition(
            id: "test_loop_dungeon",
            name: "Test Loop Dungeon",
            waves: new[]
            {
                new DungeonDefinition.Wave(new[] { (enemyDef.Id, 1) })
            },
            waveRespawnDelaySeconds: 0.5,
            runRespawnDelaySeconds: 1.0
        );

        var provider = new DungeonEncounterProvider(dungeon, loop: true);
        
        var stats = new CharacterStats { AttackPower = 100 };
        var rng = new RngContext(456);
        var profession = Profession.Warrior;
        var module = new TestProfessionModule();

        var engine = new BattleEngine(
            battleId: Guid.NewGuid(),
            characterId: Guid.NewGuid(),
            profession: profession,
            stats: stats,
            rng: rng,
            provider: provider,
            module: module
        );

        var initialPlayerHp = engine.Context.Player.CurrentHp;

        // Act: 推进战斗完成第一轮
        engine.AdvanceTo(15.0, 500);

        // 验证第一轮完成
        var hpAfterRun1 = engine.Context.Player.CurrentHp;
        Assert.True(hpAfterRun1 < initialPlayerHp, "Player should take damage in run 1");

        // 继续推进，让第二轮开始并进行一段时间
        engine.AdvanceTo(30.0, 500);

        // 验证第二轮中玩家继续受伤
        var hpAfterRun2 = engine.Context.Player.CurrentHp;
        Assert.True(hpAfterRun2 < hpAfterRun1,
            $"Player should take damage in run 2. HP after run 1: {hpAfterRun1}, HP after run 2: {hpAfterRun2}");
    }

    private class TestProfessionModule : IProfessionModule
    {
        public string Id => "test_profession";
        public double BaseAttackInterval => 2.0;
        public double BaseSpecialInterval => 5.0;
        
        // 默认测试行为
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
}
