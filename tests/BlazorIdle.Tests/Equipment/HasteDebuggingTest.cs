using BlazorIdle.Server.Domain.Characters;
using BlazorIdle.Server.Domain.Combat;
using BlazorIdle.Server.Domain.Combat.Engine;
using BlazorIdle.Server.Domain.Combat.Enemies;
using BlazorIdle.Server.Domain.Combat.Professions;
using BlazorIdle.Server.Domain.Combat.Rng;
using BlazorIdle.Shared.Models;
using Xunit;
using Xunit.Abstractions;

namespace BlazorIdle.Tests.Equipment;

/// <summary>
/// 急速调试测试 - 直接测试BattleEngine的急速应用
/// </summary>
public class HasteDebuggingTest
{
    private readonly ITestOutputHelper _output;

    public HasteDebuggingTest(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void BattleEngine_ShouldApplyHasteToTracks()
    {
        // Arrange - 创建有急速的角色属性
        var statsWithHaste = new CharacterStats
        {
            AttackPower = 50.0,
            CritChance = 0.05,
            CritMultiplier = 2.0,
            HastePercent = 1.0 // 100% 急速
        };

        var rng = new RngContext(12345UL);
        var enemyDef = EnemyRegistry.Resolve("dummy");

        // Act - 创建BattleEngine
        var engine = new BattleEngine(
            battleId: Guid.NewGuid(),
            characterId: Guid.NewGuid(),
            profession: Profession.Warrior,
            stats: statsWithHaste,
            rng: rng,
            enemyDef: enemyDef,
            enemyCount: 1
        );

        // Assert - 检查TrackState的急速是否被应用
        var attackTrack = engine.Context.Tracks.FirstOrDefault(t => t.TrackType == TrackType.Attack);
        
        Assert.NotNull(attackTrack);
        
        _output.WriteLine($"AttackTrack Properties:");
        _output.WriteLine($"  BaseInterval: {attackTrack.BaseInterval}");
        _output.WriteLine($"  HasteFactor: {attackTrack.HasteFactor}");
        _output.WriteLine($"  CurrentInterval: {attackTrack.CurrentInterval}");
        _output.WriteLine($"  Expected (with 100% haste): {attackTrack.BaseInterval / 2.0}");
        
        _output.WriteLine($"\nContext.Stats.HastePercent: {engine.Context.Stats.HastePercent:P0}");
        
        // 100% 急速应该使HasteFactor=2.0, CurrentInterval = BaseInterval / 2
        Assert.True(attackTrack.HasteFactor > 1.5, 
            $"HasteFactor应该大于1.5（100%急速），实际: {attackTrack.HasteFactor}");
        
        Assert.True(attackTrack.CurrentInterval < attackTrack.BaseInterval * 0.6, 
            $"CurrentInterval应该比BaseInterval小很多，BaseInterval: {attackTrack.BaseInterval}, CurrentInterval: {attackTrack.CurrentInterval}");
    }

    [Fact]
    public void BattleEngine_TrackState_ManualTest()
    {
        // Arrange - 手动测试TrackState的SetHaste方法
        var track = new TrackState(TrackType.Attack, baseInterval: 2.5, startAt: 0);
        
        _output.WriteLine($"初始状态:");
        _output.WriteLine($"  BaseInterval: {track.BaseInterval}");
        _output.WriteLine($"  HasteFactor: {track.HasteFactor}");
        _output.WriteLine($"  CurrentInterval: {track.CurrentInterval}");
        
        // Act - 设置100%急速
        track.SetHaste(2.0); // 2.0 表示2倍速度（100%急速）
        
        _output.WriteLine($"\n设置100%急速后:");
        _output.WriteLine($"  HasteFactor: {track.HasteFactor}");
        _output.WriteLine($"  CurrentInterval: {track.CurrentInterval}");
        _output.WriteLine($"  Expected: {2.5 / 2.0}");
        
        // Assert
        Assert.Equal(2.0, track.HasteFactor);
        Assert.Equal(1.25, track.CurrentInterval);
    }
}
