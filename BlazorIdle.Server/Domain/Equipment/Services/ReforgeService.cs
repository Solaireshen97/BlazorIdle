using BlazorIdle.Server.Domain.Common.Utilities;
using BlazorIdle.Server.Domain.Equipment.Configuration;
using BlazorIdle.Server.Domain.Equipment.Models;
using BlazorIdle.Server.Domain.Equipment.ValueObjects;
using BlazorIdle.Server.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BlazorIdle.Server.Domain.Equipment.Services;

/// <summary>
/// 装备重铸服务
/// 负责装备品级提升（T1->T2->T3）
/// </summary>
public class ReforgeService
{
    private readonly GameDbContext _context;

    public ReforgeService(GameDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// 重铸装备（提升品级）
    /// </summary>
    /// <param name="characterId">角色ID</param>
    /// <param name="gearInstanceId">装备实例ID</param>
    /// <returns>重铸结果</returns>
    /// <exception cref="ArgumentException">当ID无效时抛出</exception>
    public async Task<ReforgeResult> ReforgeAsync(Guid characterId, Guid gearInstanceId)
    {
        // 参数验证
        ValidationHelper.ValidateGuid(characterId, nameof(characterId));
        ValidationHelper.ValidateGuid(gearInstanceId, nameof(gearInstanceId));
        
        // 1. 获取装备实例
        var gear = await _context.Set<GearInstance>()
            .Include(g => g.Definition)
            .FirstOrDefaultAsync(g => g.Id == gearInstanceId);

        if (gear == null)
        {
            return ReforgeResult.Failure("装备不存在");
        }

        // 2. 验证装备归属
        if (gear.CharacterId != characterId)
        {
            return ReforgeResult.Failure("该装备不属于你");
        }

        // 3. 验证品级是否可提升
        if (gear.TierLevel >= EquipmentSystemConfig.TierConfig.MaxTier)
        {
            return ReforgeResult.Failure("装备已达到最高品级");
        }

        // 4. 计算重铸成本
        var cost = CalculateReforgeCost(gear);

        // 5. 验证材料是否足够（这里简化处理，实际应该检查背包）
        // TODO: 集成背包系统检查材料

        // 6. 提升品级并重新计算属性
        var oldTier = gear.TierLevel;
        var newTier = oldTier + 1;

        gear.TierLevel = newTier;
        gear.UpdatedAt = DateTime.UtcNow;

        // 重新应用品级系数到基础属性
        RecalculateStatsWithNewTier(gear, oldTier, newTier);

        // 重新计算装备评分
        gear.QualityScore = CalculateQualityScore(gear);

        // 7. 扣除材料（这里简化处理）
        // TODO: 集成背包系统扣除材料

        await _context.SaveChangesAsync();

        return ReforgeResult.Success($"成功将装备提升至 T{newTier}", gear);
    }

    /// <summary>
    /// 重新计算属性（应用新的品级系数）
    /// </summary>
    /// <param name="gear">装备实例</param>
    /// <param name="oldTier">旧品级</param>
    /// <param name="newTier">新品级</param>
    private void RecalculateStatsWithNewTier(GearInstance gear, int oldTier, int newTier)
    {
        // 防御性编程：验证输入
        if (gear == null || gear.RolledStats == null)
        {
            return;
        }

        var oldMultiplier = GetTierMultiplier(oldTier);
        var newMultiplier = GetTierMultiplier(newTier);

        // 防止除以零
        if (oldMultiplier <= 0)
        {
            return;
        }

        // 计算倍率变化
        var ratio = newMultiplier / oldMultiplier;

        // 应用到所有基础属性
        var updatedStats = new Dictionary<StatType, double>();
        foreach (var (statType, value) in gear.RolledStats)
        {
            // 确保数值不为负
            updatedStats[statType] = Math.Max(0, value * ratio);
        }
        gear.RolledStats = updatedStats;
    }

    /// <summary>
    /// 获取品级系数
    /// </summary>
    private double GetTierMultiplier(int tierLevel)
    {
        return EquipmentSystemConfig.TierConfig.GetMultiplier(tierLevel);
    }

    /// <summary>
    /// 计算装备评分
    /// </summary>
    /// <param name="gear">装备实例</param>
    /// <returns>装备评分（整数值，至少为0）</returns>
    private int CalculateQualityScore(GearInstance gear)
    {
        // 防御性编程：验证输入
        if (gear == null)
        {
            return 0;
        }

        // 基础属性分数（确保不为null）
        var statScore = (gear.RolledStats?.Values.Sum() ?? 0) * 0.1;

        // 词条分数（确保不为null）
        var affixScore = (gear.Affixes?.Sum(a => a.RolledValue * 0.2) ?? 0);

        // 稀有度加成
        var rarityMultiplier = EquipmentSystemConfig.QualityScoreConfig.RarityScoreMultipliers.TryGetValue(
            gear.Rarity, 
            out var rMult) 
                ? rMult 
                : 1.0;

        // 品级加成
        var tierMultiplier = EquipmentSystemConfig.TierConfig.GetMultiplier(gear.TierLevel);

        var totalScore = (statScore + affixScore) * rarityMultiplier * tierMultiplier;
        // 确保结果不为负
        return Math.Max(0, (int)Math.Round(totalScore));
    }

    /// <summary>
    /// 计算重铸成本
    /// </summary>
    /// <param name="gear">装备实例</param>
    /// <returns>成本字典（材料ID -> 数量）</returns>
    private Dictionary<string, int> CalculateReforgeCost(GearInstance gear)
    {
        var cost = new Dictionary<string, int>();

        // 防御性编程：验证输入
        if (gear == null)
        {
            return cost;
        }

        // 基础成本根据当前品级和稀有度
        var baseCost = (gear.TierLevel + 1) * 10; // T1->T2需要20，T2->T3需要30

        // 稀有度倍率
        var rarityMultiplier = EquipmentSystemConfig.ReforgeConfig.RarityCostMultipliers.TryGetValue(
            gear.Rarity, 
            out var rMult) 
                ? rMult 
                : 1.0;

        // 需要的材料（确保至少为最小值）
        cost[EquipmentSystemConfig.ReforgeConfig.EssenceMaterial] = Math.Max(
            EquipmentSystemConfig.ReforgeConfig.MinMaterialAmount, 
            (int)(baseCost * rarityMultiplier));

        // 根据稀有度需要对应的稀有材料
        var rareMaterial = EquipmentSystemConfig.DisenchantConfig.RareMaterials.TryGetValue(
            gear.Rarity, 
            out var rm) 
                ? rm 
                : null;

        if (rareMaterial != null)
        {
            var rareMaterialCount = gear.TierLevel + 1; // T1->T2需要2个，T2->T3需要3个
            cost[rareMaterial] = Math.Max(
                EquipmentSystemConfig.ReforgeConfig.MinMaterialAmount, 
                rareMaterialCount);
        }

        // 金币成本（确保至少为最小值）
        var goldCost = gear.ItemLevel * 100 * Math.Max(1, gear.TierLevel);
        cost[EquipmentSystemConfig.ReforgeConfig.Gold] = Math.Max(
            EquipmentSystemConfig.ReforgeConfig.MinGoldCost, 
            goldCost);

        return cost;
    }

    /// <summary>
    /// 预览重铸成本（不实际重铸）
    /// </summary>
    /// <param name="gearInstanceId">装备实例ID</param>
    /// <returns>成本字典</returns>
    public async Task<ReforgeCostPreview> PreviewReforgeCostAsync(Guid gearInstanceId)
    {
        var gear = await _context.Set<GearInstance>()
            .Include(g => g.Definition)
            .FirstOrDefaultAsync(g => g.Id == gearInstanceId);

        if (gear == null)
        {
            return new ReforgeCostPreview
            {
                CanReforge = false,
                Message = "装备不存在"
            };
        }

        if (gear.TierLevel >= EquipmentSystemConfig.TierConfig.MaxTier)
        {
            return new ReforgeCostPreview
            {
                CanReforge = false,
                Message = "装备已达到最高品级"
            };
        }

        var cost = CalculateReforgeCost(gear);
        var newTier = gear.TierLevel + 1;

        // 预览属性变化
        var oldMultiplier = GetTierMultiplier(gear.TierLevel);
        var newMultiplier = GetTierMultiplier(newTier);
        var ratio = newMultiplier / oldMultiplier;

        var previewStats = new Dictionary<StatType, double>();
        foreach (var (statType, value) in gear.RolledStats)
        {
            previewStats[statType] = value * ratio;
        }

        return new ReforgeCostPreview
        {
            CanReforge = true,
            Message = "可以重铸",
            CurrentTier = gear.TierLevel,
            NextTier = newTier,
            Cost = cost,
            CurrentStats = gear.RolledStats,
            PreviewStats = previewStats
        };
    }
}

/// <summary>
/// 重铸结果
/// </summary>
public class ReforgeResult
{
    /// <summary>
    /// 是否成功
    /// </summary>
    public bool IsSuccess { get; set; }

