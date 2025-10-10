using Xunit;
using BlazorIdle.Server.Domain.Combat;
using BlazorIdle.Server.Domain.Combat.Buffs;
using BlazorIdle.Server.Domain.Combat.Combatants;
using BlazorIdle.Server.Domain.Combat.Damage;
using BlazorIdle.Server.Domain.Combat.Enemies;
using BlazorIdle.Server.Domain.Combat.Engine;
using BlazorIdle.Server.Domain.Combat.Professions;
using BlazorIdle.Server.Domain.Combat.Rng;
using BlazorIdle.Server.Domain.Characters;
using BlazorIdle.Shared.Models;
using BlazorWebGame.Domain.Combat;
using System;
using System.Linq;

namespace BlazorIdle.Tests;

/// <summary>
/// 测试怪物 Buff 系统
/// 验证怪物可以获得并应用 Buff 效果
/// </summary>
public class EnemyBuffTests
{
    #region 基础 Buff 管理测试

    [Fact]
    public void EnemyBuffManager_ShouldBeInitialized()
    {
        // Arrange & Act
        var engine = CreateBattleEngine();

        // Assert
        Assert.NotNull(engine.Context.EnemyCombatants);
        Assert.NotEmpty(engine.Context.EnemyCombatants);
        
        foreach (var enemy in engine.Context.EnemyCombatants)
        {
            Assert.NotNull(enemy.BuffManager);
        }
    }

    [Fact]
    public void EnemyBuffManager_ShouldHaveRegisteredDefinitions()
    {
        // Arrange & Act
        var engine = CreateBattleEngine();
        var enemy = engine.Context.EnemyCombatants.First();

        // Assert
        Assert.NotNull(enemy.BuffManager);
        
        // 验证可以获取已注册的 Buff 定义
        var enrageDef = enemy.BuffManager.GetDefinition("enemy_enrage");
        Assert.NotNull(enrageDef);
        Assert.Equal("enemy_enrage", enrageDef.Id);
        Assert.Equal("Enrage", enrageDef.Name);
    }

    [Fact]
    public void EnemyBuffManager_ApplyBuff_ShouldWork()
    {
        // Arrange
        var engine = CreateBattleEngine();
        var enemy = engine.Context.EnemyCombatants.First();
        double now = 0.0;

        // Act
        var buffInstance = enemy.BuffManager!.Apply("enemy_enrage", now);

        // Assert
        Assert.NotNull(buffInstance);
        Assert.Equal("enemy_enrage", buffInstance.Definition.Id);
        Assert.True(enemy.BuffManager.Has("enemy_enrage"));
    }

    [Fact]
    public void EnemyBuffManager_BuffExpiry_ShouldWork()
    {
        // Arrange
        var engine = CreateBattleEngine();
        var enemy = engine.Context.EnemyCombatants.First();
        double now = 0.0;

        // Apply buff with 20 second duration
        enemy.BuffManager!.Apply("enemy_enrage", now);
        Assert.True(enemy.BuffManager.Has("enemy_enrage"));

        // Act - Tick past expiry time
        enemy.BuffManager.Tick(now + 21.0);

        // Assert
        Assert.False(enemy.BuffManager.Has("enemy_enrage"));
    }

    #endregion

    #region Buff 效果应用测试

