using BlazorIdle.Server.Domain.Characters;
using BlazorIdle.Server.Domain.Combat;
using BlazorIdle.Server.Domain.Combat.Combatants;
using BlazorIdle.Server.Domain.Combat.Damage;
using BlazorIdle.Shared.Models;
using BlazorWebGame.Domain.Combat;
using Xunit;

namespace BlazorIdle.Tests;

/// <summary>
/// Phase 3 单元测试：玩家死亡与复活系统
/// 验证玩家受伤、死亡、复活流程
/// </summary>
public class PlayerDeathReviveTests
{
    [Fact]
    public void PlayerCombatant_ReceiveDamage_ShouldReduceHp()
    {
        // Arrange
        var player = new PlayerCombatant("p1", "Player", new CharacterStats(), stamina: 10);
        var initialHp = player.CurrentHp;

        // Act
        var actualDamage = player.ReceiveDamage(30, DamageType.Physical, 1.0);

        // Assert
        Assert.Equal(30, actualDamage);
        Assert.Equal(initialHp - 30, player.CurrentHp);
        Assert.False(player.IsDead);
        Assert.Equal(CombatantState.Alive, player.State);
    }

    [Fact]
    public void PlayerCombatant_ReceiveDamage_WhenDead_ShouldNotTakeDamage()
    {
        // Arrange
        var player = new PlayerCombatant("p1", "Player", new CharacterStats(), stamina: 10);
        
        // 先击杀玩家
        player.ReceiveDamage(150, DamageType.Physical, 1.0);
        Assert.True(player.IsDead);
        
        var hpAfterDeath = player.CurrentHp;

        // Act - 死亡后再次受伤
        var actualDamage = player.ReceiveDamage(50, DamageType.Physical, 2.0);

        // Assert - 不应受到伤害
        Assert.Equal(0, actualDamage);
        Assert.Equal(hpAfterDeath, player.CurrentHp);
    }

    [Fact]
    public void PlayerCombatant_ReceiveDamage_WhenKilled_ShouldTransitionToDead()
    {
        // Arrange
        var player = new PlayerCombatant("p1", "Player", new CharacterStats(), stamina: 10);

        // Act
        player.ReceiveDamage(150, DamageType.Physical, 5.0);

        // Assert
        Assert.True(player.IsDead);
        Assert.Equal(0, player.CurrentHp);
        Assert.Equal(CombatantState.Dead, player.State);
        Assert.Equal(5.0, player.DeathTime);
        Assert.False(player.CanBeTargeted());
        Assert.False(player.CanAct());
    }

    [Fact]
    public void PlayerCombatant_OnDeath_ShouldScheduleReviveTime()
    {
        // Arrange
        var player = new PlayerCombatant("p1", "Player", new CharacterStats(), stamina: 10)
        {
            ReviveDurationSeconds = 10.0,
            AutoReviveEnabled = true
        };

        // Act
        player.ReceiveDamage(150, DamageType.Physical, 5.0);

        // Assert
        Assert.Equal(15.0, player.ReviveAt); // 5.0 + 10.0
    }

    [Fact]
    public void PlayerCombatant_OnDeath_WhenAutoReviveDisabled_ShouldNotScheduleRevive()
    {
        // Arrange
        var player = new PlayerCombatant("p1", "Player", new CharacterStats(), stamina: 10)
        {
            AutoReviveEnabled = false
        };

        // Act
        player.ReceiveDamage(150, DamageType.Physical, 5.0);

        // Assert
        Assert.Null(player.ReviveAt);
    }

    [Fact]
    public void PlayerCombatant_OnRevive_ShouldRestoreFullHp()
    {
        // Arrange
        var player = new PlayerCombatant("p1", "Player", new CharacterStats(), stamina: 10);
        player.ReceiveDamage(150, DamageType.Physical, 5.0);
        Assert.True(player.IsDead);

        // Act
        player.OnRevive(15.0);

        // Assert
        Assert.False(player.IsDead);
        Assert.Equal(player.MaxHp, player.CurrentHp);
        Assert.Equal(CombatantState.Alive, player.State);
        Assert.Null(player.DeathTime);
        Assert.Null(player.ReviveAt);
        Assert.True(player.CanBeTargeted());
        Assert.True(player.CanAct());
    }

