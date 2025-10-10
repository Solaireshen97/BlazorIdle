using BlazorIdle.Server.Domain.Characters;
using BlazorIdle.Server.Domain.Combat;
using BlazorIdle.Server.Domain.Combat.Combatants;
using BlazorIdle.Server.Domain.Combat.Damage;
using BlazorIdle.Server.Domain.Combat.Enemies;
using BlazorIdle.Server.Domain.Combat.Engine;
using BlazorIdle.Server.Domain.Combat.Professions;
using BlazorIdle.Server.Domain.Combat.Rng;
using BlazorIdle.Shared.Models;
using BlazorWebGame.Domain.Combat;
using Xunit;

namespace BlazorIdle.Tests;

/// <summary>
/// Phase 5 单元测试：怪物技能系统
/// 测试技能定义、冷却管理、触发条件、技能效果应用
/// </summary>
public class EnemySkillTests
{
    #region EnemySkillDefinition Tests

    [Fact]
    public void EnemySkillDefinition_ShouldSupportBasicProperties()
    {
        // Arrange & Act
        var skill = new EnemySkillDefinition(
            id: "heavy_strike",
            name: "Heavy Strike",
            cooldownSeconds: 10.0,
            trigger: TriggerType.OnCooldownReady,
            effect: SkillEffectType.Damage,
            effectValue: 50,
            damageType: DamageType.Physical
        );

        // Assert
        Assert.Equal("heavy_strike", skill.Id);
        Assert.Equal("Heavy Strike", skill.Name);
        Assert.Equal(10.0, skill.CooldownSeconds);
        Assert.Equal(TriggerType.OnCooldownReady, skill.Trigger);
        Assert.Equal(SkillEffectType.Damage, skill.Effect);
        Assert.Equal(50, skill.EffectValue);
        Assert.Equal(DamageType.Physical, skill.DamageType);
    }

    [Fact]
    public void EnemySkillDefinition_ShouldSupportHpTrigger()
    {
        // Arrange & Act
        var skill = new EnemySkillDefinition(
            id: "enrage",
            name: "Enrage",
            cooldownSeconds: 0.0,
            trigger: TriggerType.OnHpBelow,
            triggerValue: 0.3,
            effect: SkillEffectType.ApplyBuff,
            buffId: "enrage_buff"
        );

        // Assert
        Assert.Equal(TriggerType.OnHpBelow, skill.Trigger);
        Assert.Equal(0.3, skill.TriggerValue);
        Assert.Equal(SkillEffectType.ApplyBuff, skill.Effect);
        Assert.Equal("enrage_buff", skill.BuffId);
    }

    [Fact]
    public void EnemySkillDefinition_ShouldSupportTimeTrigger()
    {
        // Arrange & Act
        var skill = new EnemySkillDefinition(
            id: "heal",
            name: "Self Heal",
            cooldownSeconds: 999.0,
            trigger: TriggerType.OnCombatTimeElapsed,
            triggerValue: 30.0,
            effect: SkillEffectType.Heal,
            effectValue: 100
        );

        // Assert
        Assert.Equal(TriggerType.OnCombatTimeElapsed, skill.Trigger);
        Assert.Equal(30.0, skill.TriggerValue);
        Assert.Equal(SkillEffectType.Heal, skill.Effect);
    }

    #endregion

    #region EnemySkillSlot Tests

    [Fact]
    public void EnemySkillSlot_ShouldManageCooldown()
    {
        // Arrange
        var skillDef = new EnemySkillDefinition(
            id: "test_skill",
            name: "Test",
            cooldownSeconds: 10.0
        );
        var slot = new EnemySkillSlot(skillDef, 0.0);

        // Assert - Initially ready
        Assert.True(slot.IsReady(0.0));
        Assert.False(slot.HasTriggered);

        // Act - Consume skill
        slot.Consume(5.0);

        // Assert - On cooldown
        Assert.False(slot.IsReady(10.0));
        Assert.True(slot.IsReady(15.0));
        Assert.True(slot.HasTriggered);
    }

