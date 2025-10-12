using BlazorIdle.Server.Application.Abstractions;
using BlazorIdle.Server.Domain.Equipment.Models;
using BlazorIdle.Server.Domain.Equipment.ValueObjects;

namespace BlazorIdle.Server.Domain.Equipment.Services;

/// <summary>
/// 装备生成服务
/// 负责根据装备定义生成装备实例，包括稀有度Roll、属性Roll、词条生成等
/// </summary>
public class GearGenerationService
{
    private readonly Random _random;
    private readonly IAffixRepository _affixRepository;

    public GearGenerationService(IAffixRepository affixRepository)
    {
        _random = new Random();
        _affixRepository = affixRepository;
    }

    /// <summary>
    /// 生成装备实例
    /// </summary>
    /// <param name="definition">装备定义</param>
    /// <param name="characterLevel">角色等级（影响物品等级）</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>生成的装备实例</returns>
    /// <exception cref="ArgumentNullException">当definition为null时抛出</exception>
    /// <exception cref="ArgumentOutOfRangeException">当characterLevel小于1时抛出</exception>
    public async Task<GearInstance> GenerateAsync(GearDefinition definition, int characterLevel, CancellationToken ct = default)
    {
        // 参数验证
        if (definition == null)
        {
            throw new ArgumentNullException(nameof(definition), "装备定义不能为空");
        }
        
        if (characterLevel < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(characterLevel), characterLevel, "角色等级必须大于0");
        }
        
        // 1. 确定稀有度
        var rarity = RollRarity(definition.RarityWeights);
        
        // 2. 确定品级（默认T1）
        var tierLevel = 1;
        
        // 3. 计算物品等级
        var itemLevel = CalculateItemLevel(characterLevel, rarity);
        
        // 4. Roll基础属性
        var rolledStats = RollBaseStats(definition.BaseStats, tierLevel);
        
        // 5. 生成词条
        var affixes = await GenerateAffixesAsync(definition.AllowedAffixPool, rarity, itemLevel, ct);
        
        // 6. 计算装备评分
        var qualityScore = CalculateQualityScore(rolledStats, affixes, rarity, tierLevel);
        
        // 7. 创建装备实例
        var instance = new GearInstance
        {
            Id = Guid.NewGuid(),
            DefinitionId = definition.Id,
            Rarity = rarity,
            TierLevel = tierLevel,
            ItemLevel = itemLevel,
            RolledStats = rolledStats,
            Affixes = affixes,
            QualityScore = qualityScore,
            SetId = definition.SetId,
            IsEquipped = false,
            IsBound = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        return instance;
    }

    /// <summary>
    /// 根据权重Roll稀有度
    /// </summary>
    private Rarity RollRarity(Dictionary<Rarity, double> rarityWeights)
    {
        if (rarityWeights == null || rarityWeights.Count == 0)
        {
            // 默认权重
            rarityWeights = new Dictionary<Rarity, double>
            {
                { Rarity.Common, 50.0 },
                { Rarity.Rare, 30.0 },
                { Rarity.Epic, 15.0 },
                { Rarity.Legendary, 5.0 }
            };
        }

        var totalWeight = rarityWeights.Values.Sum();
        var roll = _random.NextDouble() * totalWeight;
        var cumulative = 0.0;

        foreach (var (rarity, weight) in rarityWeights)
        {
            cumulative += weight;
            if (roll < cumulative)
            {
                return rarity;
            }
        }

        return Rarity.Common; // 默认返回普通品质
    }

    /// <summary>
    /// 计算物品等级
    /// </summary>
    private int CalculateItemLevel(int characterLevel, Rarity rarity)
    {
        var rarityBonus = rarity switch
        {
            Rarity.Common => 0,
            Rarity.Rare => 2,
            Rarity.Epic => 5,
            Rarity.Legendary => 10,
            _ => 0
        };

        return characterLevel + rarityBonus;
    }