    [Fact]
    public void PlayerDeathEvent_Execute_ShouldPauseAllTracks()
    {
        // Arrange
        var context = CreateTestBattleContext();
        var player = context.Player;
        
        // 模拟玩家死亡
        player.ReceiveDamageWithContext(150, DamageType.Physical, 5.0, context);
        
        // 创建一些 Track
        var track1 = new TrackState(TrackType.Attack, 1.0, 6.0);
        var track2 = new TrackState(TrackType.Special, 2.0, 7.0);
        context.Tracks.Add(track1);
        context.Tracks.Add(track2);
        
        var deathEvent = new PlayerDeathEvent(5.0, player);

        // Act
        deathEvent.Execute(context);

        // Assert
        Assert.True(track1.NextTriggerAt > 1e8); // 被设置为极大值
        Assert.True(track2.NextTriggerAt > 1e8);
    }

    [Fact]
    public void PlayerDeathEvent_Execute_ShouldScheduleReviveEvent()
    {
        // Arrange
        var context = CreateTestBattleContext();
        var player = context.Player;
        player.ReviveDurationSeconds = 10.0;
        
        // 手动让玩家死亡（不通过 ReceiveDamageWithContext，避免自动调度死亡事件）
        player.ReceiveDamage(150, DamageType.Physical, 5.0);
        
        var deathEvent = new PlayerDeathEvent(5.0, player);

        // Act
        deathEvent.Execute(context);

        // Assert - 应该已调度复活事件
        var nextEvent = context.Scheduler.PeekNext();
        Assert.NotNull(nextEvent);
        Assert.IsType<PlayerReviveEvent>(nextEvent);
        Assert.Equal(15.0, nextEvent.ExecuteAt);
    }

    [Fact]
    public void PlayerReviveEvent_Execute_ShouldRestorePlayerAndTracks()
    {
        // Arrange
        var context = CreateTestBattleContext();
        var player = context.Player;
        
        // 玩家死亡
        player.ReceiveDamageWithContext(150, DamageType.Physical, 5.0, context);
        
        // 创建一些被暂停的 Track
        var track1 = new TrackState(TrackType.Attack, 1.0, 1e9);
        var track2 = new TrackState(TrackType.Special, 2.0, 1e9);
        context.Tracks.Add(track1);
        context.Tracks.Add(track2);
        
        var reviveEvent = new PlayerReviveEvent(15.0, player);

        // Act
        reviveEvent.Execute(context);

        // Assert
        Assert.False(player.IsDead);
        Assert.Equal(player.MaxHp, player.CurrentHp);
        
        // Track 应该被恢复到当前时间附近
        Assert.True(track1.NextTriggerAt < 20.0);
        Assert.True(track2.NextTriggerAt < 20.0);
    }

    [Fact]
    public void AttackTickEvent_WhenPlayerDead_ShouldSkipAttack()
    {
        // Arrange
        var context = CreateTestBattleContext();
        var player = context.Player;
        
        // 玩家死亡
        player.ReceiveDamageWithContext(150, DamageType.Physical, 5.0, context);
        Assert.True(player.IsDead);
        
        var track = new TrackState(TrackType.Attack, 2.0, 6.0);
        context.Tracks.Add(track);
        
        var attackEvent = new AttackTickEvent(6.0, track);

        // Act
        attackEvent.Execute(context);

        // Assert - Track 应该继续推进（跳过攻击）
        Assert.Equal(8.0, track.NextTriggerAt);
    }

    [Fact]
    public void SpecialPulseEvent_WhenPlayerDead_ShouldSkipPulse()
    {
        // Arrange
        var context = CreateTestBattleContext();
        var player = context.Player;
        
        // 玩家死亡
        player.ReceiveDamageWithContext(150, DamageType.Physical, 5.0, context);
        Assert.True(player.IsDead);
        
        var track = new TrackState(TrackType.Special, 3.0, 6.0);
        context.Tracks.Add(track);
        
        var pulseEvent = new SpecialPulseEvent(6.0, track);

        // Act
        pulseEvent.Execute(context);

        // Assert - Track 应该继续推进
        Assert.Equal(9.0, track.NextTriggerAt);
    }

    #region Helper Methods

    private BattleContext CreateTestBattleContext()
    {
        var battle = new Battle
        {
            Id = System.Guid.NewGuid(),
            CharacterId = System.Guid.NewGuid(),
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
            stats: new CharacterStats(),
            stamina: 10
        );
    }

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
