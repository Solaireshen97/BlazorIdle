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
    public async Task<ReforgeResult> ReforgeAsync(Guid characterId, Guid gearInstanceId)
    {
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
        if (gear.TierLevel >= 3)
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
    private void RecalculateStatsWithNewTier(GearInstance gear, int oldTier, int newTier)
    {
        var oldMultiplier = GetTierMultiplier(oldTier);
        var newMultiplier = GetTierMultiplier(newTier);

        // 计算倍率变化
        var ratio = newMultiplier / oldMultiplier;

        // 应用到所有基础属性
        var updatedStats = new Dictionary<StatType, double>();
        foreach (var (statType, value) in gear.RolledStats)
        {
            updatedStats[statType] = value * ratio;
        }
        gear.RolledStats = updatedStats;
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
    /// 计算装备评分
    /// </summary>
    private int CalculateQualityScore(GearInstance gear)
    {
        // 基础属性分数
        var statScore = gear.RolledStats.Values.Sum() * 0.1;

        // 词条分数
        var affixScore = gear.Affixes.Sum(a => a.RolledValue * 0.2);

        // 稀有度加成
        var rarityMultiplier = gear.Rarity switch
        {
            Rarity.Common => 1.0,
            Rarity.Rare => 1.5,
            Rarity.Epic => 2.0,
            Rarity.Legendary => 3.0,
            _ => 1.0
        };

        // 品级加成
        var tierMultiplier = gear.TierLevel switch
        {
            1 => 0.8,
            2 => 1.0,
            3 => 1.2,
            _ => 1.0
        };

        var totalScore = (statScore + affixScore) * rarityMultiplier * tierMultiplier;
        return (int)Math.Round(totalScore);
    }

    /// <summary>
    /// 计算重铸成本
    /// </summary>
    /// <param name="gear">装备实例</param>
    /// <returns>成本字典（材料ID -> 数量）</returns>
    private Dictionary<string, int> CalculateReforgeCost(GearInstance gear)
    {
        var cost = new Dictionary<string, int>();

        // 基础成本根据当前品级和稀有度
        var baseCost = (gear.TierLevel + 1) * 10; // T1->T2需要20，T2->T3需要30

        // 稀有度倍率
        var rarityMultiplier = gear.Rarity switch
        {
            Rarity.Common => 1.0,
            Rarity.Rare => 2.0,
            Rarity.Epic => 4.0,
            Rarity.Legendary => 8.0,
            _ => 1.0
        };

        // 需要的材料
        cost["material_essence"] = (int)(baseCost * rarityMultiplier);

        // 根据稀有度需要对应的稀有材料
        var rareMaterial = gear.Rarity switch
        {
            Rarity.Rare => "essence_rare",
            Rarity.Epic => "essence_epic",
            Rarity.Legendary => "essence_legendary",
            _ => null
        };

        if (rareMaterial != null)
        {
            cost[rareMaterial] = gear.TierLevel + 1; // T1->T2需要2个，T2->T3需要3个
        }

        // 金币成本
        cost["gold"] = gear.ItemLevel * 100 * gear.TierLevel;

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

        if (gear.TierLevel >= 3)
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