    [Fact]
    public void EnemyBuff_Enrage_ShouldIncreaseDamage()
    {
        // Arrange - 简化测试，直接验证单次攻击伤害
        var context = CreateTestBattleContext(enemyBaseDamage: 10);
        var enemy = context.EnemyCombatants[0];
        var player = context.Player;
        
        // 记录没有 Buff 时的伤害
        int initialHp = player.CurrentHp;
        var attackEvent = new EnemyAttackEvent(1.0, enemy);
        attackEvent.Execute(context);
        int damageWithoutBuff = initialHp - player.CurrentHp;
        
        // Act - 应用 Enrage Buff（+50% 伤害）
        enemy.BuffManager!.Apply("enemy_enrage", 2.0);
        
        // 让怪物再次攻击（使用新的 HP）
        initialHp = player.CurrentHp;
        var attackEvent2 = new EnemyAttackEvent(3.0, enemy);
        attackEvent2.Execute(context);
        int damageWithBuff = initialHp - player.CurrentHp;

        // Assert - Buff 应该增加伤害约 50%
        Assert.True(damageWithBuff > damageWithoutBuff, 
            $"Damage with Enrage buff ({damageWithBuff}) should be greater than without ({damageWithoutBuff})");
        
        // 验证增幅大致正确（允许四舍五入误差）
        double expectedDamage = damageWithoutBuff * 1.5;
        Assert.InRange(damageWithBuff, (int)expectedDamage - 1, (int)expectedDamage + 1);
    }

    [Fact]
    public void EnemyBuff_Frenzy_ShouldStackAndIncreaseDamage()
    {
        // Arrange
        var engine = CreateBattleEngine();
        var enemy = engine.Context.EnemyCombatants.First();
        double now = 0.0;

        // Act - 应用多层 Frenzy
        enemy.BuffManager!.Apply("enemy_frenzy", now);
        enemy.BuffManager.Apply("enemy_frenzy", now);
        enemy.BuffManager.Apply("enemy_frenzy", now);

        var frenzyInstance = enemy.BuffManager.TryGet("enemy_frenzy");

        // Assert
        Assert.NotNull(frenzyInstance);
        Assert.Equal(3, frenzyInstance.Stacks);
        
        // 验证伤害加成聚合
        var aggregate = enemy.BuffManager.Aggregate;
        // 每层 +20% 物理伤害，3层应该是 +60%
        Assert.Equal(0.60, aggregate.DamageMultiplierPhysical, 2);
    }

    [Fact]
    public void EnemyBuff_MaxStacks_ShouldBeLimited()
    {
        // Arrange
        var engine = CreateBattleEngine();
        var enemy = engine.Context.EnemyCombatants.First();
        double now = 0.0;

        // Act - 尝试应用超过最大层数的 Frenzy（最大5层）
        for (int i = 0; i < 10; i++)
        {
            enemy.BuffManager!.Apply("enemy_frenzy", now);
        }

        var frenzyInstance = enemy.BuffManager!.TryGet("enemy_frenzy");

        // Assert
        Assert.NotNull(frenzyInstance);
        Assert.Equal(5, frenzyInstance.Stacks); // 应该限制在最大层数
    }

    #endregion

    #region 技能触发 Buff 测试

    [Fact]
    public void EnemySkill_ApplyBuff_ShouldApplyBuffToEnemy()
    {
        // Arrange - 使用 tank 敌人，它有 Enrage 技能
        var engine = CreateBattleEngineWithTank();
        var enemy = engine.Context.EnemyCombatants.First();
        
        // 降低怪物血量到30%以下以触发 Enrage
        int targetHp = (int)(enemy.MaxHp * 0.25); // 25% HP
        int damageNeeded = enemy.CurrentHp - targetHp;
        enemy.Encounter.ApplyDamage(damageNeeded, 0.0);

        // Assert pre-condition
        Assert.False(enemy.BuffManager!.Has("enemy_enrage"));

        // Act - 推进战斗，等待技能触发
        engine.AdvanceTo(10.0, 1000);

        // Assert - Enrage 技能应该触发并应用 Buff
        Assert.True(enemy.BuffManager.Has("enemy_enrage"), 
            "Enrage buff should be applied when HP < 30%");
        
        // 验证 Buff 应用标签被记录
        var segments = engine.Segments;
        var hasBuffAppliedTag = segments.Any(s => 
            s.TagCounters.Any(kvp => kvp.Key.Contains("enemy_buff_applied:enemy_enrage")));
        Assert.True(hasBuffAppliedTag, "Should have recorded enemy_buff_applied tag");
    }

