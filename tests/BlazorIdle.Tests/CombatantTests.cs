using BlazorIdle.Server.Domain.Characters;
using BlazorIdle.Server.Domain.Combat;
using BlazorIdle.Server.Domain.Combat.Combatants;
using BlazorIdle.Server.Domain.Combat.Damage;
using BlazorIdle.Server.Domain.Combat.Enemies;
using BlazorIdle.Server.Domain.Combat.Professions;
using BlazorIdle.Server.Domain.Combat.Rng;
using BlazorIdle.Shared.Models;
using BlazorWebGame.Domain.Combat;
using Xunit;

namespace BlazorIdle.Tests;

/// <summary>
/// Phase 1: 基础架构准备 - Combatant 抽象层测试
/// </summary>
public class CombatantTests
{
    [Fact]
    public void PlayerCombatant_Creation_InitializesCorrectly()
    {
        // Arrange
        var stats = new CharacterStats
        {
            AttackPower = 10,
            SpellPower = 15
        };
        var characterId = "char-123";
        var characterName = "TestHero";
        var stamina = 5;

        // Act
        var player = new PlayerCombatant(stats, characterId, characterName, stamina);

        // Assert
        Assert.Equal(characterId, player.Id);
        Assert.Equal(characterName, player.Name);
        Assert.Equal(50, player.MaxHp); // 5 stamina * 10 HP per stamina
        Assert.Equal(50, player.CurrentHp); // Phase 1: 始终满血
        Assert.False(player.IsDead);
        Assert.Equal(CombatantState.Alive, player.State);
        Assert.Null(player.DeathTime);
        Assert.Null(player.ReviveAt);
        Assert.Equal(1.0, player.ThreatWeight);
    }

    [Fact]
    public void PlayerCombatant_CanBeTargeted_ReturnsTrue()
    {
        // Arrange
        var stats = new CharacterStats();
        var player = new PlayerCombatant(stats, "char-1", "Player", 10);

        // Act & Assert
        Assert.True(player.CanBeTargeted());
    }

    [Fact]
    public void PlayerCombatant_CanAct_ReturnsTrue()
    {
        // Arrange
        var stats = new CharacterStats();
        var player = new PlayerCombatant(stats, "char-1", "Player", 10);

        // Act & Assert
        Assert.True(player.CanAct());
    }

    [Fact]
    public void PlayerCombatant_ReceiveDamage_Phase1_DoesNotReduceHp()
    {
        // Arrange
        var stats = new CharacterStats();
        var player = new PlayerCombatant(stats, "char-1", "Player", 10);
        var initialHp = player.CurrentHp;

        // Act - Phase 1: 玩家不受伤害
        var actualDamage = player.ReceiveDamage(50, DamageType.Physical, 10.0);

        // Assert
        Assert.Equal(0, actualDamage); // Phase 1: 返回 0
        Assert.Equal(initialHp, player.CurrentHp); // HP 不变
        Assert.False(player.IsDead);
    }

    [Fact]
    public void PlayerCombatant_ThreatWeight_CanBeModified()
    {
        // Arrange
        var stats = new CharacterStats();
        var player = new PlayerCombatant(stats, "char-1", "Player", 10);

        // Act
        player.ThreatWeight = 5.0;

        // Assert
        Assert.Equal(5.0, player.ThreatWeight);
    }

    [Fact]
    public void EnemyCombatant_Creation_InitializesCorrectly()
    {
        // Arrange
        var enemyDef = new EnemyDefinition(
            id: "goblin-1",
            name: "Goblin",
            level: 5,
            maxHp: 100,
            armor: 10
        );
        var encounter = new Encounter(enemyDef);

        // Act
        var enemy = new EnemyCombatant(encounter);

        // Assert
        Assert.Equal("goblin-1", enemy.Id);
        Assert.Equal("Goblin", enemy.Name);
        Assert.Equal(100, enemy.MaxHp);
        Assert.Equal(100, enemy.CurrentHp);
        Assert.False(enemy.IsDead);
        Assert.Equal(CombatantState.Alive, enemy.State);
        Assert.Null(enemy.DeathTime);
        Assert.Null(enemy.ReviveAt);
        Assert.Equal(1.0, enemy.ThreatWeight);
    }

    [Fact]
    public void EnemyCombatant_ReceiveDamage_ReducesHp()
    {
        // Arrange
        var enemyDef = new EnemyDefinition("enemy-1", "Enemy", 1, 100);
        var encounter = new Encounter(enemyDef);
        var enemy = new EnemyCombatant(encounter);

        // Act
        var actualDamage = enemy.ReceiveDamage(30, DamageType.Physical, 5.0);

        // Assert
        Assert.Equal(30, actualDamage);
        Assert.Equal(70, enemy.CurrentHp);
        Assert.False(enemy.IsDead);
        Assert.Equal(CombatantState.Alive, enemy.State);
    }

