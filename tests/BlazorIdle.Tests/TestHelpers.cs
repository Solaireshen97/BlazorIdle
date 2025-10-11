using BlazorIdle.Server.Domain.Equipment.Models;
using BlazorIdle.Server.Domain.Equipment.Services;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BlazorIdle.Tests;

/// <summary>
/// Shared test helpers
/// </summary>
public static class TestHelpers
{
    /// <summary>
    /// Creates a fake EquipmentStatsIntegration for tests that returns empty equipment stats
    /// </summary>
    public static EquipmentStatsIntegration CreateFakeEquipmentStatsIntegration()
    {
        var fakeStatsAggregation = new FakeStatsAggregationForTests();
        return new EquipmentStatsIntegration(fakeStatsAggregation);
    }
}

/// <summary>
/// Fake StatsAggregationService implementation for tests
/// Returns empty equipment stats to avoid database dependencies
/// </summary>
internal class FakeStatsAggregationForTests : StatsAggregationService
{
    public FakeStatsAggregationForTests() : base(null!, new ArmorCalculator(), new BlockCalculator())
    {
    }

    public override Task<Dictionary<StatType, double>> CalculateEquipmentStatsAsync(Guid characterId)
    {
        // Return empty stats for tests (no equipment)
        return Task.FromResult(new Dictionary<StatType, double>());
    }
}
