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
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace BlazorIdle.Tests;

/// <summary>
/// Phase 5 单元测试：怪物技能系统
/// 测试技能冷却、触发条件、技能效果等
/// </summary>
public class EnemySkillTests
{
    #region EnemySkillDefinition Tests

    [Fact]
    public void EnemySkillDefinition_ShouldSupportAllProperties()
    {
        // Arrange & Act
        var skill = new EnemySkillDefinition(
            id: "test_skill",
            name: "Test Skill",
            cooldownSeconds: 10.0,
            trigger: TriggerType.OnCooldownReady,
            triggerValue: 0.5,
            activationChance: 0.8,
            effect: SkillEffectType.Damage,
            effectValue: 50,
            damageType: DamageType.Magic,
            buffId: "test_buff",
            maxTargets: 2
        );

        // Assert
        Assert.Equal("test_skill", skill.Id);
        Assert.Equal("Test Skill", skill.Name);
        Assert.Equal(10.0, skill.CooldownSeconds);
        Assert.Equal(TriggerType.OnCooldownReady, skill.Trigger);
        Assert.Equal(0.5, skill.TriggerValue);
        Assert.Equal(0.8, skill.ActivationChance);
        Assert.Equal(SkillEffectType.Damage, skill.Effect);
        Assert.Equal(50, skill.EffectValue);
        Assert.Equal(DamageType.Magic, skill.DamageType);
        Assert.Equal("test_buff", skill.BuffId);
        Assert.Equal(2, skill.MaxTargets);
    }

    [Fact]
    public void EnemySkillDefinition_DefaultValues_ShouldBeCorrect()
    {
        // Arrange & Act
        var skill = new EnemySkillDefinition(
            id: "simple_skill",
            name: "Simple Skill",
            cooldownSeconds: 5.0
        );

        // Assert
        Assert.Equal(TriggerType.OnCooldownReady, skill.Trigger);
        Assert.Equal(0.0, skill.TriggerValue);
        Assert.Equal(1.0, skill.ActivationChance);
        Assert.Equal(SkillEffectType.Damage, skill.Effect);
        Assert.Equal(0, skill.EffectValue);
        Assert.Equal(DamageType.Physical, skill.DamageType);
        Assert.Null(skill.BuffId);
        Assert.Equal(1, skill.MaxTargets);
    }

    #endregion

    #region EnemySkillSlot Tests

    [Fact]
    public void EnemySkillSlot_IsReady_ShouldRespectCooldown()
    {
        // Arrange
        var skill = CreateTestSkill("test", cooldown: 10.0);
        var slot = new EnemySkillSlot(skill);

        // Act & Assert - Initially ready
        Assert.True(slot.IsReady(0.0));

        // Mark used at time 5.0
        slot.MarkUsed(5.0);

        // Not ready before cooldown ends
        Assert.False(slot.IsReady(10.0));
        Assert.False(slot.IsReady(14.9));

        // Ready after cooldown ends
        Assert.True(slot.IsReady(15.0));
        Assert.True(slot.IsReady(20.0));
    }

    [Fact]
    public void EnemySkillSlot_OnCombatTimeElapsed_ShouldMarkTriggered()
    {
        // Arrange
        var skill = CreateTestSkill("test", trigger: TriggerType.OnCombatTimeElapsed);
        var slot = new EnemySkillSlot(skill);

        // Act
        Assert.False(slot.HasTriggered);
        slot.MarkUsed(10.0);

        // Assert
        Assert.True(slot.HasTriggered);
    }

    [Fact]
    public void EnemySkillSlot_OnCooldownReady_ShouldNotMarkTriggered()
    {
        // Arrange
        var skill = CreateTestSkill("test", trigger: TriggerType.OnCooldownReady);
        var slot = new EnemySkillSlot(skill);

        // Act
        slot.MarkUsed(10.0);

        // Assert
        Assert.False(slot.HasTriggered);
    }

    #endregion

    #region EnemySkillManager Tests

    [Fact]
    public void EnemySkillManager_CheckForReadySkill_ShouldReturnNullWhenNoCooldown()
    {
        // Arrange
        var context = CreateTestBattleContext();
        var enemy = context.EnemyCombatants[0];
        var skills = new List<EnemySkillDefinition>
        {
            CreateTestSkill("skill1", cooldown: 10.0)
        };
        var rng = new RngContext(12345);
        var manager = new EnemySkillManager(enemy, skills, 0.0, rng);

        // Mark skill as used
        manager.SkillSlots[0].MarkUsed(0.0);

        // Act
        var readySkill = manager.CheckForReadySkill(5.0);

        // Assert - Still on cooldown
        Assert.Null(readySkill);
    }