    /// <summary>
    /// 消息
    /// </summary>
    public string Message { get; set; } = "";

    /// <summary>
    /// 重铸后的装备
    /// </summary>
    public GearInstance? ReforgedGear { get; set; }

    public static ReforgeResult Success(string message, GearInstance gear) =>
        new() { IsSuccess = true, Message = message, ReforgedGear = gear };

    public static ReforgeResult Failure(string message) =>
        new() { IsSuccess = false, Message = message };
}

/// <summary>
/// 重铸成本预览
/// </summary>
public class ReforgeCostPreview
{
    /// <summary>
    /// 是否可以重铸
    /// </summary>
    public bool CanReforge { get; set; }

    /// <summary>
    /// 消息
    /// </summary>
    public string Message { get; set; } = "";

    /// <summary>
    /// 当前品级
    /// </summary>
    public int CurrentTier { get; set; }

    /// <summary>
    /// 下一品级
    /// </summary>
    public int NextTier { get; set; }

    /// <summary>
    /// 成本（材料ID -> 数量）
    /// </summary>
    public Dictionary<string, int> Cost { get; set; } = new();

    /// <summary>
    /// 当前属性
    /// </summary>
    public Dictionary<StatType, double> CurrentStats { get; set; } = new();

    /// <summary>
    /// 预览属性（重铸后）
    /// </summary>
    public Dictionary<StatType, double> PreviewStats { get; set; } = new();
}
