using BlazorIdle.Server.Domain.Equipment.Models;
using BlazorIdle.Server.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BlazorIdle.Server.Domain.Equipment.Services;

/// <summary>
/// 装备掉落服务
/// 负责根据掉落表生成装备实例
/// </summary>
public class GearDropService
{
    private readonly GameDbContext _context;
    private readonly GearGenerationService _gearGenerationService;
    private readonly ILogger<GearDropService> _logger;
    private readonly Random _random;

    public GearDropService(
        GameDbContext context,
        GearGenerationService gearGenerationService,
        ILogger<GearDropService> logger)
    {
        _context = context;
        _gearGenerationService = gearGenerationService;
        _logger = logger;
        _random = new Random();
    }

    /// <summary>
    /// 根据物品ID生成装备实例（如果物品是装备类型）
    /// </summary>
    /// <param name="itemId">物品ID，格式为 "gear:装备定义ID" 或普通物品ID</param>
    /// <param name="characterId">角色ID</param>
    /// <param name="characterLevel">角色等级，用于计算物品等级</param>
    /// <returns>生成的装备实例，如果不是装备类型返回null</returns>
    public async Task<GearInstance?> TryGenerateGearFromItemIdAsync(
        string itemId,
        Guid characterId,
        int characterLevel)
    {
        // 检查是否是装备类型物品
        if (!itemId.StartsWith("gear:", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        // 提取装备定义ID
        var gearDefinitionId = itemId["gear:".Length..];

        // 查找装备定义
        var definition = await _context.GearDefinitions
            .FirstOrDefaultAsync(g => g.Id == gearDefinitionId);

        if (definition == null)
        {
            _logger.LogWarning("Gear definition {DefinitionId} not found for item {ItemId}", 
                gearDefinitionId, itemId);
            return null;
        }

        try
        {
            // 生成装备实例
            var gearInstance = _gearGenerationService.Generate(
                definition,
                characterLevel);

            // 设置所属角色
            gearInstance.CharacterId = characterId;

            // 保存到数据库
            _context.GearInstances.Add(gearInstance);
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Generated gear instance {GearId} of type {DefinitionId} for character {CharacterId}",
                gearInstance.Id, gearDefinitionId, characterId);

            return gearInstance;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "Failed to generate gear instance from item {ItemId} for character {CharacterId}",
                itemId, characterId);
            throw;
        }
    }

    /// <summary>
    /// 批量生成装备实例
    /// </summary>
    /// <param name="itemDrops">掉落的物品字典（物品ID -> 数量）</param>
    /// <param name="characterId">角色ID</param>
    /// <param name="characterLevel">角色等级</param>
    /// <returns>生成的装备实例列表和更新后的物品字典（移除已处理的装备项）</returns>
    public async Task<(List<GearInstance> gearInstances, Dictionary<string, int> remainingItems)> 
        ProcessItemDropsAsync(
            Dictionary<string, int> itemDrops,
            Guid characterId,
            int characterLevel)
    {
        var gearInstances = new List<GearInstance>();
        var remainingItems = new Dictionary<string, int>();

        foreach (var (itemId, quantity) in itemDrops)
        {
            // 检查是否是装备类型
            if (itemId.StartsWith("gear:", StringComparison.OrdinalIgnoreCase))
            {
                // 为每个数量生成一个装备实例
                for (int i = 0; i < quantity; i++)
                {
                    var gearInstance = await TryGenerateGearFromItemIdAsync(
                        itemId, 
                        characterId, 
                        characterLevel);

                    if (gearInstance != null)
                    {
                        gearInstances.Add(gearInstance);
                    }
                }
            }
            else
            {
                // 非装备物品，保留在普通物品列表中
                remainingItems[itemId] = quantity;
            }
        }

        _logger.LogInformation(
            "Processed item drops for character {CharacterId}: Generated {GearCount} gear instances, {ItemCount} regular items",
            characterId, gearInstances.Count, remainingItems.Count);

        return (gearInstances, remainingItems);
    }

    /// <summary>
    /// 随机选择装备定义进行掉落
    /// </summary>
    /// <param name="characterLevel">角色等级，用于筛选合适等级的装备</param>
    /// <param name="maxLevelDiff">允许的最大等级差异（默认5级）</param>
    /// <returns>随机选择的装备定义ID，如果没有合适的返回null</returns>
    public async Task<string?> SelectRandomGearDefinitionAsync(
        int characterLevel,
        int maxLevelDiff = 5)
    {
        var minLevel = Math.Max(1, characterLevel - maxLevelDiff);
        var maxLevel = characterLevel + maxLevelDiff;

        var eligibleGear = await _context.GearDefinitions
            .Where(g => g.RequiredLevel >= minLevel && g.RequiredLevel <= maxLevel)
            .Select(g => g.Id)
            .ToListAsync();

        if (eligibleGear.Count == 0)
        {
            // 如果没有合适等级的装备，选择所有装备
            eligibleGear = await _context.GearDefinitions
                .Select(g => g.Id)
                .ToListAsync();
        }

        if (eligibleGear.Count == 0)
        {
            _logger.LogWarning("No gear definitions available for dropping");
            return null;
        }

        // 随机选择一个
        var index = _random.Next(eligibleGear.Count);
        return eligibleGear[index];
    }
}