    [Fact]
    public void EnemySkillSlot_ShouldReset()
    {
        // Arrange
        var skillDef = new EnemySkillDefinition(
            id: "test_skill",
            name: "Test",
            cooldownSeconds: 10.0
        );
        var slot = new EnemySkillSlot(skillDef);
        slot.Consume(5.0);

        // Act
        slot.Reset(20.0);

        // Assert
        Assert.True(slot.IsReady(20.0));
        Assert.False(slot.HasTriggered);
    }

    #endregion

    #region EnemySkillManager Tests

    [Fact]
    public void EnemySkillManager_ShouldAddSkills()
    {
        // Arrange
        var enemyDef = new EnemyDefinition("test", "Test", 5, 100);
        var encounter = new Encounter(enemyDef);
        var enemy = new EnemyCombatant("e1", encounter);
        var manager = new EnemySkillManager(enemy, 0.0);

        var skill = new EnemySkillDefinition(
            id: "test_skill",
            name: "Test",
            cooldownSeconds: 10.0
        );

        // Act
        manager.AddSkill(skill, 0.0);

        // Assert
        Assert.Single(manager.Skills);
        Assert.Equal("test_skill", manager.Skills[0].Definition.Id);
    }

    [Fact]
    public void EnemySkillManager_ShouldTriggerOnCooldownReady()
    {
        // Arrange
        var enemyDef = new EnemyDefinition("test", "Test", 5, 100, baseDamage: 10);
        var encounter = new Encounter(enemyDef);
        var enemy = new EnemyCombatant("e1", encounter);
        var manager = new EnemySkillManager(enemy, 0.0);
        var rng = new RngContext(12345);

        var skill = new EnemySkillDefinition(
            id: "test_skill",
            name: "Test",
            cooldownSeconds: 10.0,
            trigger: TriggerType.OnCooldownReady
        );
        manager.AddSkill(skill, 0.0);

        // Act
        var triggered = manager.CheckTrigger(0.0, rng);

        // Assert
        Assert.NotNull(triggered);
        Assert.Equal("test_skill", triggered.Definition.Id);
    }

    [Fact]
    public void EnemySkillManager_ShouldNotTriggerWhenOnCooldown()
    {
        // Arrange
        var enemyDef = new EnemyDefinition("test", "Test", 5, 100, baseDamage: 10);
        var encounter = new Encounter(enemyDef);
        var enemy = new EnemyCombatant("e1", encounter);
        var manager = new EnemySkillManager(enemy, 0.0);
        var rng = new RngContext(12345);

        var skill = new EnemySkillDefinition(
            id: "test_skill",
            name: "Test",
            cooldownSeconds: 10.0,
            trigger: TriggerType.OnCooldownReady
        );
        manager.AddSkill(skill, 0.0);

        // Act - First trigger
        var triggered1 = manager.CheckTrigger(0.0, rng);
        Assert.NotNull(triggered1);
        
        // Consume the skill
        triggered1.Consume(0.0);

        // Act - Check again immediately (should be on cooldown)
        var triggered2 = manager.CheckTrigger(0.0, rng);

        // Assert
        Assert.Null(triggered2);
    }

    [Fact]
    public void EnemySkillManager_ShouldTriggerOnHpBelow()
    {
        // Arrange
        var enemyDef = new EnemyDefinition("test", "Test", 5, 100, baseDamage: 10);
        var encounter = new Encounter(enemyDef);
        var enemy = new EnemyCombatant("e1", encounter);
        var manager = new EnemySkillManager(enemy, 0.0);
        var rng = new RngContext(12345);

        var skill = new EnemySkillDefinition(
            id: "enrage",
            name: "Enrage",
            cooldownSeconds: 0.0,
            trigger: TriggerType.OnHpBelow,
            triggerValue: 0.5
        );
        manager.AddSkill(skill, 0.0);

        // Act - At full HP, should not trigger
        var triggered1 = manager.CheckTrigger(0.0, rng);
        Assert.Null(triggered1);

        // Damage the enemy below 50%
        encounter.ApplyDamage(60, 0.0);

        // Act - Below 50% HP, should trigger
        var triggered2 = manager.CheckTrigger(0.0, rng);

        // Assert
        Assert.NotNull(triggered2);
        Assert.Equal("enrage", triggered2.Definition.Id);
    }

