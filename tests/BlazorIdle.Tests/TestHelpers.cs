using BlazorIdle.Server.Domain.Equipment.Models;
using BlazorIdle.Server.Domain.Equipment.Services;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BlazorIdle.Tests;

/// <summary>
/// Test helper classes shared across test files
/// </summary>
public static class TestHelpers
{
    /// <summary>
    /// Fake EquipmentStatsIntegration for testing - returns base stats without equipment
    /// </summary>
    public class FakeEquipmentStatsIntegration : EquipmentStatsIntegration
    {
        public FakeEquipmentStatsIntegration() : base(new FakeStatsAggregationService())
        {
        }
    }

    /// <summary>
    /// Fake StatsAggregationService that returns empty equipment stats
    /// </summary>
    public class FakeStatsAggregationService : StatsAggregationService
    {
        private Dictionary<Guid, Dictionary<StatType, double>> _equipmentStats = new();

        public FakeStatsAggregationService() : base(null!, new ArmorCalculator(), new BlockCalculator())
        {
        }

        public override Task<Dictionary<StatType, double>> CalculateEquipmentStatsAsync(Guid characterId)
        {
            // Return empty stats for tests - simulates no equipment
            if (_equipmentStats.TryGetValue(characterId, out var stats))
            {
                return Task.FromResult(stats);
            }
            return Task.FromResult(new Dictionary<StatType, double>());
        }

        public override Task<double> CalculateBlockChanceAsync(Guid characterId, double characterStrength = 0)
        {
            // Return 0 for tests - simulates no shield equipped
            return Task.FromResult(0.0);
        }

        /// <summary>
        /// Phase 5: 获取主手武器类型 - 测试实现返回无武器
        /// </summary>
        public override Task<WeaponType> GetMainHandWeaponTypeAsync(Guid characterId)
        {
            // 测试中模拟无武器装备
            return Task.FromResult(WeaponType.None);
        }

        /// <summary>
        /// Phase 5: 获取副手武器类型 - 测试实现返回无武器
        /// </summary>
        public override Task<WeaponType> GetOffHandWeaponTypeAsync(Guid characterId)
        {
            // 测试中模拟无武器装备
            return Task.FromResult(WeaponType.None);
        }

        /// <summary>
        /// Phase 5: 检查是否双持 - 测试实现返回false
        /// </summary>
        public override Task<bool> IsDualWieldingAsync(Guid characterId)
        {
            // 测试中模拟非双持状态
            return Task.FromResult(false);
        }

        public void SetEquipmentStats(Guid characterId, Dictionary<StatType, double> stats)
        {
            _equipmentStats[characterId] = stats;
        }
    }
}
