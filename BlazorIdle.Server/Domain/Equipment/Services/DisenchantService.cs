using BlazorIdle.Server.Domain.Equipment.Models;
using BlazorIdle.Server.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BlazorIdle.Server.Domain.Equipment.Services;

/// <summary>
/// 装备分解服务
/// 负责将装备分解为材料
/// </summary>
public class DisenchantService
{
    private readonly GameDbContext _context;

    public DisenchantService(GameDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// 分解装备
    /// </summary>
    /// <param name="characterId">角色ID</param>
    /// <param name="gearInstanceId">装备实例ID</param>
    /// <returns>分解结果，包含获得的材料</returns>
    public async Task<DisenchantResult> DisenchantAsync(Guid characterId, Guid gearInstanceId)
    {
        // 1. 获取装备实例
        var gear = await _context.Set<GearInstance>()
            .Include(g => g.Definition)
            .FirstOrDefaultAsync(g => g.Id == gearInstanceId);

        if (gear == null)
        {
            return DisenchantResult.Failure("装备不存在");
        }

        // 2. 验证装备归属
        if (gear.CharacterId != characterId)
        {
            return DisenchantResult.Failure("该装备不属于你");
        }

        // 3. 验证装备未装备
        if (gear.IsEquipped)
        {
            return DisenchantResult.Failure("请先卸下装备再分解");
        }

        // 4. 验证装备未绑定（或允许分解绑定装备）
        // 这里假设绑定装备也可以分解，根据游戏设计可调整

        // 5. 计算分解产出
        var materials = CalculateDisenchantMaterials(gear);

        // 6. 删除装备
        _context.Set<GearInstance>().Remove(gear);

        // 7. 添加材料到背包（这里简化处理，实际应该调用背包系统）
        // TODO: 集成背包系统添加材料

        await _context.SaveChangesAsync();

        return DisenchantResult.Success("分解成功", materials);
    }

    /// <summary>
    /// 批量分解装备
    /// </summary>
    /// <param name="characterId">角色ID</param>
    /// <param name="gearInstanceIds">装备实例ID列表</param>
    /// <returns>批量分解结果</returns>
    public async Task<BatchDisenchantResult> DisenchantBatchAsync(Guid characterId, List<Guid> gearInstanceIds)
    {
        // 参数验证
        if (gearInstanceIds == null || gearInstanceIds.Count == 0)
        {
            return new BatchDisenchantResult
            {
                SuccessCount = 0,
                FailCount = 0,
                TotalMaterials = new Dictionary<string, int>(),
                Errors = new List<string> { "装备列表为空" }
            };
        }

        var successCount = 0;
        var failCount = 0;
        var totalMaterials = new Dictionary<string, int>();
        var errors = new List<string>();

        foreach (var gearId in gearInstanceIds)
        {
            try
            {
                var result = await DisenchantAsync(characterId, gearId);
                if (result.IsSuccess)
                {
                    successCount++;
                    // 合并材料
                    foreach (var (materialId, amount) in result.Materials)
                    {
                        if (!totalMaterials.ContainsKey(materialId))
                        {
                            totalMaterials[materialId] = 0;
                        }
                        totalMaterials[materialId] += amount;
                    }
                }
                else
                {
                    failCount++;
                    errors.Add($"{gearId}: {result.Message}");
                }
            }
            catch (Exception ex)
            {
                // 增强错误处理：记录异常信息
                failCount++;
                errors.Add($"{gearId}: 分解时发生异常 - {ex.Message}");
            }
        }

        return new BatchDisenchantResult
        {
            SuccessCount = successCount,
            FailCount = failCount,
            TotalMaterials = totalMaterials,
            Errors = errors
        };
    }

    /// <summary>
    /// 计算分解产出材料
    /// </summary>
    /// <param name="gear">装备实例</param>
    /// <returns>材料字典（材料ID -> 数量）</returns>
    private Dictionary<string, int> CalculateDisenchantMaterials(GearInstance gear)
    {
        // 防御性编程：验证输入参数
        if (gear == null)
        {
            return new Dictionary<string, int>();
        }

        var materials = new Dictionary<string, int>();

        // 基础材料（根据装备槽位和物品等级）
        var baseMaterialId = GetBaseMaterialId(gear);
        var baseMaterialAmount = CalculateBaseMaterialAmount(gear);
        if (baseMaterialAmount > 0)
        {
            materials[baseMaterialId] = baseMaterialAmount;
        }

        // 根据稀有度给予额外材料
        var rareMaterialId = GetRareMaterialId(gear.Rarity);
        if (rareMaterialId != null)
        {
            var rareMaterialAmount = CalculateRareMaterialAmount(gear);
            if (rareMaterialAmount > 0)
            {
                materials[rareMaterialId] = rareMaterialAmount;
            }
        }

        // 根据品级给予额外材料
        if (gear.TierLevel >= 2)
        {
            var tierMaterialId = "essence_tier";
            var tierAmount = gear.TierLevel - 1; // T2给1个，T3给2个
            if (tierAmount > 0)
            {
                materials[tierMaterialId] = tierAmount;
            }
        }

        return materials;
    }

    /// <summary>
    /// 获取基础材料ID
    /// </summary>
    private string GetBaseMaterialId(GearInstance gear)
    {
        // 根据护甲类型返回不同的基础材料
        if (gear.Definition == null)
        {
            return "material_generic";
        }

        return gear.Definition.ArmorType switch
        {
            ArmorType.Cloth => "material_cloth",
            ArmorType.Leather => "material_leather",
            ArmorType.Mail => "material_mail",
            ArmorType.Plate => "material_plate",
            _ => gear.Definition.WeaponType != WeaponType.None 
                ? "material_weapon" 
                : "material_generic"
        };
    }

    /// <summary>
    /// 计算基础材料数量
    /// </summary>
    /// <param name="gear">装备实例</param>
    /// <returns>基础材料数量（至少为1）</returns>
    private int CalculateBaseMaterialAmount(GearInstance gear)
    {
        // 防御性编程：确保至少返回1个材料
        if (gear == null)
        {
            return 1;
        }

        // 基础数量根据物品等级（确保至少为1）
        var baseAmount = Math.Max(1, 1 + gear.ItemLevel / 10);

        // 根据槽位调整（胸甲和双手武器给更多材料）
        var slotMultiplier = gear.Definition?.Slot switch
        {
            EquipmentSlot.Chest => 1.5,
            EquipmentSlot.TwoHand => 1.5,
            EquipmentSlot.Legs => 1.3,
            _ => 1.0
        };

        // 确保结果至少为1
        return Math.Max(1, (int)(baseAmount * slotMultiplier));
    }

    /// <summary>
    /// 获取稀有材料ID
    /// </summary>
    private string? GetRareMaterialId(Rarity rarity)
    {
        return rarity switch
        {
            Rarity.Common => null, // 普通装备不给稀有材料
            Rarity.Rare => "essence_rare",
            Rarity.Epic => "essence_epic",
            Rarity.Legendary => "essence_legendary",
            _ => null
        };
    }

    /// <summary>
    /// 计算稀有材料数量
    /// </summary>
    private int CalculateRareMaterialAmount(GearInstance gear)
    {
        return gear.Rarity switch
        {
            Rarity.Rare => 1,
            Rarity.Epic => 3,
            Rarity.Legendary => 10,
            _ => 0
        };
    }

    /// <summary>
    /// 预览分解产出（不实际分解）
    /// </summary>
    /// <param name="gearInstanceId">装备实例ID</param>
    /// <returns>预览结果</returns>
    public async Task<Dictionary<string, int>> PreviewDisenchantAsync(Guid gearInstanceId)
    {
        var gear = await _context.Set<GearInstance>()
            .Include(g => g.Definition)
            .FirstOrDefaultAsync(g => g.Id == gearInstanceId);

        if (gear == null)
        {
            return new Dictionary<string, int>();
        }

        return CalculateDisenchantMaterials(gear);
    }
}

/// <summary>
/// 分解结果
/// </summary>
public class DisenchantResult
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
    /// 获得的材料（材料ID -> 数量）
    /// </summary>
    public Dictionary<string, int> Materials { get; set; } = new();

    public static DisenchantResult Success(string message, Dictionary<string, int> materials) => 
        new() { IsSuccess = true, Message = message, Materials = materials };

    public static DisenchantResult Failure(string message) => 
        new() { IsSuccess = false, Message = message };
}

/// <summary>
/// 批量分解结果
/// </summary>
public class BatchDisenchantResult
{
    /// <summary>
    /// 成功数量
    /// </summary>
    public int SuccessCount { get; set; }

    /// <summary>
    /// 失败数量
    /// </summary>
    public int FailCount { get; set; }

    /// <summary>
    /// 总材料（材料ID -> 数量）
    /// </summary>
    public Dictionary<string, int> TotalMaterials { get; set; } = new();

    /// <summary>
    /// 错误列表
    /// </summary>
    public List<string> Errors { get; set; } = new();
}
