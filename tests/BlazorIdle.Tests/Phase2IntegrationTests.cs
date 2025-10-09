using BlazorIdle.Server.Domain.Characters;
using BlazorIdle.Server.Domain.Combat;
using BlazorIdle.Server.Domain.Combat.Combatants;
using BlazorIdle.Server.Domain.Combat.Enemies;
using BlazorIdle.Server.Domain.Combat.Professions;
using BlazorIdle.Server.Domain.Combat.Rng;
using BlazorIdle.Shared.Models;
using BlazorWebGame.Domain.Combat;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace BlazorIdle.Tests;

/// <summary>
/// Phase 2 集成测试：验证 TargetSelector 在完整战斗场景中的行为
/// </summary>
public class Phase2IntegrationTests
{
    [Fact]
    public void AttackTickEvent_WithMultipleEnemies_ShouldDistributeAttacksRandomly()
    {
        // Arrange - 创建一个有 3 个敌人的战斗上下文
        var battleId = Guid.NewGuid();
        var characterId = Guid.NewGuid();
        var stats = new CharacterStats 
        { 
            AttackPower = 50,
            SpellPower = 50,
            CritChance = 0.0 // 禁用暴击以简化测试
        };
        
        var enemy1 = new EnemyDefinition("goblin1", "Goblin1", 5, 1000, 10, 0.0, 1.0, 1.0, 1.0);
        var enemy2 = new EnemyDefinition("goblin2", "Goblin2", 5, 1000, 10, 0.0, 1.0, 1.0, 1.0);
        var enemy3 = new EnemyDefinition("goblin3", "Goblin3", 5, 1000, 10, 0.0, 1.0, 1.0, 1.0);
        
        var encounterGroup = new EncounterGroup(new[] { enemy1, enemy2, enemy3 });
        
        var battle = new Battle
        {
            Id = battleId,
            CharacterId = characterId,
            AttackIntervalSeconds = 2.0,
            SpecialIntervalSeconds = 10.0
        };

        var clock = new GameClock();
        var scheduler = new EventScheduler();
        var collector = new SegmentCollector();
        var rng = new RngContext(12345);
        var professionModule = new TestProfessionModule();

        var context = new BattleContext(
            battle: battle,
            clock: clock,
            scheduler: scheduler,
            collector: collector,
            professionModule: professionModule,
            profession: Profession.Warrior,
            rng: rng,
            encounterGroup: encounterGroup,
            stats: stats,
            stamina: 20
        );

        // Act - 手动执行 30 次攻击事件
        var track = new TrackState(TrackType.Attack, 2.0, 0.0); // 2 秒攻击间隔
        
        for (int i = 0; i < 30; i++)
        {
            var evt = new AttackTickEvent(clock.CurrentTime, track);
            evt.Execute(context);
            clock.AdvanceTo(track.NextTriggerAt);
        }

        // Assert - 验证所有敌人都受到了伤害
        var allEnemies = encounterGroup.All.ToList();
        
        // 所有敌人的初始HP为 1000，应该都有伤害
        Assert.True(allEnemies[0].CurrentHp < 1000, "Enemy 1 should have taken damage");
        Assert.True(allEnemies[1].CurrentHp < 1000, "Enemy 2 should have taken damage");
        Assert.True(allEnemies[2].CurrentHp < 1000, "Enemy 3 should have taken damage");
        
        // 验证伤害分布不是完全集中在一个敌人身上
        var damageDistribution = allEnemies.Select(e => 1000 - e.CurrentHp).ToList();
        var totalDamage = damageDistribution.Sum();
        
        // 每个敌人至少应该受到总伤害的 10%（考虑到随机性）
        foreach (var damage in damageDistribution)
        {
            var ratio = (double)damage / totalDamage;
            Assert.True(ratio > 0.1, $"Damage should be distributed, but got ratio: {ratio}");
        }
    }

    [Fact]
    public void AttackTickEvent_WithEqualWeights_ShouldDistributeEvenly()
    {
        // Arrange
        var battleId = Guid.NewGuid();
        var characterId = Guid.NewGuid();
        var stats = new CharacterStats 
        { 
            AttackPower = 50,
            CritChance = 0.0
        };
        
        var normalEnemy = new EnemyDefinition("normal", "Normal", 5, 1000, 10, 0.0, 1.0, 1.0, 1.0);
        var taunterEnemy = new EnemyDefinition("taunter", "Taunter", 5, 1000, 10, 0.0, 1.0, 1.0, 1.0);
        
        var encounterGroup = new EncounterGroup(new[] { normalEnemy, taunterEnemy });
        
        var battle = new Battle
        {
            Id = battleId,
            CharacterId = characterId,
            AttackIntervalSeconds = 2.0,
            SpecialIntervalSeconds = 10.0
        };

        var clock = new GameClock();
        var scheduler = new EventScheduler();
        var collector = new SegmentCollector();
        var rng = new RngContext(99999);
        var professionModule = new TestProfessionModule();

        var context = new BattleContext(
            battle: battle,
            clock: clock,
            scheduler: scheduler,
            collector: collector,
            professionModule: professionModule,
            profession: Profession.Warrior,
            rng: rng,
            encounterGroup: encounterGroup,
            stats: stats,
            stamina: 20
        );

        // Act - 执行 50 次攻击
        var track = new TrackState(TrackType.Attack, 2.0, 0.0);
        
        for (int i = 0; i < 50; i++)
        {
            var evt = new AttackTickEvent(clock.CurrentTime, track);
            evt.Execute(context);
            clock.AdvanceTo(track.NextTriggerAt);
        }

        // Assert - 验证两个敌人都受到了伤害
        var allEnemies = encounterGroup.All.ToList();
        Assert.True(allEnemies[0].CurrentHp < 1000);
        Assert.True(allEnemies[1].CurrentHp < 1000);
        
        // 由于权重相等（默认都是 1.0），伤害应该大致平均分布
        var damage1 = 1000 - allEnemies[0].CurrentHp;
        var damage2 = 1000 - allEnemies[1].CurrentHp;
        var totalDamage = damage1 + damage2;
        
        var ratio1 = (double)damage1 / totalDamage;
        var ratio2 = (double)damage2 / totalDamage;
        
        // 两个敌人的伤害占比应该在 20%-80% 之间（允许随机波动）
        Assert.InRange(ratio1, 0.2, 0.8);
        Assert.InRange(ratio2, 0.2, 0.8);
    }

