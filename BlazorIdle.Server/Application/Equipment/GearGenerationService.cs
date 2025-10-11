using BlazorIdle.Server.Application.Abstractions;
using BlazorIdle.Server.Domain.Equipment.Models;
using BlazorIdle.Server.Domain.Equipment.ValueObjects;
using Microsoft.Extensions.Logging;

namespace BlazorIdle.Server.Application.Equipment;

/// <summary>
/// 装备生成服务
/// 负责根据装备定义生成装备实例，包括稀有度、属性、词条等
/// </summary>
public class GearGenerationService
{
    private readonly IGearDefinitionRepository _definitionRepo;
    private readonly IAffixRepository _affixRepo;
    private readonly ILogger<GearGenerationService> _logger;

    public GearGenerationService(
        IGearDefinitionRepository definitionRepo,
        IAffixRepository affixRepo,
        ILogger<GearGenerationService> logger)
    {
        _definitionRepo = definitionRepo;
        _affixRepo = affixRepo;
        _logger = logger;
    }

    /// <summary>
    /// 生成装备实例
    /// </summary>
    /// <param name="definitionId">装备定义ID</param>
    /// <param name="characterLevel">角色等级（影响物品等级）</param>
    /// <param name="rng">随机数生成器（可选，用于测试）</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>生成的装备实例</returns>
    public async Task<GearInstance> GenerateAsync(
        string definitionId,
        int characterLevel,
        Random? rng = null,
        CancellationToken ct = default)
    {
        rng ??= new Random();
        
        // 1. 获取装备定义
        var definition = await _definitionRepo.GetByIdAsync(definitionId, ct);
        if (definition == null)
        {
            throw new InvalidOperationException($"Gear definition not found: {definitionId}");
        }

        // 2. 确定稀有度
        var rarity = RollRarity(definition.RarityWeights, rng);

        // 3. 确定品级（默认T1）
        var tierLevel = 1;

        // 4. 计算物品等级（基于角色等级 + 稀有度加成）
        var itemLevel = characterLevel + GetRarityBonus(rarity);

        // 5. Roll基础属性
        var rolledStats = RollBaseStats(definition.BaseStats, tierLevel, rng);

        // 6. 生成词条
        var affixes = await GenerateAffixesAsync(definition, rarity, rng, ct);

        // 7. 计算装备评分
        var qualityScore = CalculateQualityScore(rolledStats, affixes);

        // 8. 创建装备实例
        var instance = new GearInstance
        {
            Id = Guid.NewGuid(),
            DefinitionId = definitionId,
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

        _logger.LogDebug(
            "Generated gear: {DefId}, Rarity={Rarity}, iLvl={ItemLevel}, Score={Score}",
            definitionId, rarity, itemLevel, qualityScore);

        return instance;
    }

    /// <summary>
    /// 根据稀有度权重随机选择稀有度
    /// </summary>
    private Rarity RollRarity(Dictionary<Rarity, double> weights, Random rng)
    {
        if (weights == null || weights.Count == 0)
        {
            // 默认权重：普通60%, 稀有30%, 史诗8%, 传说2%
            weights = new Dictionary<Rarity, double>
            {
                { Rarity.Common, 0.60 },
                { Rarity.Rare, 0.30 },
                { Rarity.Epic, 0.08 },
                { Rarity.Legendary, 0.02 }
            };
        }

        var totalWeight = weights.Values.Sum();
        var roll = rng.NextDouble() * totalWeight;
        
        var accumulated = 0.0;
        foreach (var (rarity, weight) in weights)
        {
            accumulated += weight;
            if (roll <= accumulated)
            {
                return rarity;
            }
        }

        return Rarity.Common; // 保底
    }

    /// <summary>
    /// 根据稀有度获取物品等级加成
    /// </summary>
    private int GetRarityBonus(Rarity rarity)
    {
        return rarity switch
        {
            Rarity.Common => 0,
            Rarity.Rare => 2,
            Rarity.Epic => 5,
            Rarity.Legendary => 10,
            _ => 0
        };
    }

    /// <summary>
    /// Roll基础属性（在范围内随机，受品级影响）
    /// </summary>
    private Dictionary<StatType, double> RollBaseStats(
        Dictionary<StatType, StatRange> baseStats,
        int tierLevel,
        Random rng)
    {
        var rolled = new Dictionary<StatType, double>();
        
        // 品级系数（T1=0.8, T2=1.0, T3=1.2）
        var tierMultiplier = tierLevel switch
        {
            1 => 0.8,
            2 => 1.0,
            3 => 1.2,
            _ => 1.0
        };

        foreach (var (statType, range) in baseStats)
        {
            // 在范围内随机
            var value = range.Min + rng.NextDouble() * (range.Max - range.Min);
            // 应用品级系数
            value *= tierMultiplier;
            rolled[statType] = Math.Round(value, 2);
        }

        return rolled;
    }

    /// <summary>
    /// 生成词条
    /// </summary>
    private async Task<List<AffixInstance>> GenerateAffixesAsync(
        GearDefinition definition,
        Rarity rarity,
        Random rng,
        CancellationToken ct)
    {
        var affixes = new List<AffixInstance>();
        
        // 根据稀有度决定词条数量
        var affixCount = GetAffixCount(rarity, rng);
        if (affixCount == 0)
        {
            return affixes;
        }

        // 获取可用词条池
        var availableAffixes = await GetAvailableAffixesAsync(definition, ct);
        if (availableAffixes.Count == 0)
        {
            _logger.LogWarning("No affixes available for definition: {DefId}", definition.Id);
            return affixes;
        }

        // 随机选择词条（不重复）
        var selectedAffixes = new HashSet<string>();
        var attempts = 0;
        while (affixes.Count < affixCount && attempts < 100)
        {
            attempts++;
            var affix = SelectWeightedAffix(availableAffixes, rng);
            
            if (affix != null && selectedAffixes.Add(affix.Id))
            {
                // Roll词条数值
                var value = affix.ValueMin + rng.NextDouble() * (affix.ValueMax - affix.ValueMin);
                
                var instance = new AffixInstance(
                    affix.Id,
                    affix.StatType,
                    affix.ModifierType,
                    Math.Round(value, 2)
                );
                
                affixes.Add(instance);
            }
        }

        return affixes;
    }

    /// <summary>
    /// 根据稀有度决定词条数量
    /// </summary>
    private int GetAffixCount(Rarity rarity, Random rng)
    {
        return rarity switch
        {
            Rarity.Common => 0,
            Rarity.Rare => rng.Next(1, 3), // 1-2个词条
            Rarity.Epic => rng.Next(2, 4), // 2-3个词条
            Rarity.Legendary => rng.Next(3, 5), // 3-4个词条
            _ => 0
        };
    }

    /// <summary>
    /// 获取可用词条列表
    /// </summary>
    private async Task<List<Affix>> GetAvailableAffixesAsync(
        GearDefinition definition,
        CancellationToken ct)
    {
        // 如果装备定义有限定词条池
        if (definition.AllowedAffixPool != null && definition.AllowedAffixPool.Count > 0)
        {
            var affixes = new List<Affix>();
            foreach (var affixId in definition.AllowedAffixPool)
            {
                var affix = await _affixRepo.GetByIdAsync(affixId, ct);
                if (affix != null)
                {
                    affixes.Add(affix);
                }
            }
            return affixes;
        }

        // 否则获取所有适用该槽位的词条
        return await _affixRepo.GetBySlotAsync(definition.Slot, ct);
    }

    /// <summary>
    /// 根据权重选择词条
    /// </summary>
    private Affix? SelectWeightedAffix(List<Affix> affixes, Random rng)
    {
        if (affixes.Count == 0)
        {
            return null;
        }

        var totalWeight = affixes.Sum(a => a.RarityWeight);
        var roll = rng.NextDouble() * totalWeight;
        
        var accumulated = 0.0;
        foreach (var affix in affixes)
        {
            accumulated += affix.RarityWeight;
            if (roll <= accumulated)
            {
                return affix;
            }
        }

        return affixes[rng.Next(affixes.Count)]; // 保底
    }

    /// <summary>
    /// 计算装备评分
    /// </summary>
    private int CalculateQualityScore(
        Dictionary<StatType, double> stats,
        List<AffixInstance> affixes)
    {
        var score = 0.0;

        // 基础属性得分
        foreach (var value in stats.Values)
        {
            score += value;
        }

        // 词条得分（词条价值更高）
        foreach (var affix in affixes)
        {
            score += affix.RolledValue * 2.0; // 词条价值系数
        }

        return (int)Math.Round(score);
    }
}