    [Fact]
    public void EnemySkillManager_CheckForReadySkill_ShouldReturnSkillWhenReady()
    {
        // Arrange
        var context = CreateTestBattleContext();
        var enemy = context.EnemyCombatants[0];
        var skills = new List<EnemySkillDefinition>
        {
            CreateTestSkill("skill1", cooldown: 10.0)
        };
        var rng = new RngContext(12345);
        var manager = new EnemySkillManager(enemy, skills, 0.0, rng);

        // Act - Check immediately (skill should be ready initially)
        var readySkill = manager.CheckForReadySkill(0.0);

        // Assert
        Assert.NotNull(readySkill);
        Assert.Equal("skill1", readySkill.Definition.Id);
    }

    [Fact]
    public void EnemySkillManager_OnHpBelow_ShouldNotTriggerAboveThreshold()
    {
        // Arrange
        var context = CreateTestBattleContext();
        var enemy = context.EnemyCombatants[0];
        var skills = new List<EnemySkillDefinition>
        {
            CreateTestSkill("low_hp_skill", 
                trigger: TriggerType.OnHpBelow, 
                triggerValue: 0.3) // 30% HP
        };
        var rng = new RngContext(12345);
        var manager = new EnemySkillManager(enemy, skills, 0.0, rng);

        // Enemy at full HP
        Assert.Equal(100, enemy.CurrentHp);

        // Act
        var readySkill = manager.CheckForReadySkill(0.0);

        // Assert - Should not trigger at full HP
        Assert.Null(readySkill);
    }

    [Fact]
    public void EnemySkillManager_OnHpBelow_ShouldTriggerBelowThreshold()
    {
        // Arrange
        var context = CreateTestBattleContext();
        var enemy = context.EnemyCombatants[0];
        var skills = new List<EnemySkillDefinition>
        {
            CreateTestSkill("low_hp_skill", 
                trigger: TriggerType.OnHpBelow, 
                triggerValue: 0.5) // 50% HP
        };
        var rng = new RngContext(12345);
        var manager = new EnemySkillManager(enemy, skills, 0.0, rng);

        // Damage enemy to 40% HP
        enemy.Encounter.ApplyDamage(60, 0.0);
        Assert.Equal(40, enemy.CurrentHp);

        // Act
        var readySkill = manager.CheckForReadySkill(0.0);

        // Assert - Should trigger below 50% HP
        Assert.NotNull(readySkill);
        Assert.Equal("low_hp_skill", readySkill.Definition.Id);
    }

    [Fact]
    public void EnemySkillManager_OnCombatTimeElapsed_ShouldNotTriggerBeforeTime()
    {
        // Arrange
        var context = CreateTestBattleContext();
        var enemy = context.EnemyCombatants[0];
        var skills = new List<EnemySkillDefinition>
        {
            CreateTestSkill("delayed_skill", 
                trigger: TriggerType.OnCombatTimeElapsed, 
                triggerValue: 30.0) // After 30 seconds
        };
        var rng = new RngContext(12345);
        var manager = new EnemySkillManager(enemy, skills, 0.0, rng);

        // Act - Check at 20 seconds
        var readySkill = manager.CheckForReadySkill(20.0);

        // Assert - Should not trigger before 30 seconds
        Assert.Null(readySkill);
    }

    [Fact]
    public void EnemySkillManager_OnCombatTimeElapsed_ShouldTriggerAfterTime()
    {
        // Arrange
        var context = CreateTestBattleContext();
        var enemy = context.EnemyCombatants[0];
        var skills = new List<EnemySkillDefinition>
        {
            CreateTestSkill("delayed_skill", 
                trigger: TriggerType.OnCombatTimeElapsed, 
                triggerValue: 30.0) // After 30 seconds
        };
        var rng = new RngContext(12345);
        var manager = new EnemySkillManager(enemy, skills, 0.0, rng);

        // Act - Check at 35 seconds
        var readySkill = manager.CheckForReadySkill(35.0);

        // Assert - Should trigger after 30 seconds
        Assert.NotNull(readySkill);
        Assert.Equal("delayed_skill", readySkill.Definition.Id);
    }

