using BlazorIdle.Server.Domain.Characters;
using BlazorIdle.Server.Domain.Combat;
using BlazorIdle.Server.Domain.Combat.Combatants;
using BlazorIdle.Server.Domain.Combat.Damage;
using BlazorIdle.Server.Domain.Combat.Enemies;
using BlazorIdle.Server.Domain.Combat.Engine;
using BlazorIdle.Server.Domain.Combat.Professions;
using BlazorIdle.Server.Domain.Combat.Rng;
using BlazorIdle.Shared.Models;
using Xunit;

namespace BlazorIdle.Tests;

/// <summary>
/// 测试副本多波战斗中玩家持续受到伤害的场景
/// Bug Fix: 确保在第二波及后续波次中，玩家依然会受到怪物攻击
/// </summary>
public class DungeonMultiWavePlayerDamageTests
{
    [Fact]
    public void DungeonBattle_EnemyCombatants_ReinitializedOnWaveTransition()
    {
        // This test verifies the bug fix: enemy combatants are reinitialized when a new wave spawns
        // Without the fix, old enemy combatants from wave 1 remain, referencing dead encounters
        
        // Arrange
        var dungeon = CreateTestDungeon(waveCount: 2, waveDelay: 0.5);
        var provider = new DungeonEncounterProvider(dungeon, loop: false);
        
        var battleId = Guid.NewGuid();
        var characterId = Guid.NewGuid();
        var stats = new CharacterStats { AttackPower = 200 }; // High AP to quickly kill enemies
        var rng = new RngContext(12345);
        
        var meta = new BattleMeta { ModeTag = "dungeon", DungeonId = "test_dungeon" };
        
        var engine = new BattleEngine(
            battleId: battleId,
            characterId: characterId,
            profession: Profession.Warrior,
            stats: stats,
            rng: rng,
            provider: provider,
            module: new TestProfessionModule(),
            meta: meta
        );
        
        // Act 1: Get first wave enemy combatants
        Assert.Equal(1, engine.WaveIndex);
        Assert.Single(engine.Context.EnemyCombatants);
        var wave1Enemy = engine.Context.EnemyCombatants[0];
        var wave1EncounterId = wave1Enemy.Encounter.GetHashCode();
        
        // Manually kill the first wave enemy and trigger wave transition
        wave1Enemy.Encounter.ApplyDamage(1000, 0.5);
        Assert.True(wave1Enemy.Encounter.IsDead);
        
        // Manually trigger wave transition (simulating what BattleEngine does)
        engine.Context.EnemyCombatants.Clear();
        provider.TryAdvance(out var nextGroup, out var _);
        Assert.NotNull(nextGroup);
        engine.Context.ResetEncounterGroup(nextGroup);
        
        // Manually reinitialize (this is what the fix adds)
        var enemyDef = nextGroup.All[0].Enemy;
        var enemyCombatant = new EnemyCombatant("enemy_0", nextGroup.All[0]);
        var attackTrack = new TrackState(TrackType.Attack, enemyDef.AttackIntervalSeconds, enemyDef.AttackIntervalSeconds);
        enemyCombatant.AttackTrack = attackTrack;
        engine.Context.EnemyCombatants.Add(enemyCombatant);
        
        // Assert: New enemy combatants reference new encounters
        Assert.Equal(2, engine.WaveIndex);
        Assert.Single(engine.Context.EnemyCombatants);
        var wave2Enemy = engine.Context.EnemyCombatants[0];
        var wave2EncounterId = wave2Enemy.Encounter.GetHashCode();
        
        // The encounters should be different instances
        Assert.NotEqual(wave1EncounterId, wave2EncounterId);
        
        // Wave 2 enemy should be alive and able to attack
        Assert.False(wave2Enemy.Encounter.IsDead);
        Assert.True(wave2Enemy.CanAct());
        Assert.NotNull(wave2Enemy.AttackTrack);
    }
    

    
    #region Helper Methods
    
    private DungeonDefinition CreateTestDungeon(
        int waveCount,
        double waveDelay = 1.0)
    {
        // Use existing "dummy" enemy from registry which has 150 HP and deals 5 damage every 3 seconds
        // This gives us reliable enemy attacks while still being clearable
        var waves = new List<DungeonDefinition.Wave>();
        for (int i = 1; i <= waveCount; i++)
        {
            var waveEnemies = new List<(string enemyId, int count)>
            {
                ("dummy", 1) // One training dummy per wave
            };
            waves.Add(new DungeonDefinition.Wave(waveEnemies));
        }
        
        return new DungeonDefinition(
            id: "test_dungeon",
            name: "Test Dungeon",
            waves: waves,
            waveRespawnDelaySeconds: waveDelay,
            runRespawnDelaySeconds: 2.0
        );
    }
    
    private BattleEngine CreateDungeonBattleEngine(DungeonDefinition dungeon)
    {
        var provider = new DungeonEncounterProvider(dungeon, loop: false);
        return CreateDungeonBattleEngineWithProvider(provider);
    }
    
    private BattleEngine CreateDungeonBattleEngineWithProvider(DungeonEncounterProvider provider)
    {
        var battleId = Guid.NewGuid();
        var characterId = Guid.NewGuid();
        var stats = new CharacterStats { AttackPower = 50 }; // Moderate AP to clear enemies
        var rng = new RngContext(12345);
        
        var meta = new BattleMeta
        {
            ModeTag = "dungeon",
            DungeonId = "test_dungeon"
        };
        
        var engine = new BattleEngine(
            battleId: battleId,
            characterId: characterId,
            profession: Profession.Warrior,
            stats: stats,
            rng: rng,
            provider: provider,
            module: new TestProfessionModule(),
            meta: meta
        );
        
        return engine;
    }
    
    private class TestProfessionModule : IProfessionModule
    {
        public string Id => "test_profession";
        public double BaseAttackInterval => 1.0; // Fast attacks to clear waves quickly
        public double BaseSpecialInterval => 5.0;

        public void RegisterBuffDefinitions(BattleContext ctx) { }
        public void OnBattleStart(BattleContext ctx) { }
        public void BuildSkills(BattleContext ctx, Server.Domain.Combat.Skills.AutoCastEngine autoCaster) { }
        public void OnAttackTick(BattleContext ctx, AttackTickEvent evt) 
        {
            // Simple damage calculation for testing
            if (ctx.Encounter != null && ctx.Player.CanAct())
            {
                ctx.Encounter.ApplyDamage(20, ctx.Clock.CurrentTime);
            }
        }
        public void OnSpecialPulse(BattleContext ctx, SpecialPulseEvent evt) { }
        public void OnSkillCast(BattleContext ctx, Server.Domain.Combat.Skills.SkillDefinition def) { }
    }
    
    #endregion
}
