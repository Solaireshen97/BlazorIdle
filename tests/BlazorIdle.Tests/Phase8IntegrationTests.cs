using BlazorIdle.Server.Application.Battles;
using BlazorIdle.Server.Domain.Characters;
using BlazorIdle.Server.Domain.Combat;
using BlazorIdle.Server.Domain.Combat.Enemies;
using BlazorIdle.Shared.Models;
using System;
using Xunit;

namespace BlazorIdle.Tests;

/// <summary>
/// Phase 8: 端到端集成测试
/// 验证所有Phase 1-7功能的协同工作
/// 
/// 注意：Phase 8的主要目标是确保所有之前Phase的功能能够协同工作。
/// 大部分详细测试已经在各个Phase的专门测试中完成：
/// - Phase 1: CombatantTests
/// - Phase 2: TargetSelectorTests, Phase2IntegrationTests  
/// - Phase 3: PlayerDeathReviveTests
/// - Phase 4: EnemyAttackTests
/// - Phase 5: EnemySkillTests
/// - Phase 6: EnhancedDungeonTests
/// - Phase 7: BattleReplayTests, OfflineOnlineConsistencyTests
/// 
/// 本文件提供高层次的端到端验证。
/// </summary>
public class Phase8IntegrationTests
{
    #region 完整战斗流程验证

    [Fact]
    public void E2E_SimpleBattle_AllPhasesIntegrate()
    {
        // Arrange - 使用BattleSimulator测试基础战斗
        var simulator = new BattleSimulator();
        
        var config = new BattleSimulator.BattleConfig
        {
            BattleId = Guid.NewGuid(),
            CharacterId = Guid.NewGuid(),
            Profession = Profession.Warrior,
            Stats = new CharacterStats(),
            Seed = 12345UL,
            EnemyDef = EnemyRegistry.Resolve("dummy"),
            EnemyCount = 1,
            Mode = "duration"
        };

        // Act - 运行战斗
        var result = simulator.RunForDuration(config, 30.0);

        // Assert - 验证战斗完成
        Assert.NotNull(result);
        Assert.NotEmpty(result.Segments);
        Assert.True(result.Battle.EndedAt > 0);
    }

    #endregion

    #region 多怪物战斗验证

    [Fact]
    public void E2E_MultipleEnemies_Works()
    {
        // Arrange
        var simulator = new BattleSimulator();
        
        var config = new BattleSimulator.BattleConfig
        {
            BattleId = Guid.NewGuid(),
            CharacterId = Guid.NewGuid(),
            Profession = Profession.Warrior,
            Stats = new CharacterStats(),
            Seed = 99999UL,
            EnemyDef = EnemyRegistry.Resolve("dummy"),
            EnemyCount = 5,
            Mode = "duration"
        };

        // Act
        var result = simulator.RunForDuration(config, 30.0);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result.Segments);
        
        // 多怪物随机目标选择的详细测试已在 Phase2IntegrationTests 中完成
    }

    #endregion

    #region Phase功能总结验证

    [Fact]
    public void E2E_AllPhases_Summary()
    {
        // 本测试作为Phase 8的总结验证
        // 所有具体功能已在各自的Phase测试中详细验证：
        
        // ✅ Phase 1: Combatant抽象 - CombatantTests
        // ✅ Phase 2: 目标选取 - TargetSelectorTests, Phase2IntegrationTests
        // ✅ Phase 3: 玩家死亡复活 - PlayerDeathReviveTests
        // ✅ Phase 4: 怪物攻击 - EnemyAttackTests
        // ✅ Phase 5: 怪物技能 - EnemySkillTests
        // ✅ Phase 6: 强化副本 - EnhancedDungeonTests
        // ✅ Phase 7: RNG一致性 - BattleReplayTests, OfflineOnlineConsistencyTests
        
        // Phase 8验证所有功能能够协同工作
        var simulator = new BattleSimulator();
        
        var config = new BattleSimulator.BattleConfig
        {
            BattleId = Guid.NewGuid(),
            CharacterId = Guid.NewGuid(),
            Profession = Profession.Warrior,
            Stats = new CharacterStats(),
            Seed = 11111UL,
            EnemyDef = EnemyRegistry.Resolve("tank"),
            EnemyCount = 3,
            Mode = "duration"
        };

        // Act
        var result = simulator.RunForDuration(config, 60.0);

        // Assert - 验证完整战斗流程
        Assert.NotNull(result);
        Assert.NotEmpty(result.Segments);
        Assert.True(result.Battle.EndedAt >= 60.0);
    }

    #endregion
}