    [Fact]
    public void EnemySkillManager_OnCombatTimeElapsed_ShouldOnlyTriggerOnce()
    {
        // Arrange
        var context = CreateTestBattleContext();
        var enemy = context.EnemyCombatants[0];
        var skills = new List<EnemySkillDefinition>
        {
            CreateTestSkill("delayed_skill", 
                trigger: TriggerType.OnCombatTimeElapsed, 
                triggerValue: 30.0,
                cooldown: 5.0) // Short cooldown
        };
        var rng = new RngContext(12345);
        var manager = new EnemySkillManager(enemy, skills, 0.0, rng);

        // Act - First check at 35 seconds
        var readySkill1 = manager.CheckForReadySkill(35.0);
        Assert.NotNull(readySkill1);
        readySkill1.MarkUsed(35.0);

        // Second check at 45 seconds (after cooldown)
        var readySkill2 = manager.CheckForReadySkill(45.0);

        // Assert - Should not trigger again (already triggered)
        Assert.Null(readySkill2);
    }

    #endregion

    #region EnemySkillCastEvent Tests

    [Fact]
    public void EnemySkillCastEvent_Damage_ShouldDamagePlayer()
    {
        // Arrange
        var context = CreateTestBattleContext();
        var enemy = context.EnemyCombatants[0];
        var skill = CreateTestSkill("damage_skill", 
            effect: SkillEffectType.Damage, 
            effectValue: 30);
        var slot = new EnemySkillSlot(skill);
        var initialPlayerHp = context.Player.CurrentHp;

        // Act
        var castEvent = new EnemySkillCastEvent(1.0, enemy, slot);
        castEvent.Execute(context);

        // Assert
        Assert.Equal(initialPlayerHp - 30, context.Player.CurrentHp);
        Assert.False(context.Player.IsDead);
    }

    [Fact]
    public void EnemySkillCastEvent_Damage_ShouldTriggerPlayerDeathIfFatal()
    {
        // Arrange
        var context = CreateTestBattleContext();
        var enemy = context.EnemyCombatants[0];
        
        // Damage player to low HP first
        context.Player.ReceiveDamage(80, DamageType.Physical, 0.0);
        Assert.Equal(20, context.Player.CurrentHp);

        var skill = CreateTestSkill("fatal_skill", 
            effect: SkillEffectType.Damage, 
            effectValue: 25);
        var slot = new EnemySkillSlot(skill);

        // Act
        var castEvent = new EnemySkillCastEvent(1.0, enemy, slot);
        castEvent.Execute(context);

        // Assert - Player should be dead
        Assert.True(context.Player.IsDead);
        Assert.Equal(0, context.Player.CurrentHp);
        
        // Check if death event was scheduled
        var deathEvents = context.Scheduler.Count;
        Assert.True(deathEvents > 0); // At least one event should be scheduled
    }

    [Fact]
    public void EnemySkillCastEvent_Heal_ShouldHealEnemy()
    {
        // Arrange
        var context = CreateTestBattleContext();
        var enemy = context.EnemyCombatants[0];
        
        // Damage enemy first
        enemy.Encounter.ApplyDamage(60, 0.0);
        Assert.Equal(40, enemy.CurrentHp);

        var skill = CreateTestSkill("heal_skill", 
            effect: SkillEffectType.Heal, 
            effectValue: 30);
        var slot = new EnemySkillSlot(skill);

        // Act
        var castEvent = new EnemySkillCastEvent(1.0, enemy, slot);
        castEvent.Execute(context);

        // Assert
        Assert.Equal(70, enemy.CurrentHp);
    }

    [Fact]
    public void EnemySkillCastEvent_Heal_ShouldNotExceedMaxHp()
    {
        // Arrange
        var context = CreateTestBattleContext();
        var enemy = context.EnemyCombatants[0];
        
        // Damage enemy slightly
        enemy.Encounter.ApplyDamage(10, 0.0);
        Assert.Equal(90, enemy.CurrentHp);

        var skill = CreateTestSkill("big_heal_skill", 
            effect: SkillEffectType.Heal, 
            effectValue: 50); // More than needed
        var slot = new EnemySkillSlot(skill);

        // Act
        var castEvent = new EnemySkillCastEvent(1.0, enemy, slot);
        castEvent.Execute(context);

        // Assert - Should cap at max HP
        Assert.Equal(100, enemy.CurrentHp);
        Assert.Equal(enemy.MaxHp, enemy.CurrentHp);
    }

    [Fact]
    public void EnemySkillCastEvent_ApplyBuff_ShouldRecordTag()
    {
        // Arrange
        var context = CreateTestBattleContext();
        var enemy = context.EnemyCombatants[0];
        var skill = CreateTestSkill("buff_skill", 
            effect: SkillEffectType.ApplyBuff, 
            buffId: "test_buff");
        var slot = new EnemySkillSlot(skill);

        // Act
        var castEvent = new EnemySkillCastEvent(1.0, enemy, slot);
        castEvent.Execute(context);

        // Assert - Check tags were recorded by flushing the segment
        var segment = context.SegmentCollector.Flush(1.0);
        Assert.True(segment.TagCounters.ContainsKey("enemy_skill_cast:buff_skill"));
        Assert.True(segment.TagCounters.ContainsKey("enemy_buff_applied:test_buff"));
    }

