using BlazorIdle.Server.Domain.Characters;
using BlazorIdle.Server.Domain.Combat;
using BlazorIdle.Server.Domain.Combat.Combatants;
using BlazorIdle.Server.Domain.Combat.Damage;
using BlazorIdle.Server.Domain.Combat.Enemies;
using BlazorIdle.Shared.Models;
using BlazorWebGame.Domain.Combat;
using Xunit;

namespace BlazorIdle.Tests;

/// <summary>
/// Phase 1 单元测试：Combatant 创建与状态查询
/// 验证基础架构不影响现有战斗逻辑
/// </summary>
public class CombatantTests
{
    #region PlayerCombatant Tests

    [Fact]
    public void PlayerCombatant_Initialization_ShouldSetCorrectDefaults()
    {
        // Arrange & Act
        var stats = new CharacterStats { AttackPower = 100, SpellPower = 50 };
        var player = new PlayerCombatant("player1", "TestPlayer", stats, stamina: 20);

        // Assert
        Assert.Equal("player1", player.Id);
        Assert.Equal("TestPlayer", player.Name);
        Assert.Equal(200, player.MaxHp); // 20 stamina * 10 = 200 HP
        Assert.Equal(200, player.CurrentHp);
        Assert.Equal(CombatantState.Alive, player.State);
        Assert.False(player.IsDead);
        Assert.Null(player.DeathTime);
        Assert.Null(player.ReviveAt);
        Assert.Equal(1.0, player.ThreatWeight);
        Assert.Equal(10.0, player.ReviveDurationSeconds);
        Assert.True(player.AutoReviveEnabled);
    }

    [Fact]
    public void PlayerCombatant_CanBeTargeted_WhenAlive_ShouldReturnTrue()
    {
        // Arrange
        var player = new PlayerCombatant("p1", "Player", new CharacterStats());

        // Act & Assert
        Assert.True(player.CanBeTargeted());
    }

    [Fact]
    public void PlayerCombatant_CanAct_WhenAlive_ShouldReturnTrue()
    {
        // Arrange
        var player = new PlayerCombatant("p1", "Player", new CharacterStats());

        // Act & Assert
        Assert.True(player.CanAct());
    }

    [Fact]
    public void PlayerCombatant_ReceiveDamage_Phase3_ShouldTakeDamage()
    {
        // Arrange
        var player = new PlayerCombatant("p1", "Player", new CharacterStats(), stamina: 10);
        var initialHp = player.CurrentHp;

        // Act
        var actualDamage = player.ReceiveDamage(50, DamageType.Physical, 1.0);

        // Assert - Phase 3: 玩家实际受伤害
        Assert.Equal(50, actualDamage);
        Assert.Equal(initialHp - 50, player.CurrentHp);
        Assert.False(player.IsDead);
        Assert.Equal(CombatantState.Alive, player.State);
    }

    [Fact]
    public void PlayerCombatant_ThreatWeight_ShouldBeModifiable()
    {
        // Arrange
        var player = new PlayerCombatant("p1", "Player", new CharacterStats());

        // Act
        player.ThreatWeight = 5.0;

        // Assert
        Assert.Equal(5.0, player.ThreatWeight);
    }

    #endregion

    #region EnemyCombatant Tests

    [Fact]
    public void EnemyCombatant_Initialization_ShouldWrapEncounterCorrectly()
    {
        // Arrange
        var enemyDef = new EnemyDefinition("goblin", "Goblin", 5, 100, 10, 0.1, 1.0, 1.0, 1.0);
        var encounter = new Encounter(enemyDef);
        
        // Act
        var enemy = new EnemyCombatant("enemy1", encounter);

        // Assert
        Assert.Equal("enemy1", enemy.Id);
        Assert.Equal("Goblin", enemy.Name);
        Assert.Equal(100, enemy.MaxHp);
        Assert.Equal(100, enemy.CurrentHp);
        Assert.Equal(CombatantState.Alive, enemy.State);
        Assert.False(enemy.IsDead);
        Assert.Null(enemy.DeathTime);
        Assert.Null(enemy.ReviveAt);
        Assert.Equal(1.0, enemy.ThreatWeight);
    }

    [Fact]
    public void EnemyCombatant_CanBeTargeted_WhenAlive_ShouldReturnTrue()
    {
        // Arrange
        var enemyDef = new EnemyDefinition("goblin", "Goblin", 5, 100, 10, 0.1, 1.0, 1.0, 1.0);
        var encounter = new Encounter(enemyDef);
        var enemy = new EnemyCombatant("e1", encounter);

        // Act & Assert
        Assert.True(enemy.CanBeTargeted());
    }

    [Fact]
    public void EnemyCombatant_CanAct_WhenAlive_ShouldReturnTrue()
    {
        // Arrange
        var enemyDef = new EnemyDefinition("goblin", "Goblin", 5, 100, 10, 0.1, 1.0, 1.0, 1.0);
        var encounter = new Encounter(enemyDef);
        var enemy = new EnemyCombatant("e1", encounter);

        // Act & Assert
        Assert.True(enemy.CanAct());
    }