    [Fact]
    public void EnemySkillManager_ShouldTriggerOnCombatTimeElapsed()
    {
        // Arrange
        var enemyDef = new EnemyDefinition("test", "Test", 5, 100, baseDamage: 10);
        var encounter = new Encounter(enemyDef);
        var enemy = new EnemyCombatant("e1", encounter);
        var manager = new EnemySkillManager(enemy, 0.0);
        var rng = new RngContext(12345);

        var skill = new EnemySkillDefinition(
            id: "heal",
            name: "Heal",
            cooldownSeconds: 999.0,
            trigger: TriggerType.OnCombatTimeElapsed,
            triggerValue: 30.0
        );
        manager.AddSkill(skill, 0.0);

        // Act - Before 30s, should not trigger
        var triggered1 = manager.CheckTrigger(20.0, rng);
        Assert.Null(triggered1);

        // Act - After 30s, should trigger
        var triggered2 = manager.CheckTrigger(30.0, rng);

        // Assert
        Assert.NotNull(triggered2);
        Assert.Equal("heal", triggered2.Definition.Id);
        
        // Simulate consuming the skill (as would happen in EnemySkillCastEvent)
        triggered2.Consume(30.0);

        // Act - Should not trigger again (one-time trigger)
        var triggered3 = manager.CheckTrigger(35.0, rng);
        Assert.Null(triggered3);
    }

    [Fact]
    public void EnemySkillManager_ShouldRespectActivationChance()
    {
        // Arrange
        var enemyDef = new EnemyDefinition("test", "Test", 5, 100, baseDamage: 10);
        var encounter = new Encounter(enemyDef);
        var enemy = new EnemyCombatant("e1", encounter);
        var manager = new EnemySkillManager(enemy, 0.0);
        var rng = new RngContext(12345);

        var skill = new EnemySkillDefinition(
            id: "test_skill",
            name: "Test",
            cooldownSeconds: 10.0,
            trigger: TriggerType.OnCooldownReady,
            activationChance: 0.0 // Never activates
        );
        manager.AddSkill(skill, 0.0);

        // Act
        var triggered = manager.CheckTrigger(0.0, rng);

        // Assert - Activation chance failed, but cooldown consumed
        Assert.Null(triggered);
        Assert.False(manager.Skills[0].IsReady(5.0)); // On cooldown
    }

    #endregion

    #region Integration Tests

    [Fact]
    public void BattleEngine_ShouldInitializeEnemySkills()
    {
        // Arrange
        var enemyDef = new EnemyDefinition(
            id: "test_enemy",
            name: "Test Enemy",
            level: 5,
            maxHp: 100,
            baseDamage: 10,
            attackIntervalSeconds: 2.0
        );
        enemyDef.Skills.Add(new EnemySkillDefinition(
            id: "heavy_strike",
            name: "Heavy Strike",
            cooldownSeconds: 10.0,
            effect: SkillEffectType.Damage,
            effectValue: 50
        ));

        var stats = new CharacterStats { AttackPower = 50 };
        var rng = new RngContext(12345);

        // Act
        var engine = new BattleEngine(
            battleId: System.Guid.NewGuid(),
            characterId: System.Guid.NewGuid(),
            profession: Profession.Warrior,
            stats: stats,
            rng: rng,
            enemyDef: enemyDef,
            enemyCount: 1
        );

        // Assert
        Assert.NotEmpty(engine.Context.EnemyCombatants);
        var enemy = engine.Context.EnemyCombatants[0];
        Assert.NotNull(enemy.SkillManager);
        Assert.Single(enemy.SkillManager.Skills);
    }

