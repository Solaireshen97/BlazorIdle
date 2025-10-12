using BlazorIdle.Server.Application.Abstractions;
using BlazorIdle.Server.Domain.Equipment.Configuration;
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
    /// <exception cref="ArgumentNullException">当装备定义为null时抛出</exception>
    /// <exception cref="ArgumentException">当角色等级无效时抛出</exception>
    public async Task<GearInstance> GenerateAsync(GearDefinition definition, int characterLevel, CancellationToken ct = default)
    {
        // 参数验证
        if (definition == null)
        {
            throw new ArgumentNullException(nameof(definition), "装备定义不能为null");
        }
        
        if (characterLevel < 1)
        {
            throw new ArgumentException("角色等级必须大于0", nameof(characterLevel));
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
        // 使用配置中的默认权重
        if (rarityWeights == null || rarityWeights.Count == 0)
        {
            rarityWeights = EquipmentSystemConfig.RarityConfig.DefaultWeights;
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
    /// <param name="characterLevel">角色等级</param>
    /// <param name="rarity">稀有度</param>
    /// <returns>物品等级（至少为1）</returns>
    private int CalculateItemLevel(int characterLevel, Rarity rarity)
    {
        // 确保角色等级至少为1
        characterLevel = Math.Max(1, characterLevel);

        var rarityBonus = rarity switch
        {
            Rarity.Common => 0,
            Rarity.Rare => 2,
            Rarity.Epic => 5,
            Rarity.Legendary => 10,
            _ => 0
        };

        // 确保物品等级至少为1
        return Math.Max(1, characterLevel + rarityBonus);
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
    /// <param name="affixDef">词条定义</param>
    /// <param name="itemLevel">物品等级</param>
    /// <returns>词条实例</returns>
    private AffixInstance RollAffixValue(Affix affixDef, int itemLevel)
    {
        // 防御性编程：确保最小值不大于最大值
        var minValue = Math.Min(affixDef.ValueMin, affixDef.ValueMax);
        var maxValue = Math.Max(affixDef.ValueMin, affixDef.ValueMax);

        // Roll词条数值（在范围内随机）
        var rolledValue = minValue + _random.NextDouble() * (maxValue - minValue);
        
        // 可选：根据物品等级调整词条数值
        // var levelMultiplier = 1.0 + (itemLevel - 1) * 0.02; // 每级提升2%
        // rolledValue *= levelMultiplier;

        // 确保数值不为负
        rolledValue = Math.Max(0, rolledValue);

        return new AffixInstance(
            affixDef.Id, 
            affixDef.StatType, 
            affixDef.ModifierType, 
            rolledValue);
    }

    /// <summary>
    /// 计算装备评分
    /// </summary>
    /// <param name="rolledStats">Roll的基础属性</param>
    /// <param name="affixes">词条列表</param>
    /// <param name="rarity">稀有度</param>
    /// <param name="tierLevel">品级</param>
    /// <returns>装备评分（至少为0）</returns>
    private int CalculateQualityScore(
        Dictionary<StatType, double> rolledStats,
        List<AffixInstance> affixes,
        Rarity rarity,
        int tierLevel)
    {
        // 防御性编程：处理null情况
        var statScore = (rolledStats?.Values.Sum() ?? 0) * 0.1;
        
        // 词条分数（确保不为null）
        var affixScore = (affixes?.Sum(a => a.RolledValue * 0.2) ?? 0);
        
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
        // 确保结果不为负
        return Math.Max(0, (int)Math.Round(totalScore));
    }
}
