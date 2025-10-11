using BlazorIdle.Server.Domain.Characters;
using BlazorIdle.Server.Domain.Equipment.Models;
using BlazorIdle.Server.Domain.Equipment.Services;
using BlazorIdle.Shared.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BlazorIdle.Tests;

/// <summary>
/// 测试辅助类
/// </summary>
public static class TestHelper
{
    /// <summary>
    /// 创建一个测试用的EquipmentStatsIntegration实例
    /// 该实例返回不包含装备加成的基础属性（用于测试）
    /// </summary>
    public static EquipmentStatsIntegration CreateTestEquipmentStatsIntegration()
    {
        var fakeStatsAggregationService = new FakeStatsAggregationService();
        return new EquipmentStatsIntegration(fakeStatsAggregationService);
    }

    /// <summary>
    /// 用于测试的假StatsAggregationService
    /// 返回空的装备属性（模拟无装备情况）
    /// </summary>
    private class FakeStatsAggregationService : StatsAggregationService
    {
        public FakeStatsAggregationService() : base(null!)
        {
        }

        public override Task<Dictionary<StatType, double>> CalculateEquipmentStatsAsync(Guid characterId)
        {
            // 返回空的装备属性字典，模拟无装备情况
            return Task.FromResult(new Dictionary<StatType, double>());
        }
    }
}