    [Fact]
    public void EnemySkillCastEvent_ShouldMarkSkillUsed()
    {
        // Arrange
        var context = CreateTestBattleContext();
        var enemy = context.EnemyCombatants[0];
        var skill = CreateTestSkill("test_skill", cooldown: 10.0);
        var slot = new EnemySkillSlot(skill);
        
        Assert.True(slot.IsReady(0.0));

        // Act
        var castEvent = new EnemySkillCastEvent(5.0, enemy, slot);
        castEvent.Execute(context);

        // Assert - Skill should be on cooldown
        Assert.False(slot.IsReady(10.0));
        Assert.True(slot.IsReady(15.0));
    }

    [Fact]
    public void EnemySkillCastEvent_DeadEnemy_ShouldNotExecute()
    {
        // Arrange
        var context = CreateTestBattleContext();
        var enemy = context.EnemyCombatants[0];
        
        // Kill the enemy
        enemy.Encounter.ApplyDamage(1000, 0.0);
        Assert.True(enemy.IsDead);

        var skill = CreateTestSkill("test_skill", 
            effect: SkillEffectType.Damage, 
            effectValue: 30);
        var slot = new EnemySkillSlot(skill);
        var initialPlayerHp = context.Player.CurrentHp;

        // Act
        var castEvent = new EnemySkillCastEvent(1.0, enemy, slot);
        castEvent.Execute(context);

        // Assert - Player should not be damaged
        Assert.Equal(initialPlayerHp, context.Player.CurrentHp);
    }

    #endregion

    #region BattleEngine Integration Tests

    [Fact]
    public void BattleEngine_ShouldInitializeEnemySkills()
    {
        // Arrange
        var enemyDef = new EnemyDefinition(
            id: "test_enemy",
            name: "Test Enemy",
            level: 5,
            maxHp: 100,
            baseDamage: 5,  // Need attack for EnemyCombatant to be created
            attackIntervalSeconds: 3.0,
            skills: new List<EnemySkillDefinition>
            {
                CreateTestSkill("skill1", cooldown: 5.0)
            }
        );

        var battleId = Guid.NewGuid();
        var characterId = Guid.NewGuid();
        var profession = Profession.Warrior;
        var stats = new CharacterStats { AttackPower = 10, SpellPower = 0 };
        var rng = RngContext.FromGuid(battleId);

        // Act
        var engine = new BattleEngine(
            battleId,
            characterId,
            profession,
            stats,
            rng,
            enemyDef,
            enemyCount: 1,
            module: new TestProfessionModule()
        );

        // Assert
        Assert.NotEmpty(engine.Context.EnemyCombatants);
        var enemy = engine.Context.EnemyCombatants[0];
        Assert.NotNull(enemy.SkillManager);
        Assert.Single(enemy.SkillManager.SkillSlots);
    }

    [Fact]
    public void BattleEngine_ShouldScheduleSkillCheckEvents()
    {
        // Arrange
        var enemyDef = new EnemyDefinition(
            id: "test_enemy",
            name: "Test Enemy",
            level: 5,
            maxHp: 100,
            baseDamage: 5,
            attackIntervalSeconds: 3.0,
            skills: new List<EnemySkillDefinition>
            {
                CreateTestSkill("skill1", cooldown: 5.0)
            }
        );

        var battleId = Guid.NewGuid();
        var characterId = Guid.NewGuid();
        var profession = Profession.Warrior;
        var stats = new CharacterStats { AttackPower = 10, SpellPower = 0 };
        var rng = RngContext.FromGuid(battleId);

        var engine = new BattleEngine(
            battleId,
            characterId,
            profession,
            stats,
            rng,
            enemyDef,
            enemyCount: 1,
            module: new TestProfessionModule()
        );

        // Act - Advance time to trigger segment flush
        engine.AdvanceTo(6.0, 1000);

        // Assert - Check that skill check events were scheduled and executed
        var segments = engine.Segments;
        var tags = segments.SelectMany(s => s.TagCounters.Keys).ToList();
        
        // Should have initialized the skill manager
        Assert.Contains("enemy_skill_manager_initialized", tags);
    }