    [Fact]
    public void EnemyCombatant_ReceiveDamage_Death_UpdatesStateCorrectly()
    {
        // Arrange
        var enemyDef = new EnemyDefinition("enemy-1", "Enemy", 1, 100);
        var encounter = new Encounter(enemyDef);
        var enemy = new EnemyCombatant(encounter);

        // Act
        var actualDamage = enemy.ReceiveDamage(150, DamageType.Physical, 10.0);

        // Assert
        Assert.Equal(150, actualDamage);
        Assert.Equal(0, enemy.CurrentHp);
        Assert.True(enemy.IsDead);
        Assert.Equal(CombatantState.Dead, enemy.State);
        Assert.Equal(10.0, enemy.DeathTime);
        Assert.Null(enemy.ReviveAt); // Phase 1: 怪物不复活
    }

    [Fact]
    public void EnemyCombatant_CanBeTargeted_AliveReturnsTrue()
    {
        // Arrange
        var enemyDef = new EnemyDefinition("enemy-1", "Enemy", 1, 100);
        var encounter = new Encounter(enemyDef);
        var enemy = new EnemyCombatant(encounter);

        // Act & Assert
        Assert.True(enemy.CanBeTargeted());
    }

    [Fact]
    public void EnemyCombatant_CanBeTargeted_DeadReturnsFalse()
    {
        // Arrange
        var enemyDef = new EnemyDefinition("enemy-1", "Enemy", 1, 100);
        var encounter = new Encounter(enemyDef);
        var enemy = new EnemyCombatant(encounter);
        enemy.ReceiveDamage(100, DamageType.Physical, 5.0);

        // Act & Assert
        Assert.False(enemy.CanBeTargeted());
    }

    [Fact]
    public void EnemyCombatant_CanAct_Phase1_ReturnsFalse()
    {
        // Arrange
        var enemyDef = new EnemyDefinition("enemy-1", "Enemy", 1, 100);
        var encounter = new Encounter(enemyDef);
        var enemy = new EnemyCombatant(encounter);

        // Act & Assert - Phase 1: 怪物暂不主动行动
        Assert.False(enemy.CanAct());
    }

    [Fact]
    public void EnemyCombatant_ThreatWeight_CanBeModified()
    {
        // Arrange
        var enemyDef = new EnemyDefinition("enemy-1", "Enemy", 1, 100);
        var encounter = new Encounter(enemyDef);
        var enemy = new EnemyCombatant(encounter);

        // Act
        enemy.ThreatWeight = 3.0;

        // Assert
        Assert.Equal(3.0, enemy.ThreatWeight);
    }

    [Fact]
    public void BattleContext_IncludesPlayerCombatant()
    {
        // Arrange
        var battleId = Guid.NewGuid();
        var characterId = Guid.NewGuid();
        var stats = new CharacterStats { AttackPower = 20 };
        var enemyDef = new EnemyDefinition("enemy-1", "Enemy", 1, 100);
        var encounter = new Encounter(enemyDef);
        
        var battle = new Battle
        {
            Id = battleId,
            CharacterId = characterId,
            AttackIntervalSeconds = 1.5,
            SpecialIntervalSeconds = 5.0,
            StartedAt = 0
        };

        var clock = new GameClock();
        var scheduler = new EventScheduler();
        var collector = new SegmentCollector();
        var rng = new RngContext(12345);

        // Act
        var context = new BattleContext(
            battle: battle,
            clock: clock,
            scheduler: scheduler,
            collector: collector,
            professionModule: ProfessionRegistry.Resolve(Profession.Warrior),
            profession: Profession.Warrior,
            rng: rng,
            encounter: encounter,
            encounterGroup: null,
            stats: stats,
            stamina: 8,
            characterName: "TestWarrior"
        );

        // Assert
        Assert.NotNull(context.Player);
        Assert.Equal(characterId.ToString(), context.Player.Id);
        Assert.Equal("TestWarrior", context.Player.Name);
        Assert.Equal(80, context.Player.MaxHp); // 8 stamina * 10
        Assert.Equal(stats, context.Player.Stats);
    }

    [Fact]
    public void CombatantInterface_AllowsPolymorphicUse()
    {
        // Arrange
        var playerStats = new CharacterStats();
        var player = new PlayerCombatant(playerStats, "player-1", "Hero", 10);

        var enemyDef = new EnemyDefinition("enemy-1", "Monster", 1, 100);
        var encounter = new Encounter(enemyDef);
        var enemy = new EnemyCombatant(encounter);

        // Act - 使用 ICombatant 接口
        ICombatant playerCombatant = player;
        ICombatant enemyCombatant = enemy;

        // Assert - 验证多态性
        Assert.True(playerCombatant.CanBeTargeted());
        Assert.True(enemyCombatant.CanBeTargeted());
        Assert.Equal(100, playerCombatant.CurrentHp);
        Assert.Equal(100, enemyCombatant.CurrentHp);
    }

    [Fact]
    public void CombatantState_EnumValues_AreCorrect()
    {
        // Assert
        Assert.Equal(0, (int)CombatantState.Alive);
        Assert.Equal(1, (int)CombatantState.Dead);
        Assert.Equal(2, (int)CombatantState.Reviving);
    }
}
