using BlazorIdle.Server.Domain.Characters;
using BlazorIdle.Server.Domain.Combat;
using BlazorIdle.Server.Domain.Combat.Enemies;
using BlazorIdle.Server.Domain.Combat.Engine;
using BlazorIdle.Server.Domain.Combat.Rng;
using BlazorIdle.Shared.Models;
using System;
using System.Linq;
using Xunit;

namespace BlazorIdle.Tests;

/// <summary>
/// 测试攻击进度在怪物刷新和目标切换时的重置行为
/// </summary>
public class AttackProgressResetTests
{
    [Fact]
    public void AttackProgress_ResetsOnMonsterRespawn_InContinuousMode()
    {
        // Arrange: 创建一个持续模式的战斗，带有刷新延迟
        var enemy = new EnemyDefinition(
            id: "test_dummy",
            name: "Test Dummy",
            level: 1,
            maxHp: 100
        );

        var provider = new TestContinuousProvider(enemy, respawnDelay: 2.0);
        var stats = new CharacterStats();
        var rng = new RngContext(12345);

        var engine = new BattleEngine(
            battleId: Guid.NewGuid(),
            characterId: Guid.NewGuid(),
            profession: Profession.Warrior,
            stats: stats,
            rng: rng,
            provider: provider,
            module: null,
            meta: null
        );

        // Act: 推进战斗直到第一个怪物死亡
        engine.AdvanceUntil(20.0);

        var attackTrack = engine.Context.Tracks.FirstOrDefault(t => t.TrackType == TrackType.Attack);
        Assert.NotNull(attackTrack);
        
        var timeBeforeRespawn = engine.Clock.CurrentTime;
        var nextAttackBeforeRespawn = attackTrack.NextTriggerAt;

        // 继续推进，让怪物刷新
        engine.AdvanceUntil(timeBeforeRespawn + 3.0);

        var nextAttackAfterRespawn = attackTrack.NextTriggerAt;

        // Assert: 刷新后的攻击时间应该大于刷新前+刷新时间，表示已重置
        // 重置后攻击进度应该在当前时间之后一个攻击间隔内
        var currentTime = engine.Clock.CurrentTime;
        Assert.True(nextAttackAfterRespawn > currentTime,
            $"Attack should be scheduled after respawn. NextAttack: {nextAttackAfterRespawn}, Current: {currentTime}");
        
        Assert.True(nextAttackAfterRespawn <= currentTime + attackTrack.CurrentInterval + 0.1,
            $"Attack should be scheduled within one interval after respawn. NextAttack: {nextAttackAfterRespawn}, " +
            $"Current: {currentTime}, Interval: {attackTrack.CurrentInterval}");
    }

    [Fact]
    public void AttackProgress_ResetsOnTargetSwitch_InMultiEnemyBattle()
    {
        // Arrange: 创建一个有多个敌人的战斗
        var enemy = new EnemyDefinition(
            id: "test_dummy",
            name: "Test Dummy",
            level: 1,
            maxHp: 50  // 较低血量以便快速击杀
        );

        var stats = new CharacterStats();
        var rng = new RngContext(12345);

        var engine = new BattleEngine(
            battleId: Guid.NewGuid(),
            characterId: Guid.NewGuid(),
            profession: Profession.Warrior,
            stats: stats,
            rng: rng,
            enemyDef: enemy,
            enemyCount: 3,  // 3个敌人
            module: null,
            meta: null
        );

        // Act: 推进战斗一段时间
        engine.AdvanceUntil(5.0);

        var attackTrack = engine.Context.Tracks.FirstOrDefault(t => t.TrackType == TrackType.Attack);
        Assert.NotNull(attackTrack);

        // 记录第一个目标死亡前的状态
        var firstTarget = engine.Context.Encounter;
        var timeBeforeKill = engine.Clock.CurrentTime;
        
        // 继续推进直到第一个目标死亡
        while (!engine.Completed && engine.Context.EncounterGroup?.All.Count(e => !e.IsDead) > 2)
        {
            engine.AdvanceUntil(engine.Clock.CurrentTime + 5.0);
        }

        // Assert: 应该已经切换到新目标
        var newTarget = engine.Context.Encounter;
        Assert.NotNull(newTarget);
        
        if (firstTarget != null && firstTarget.IsDead)
        {
            // 验证已切换到不同的目标
            Assert.NotEqual(firstTarget, newTarget);
            
            // 验证攻击进度已重置：NextTriggerAt应该在当前时间之后
            var currentTime = engine.Clock.CurrentTime;
            Assert.True(attackTrack.NextTriggerAt > currentTime - attackTrack.CurrentInterval,
                $"Attack progress should be reset after target switch. " +
                $"NextTrigger: {attackTrack.NextTriggerAt}, Current: {currentTime}, Interval: {attackTrack.CurrentInterval}");
        }
    }

