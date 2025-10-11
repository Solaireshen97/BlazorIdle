using BlazorIdle.Server.Domain.Equipment.Models;
using BlazorIdle.Server.Domain.Equipment.ValueObjects;
using BlazorIdle.Server.Infrastructure.Persistence;
using BlazorIdle.Server.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace BlazorIdle.Server.Domain.Equipment.Services;

/// <summary>
/// 装备词条重置服务
/// 负责装备词条的重新Roll（Reroll）
/// </summary>
public class RerollService
{
    private readonly GameDbContext _context;
    private readonly IAffixRepository _affixRepository;
    private readonly Random _random;

    public RerollService(GameDbContext context, IAffixRepository affixRepository)
    {
        _context = context;
        _affixRepository = affixRepository;
        _random = new Random();
    }

    /// <summary>
    /// 重置装备词条（重新Roll所有词条）
    /// </summary>
    /// <param name="characterId">角色ID</param>
    /// <param name="gearInstanceId">装备实例ID</param>
    /// <returns>重置结果</returns>
    public async Task<RerollResult> RerollAffixesAsync(Guid characterId, Guid gearInstanceId)
    {
        // 1. 获取装备实例
        var gear = await _context.Set<GearInstance>()
            .Include(g => g.Definition)
            .FirstOrDefaultAsync(g => g.Id == gearInstanceId);

        if (gear == null)
        {
            return RerollResult.Failure("装备不存在");
        }

        // 2. 验证装备归属
        if (gear.CharacterId != characterId)
        {
            return RerollResult.Failure("该装备不属于你");
        }

        // 3. 验证装备未装备（避免影响当前战斗状态）
        if (gear.IsEquipped)
        {
            return RerollResult.Failure("请先卸下装备再进行词条重置");
        }

        // 4. 计算重置成本
        var cost = CalculateRerollCost(gear);

        // 5. 验证材料是否足够（这里简化处理，实际应该检查背包）
        // TODO: 集成背包系统检查材料

        // 6. 保存旧词条（用于返回对比）
        var oldAffixes = new List<AffixInstance>(gear.Affixes);

        // 7. 重新生成词条
        var newAffixes = await GenerateNewAffixes(gear);
        gear.Affixes = newAffixes;
        gear.RerollCount++;
        gear.UpdatedAt = DateTime.UtcNow;

        // 8. 重新计算装备评分
        gear.QualityScore = CalculateQualityScore(gear);

        // 9. 扣除材料（这里简化处理）
        // TODO: 集成背包系统扣除材料

        await _context.SaveChangesAsync();

        return RerollResult.Success("词条重置成功", gear, oldAffixes, newAffixes);
    }

    /// <summary>
    /// 重置单个词条
    /// </summary>
    /// <param name="characterId">角色ID</param>
    /// <param name="gearInstanceId">装备实例ID</param>
    /// <param name="affixIndex">词条索引（0-based）</param>
    /// <returns>重置结果</returns>
    public async Task<RerollResult> RerollSingleAffixAsync(Guid characterId, Guid gearInstanceId, int affixIndex)
    {
        // 1. 获取装备实例
        var gear = await _context.Set<GearInstance>()
            .Include(g => g.Definition)
            .FirstOrDefaultAsync(g => g.Id == gearInstanceId);

        if (gear == null)
        {
            return RerollResult.Failure("装备不存在");
        }

        // 2. 验证装备归属
        if (gear.CharacterId != characterId)
        {
            return RerollResult.Failure("该装备不属于你");
        }

        // 3. 验证词条索引
        if (affixIndex < 0 || affixIndex >= gear.Affixes.Count)
        {
            return RerollResult.Failure("词条索引无效");
        }

        // 4. 验证装备未装备
        if (gear.IsEquipped)
        {
            return RerollResult.Failure("请先卸下装备再进行词条重置");
        }

        // 5. 计算重置成本（单个词条成本更低）
        var cost = CalculateSingleAffixRerollCost(gear);

        // 6. 验证材料是否足够
        // TODO: 集成背包系统检查材料

        // 7. 保存旧词条
        var oldAffixes = new List<AffixInstance>(gear.Affixes);
        var oldAffix = gear.Affixes[affixIndex];

        // 8. 重新生成单个词条
        var newAffix = await GenerateSingleAffix(gear);
        gear.Affixes[affixIndex] = newAffix;
        gear.RerollCount++;
        gear.UpdatedAt = DateTime.UtcNow;

        // 9. 重新计算装备评分
        gear.QualityScore = CalculateQualityScore(gear);

        // 10. 扣除材料
        // TODO: 集成背包系统扣除材料

        await _context.SaveChangesAsync();

        return RerollResult.Success($"成功重置第 {affixIndex + 1} 个词条", gear, oldAffixes, gear.Affixes);
    }

    /// <summary>
    /// 预览重置成本（不实际执行）
    /// </summary>
    /// <param name="gearInstanceId">装备实例ID</param>
    /// <returns>材料成本</returns>
    public async Task<Dictionary<string, int>> PreviewRerollCostAsync(Guid gearInstanceId)
    {
        var gear = await _context.Set<GearInstance>()
            .FirstOrDefaultAsync(g => g.Id == gearInstanceId);

        if (gear == null)
        {
            return new Dictionary<string, int>();
        }

        return CalculateRerollCost(gear);
    }