    [Fact]
    public void AttackTickEvent_WithSameSeed_ShouldProduceDeterministicResults()
    {
        // Arrange
        const ulong seed = 777777;
        var stats = new CharacterStats 
        { 
            AttackPower = 50,
            CritChance = 0.0
        };
        
        var enemy1 = new EnemyDefinition("e1", "Enemy1", 5, 1000, 10, 0.0, 1.0, 1.0, 1.0);
        var enemy2 = new EnemyDefinition("e2", "Enemy2", 5, 1000, 10, 0.0, 1.0, 1.0, 1.0);
        
        // Act - 运行两次完全相同的战斗
        var result1 = RunAttackSequence(seed, new[] { enemy1, enemy2 }, stats, 20);
        var result2 = RunAttackSequence(seed, new[] { enemy1, enemy2 }, stats, 20);

        // Assert - 结果应该完全一致
        Assert.Equal(result1.Enemy1Hp, result2.Enemy1Hp);
        Assert.Equal(result1.Enemy2Hp, result2.Enemy2Hp);
    }

    [Fact]
    public void AttackTickEvent_WithNoEnemies_ShouldNotCrash()
    {
        // Arrange - 创建一个没有敌人的战斗上下文（边界情况）
        var battleId = Guid.NewGuid();
        var characterId = Guid.NewGuid();
        var stats = new CharacterStats();
        
        var battle = new Battle
        {
            Id = battleId,
            CharacterId = characterId,
            AttackIntervalSeconds = 2.0,
            SpecialIntervalSeconds = 10.0
        };

        var clock = new GameClock();
        var scheduler = new EventScheduler();
        var collector = new SegmentCollector();
        var rng = new RngContext(123);
        var professionModule = new TestProfessionModule();

        var context = new BattleContext(
            battle: battle,
            clock: clock,
            scheduler: scheduler,
            collector: collector,
            professionModule: professionModule,
            profession: Profession.Warrior,
            rng: rng,
            stats: stats
        );

        // Act & Assert - 应该不会崩溃
        var track = new TrackState(TrackType.Attack, 2.0, 0.0);
        
        for (int i = 0; i < 5; i++)
        {
            var evt = new AttackTickEvent(clock.CurrentTime, track);
            evt.Execute(context); // 应该优雅地处理没有目标的情况
            clock.AdvanceTo(track.NextTriggerAt);
        }
        
        // 验证战斗可以正常运行
        Assert.True(clock.CurrentTime > 0);
    }

    #region Helper Methods

    private (int Enemy1Hp, int Enemy2Hp) RunAttackSequence(ulong seed, EnemyDefinition[] enemies, CharacterStats stats, int attackCount)
    {
        var battleId = Guid.NewGuid();
        var characterId = Guid.NewGuid();
        
        var encounterGroup = new EncounterGroup(enemies);
        
        var battle = new Battle
        {
            Id = battleId,
            CharacterId = characterId,
            AttackIntervalSeconds = 2.0,
            SpecialIntervalSeconds = 10.0
        };

        var clock = new GameClock();
        var scheduler = new EventScheduler();
        var collector = new SegmentCollector();
        var rng = new RngContext(seed);
        var professionModule = new TestProfessionModule();

        var context = new BattleContext(
            battle: battle,
            clock: clock,
            scheduler: scheduler,
            collector: collector,
            professionModule: professionModule,
            profession: Profession.Warrior,
            rng: rng,
            encounterGroup: encounterGroup,
            stats: stats,
            stamina: 20
        );

        var track = new TrackState(TrackType.Attack, 2.0, 0.0);
        
        for (int i = 0; i < attackCount; i++)
        {
            var evt = new AttackTickEvent(clock.CurrentTime, track);
            evt.Execute(context);
            clock.AdvanceTo(track.NextTriggerAt);
        }

        var allEnemies = encounterGroup.All.ToList();
        return (allEnemies[0].CurrentHp, allEnemies[1].CurrentHp);
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
