using BlazorIdle.Client.Services;

namespace BlazorIdle.Client.Services;

/// <summary>
/// 装备对比服务
/// 用于比较两件装备的属性差异
/// </summary>
public class EquipmentComparisonService
{
    /// <summary>
    /// 对比结果
    /// </summary>
    public class ComparisonResult
    {
        /// <summary>属性差异（statId -> 差值）</summary>
        public Dictionary<string, double> StatDifferences { get; set; } = new();
        
        /// <summary>装备评分差</summary>
        public int QualityScoreDifference { get; set; }
        
        /// <summary>物品等级差</summary>
        public int ItemLevelDifference { get; set; }
        
        /// <summary>是否为升级</summary>
        public bool IsUpgrade { get; set; }
        
        /// <summary>总体评估分数（正数表示新装备更好）</summary>
        public double OverallScore { get; set; }
    }

    /// <summary>
    /// 对比两件装备
    /// </summary>
    /// <param name="currentGear">当前装备（可为null）</param>
    /// <param name="newGear">新装备</param>
    /// <returns>对比结果</returns>
    public ComparisonResult Compare(GearInstanceDto? currentGear, GearInstanceDto newGear)
    {
        var result = new ComparisonResult();
        
        // 如果当前没有装备，所有差异都是新装备的属性
        if (currentGear == null)
        {
            result.StatDifferences = new Dictionary<string, double>(newGear.Stats);
            result.QualityScoreDifference = newGear.QualityScore;
            result.ItemLevelDifference = newGear.ItemLevel;
            result.IsUpgrade = true;
            result.OverallScore = CalculateOverallScore(result);
            return result;
        }
        
        // 计算装备评分差
        result.QualityScoreDifference = newGear.QualityScore - currentGear.QualityScore;
        result.ItemLevelDifference = newGear.ItemLevel - currentGear.ItemLevel;
        
        // 计算所有属性的差异
        var allStatIds = currentGear.Stats.Keys.Union(newGear.Stats.Keys).ToHashSet();
        
        foreach (var statId in allStatIds)
        {
            var currentValue = currentGear.Stats.GetValueOrDefault(statId, 0);
            var newValue = newGear.Stats.GetValueOrDefault(statId, 0);
            var difference = newValue - currentValue;
            
            if (Math.Abs(difference) > 0.001) // 忽略极小的差异
            {
                result.StatDifferences[statId] = difference;
            }
        }
        
        // 计算总体分数并判断是否为升级
        result.OverallScore = CalculateOverallScore(result);
        result.IsUpgrade = result.OverallScore > 0;
        
        return result;
    }
    
    /// <summary>
    /// 计算总体评估分数
    /// </summary>
    private double CalculateOverallScore(ComparisonResult result)
    {
        double score = 0;
        
        // 装备评分权重最高
        score += result.QualityScoreDifference * 0.5;
        
        // 物品等级有一定权重
        score += result.ItemLevelDifference * 2;
        
        // 各属性的权重
        var statWeights = GetStatWeights();
        
        foreach (var (statId, difference) in result.StatDifferences)
        {
            var weight = statWeights.GetValueOrDefault(statId, 1.0);
            score += difference * weight;
        }
        
        return score;
    }
    
    /// <summary>
    /// 获取属性权重
    /// 用于计算装备的综合价值
    /// </summary>
    private Dictionary<string, double> GetStatWeights()
    {
        return new Dictionary<string, double>
        {
            // 主属性权重较高
            { "Strength", 2.0 },
            { "Agility", 2.0 },
            { "Intellect", 2.0 },
            { "Stamina", 1.5 },
            
            // 攻击属性
            { "AttackPower", 1.5 },
            { "SpellPower", 1.5 },
            { "CritChance", 50.0 },      // 1% = 50分
            { "CritRating", 0.5 },
            { "HastePercent", 40.0 },    // 1% = 40分
            { "Haste", 0.4 },
            
            // 防御属性
            { "Armor", 0.3 },
            { "BlockChance", 30.0 },     // 1% = 30分
            
            // 其他属性
            { "HealthRegen", 1.0 },
            { "ManaRegen", 1.0 }
        };
    }
    
    /// <summary>
    /// 获取属性差异的显示文本
    /// </summary>
    /// <param name="statId">属性ID</param>
    /// <param name="difference">差值</param>
    /// <param name="useColor">是否使用颜色标记</param>
    /// <returns>显示文本</returns>
    public string GetDifferenceDisplayText(string statId, double difference, bool useColor = true)
    {
        var isPercentage = IsPercentageStat(statId);
        var sign = difference > 0 ? "+" : "";
        var value = isPercentage ? $"{difference * 100:F1}%" : $"{difference:F0}";
        
        if (!useColor)
        {
            return $"{sign}{value}";
        }
        
        var color = difference > 0 ? "green" : "red";
        var arrow = difference > 0 ? "↑" : "↓";
        
        return $"<span style='color: {color};'>{arrow} {sign}{value}</span>";
    }
    
    /// <summary>
    /// 判断是否为百分比属性
    /// </summary>
    private bool IsPercentageStat(string statId)
    {
        return statId switch
        {
            "CritChance" or "HastePercent" or "BlockChance" => true,
            _ => false
        };
    }
    
    /// <summary>
    /// 获取属性显示名称
    /// </summary>
    public string GetStatDisplayName(string statId)
    {
        return statId switch
        {
            "AttackPower" => "攻击力",
            "SpellPower" => "法术强度",
            "Armor" => "护甲",
            "CritChance" => "暴击率",
            "CritRating" => "暴击等级",
            "HastePercent" => "急速",
            "Haste" => "急速等级",
            "Strength" => "力量",
            "Agility" => "敏捷",
            "Intellect" => "智力",
            "Stamina" => "耐力",
            "BlockChance" => "格挡率",
            "HealthRegen" => "生命回复",
            "ManaRegen" => "法力回复",
            _ => statId
        };
    }
}
