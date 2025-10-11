using BlazorIdle.Server.Application.Abstractions;
using BlazorIdle.Server.Domain.Equipment.Models;
using Microsoft.Extensions.Logging;

namespace BlazorIdle.Server.Application.Equipment;

/// <summary>
/// 属性聚合服务
/// 负责计算装备提供的总属性加成
/// </summary>
public class StatsAggregationService
{
    private readonly IGearInstanceRepository _gearRepo;
    private readonly IGearSetRepository _setRepo;
    private readonly ILogger<StatsAggregationService> _logger;

    public StatsAggregationService(
        IGearInstanceRepository gearRepo,
        IGearSetRepository setRepo,
        ILogger<StatsAggregationService> logger)
    {
        _gearRepo = gearRepo;
        _setRepo = setRepo;
        _logger = logger;
    }

    /// <summary>
    /// 计算角色装备的总属性
    /// </summary>
    /// <param name="characterId">角色ID</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>属性字典（属性类型 -> 数值）</returns>
    public async Task<Dictionary<StatType, double>> GetTotalStatsAsync(
        Guid characterId,
        CancellationToken ct = default)
    {
        var totalStats = new Dictionary<StatType, double>();

        // 1. 获取所有已装备的装备
        var equippedGear = await _gearRepo.GetEquippedGearAsync(characterId, ct);
        if (equippedGear.Count == 0)
        {
            return totalStats;
        }

        // 2. 累加基础属性
        foreach (var gear in equippedGear)
        {
            foreach (var (statType, value) in gear.RolledStats)
            {
                if (!totalStats.ContainsKey(statType))
                {
                    totalStats[statType] = 0;
                }
                totalStats[statType] += value;
            }
        }

        // 3. 累加词条属性
        foreach (var gear in equippedGear)
        {
            foreach (var affix in gear.Affixes)
            {
                if (affix.ModifierType == ModifierType.Flat)
                {
                    if (!totalStats.ContainsKey(affix.StatType))
                    {
                        totalStats[affix.StatType] = 0;
                    }
                    totalStats[affix.StatType] += affix.RolledValue;
                }
                // TODO: 处理百分比修饰符和触发效果
            }
        }

        // 4. 计算套装效果
        var setBonuses = await CalculateSetBonusesAsync(equippedGear, ct);
        foreach (var (statType, value) in setBonuses)
        {
            if (!totalStats.ContainsKey(statType))
            {
                totalStats[statType] = 0;
            }
            totalStats[statType] += value;
        }

        _logger.LogDebug(
            "Calculated total stats for character {CharacterId}: {StatCount} stats",
            characterId, totalStats.Count);

        return totalStats;
    }

    /// <summary>
    /// 计算套装加成
    /// </summary>
    private async Task<Dictionary<StatType, double>> CalculateSetBonusesAsync(
        List<GearInstance> equippedGear,
        CancellationToken ct)
    {
        var setBonuses = new Dictionary<StatType, double>();

        // 统计每个套装的装备数量
        var setCounts = new Dictionary<string, int>();
        foreach (var gear in equippedGear)
        {
            if (!string.IsNullOrEmpty(gear.SetId))
            {
                setCounts.TryGetValue(gear.SetId, out var count);
                setCounts[gear.SetId] = count + 1;
            }
        }

        // 计算每个套装的加成
        foreach (var (setId, count) in setCounts)
        {
            var gearSet = await _setRepo.GetByIdAsync(setId, ct);
            if (gearSet == null)
            {
                _logger.LogWarning("Gear set not found: {SetId}", setId);
                continue;
            }

            // 应用所有满足件数要求的套装加成
            foreach (var (requiredCount, modifiers) in gearSet.Bonuses)
            {
                if (count >= requiredCount)
                {
                    foreach (var modifier in modifiers)
                    {
                        if (modifier.ModifierType == ModifierType.Flat)
                        {
                            if (!setBonuses.ContainsKey(modifier.StatType))
                            {
                                setBonuses[modifier.StatType] = 0;
                            }
                            setBonuses[modifier.StatType] += modifier.Value;
                        }
                        // TODO: 处理百分比修饰符
                    }

                    _logger.LogDebug(
                        "Applied set bonus: {SetId} ({Count} pieces) - {Modifiers} modifiers",
                        setId, requiredCount, modifiers.Count);
                }
            }
        }

        return setBonuses;
    }

    /// <summary>
    /// 获取装备提供的某个属性的总值
    /// </summary>
    public async Task<double> GetStatValueAsync(
        Guid characterId,
        StatType statType,
        CancellationToken ct = default)
    {
        var totalStats = await GetTotalStatsAsync(characterId, ct);
        return totalStats.TryGetValue(statType, out var value) ? value : 0;
    }

    /// <summary>
    /// 获取装备评分
    /// </summary>
    public async Task<int> GetTotalGearScoreAsync(
        Guid characterId,
        CancellationToken ct = default)
    {
        var equippedGear = await _gearRepo.GetEquippedGearAsync(characterId, ct);
        return equippedGear.Sum(g => g.QualityScore);
    }
}
