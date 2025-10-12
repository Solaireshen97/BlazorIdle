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

        // Phase 5: 武器类型相关方法的Fake实现
        public override Task<WeaponType> GetMainHandWeaponTypeAsync(Guid characterId)
        {
            // 返回None表示没有武器
            return Task.FromResult(WeaponType.None);
        }

        public override Task<WeaponType> GetOffHandWeaponTypeAsync(Guid characterId)
        {
            // 返回None表示没有副手武器
            return Task.FromResult(WeaponType.None);
        }

        public override Task<bool> IsDualWieldingAsync(Guid characterId)
        {
            // 返回false表示没有双持
            return Task.FromResult(false);
        }

        public void SetEquipmentStats(Guid characterId, Dictionary<StatType, double> stats)
        {
            _equipmentStats[characterId] = stats;
        }
    }
}
