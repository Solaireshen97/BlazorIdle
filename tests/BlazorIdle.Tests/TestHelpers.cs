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
        public FakeStatsAggregationService() : base(null!, new ArmorCalculator(), new BlockCalculator())
        {
        }

        public override Task<Dictionary<StatType, double>> CalculateEquipmentStatsAsync(Guid characterId)
        {
            // Return empty stats for tests - simulates no equipment
            return Task.FromResult(new Dictionary<StatType, double>());
        }

        public override Task<double> CalculateBlockChanceAsync(Guid characterId, double characterStrength = 0)
        {
            // No shield equipped in tests - return 0
            return Task.FromResult(0.0);
        }
    }
}