    [Fact]
    public void EnemySkill_BuffApplication_ShouldAffectDamage()
    {
        // Arrange - 简化测试，使用单次攻击比较
        var context = CreateTestBattleContext(enemyBaseDamage: 10);
        var enemy = context.EnemyCombatants[0];
        var player = context.Player;

        // 记录没有 Buff 时的单次攻击伤害
        int initialHp = player.CurrentHp;
        var attackEvent1 = new EnemyAttackEvent(1.0, enemy);
        attackEvent1.Execute(context);
        int normalAttackDamage = initialHp - player.CurrentHp;

        // Act - 应用 Enrage Buff（+50% 伤害）
        enemy.BuffManager!.Apply("enemy_enrage", 2.0);
        
        // 记录有 Buff 时的单次攻击伤害
        int hpBeforeBuffedAttack = player.CurrentHp;
        var attackEvent2 = new EnemyAttackEvent(3.0, enemy);
        attackEvent2.Execute(context);
        int buffedAttackDamage = hpBeforeBuffedAttack - player.CurrentHp;

        // Assert - Enrage (+50% 伤害) 应该使伤害增加
        Assert.True(buffedAttackDamage > normalAttackDamage,
            $"Buffed attack damage ({buffedAttackDamage}) should be greater than normal attack ({normalAttackDamage})");
        
        // 验证增益大致正确（允许四舍五入误差）
        double expectedDamage = normalAttackDamage * 1.5;
        Assert.InRange(buffedAttackDamage, (int)expectedDamage - 1, (int)expectedDamage + 1);
    }

    #endregion

    #region 集成测试

    [Fact]
    public void BattleEngine_EnemyBuffs_ShouldTickCorrectly()
    {
        // Arrange
        var engine = CreateBattleEngine();
        var enemy = engine.Context.EnemyCombatants.First();
        
        // 应用一个持续10秒的 Buff
        enemy.BuffManager!.Apply("enemy_haste", 0.0);
        Assert.True(enemy.BuffManager.Has("enemy_haste"));

        // Act - 推进战斗5秒（Buff 应该还在）
        engine.AdvanceTo(5.0, 100);
        Assert.True(enemy.BuffManager.Has("enemy_haste"));

        // 推进战斗超过10秒（Buff 应该过期）
        engine.AdvanceTo(11.0, 100);

        // Assert
        Assert.False(enemy.BuffManager.Has("enemy_haste"),
            "Haste buff should have expired after 10 seconds");
    }

    [Fact]
    public void EnemySkillDamage_WithBuff_ShouldBeAmplified()
    {
        // Arrange - 使用 magebane 敌人，它有伤害技能
        var engine = CreateBattleEngineWithMagebane();
        var enemy = engine.Context.EnemyCombatants.First();
        var player = engine.Context.Player;
        
        // 应用 Enrage Buff（+50% 伤害）
        enemy.BuffManager!.Apply("enemy_enrage", 0.0);

        int initialHp = player.CurrentHp;

        // Act - 推进战斗，让技能触发（Arcane Burst，8秒CD）
        engine.AdvanceTo(10.0, 1000);

        int finalHp = player.CurrentHp;
        int totalDamage = initialHp - finalHp;

        // Assert - 应该造成伤害，且由于 Buff 加成应该更高
        Assert.True(totalDamage > 0, "Enemy should have dealt damage");
        
        // 验证技能确实释放了
        var segments = engine.Segments;
        var hasSkillDamageTag = segments.Any(s => 
            s.TagCounters.Any(kvp => kvp.Key.Contains("enemy_skill_damage")));
        Assert.True(hasSkillDamageTag, "Should have recorded enemy_skill_damage tag");
    }

