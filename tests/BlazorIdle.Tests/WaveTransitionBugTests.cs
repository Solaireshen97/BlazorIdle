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
        
        var stats = new CharacterStats { AttackPower = 200 }; // 更高的攻击力以便快速击杀
        var rng = new RngContext(123);
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
        
        // 推进到第二波完全完成
        // 刷新延迟是 1 秒，所以在 121 + 1 = 122 秒时第二波应该刷新
        // 推进到 200 秒，确保第二波完成
        engine.AdvanceTo(200.0, 2000);

        // 检查战斗状态和波次
        var currentWaveIndex = engine.WaveIndex;
        
        // 验证已经进入或完成第二波
        Assert.True(currentWaveIndex >= 2, $"Should have reached wave 2, but at wave {currentWaveIndex}");
        
        // 记录第二波的 EnemyCombatants 数量（这里是关键：应该重新初始化）
        var wave2EnemyCombatantsCount = engine.Context.EnemyCombatants.Count;
        var wave2EnemyIds = string.Join(", ", engine.Context.EnemyCombatants.Select(e => $"{e.Id}(dead:{e.IsDead})"));

        // 关键断言：玩家应该在第二波中受到伤害
        // 如果没有受到伤害，说明怪物攻击系统在波次切换后失效了
        var hpAfterWave2 = engine.Context.Player.CurrentHp;
        Assert.True(hpAfterWave2 < hpAfterWave1, 
            $"BUG REPRODUCED! Player did NOT take damage in wave 2. HP after wave 1: {hpAfterWave1}, HP after wave 2: {hpAfterWave2}. " +
            $"EnemyCombatants count: wave1={wave1EnemyCombatantsCount}, wave2={wave2EnemyCombatantsCount}. " +
            $"Wave 2 enemies: [{wave2EnemyIds}]");
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

        public void RegisterBuffDefinitions(BattleContext ctx) { }
        public void OnBattleStart(BattleContext ctx) { }
        public void BuildSkills(BattleContext ctx, Server.Domain.Combat.Skills.AutoCastEngine autoCaster) { }
        public void OnAttackTick(BattleContext ctx, AttackTickEvent evt) { }
        public void OnSpecialPulse(BattleContext ctx, SpecialPulseEvent evt) { }
        public void OnSkillCast(BattleContext ctx, Server.Domain.Combat.Skills.SkillDefinition def) { }
    }
}