    /// <summary>
    /// 重新生成所有词条
    /// </summary>
    private async Task<List<AffixInstance>> GenerateNewAffixes(GearInstance gear)
    {
        // 根据装备稀有度确定词条数量
        var affixCount = gear.Rarity switch
        {
            Rarity.Common => 0,
            Rarity.Rare => _random.Next(1, 3), // 1-2个词条
            Rarity.Epic => _random.Next(2, 4), // 2-3个词条
            Rarity.Legendary => _random.Next(3, 5), // 3-4个词条
            _ => 0
        };

        var newAffixes = new List<AffixInstance>();

        // 获取可用的词条池
        var availableAffixes = await _affixRepository.GetBySlotAsync(
            gear.Definition?.Slot ?? EquipmentSlot.Head);

        // 随机选择词条并Roll数值
        for (int i = 0; i < affixCount; i++)
        {
            if (availableAffixes.Count == 0) break;

            // 加权随机选择词条
            var selectedAffix = SelectAffixByWeight(availableAffixes);
            
            // Roll数值
            var rolledValue = _random.NextDouble() * (selectedAffix.ValueMax - selectedAffix.ValueMin) + selectedAffix.ValueMin;

            var affixInstance = new AffixInstance(
                selectedAffix.Id,
                selectedAffix.StatType,
                selectedAffix.ModifierType,
                rolledValue);

            newAffixes.Add(affixInstance);

            // 移除已选择的词条，避免重复
            availableAffixes.Remove(selectedAffix);
        }

        return newAffixes;
    }

    /// <summary>
    /// 生成单个词条
    /// </summary>
    private async Task<AffixInstance> GenerateSingleAffix(GearInstance gear)
    {
        var availableAffixes = await _affixRepository.GetBySlotAsync(
            gear.Definition?.Slot ?? EquipmentSlot.Head);

        if (availableAffixes.Count == 0)
        {
            // 如果没有可用词条，返回默认词条
            return new AffixInstance("default", StatType.AttackPower, ModifierType.Flat, 10);
        }

        var selectedAffix = SelectAffixByWeight(availableAffixes);
        var rolledValue = _random.NextDouble() * (selectedAffix.ValueMax - selectedAffix.ValueMin) + selectedAffix.ValueMin;

        return new AffixInstance(
            selectedAffix.Id,
            selectedAffix.StatType,
            selectedAffix.ModifierType,
            rolledValue);
    }

    /// <summary>
    /// 根据权重选择词条
    /// </summary>
    private Affix SelectAffixByWeight(List<Affix> affixes)
    {
        var totalWeight = affixes.Sum(a => a.RarityWeight);
        var randomValue = _random.NextDouble() * totalWeight;
        
        double currentWeight = 0;
        foreach (var affix in affixes)
        {
            currentWeight += affix.RarityWeight;
            if (randomValue <= currentWeight)
            {
                return affix;
            }
        }

        // 如果没有选中（不应该发生），返回第一个
        return affixes[0];
    }

    /// <summary>
    /// 计算完整词条重置成本
    /// </summary>
    private Dictionary<string, int> CalculateRerollCost(GearInstance gear)
    {
        var materials = new Dictionary<string, int>();

        // 基础成本（基于物品等级）
        var baseCost = gear.ItemLevel / 10;

        // 稀有度系数
        var rarityMultiplier = gear.Rarity switch
        {
            Rarity.Common => 1,
            Rarity.Rare => 2,
            Rarity.Epic => 5,
            Rarity.Legendary => 10,
            _ => 1
        };

        // 重置次数递增成本
        var rerollMultiplier = 1 + (gear.RerollCount * 0.5);

        // 总成本
        var totalCost = (int)(baseCost * rarityMultiplier * rerollMultiplier);

        // 需要的材料
        materials["material_reroll_essence"] = totalCost;

        // 高级装备需要额外的稀有材料
        if (gear.Rarity == Rarity.Epic)
        {
            materials["essence_epic"] = 1;
        }
        else if (gear.Rarity == Rarity.Legendary)
        {
            materials["essence_legendary"] = 2;
        }

        return materials;
    }

    /// <summary>
    /// 计算单个词条重置成本（约为完整重置的1/3）
    /// </summary>
    private Dictionary<string, int> CalculateSingleAffixRerollCost(GearInstance gear)
    {
        var fullCost = CalculateRerollCost(gear);
        var reducedCost = new Dictionary<string, int>();

        foreach (var (material, amount) in fullCost)
        {
            reducedCost[material] = Math.Max(1, amount / 3);
        }

        return reducedCost;
    }

    /// <summary>
    /// 计算装备评分
    /// </summary>
    private int CalculateQualityScore(GearInstance gear)
    {
        // 基础分数（基于物品等级）
        var baseScore = gear.ItemLevel * 10;

        // 基础属性分数
        var statsScore = gear.RolledStats.Sum(kvp => kvp.Value);

        // 词条分数
        var affixScore = gear.Affixes.Sum(a => a.RolledValue * 0.5);

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

        var totalScore = (baseScore + statsScore + affixScore) * rarityMultiplier * tierMultiplier;
        return (int)totalScore;
    }
}

/// <summary>
/// 词条重置结果
/// </summary>
public class RerollResult
{
    public bool IsSuccess { get; set; }
    public string Message { get; set; } = "";
    public GearInstance? UpdatedGear { get; set; }
    public List<AffixInstance> OldAffixes { get; set; } = new();
    public List<AffixInstance> NewAffixes { get; set; } = new();

    public static RerollResult Success(string message, GearInstance gear, List<AffixInstance> oldAffixes, List<AffixInstance> newAffixes)
    {
        return new RerollResult
        {
            IsSuccess = true,
            Message = message,
            UpdatedGear = gear,
            OldAffixes = oldAffixes,
            NewAffixes = newAffixes
        };
    }

    public static RerollResult Failure(string message)
    {
        return new RerollResult
        {
            IsSuccess = false,
            Message = message
        };
    }
}
