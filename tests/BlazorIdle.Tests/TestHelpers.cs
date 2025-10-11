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
        public FakeEquipmentStatsIntegration() : base(
            new FakeStatsAggregationService(), 
            new FakeGearInstanceRepository(),
            new AttackSpeedCalculator())
        {
        }
    }
    
    /// <summary>
    /// Fake GearInstanceRepository that returns empty equipment lists
    /// </summary>
    public class FakeGearInstanceRepository : BlazorIdle.Server.Application.Abstractions.IGearInstanceRepository
    {
        public Task<GearInstance?> GetByIdAsync(Guid id, CancellationToken ct = default)
        {
            return Task.FromResult<GearInstance?>(null);
        }
        
        public Task<List<GearInstance>> GetEquippedGearAsync(Guid characterId, CancellationToken ct = default)
        {
            return Task.FromResult(new List<GearInstance>());
        }
        
        public Task<List<GearInstance>> GetGearByCharacterAsync(Guid characterId, CancellationToken ct = default)
        {
            return Task.FromResult(new List<GearInstance>());
        }
        
        public Task<GearInstance?> GetEquippedGearBySlotAsync(Guid characterId, EquipmentSlot slot, CancellationToken ct = default)
        {
            return Task.FromResult<GearInstance?>(null);
        }
        
        public Task CreateAsync(GearInstance instance, CancellationToken ct = default)
        {
            return Task.CompletedTask;
        }
        
        public Task UpdateAsync(GearInstance gearInstance, CancellationToken ct = default)
        {
            return Task.CompletedTask;
        }
        
        public Task DeleteAsync(Guid id, CancellationToken ct = default)
        {
            return Task.CompletedTask;
        }
        
        public Task CreateBatchAsync(IEnumerable<GearInstance> instances, CancellationToken ct = default)
        {
            return Task.CompletedTask;
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

        public void SetEquipmentStats(Guid characterId, Dictionary<StatType, double> stats)
        {
            _equipmentStats[characterId] = stats;
        }
    }
}