    /// <summary>
    /// Roll基础属性
    /// </summary>
    private Dictionary<StatType, double> RollBaseStats(
        Dictionary<StatType, StatRange> baseStats,
        int tierLevel)
    {
        var result = new Dictionary<StatType, double>();
        
        if (baseStats == null || baseStats.Count == 0)
        {
            return result;
        }

        var tierMultiplier = GetTierMultiplier(tierLevel);

        foreach (var (statType, range) in baseStats)
        {
            var baseValue = range.Roll(_random);
            var finalValue = baseValue * tierMultiplier;
            result[statType] = finalValue;
        }

        return result;
    }

    /// <summary>
    /// 获取品级系数
    /// </summary>
    private double GetTierMultiplier(int tierLevel)
    {
        return tierLevel switch
        {
            1 => 0.8,
            2 => 1.0,
            3 => 1.2,
            _ => 1.0
        };
    }

    /// <summary>
    /// 生成词条
    /// </summary>
    private async Task<List<AffixInstance>> GenerateAffixesAsync(
        List<string> affixPool,
        Rarity rarity,
        int itemLevel,
        CancellationToken ct)
    {
        var affixes = new List<AffixInstance>();
        
        if (affixPool == null || affixPool.Count == 0)
        {
            return affixes;
        }

        // 根据稀有度决定词条数量
        var affixCount = rarity switch
        {
            Rarity.Common => 0,
            Rarity.Rare => 1,
            Rarity.Epic => 2,
            Rarity.Legendary => 3,
            _ => 0
        };

        if (affixCount == 0)
        {
            return affixes;
        }

        // 从词条池中随机选择
        var selectedAffixIds = affixPool
            .OrderBy(_ => _random.Next())
            .Take(affixCount)
            .ToList();

        // 为每个词条Roll数值，从数据库读取Affix定义
        foreach (var affixId in selectedAffixIds)
        {
            var affixDef = await _affixRepository.GetByIdAsync(affixId, ct);
            if (affixDef != null)
            {
                var affixInstance = RollAffixValue(affixDef, itemLevel);
                affixes.Add(affixInstance);
            }
        }

        return affixes;
    }

    /// <summary>
    /// Roll词条数值
    /// </summary>
    private AffixInstance RollAffixValue(Affix affixDef, int itemLevel)
    {
        // Roll词条数值（在范围内随机）
        var rolledValue = affixDef.ValueMin + _random.NextDouble() * (affixDef.ValueMax - affixDef.ValueMin);
        
        // 可选：根据物品等级调整词条数值
        // var levelMultiplier = 1.0 + (itemLevel - 1) * 0.02; // 每级提升2%
        // rolledValue *= levelMultiplier;

        return new AffixInstance(
            affixDef.Id, 
            affixDef.StatType, 
            affixDef.ModifierType, 
            rolledValue);
    }

    /// <summary>
    /// 计算装备评分
    /// </summary>
    private int CalculateQualityScore(
        Dictionary<StatType, double> rolledStats,
        List<AffixInstance> affixes,
        Rarity rarity,
        int tierLevel)
    {
        // 基础属性分数
        var statScore = rolledStats.Values.Sum() * 0.1;
        
        // 词条分数
        var affixScore = affixes.Sum(a => a.RolledValue * 0.2);
        
        // 稀有度加成
        var rarityMultiplier = rarity switch
        {
            Rarity.Common => 1.0,
            Rarity.Rare => 1.5,
            Rarity.Epic => 2.0,
            Rarity.Legendary => 3.0,
            _ => 1.0
        };
        
        // 品级加成
        var tierMultiplier = tierLevel switch
        {
            1 => 0.8,
            2 => 1.0,
            3 => 1.2,
            _ => 1.0
        };

        var totalScore = (statScore + affixScore) * rarityMultiplier * tierMultiplier;
        return (int)Math.Round(totalScore);
    }
}