    [Fact]
    public void EnemyCombatant_ReceiveDamage_ShouldApplyDamageToEncounter()
    {
        // Arrange
        var enemyDef = new EnemyDefinition("goblin", "Goblin", 5, 100, 10, 0.1, 1.0, 1.0, 1.0);
        var encounter = new Encounter(enemyDef);
        var enemy = new EnemyCombatant("e1", encounter);

        // Act
        var actualDamage = enemy.ReceiveDamage(30, DamageType.Physical, 1.0);

        // Assert
        Assert.Equal(30, actualDamage);
        Assert.Equal(70, enemy.CurrentHp);
        Assert.False(enemy.IsDead);
        Assert.Equal(CombatantState.Alive, enemy.State);
    }

    [Fact]
    public void EnemyCombatant_ReceiveDamage_WhenKilled_ShouldTransitionToDead()
    {
        // Arrange
        var enemyDef = new EnemyDefinition("goblin", "Goblin", 5, 100, 10, 0.1, 1.0, 1.0, 1.0);
        var encounter = new Encounter(enemyDef);
        var enemy = new EnemyCombatant("e1", encounter);

        // Act
        enemy.ReceiveDamage(120, DamageType.Physical, 5.0);

        // Assert
        Assert.True(enemy.IsDead);
        Assert.Equal(0, enemy.CurrentHp);
        Assert.Equal(CombatantState.Dead, enemy.State);
        Assert.Equal(5.0, enemy.DeathTime);
        Assert.Null(enemy.ReviveAt); // 敌人不复活
        Assert.False(enemy.CanBeTargeted());
        Assert.False(enemy.CanAct());
    }

    [Fact]
    public void EnemyCombatant_ThreatWeight_ShouldBeModifiable()
    {
        // Arrange
        var enemyDef = new EnemyDefinition("goblin", "Goblin", 5, 100, 10, 0.1, 1.0, 1.0, 1.0);
        var encounter = new Encounter(enemyDef);
        var enemy = new EnemyCombatant("e1", encounter);

        // Act
        enemy.ThreatWeight = 3.0;

        // Assert
        Assert.Equal(3.0, enemy.ThreatWeight);
    }

    #endregion

    #region BattleContext Integration Tests

    [Fact]
    public void BattleContext_ShouldInitializePlayerCombatant()
    {
        // Arrange
        var battleId = Guid.NewGuid();
        var characterId = Guid.NewGuid();
        var stats = new CharacterStats { AttackPower = 100 };
        
        // Act
        var context = CreateTestBattleContext(battleId, characterId, stats, stamina: 15);

        // Assert
        Assert.NotNull(context.Player);
        Assert.Equal(characterId.ToString(), context.Player.Id);
        Assert.Equal(150, context.Player.MaxHp); // 15 stamina * 10
        Assert.Equal(CombatantState.Alive, context.Player.State);
        Assert.Same(stats, context.Player.Stats);
    }

    [Fact]
    public void BattleContext_WithCustomCharacterInfo_ShouldUseProvidedValues()
    {
        // Arrange
        var battleId = Guid.NewGuid();
        var characterId = Guid.NewGuid();
        var stats = new CharacterStats();

        // Act
        var context = CreateTestBattleContext(
            battleId, 
            characterId, 
            stats, 
            stamina: 20,
            characterIdStr: "custom_char_id",
            characterName: "CustomName"
        );

        // Assert
        Assert.Equal("custom_char_id", context.Player.Id);
        Assert.Equal("CustomName", context.Player.Name);
        Assert.Equal(200, context.Player.MaxHp);
    }

    [Fact]
    public void BattleContext_BackwardCompatibility_ShouldWorkWithoutNewParameters()
    {
        // Arrange
        var battleId = Guid.NewGuid();
        var characterId = Guid.NewGuid();
        var battle = new Battle { Id = battleId, CharacterId = characterId };
        var stats = new CharacterStats();

        // Act - 使用旧的构造方式（不传 stamina/characterId/characterName）
        var context = new BattleContext(
            battle: battle,
            clock: new GameClock(),
            scheduler: new EventScheduler(),
            collector: new SegmentCollector(),
            professionModule: new TestProfessionModule(),
            profession: Profession.Warrior,
            rng: new Server.Domain.Combat.Rng.RngContext(12345),
            stats: stats
        );

        // Assert - 应该使用默认值
        Assert.NotNull(context.Player);
        Assert.Equal(100, context.Player.MaxHp); // 默认 10 stamina
        Assert.Equal(characterId.ToString(), context.Player.Id);
        Assert.Equal("Player", context.Player.Name);
    }

    #endregion

    #region Helper Methods

    private BattleContext CreateTestBattleContext(
        Guid battleId,
        Guid characterId,
        CharacterStats stats,
        int stamina = 10,
        string? characterIdStr = null,
        string? characterName = null)
    {
        var battle = new Battle
        {
            Id = battleId,
            CharacterId = characterId,
            AttackIntervalSeconds = 2.0,
            SpecialIntervalSeconds = 5.0
        };

        return new BattleContext(
            battle: battle,
            clock: new GameClock(),
            scheduler: new EventScheduler(),
            collector: new SegmentCollector(),
            professionModule: new TestProfessionModule(),
            profession: Profession.Warrior,
            rng: new Server.Domain.Combat.Rng.RngContext(12345),
            stats: stats,
            stamina: stamina,
            characterId: characterIdStr,
            characterName: characterName
        );
    }

    /// <summary>
    /// 测试用的简单职业模块
    /// </summary>
    private class TestProfessionModule : Server.Domain.Combat.Professions.IProfessionModule
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