    [Fact]
    public void BattleEngine_EnemySkill_ShouldDamagePlayerInCombat()
    {
        // Arrange - Create enemy with a damage skill
        var damageSkill = new EnemySkillDefinition(
            id: "quick_strike",
            name: "Quick Strike",
            cooldownSeconds: 2.0,
            trigger: TriggerType.OnCooldownReady,
            effect: SkillEffectType.Damage,
            effectValue: 20,
            damageType: DamageType.Physical
        );

        var enemyDef = new EnemyDefinition(
            id: "test_enemy",
            name: "Test Enemy",
            level: 5,
            maxHp: 200,
            baseDamage: 5,
            attackIntervalSeconds: 3.0,
            skills: new List<EnemySkillDefinition> { damageSkill }
        );

        var battleId = Guid.NewGuid();
        var characterId = Guid.NewGuid();
        var profession = Profession.Warrior;
        var stats = new CharacterStats { AttackPower = 10, SpellPower = 0 };
        var rng = RngContext.FromGuid(battleId);

        var engine = new BattleEngine(
            battleId,
            characterId,
            profession,
            stats,
            rng,
            enemyDef,
            enemyCount: 1,
            module: new TestProfessionModule()
        );

        var initialPlayerHp = engine.Context.Player.CurrentHp;

        // Act - Advance time to trigger skill
        engine.AdvanceTo(5.0, 1000);

        // Assert - Player should have taken damage from skill
        var currentPlayerHp = engine.Context.Player.CurrentHp;
        Assert.True(currentPlayerHp < initialPlayerHp, "Player should have taken damage");

        // Check that skill cast event was recorded
        var allTags = engine.Segments.SelectMany(s => s.TagCounters.Keys).ToList();
        Assert.Contains("enemy_skill_cast:quick_strike", allTags);
    }

    #endregion

    #region Helper Methods

    private BattleContext CreateTestBattleContext()
    {
        var battleId = Guid.NewGuid();
        var characterId = Guid.NewGuid();
        var profession = Profession.Warrior;
        var stats = new CharacterStats { AttackPower = 10, SpellPower = 0 };
        var rng = RngContext.FromGuid(battleId);

        // Create enemy with attack capability
        var enemyDef = new EnemyDefinition(
            id: "test_enemy",
            name: "Test Enemy",
            level: 5,
            maxHp: 100,
            baseDamage: 5,
            attackIntervalSeconds: 3.0
        );

        var encounter = new Encounter(enemyDef);
        var encounterGroup = EncounterGroup.FromSingle(encounter);
        
        var battle = new Battle
        {
            Id = battleId,
            CharacterId = characterId,
            AttackIntervalSeconds = 2.0,
            SpecialIntervalSeconds = 5.0
        };
        
        var context = new BattleContext(
            battle: battle,
            clock: new GameClock(),
            scheduler: new EventScheduler(),
            collector: new SegmentCollector(),
            professionModule: new TestProfessionModule(),
            profession: profession,
            rng: rng,
            stats: stats,
            stamina: 10,
            encounterGroup: encounterGroup
        );

        // Create enemy combatant
        var enemyCombatant = new EnemyCombatant("enemy_0", encounter);
        context.EnemyCombatants.Add(enemyCombatant);

        return context;
    }

    private EnemySkillDefinition CreateTestSkill(
        string id,
        double cooldown = 5.0,
        TriggerType trigger = TriggerType.OnCooldownReady,
        double triggerValue = 0.0,
        double activationChance = 1.0,
        SkillEffectType effect = SkillEffectType.Damage,
        int effectValue = 10,
        DamageType damageType = DamageType.Physical,
        string? buffId = null)
    {
        return new EnemySkillDefinition(
            id: id,
            name: id,
            cooldownSeconds: cooldown,
            trigger: trigger,
            triggerValue: triggerValue,
            activationChance: activationChance,
            effect: effect,
            effectValue: effectValue,
            damageType: damageType,
            buffId: buffId
        );
    }

    /// <summary>
    /// 测试用的简单职业模块
    /// </summary>
    private class TestProfessionModule : IProfessionModule
    {
        public string Id => "test_profession";
        public double BaseAttackInterval => 2.0;
        public double BaseSpecialInterval => 5.0;

        public void RegisterBuffDefinitions(BattleContext ctx) { }
        public void OnBattleStart(BattleContext ctx) { }
        public void BuildSkills(BattleContext ctx, BlazorIdle.Server.Domain.Combat.Skills.AutoCastEngine autoCaster) { }
        public void OnAttackTick(BattleContext ctx, AttackTickEvent evt) { }
        public void OnSpecialPulse(BattleContext ctx, SpecialPulseEvent evt) { }
        public void OnSkillCast(BattleContext ctx, BlazorIdle.Server.Domain.Combat.Skills.SkillDefinition def) { }
    }

    #endregion
}