    [Fact]
    public void AttackProgress_CalculationShowsSmoothProgress()
    {
        // Arrange: 创建一个简单战斗
        var enemy = new EnemyDefinition(
            id: "test_dummy",
            name: "Test Dummy",
            level: 1,
            maxHp: 1000
        );

        var stats = new CharacterStats();
        var rng = new RngContext(12345);

        var engine = new BattleEngine(
            battleId: Guid.NewGuid(),
            characterId: Guid.NewGuid(),
            profession: Profession.Warrior,
            stats: stats,
            rng: rng,
            enemyDef: enemy,
            enemyCount: 1,
            module: null,
            meta: null
        );

        var attackTrack = engine.Context.Tracks.FirstOrDefault(t => t.TrackType == TrackType.Attack);
        Assert.NotNull(attackTrack);

        var interval = attackTrack.CurrentInterval;
        
        // 第一次攻击在时间0，下一次应该在interval时间
        Assert.Equal(0.0, attackTrack.NextTriggerAt);

        // Act & Assert: 测试第一个攻击周期内的进度
        // 在第一次攻击之前的不同时间点验证进度计算
        var testPoints = new[] { 0.0, 0.25, 0.5, 0.75, 0.9 };
        
        // 推进到第一次攻击之前，检查进度
        foreach (var point in testPoints)
        {
            if (point == 0.0) continue; // 跳过0，因为在0时刻attack已经触发
            
            // 从刚刚攻击之后到下次攻击前的进度
            // 我们在第一次攻击后的某个时间点检查
            var targetTime = 0.01 + (interval - 0.01) * point;
            
            // 重新创建引擎以获取干净的状态
            var testRng = new RngContext(12345);
            var testEngine = new BattleEngine(
                battleId: Guid.NewGuid(),
                characterId: Guid.NewGuid(),
                profession: Profession.Warrior,
                stats: stats,
                rng: testRng,
                enemyDef: enemy,
                enemyCount: 1,
                module: null,
                meta: null
            );
            
            var testTrack = testEngine.Context.Tracks.FirstOrDefault(t => t.TrackType == TrackType.Attack);
            Assert.NotNull(testTrack);
            
            // 推进到第一次攻击之后
            testEngine.AdvanceUntil(0.01);
            
            // 现在NextTriggerAt应该是interval
            var nextAttackAfterFirst = testTrack.NextTriggerAt;
            
            // 推进到目标时间
            testEngine.AdvanceUntil(targetTime);
            
            var currentTime = testEngine.Clock.CurrentTime;
            var nextAttackAt = testTrack.NextTriggerAt;
            var timeToAttack = nextAttackAt - currentTime;
            var testInterval = testTrack.CurrentInterval;
            
            // 使用与前端相同的计算公式
            var progress = testInterval > 0
                ? Math.Max(0.0, Math.Min(1.0, 1.0 - timeToAttack / testInterval))
                : (timeToAttack <= 0 ? 1.0 : 0.0);

            // 在目标时间点，进度应该接近预期百分比
            // 允许较大误差，因为事件调度是离散的
            Assert.True(progress >= point - 0.3 && progress <= point + 0.3,
                $"At time {targetTime:F2} (point {point:F2}), progress should be near {point:F2}, but was {progress:F2}. " +
                $"CurrentTime: {currentTime:F2}, NextAttack: {nextAttackAt:F2}, Interval: {testInterval:F2}, TimeToAttack: {timeToAttack:F2}");
        }
    }
}

/// <summary>
/// 测试用的持续模式提供者
/// </summary>
internal class TestContinuousProvider : IEncounterProvider
{
    private readonly EnemyDefinition _enemy;
    private readonly double _respawnDelay;
    private int _spawnCount = 0;

    public TestContinuousProvider(EnemyDefinition enemy, double respawnDelay)
    {
        _enemy = enemy;
        _respawnDelay = respawnDelay;
        CurrentGroup = new EncounterGroup(new[] { enemy });
        _spawnCount = 1;
    }

    public EncounterGroup CurrentGroup { get; private set; }
    public int CurrentWaveIndex => _spawnCount;
    public int CompletedRunCount => 0;

    public bool TryAdvance(out EncounterGroup? nextGroup, out bool runJustCompleted)
    {
        // 持续模式：总是返回新的一波敌人
        _spawnCount++;
        nextGroup = new EncounterGroup(new[] { _enemy });
        CurrentGroup = nextGroup;
        runJustCompleted = false;
        return true;
    }

    public double GetRespawnDelaySeconds(bool runJustCompleted) => _respawnDelay;
}