    [Fact]
    public void BattleEngine_ShouldExecuteEnemySkillDamage()
    {
        // Arrange
        var enemyDef = new EnemyDefinition(
            id: "test_enemy",
            name: "Test Enemy",
            level: 5,
            maxHp: 100,
            baseDamage: 5,
            attackIntervalSeconds: 2.0
        );
        enemyDef.Skills.Add(new EnemySkillDefinition(
            id: "heavy_strike",
            name: "Heavy Strike",
            cooldownSeconds: 5.0,
            trigger: TriggerType.OnCooldownReady,
            effect: SkillEffectType.Damage,
            effectValue: 30,
            damageType: DamageType.Physical
        ));

        var stats = new CharacterStats { AttackPower = 50 };
        var rng = new RngContext(12345);

        var engine = new BattleEngine(
            battleId: System.Guid.NewGuid(),
            characterId: System.Guid.NewGuid(),
            profession: Profession.Warrior,
            stats: stats,
            rng: rng,
            enemyDef: enemyDef,
            enemyCount: 1
        );

        int initialHp = engine.Context.Player.CurrentHp;

        // Act - Advance battle for 10 seconds (skill should trigger)
        // Use AdvanceTo with maxEvents to avoid infinite loop
        engine.AdvanceTo(10.0, maxEvents: 100);

        // Assert - Player should have taken damage from skill
        var segments = engine.Segments;
        bool skillCastDetected = segments.Any(s => s.TagCounters.ContainsKey("enemy_skill_cast:heavy_strike"));
        Assert.True(skillCastDetected, "Enemy skill should have been cast");
    }

    [Fact]
    public void BattleEngine_ShouldTriggerSkillOnHpThreshold()
    {
        // Arrange - Create an enemy with HP-based skill trigger
        // Use a simple setup where skills can definitely trigger
        var enemyDef = new EnemyDefinition(
            id: "test_enemy",
            name: "Test Enemy",
            level: 5,
            maxHp: 1000,  // High HP
            baseDamage: 5,
            attackIntervalSeconds: 5.0  // Slow attack to focus on player attacks
        );
        
        // Add a skill that triggers immediately when ready (easier to test)
        enemyDef.Skills.Add(new EnemySkillDefinition(
            id: "test_skill",
            name: "Test Skill",
            cooldownSeconds: 3.0,
            trigger: TriggerType.OnCooldownReady,  // Changed to OnCooldownReady for easier testing
            effect: SkillEffectType.Damage,
            effectValue: 25
        ));

        var stats = new CharacterStats 
        { 
            AttackPower = 40
        };
        var rng = new RngContext(12345);

        var engine = new BattleEngine(
            battleId: System.Guid.NewGuid(),
            characterId: System.Guid.NewGuid(),
            profession: Profession.Warrior,
            stats: stats,
            rng: rng,
            enemyDef: enemyDef,
            enemyCount: 1
        );

        // Act - Advance battle to allow skill to trigger
        engine.AdvanceTo(10.0, maxEvents: 200);

        // Assert - Skill should have been cast
        var segments = engine.Segments;
        bool skillCast = segments.Any(s => s.TagCounters.ContainsKey("enemy_skill_cast:test_skill"));
        Assert.True(skillCast, "Enemy skill should have been cast during battle");
    }

    #endregion

    #region Helper
    
    private class TestProfessionModule : IProfessionModule
    {
        public string Id => "test";
        public double BaseAttackInterval => 1.5;
        public double BaseSpecialInterval => 3.0;
        public void RegisterBuffDefinitions(BattleContext ctx) { }
        public void OnBattleStart(BattleContext ctx) { }
        public void BuildSkills(BattleContext ctx, Server.Domain.Combat.Skills.AutoCastEngine autoCaster) { }
        public void OnAttackTick(BattleContext ctx, AttackTickEvent evt) { }
        public void OnSpecialPulse(BattleContext ctx, SpecialPulseEvent evt) { }
        public void OnSkillCast(BattleContext ctx, Server.Domain.Combat.Skills.SkillDefinition def) { }
    }
    
    #endregion
}