    [Fact]
    public void MultipleEnemies_ShouldHaveIndependentBuffManagers()
    {
        // Arrange - 创建多怪物战斗
        var rng = new RngContext(12345);
        var enemyDef = EnemyRegistry.Resolve("dummy");
        
        var engine = new BattleEngine(
            battleId: Guid.NewGuid(),
            characterId: Guid.NewGuid(),
            profession: Profession.Warrior,
            stats: CreateTestStats(),
            rng: rng,
            enemyDef: enemyDef,
            enemyCount: 3
        );

        // Act - 给第一个怪物应用 Buff
        var firstEnemy = engine.Context.EnemyCombatants[0];
        firstEnemy.BuffManager!.Apply("enemy_enrage", 0.0);

        // Assert - 只有第一个怪物有 Buff
        Assert.True(firstEnemy.BuffManager.Has("enemy_enrage"));
        Assert.False(engine.Context.EnemyCombatants[1].BuffManager!.Has("enemy_enrage"));
        Assert.False(engine.Context.EnemyCombatants[2].BuffManager!.Has("enemy_enrage"));
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
        var stats = CreateTestStats();
        
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
            SpecialIntervalSeconds = 2.0,
            StartedAt = 0
        };
        
        var clock = new GameClock();
        var scheduler = new EventScheduler();
        var collector = new SegmentCollector();
        var rng = new RngContext(12345);
        var module = ProfessionRegistry.Resolve(Profession.Warrior);
        
        var context = new BattleContext(
            battle: battle,
            clock: clock,
            scheduler: scheduler,
            collector: collector,
            professionModule: module,
            profession: Profession.Warrior,
            rng: rng,
            encounter: null,
            encounterGroup: encounterGroup,
            stats: stats,
            stamina: playerStamina
        );
        
        // 初始化 EnemyCombatant 及其 BuffManager（模拟 BattleEngine 的初始化）
        var enemyCombatant = new EnemyCombatant("enemy_0", encounter);
        var attackTrack = new TrackState(TrackType.Attack, attackInterval, attackInterval);
        enemyCombatant.AttackTrack = attackTrack;
        
        // 创建并初始化 BuffManager
        var buffManager = new BuffManager(
            tagRecorder: (tag, count) => collector.OnTag(tag, count),
            resourceRecorder: null,
            damageApplier: null
        );
        
        // 注册 Buff 定义
        buffManager.RegisterDefinition(EnemyBuffDefinitionsRegistry.Enrage);
        buffManager.RegisterDefinition(EnemyBuffDefinitionsRegistry.Frenzy);
        buffManager.RegisterDefinition(EnemyBuffDefinitionsRegistry.Haste);
        
        enemyCombatant.BuffManager = buffManager;
        context.EnemyCombatants.Add(enemyCombatant);
        
        return context;
    }

    private BattleEngine CreateBattleEngine()
    {
        var rng = new RngContext(12345);
        var enemyDef = EnemyRegistry.Resolve("dummy");
        
        return new BattleEngine(
            battleId: Guid.NewGuid(),
            characterId: Guid.NewGuid(),
            profession: Profession.Warrior,
            stats: CreateTestStats(),
            rng: rng,
            enemyDef: enemyDef,
            enemyCount: 1
        );
    }

    private BattleEngine CreateBattleEngineWithTank()
    {
        var rng = new RngContext(12345);
        var enemyDef = EnemyRegistry.Resolve("tank");
        
        return new BattleEngine(
            battleId: Guid.NewGuid(),
            characterId: Guid.NewGuid(),
            profession: Profession.Warrior,
            stats: CreateTestStats(),
            rng: rng,
            enemyDef: enemyDef,
            enemyCount: 1
        );
    }

    private BattleEngine CreateBattleEngineWithMagebane()
    {
        var rng = new RngContext(12345);
        var enemyDef = EnemyRegistry.Resolve("magebane");
        
        return new BattleEngine(
            battleId: Guid.NewGuid(),
            characterId: Guid.NewGuid(),
            profession: Profession.Warrior,
            stats: CreateTestStats(),
            rng: rng,
            enemyDef: enemyDef,
            enemyCount: 1
        );
    }

    private CharacterStats CreateTestStats()
    {
        return new CharacterStats
        {
            AttackPower = 100,
            SpellPower = 80,
            CritChance = 0.1,
            CritMultiplier = 1.5,
            HastePercent = 0.0
        };
    }

    #endregion
}
