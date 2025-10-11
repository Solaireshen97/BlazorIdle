using Microsoft.AspNetCore.Mvc;
using BlazorIdle.Server.Domain.Equipment.Services;
using BlazorIdle.Server.Domain.Equipment.Models;
using BlazorIdle.Server.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BlazorIdle.Server.Api;

/// <summary>
/// 装备系统API控制器
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class EquipmentController : ControllerBase
{
    private readonly EquipmentService _equipmentService;
    private readonly StatsAggregationService _statsAggregationService;
    private readonly DisenchantService _disenchantService;
    private readonly ReforgeService _reforgeService;
    private readonly RerollService _rerollService;
    private readonly GameDbContext _context;

    public EquipmentController(
        EquipmentService equipmentService,
        StatsAggregationService statsAggregationService,
        DisenchantService disenchantService,
        ReforgeService reforgeService,
        RerollService rerollService,
        GameDbContext context)
    {
        _equipmentService = equipmentService;
        _statsAggregationService = statsAggregationService;
        _disenchantService = disenchantService;
        _reforgeService = reforgeService;
        _rerollService = rerollService;
        _context = context;
    }
    /// <summary>
    /// 获取角色的装备栏
    /// </summary>
    /// <param name="characterId">角色ID</param>
    /// <returns>装备栏信息，包含所有装备槽和总属性</returns>
    [HttpGet("{characterId:guid}")]
    public async Task<ActionResult<object>> GetEquipment(Guid characterId)
    {
        // 获取所有装备
        var equippedGear = await _equipmentService.GetEquippedGearAsync(characterId);
        
        // 获取角色信息
        var character = await _context.Characters.FindAsync(characterId);
        if (character == null)
        {
            return NotFound(new { error = "角色不存在" });
        }

        // 定义所有17个槽位
        var allSlots = new[]
        {
            EquipmentSlot.Head, EquipmentSlot.Neck, EquipmentSlot.Shoulder,
            EquipmentSlot.Back, EquipmentSlot.Chest, EquipmentSlot.Wrist,
            EquipmentSlot.Hands, EquipmentSlot.Waist, EquipmentSlot.Legs,
            EquipmentSlot.Feet, EquipmentSlot.Finger1, EquipmentSlot.Finger2,
            EquipmentSlot.Trinket1, EquipmentSlot.Trinket2,
            EquipmentSlot.MainHand, EquipmentSlot.OffHand, EquipmentSlot.TwoHand
        };

        // 构建槽位信息
        var slots = allSlots.Select(slot =>
        {
            var gear = equippedGear.FirstOrDefault(g => g.SlotType == slot);
            return new
            {
                SlotType = slot.ToString(),
                SlotName = GetSlotDisplayName(slot),
                Item = gear != null ? new
                {
                    Id = gear.Id,
                    Name = gear.Definition?.Name ?? "未知装备",
                    Icon = gear.Definition?.Icon ?? "?",
                    Rarity = gear.Rarity.ToString(),
                    ItemLevel = gear.ItemLevel,
                    QualityScore = gear.QualityScore
                } : null,
                IsLocked = false
            };
        }).ToArray();

        // 获取总属性
        var stats = await _statsAggregationService.CalculateEquipmentStatsAsync(characterId);

        var response = new
        {
            characterId,
            characterName = character.Name,
            slots,
            totalStats = stats.ToDictionary(
                kvp => kvp.Key.ToString(),
                kvp => kvp.Value
            )
        };

        return Ok(response);
    }

    /// <summary>
    /// 装备物品
    /// </summary>
    /// <param name="characterId">角色ID</param>
    /// <param name="request">装备请求（包含装备实例ID）</param>
    /// <returns>装备结果</returns>
    [HttpPost("{characterId:guid}/equip")]
    public async Task<ActionResult<object>> EquipItem(Guid characterId, [FromBody] EquipRequest request)
    {
        var result = await _equipmentService.EquipAsync(characterId, request.GearInstanceId);
        
        if (!result.IsSuccess)
        {
            return BadRequest(new { error = result.Message });
        }

        // 返回更新后的装备信息
        var gear = await _context.Set<GearInstance>()
            .Include(g => g.Definition)
            .FirstOrDefaultAsync(g => g.Id == request.GearInstanceId);

        return Ok(new
        {
            success = true,
            message = result.Message,
            gear = gear != null ? new
            {
                Id = gear.Id,
                Name = gear.Definition?.Name ?? "未知装备",
                SlotType = gear.SlotType?.ToString(),
                IsEquipped = gear.IsEquipped
            } : null
        });
    }

    /// <summary>
    /// 卸下指定槽位的装备
    /// </summary>
    /// <param name="characterId">角色ID</param>
    /// <param name="slot">槽位类型</param>
    /// <returns>卸下结果</returns>
    [HttpDelete("{characterId:guid}/{slot}")]
    public async Task<ActionResult<object>> UnequipItem(Guid characterId, string slot)
    {
        if (!Enum.TryParse<EquipmentSlot>(slot, true, out var slotEnum))
        {
            return BadRequest(new { error = "无效的槽位类型" });
        }

        var result = await _equipmentService.UnequipAsync(characterId, slotEnum);

        return Ok(new
        {
            success = result.IsSuccess,
            message = result.Message,
            slot = slot
        });
    }

    /// <summary>
    /// 获取角色装备的总属性加成
    /// </summary>
    /// <param name="characterId">角色ID</param>
    /// <returns>总属性字典</returns>
    [HttpGet("{characterId:guid}/stats")]
    public async Task<ActionResult<object>> GetEquipmentStats(Guid characterId)
    {
        var summary = await _statsAggregationService.GetEquipmentStatsSummaryAsync(characterId);

        return Ok(new
        {
            characterId,
            stats = summary.Stats.ToDictionary(
                kvp => kvp.Key.ToString(),
                kvp => kvp.Value
            ),
            equippedCount = summary.EquippedCount,
            totalQualityScore = summary.TotalQualityScore
        });
    }

    /// <summary>
    /// 分解装备
    /// </summary>
    /// <param name="characterId">角色ID</param>
    /// <param name="request">分解请求</param>
    /// <returns>分解结果</returns>
    [HttpPost("{characterId:guid}/disenchant")]
    public async Task<ActionResult<object>> DisenchantItem(Guid characterId, [FromBody] DisenchantRequest request)
    {
        var result = await _disenchantService.DisenchantAsync(characterId, request.GearInstanceId);

        if (!result.IsSuccess)
        {
            return BadRequest(new { error = result.Message });
        }

        return Ok(new
        {
            success = true,
            message = result.Message,
            materials = result.Materials
        });
    }

    /// <summary>
    /// 批量分解装备
    /// </summary>
    /// <param name="characterId">角色ID</param>
    /// <param name="request">批量分解请求</param>
    /// <returns>批量分解结果</returns>
    [HttpPost("{characterId:guid}/disenchant-batch")]
    public async Task<ActionResult<object>> DisenchantBatch(Guid characterId, [FromBody] DisenchantBatchRequest request)
    {
        var result = await _disenchantService.DisenchantBatchAsync(characterId, request.GearInstanceIds);

        return Ok(new
        {
            successCount = result.SuccessCount,
            failCount = result.FailCount,
            totalMaterials = result.TotalMaterials,
            errors = result.Errors
        });
    }

    /// <summary>
    /// 预览装备分解
    /// </summary>
    /// <param name="gearInstanceId">装备实例ID</param>
    /// <returns>预览分解产出</returns>
    [HttpGet("disenchant-preview/{gearInstanceId:guid}")]
    public async Task<ActionResult<object>> PreviewDisenchant(Guid gearInstanceId)
    {
        var materials = await _disenchantService.PreviewDisenchantAsync(gearInstanceId);

        return Ok(new
        {
            gearInstanceId,
            materials
        });
    }

    /// <summary>
    /// 重铸装备（提升品级）
    /// </summary>
    /// <param name="characterId">角色ID</param>
    /// <param name="request">重铸请求</param>
    /// <returns>重铸结果</returns>
    [HttpPost("{characterId:guid}/reforge")]
    public async Task<ActionResult<object>> ReforgeItem(Guid characterId, [FromBody] ReforgeRequest request)
    {
        var result = await _reforgeService.ReforgeAsync(characterId, request.GearInstanceId);

        if (!result.IsSuccess)
        {
            return BadRequest(new { error = result.Message });
        }

        return Ok(new
        {
            success = true,
            message = result.Message,
            gear = result.ReforgedGear != null ? new
            {
                Id = result.ReforgedGear.Id,
                Name = result.ReforgedGear.Definition?.Name ?? "未知装备",
                TierLevel = result.ReforgedGear.TierLevel,
                QualityScore = result.ReforgedGear.QualityScore,
                RolledStats = result.ReforgedGear.RolledStats.ToDictionary(
                    kvp => kvp.Key.ToString(),
                    kvp => kvp.Value
                )
            } : null
        });
    }

    /// <summary>
    /// 预览重铸成本
    /// </summary>
    /// <param name="gearInstanceId">装备实例ID</param>
    /// <returns>重铸预览</returns>
    [HttpGet("reforge-preview/{gearInstanceId:guid}")]
    public async Task<ActionResult<object>> PreviewReforge(Guid gearInstanceId)
    {
        var preview = await _reforgeService.PreviewReforgeCostAsync(gearInstanceId);

        return Ok(new
        {
            canReforge = preview.CanReforge,
            message = preview.Message,
            currentTier = preview.CurrentTier,
            nextTier = preview.NextTier,
            cost = preview.Cost,
            currentStats = preview.CurrentStats.ToDictionary(
                kvp => kvp.Key.ToString(),
                kvp => kvp.Value
            ),
            previewStats = preview.PreviewStats.ToDictionary(
                kvp => kvp.Key.ToString(),
                kvp => kvp.Value
            )
        });
    }

    /// <summary>
    /// 重置装备所有词条
    /// </summary>
    /// <param name="characterId">角色ID</param>
    /// <param name="request">重置请求</param>
    /// <returns>重置结果</returns>
    [HttpPost("{characterId:guid}/reroll")]
    public async Task<ActionResult<object>> RerollAffixes(Guid characterId, [FromBody] RerollRequest request)
    {
        var result = await _rerollService.RerollAffixesAsync(characterId, request.GearInstanceId);

        if (!result.IsSuccess)
        {
            return BadRequest(new { error = result.Message });
        }

        return Ok(new
        {
            success = true,
            message = result.Message,
            gear = result.UpdatedGear != null ? new
            {
                Id = result.UpdatedGear.Id,
                Name = result.UpdatedGear.Definition?.Name ?? "未知装备",
                RerollCount = result.UpdatedGear.RerollCount,
                QualityScore = result.UpdatedGear.QualityScore,
                Affixes = result.UpdatedGear.Affixes.Select(a => new
                {
                    AffixId = a.AffixId,
                    DisplayText = a.DisplayText,
                    RolledValue = a.RolledValue
                }).ToList()
            } : null,
            oldAffixes = result.OldAffixes.Select(a => new
            {
                AffixId = a.AffixId,
                DisplayText = a.DisplayText,
                RolledValue = a.RolledValue
            }).ToList()
        });
    }

    /// <summary>
    /// 重置装备单个词条
    /// </summary>
    /// <param name="characterId">角色ID</param>
    /// <param name="request">单词条重置请求</param>
    /// <returns>重置结果</returns>
    [HttpPost("{characterId:guid}/reroll-single")]
    public async Task<ActionResult<object>> RerollSingleAffix(Guid characterId, [FromBody] RerollSingleRequest request)
    {
        var result = await _rerollService.RerollSingleAffixAsync(characterId, request.GearInstanceId, request.AffixIndex);

        if (!result.IsSuccess)
        {
            return BadRequest(new { error = result.Message });
        }

        return Ok(new
        {
            success = true,
            message = result.Message,
            gear = result.UpdatedGear != null ? new
            {
                Id = result.UpdatedGear.Id,
                Name = result.UpdatedGear.Definition?.Name ?? "未知装备",
                RerollCount = result.UpdatedGear.RerollCount,
                QualityScore = result.UpdatedGear.QualityScore,
                Affixes = result.UpdatedGear.Affixes.Select(a => new
                {
                    AffixId = a.AffixId,
                    DisplayText = a.DisplayText,
                    RolledValue = a.RolledValue
                }).ToList()
            } : null,
            affixIndex = request.AffixIndex
        });
    }

    /// <summary>
    /// 预览词条重置成本
    /// </summary>
    /// <param name="gearInstanceId">装备实例ID</param>
    /// <returns>预览成本</returns>
    [HttpGet("reroll-preview/{gearInstanceId:guid}")]
    public async Task<ActionResult<object>> PreviewReroll(Guid gearInstanceId)
    {
        var cost = await _rerollService.PreviewRerollCostAsync(gearInstanceId);

        return Ok(new
        {
            gearInstanceId,
            cost
        });
    }

    private static string GetSlotDisplayName(EquipmentSlot slot)
    {
        return slot switch
        {
            EquipmentSlot.Head => "头盔",
            EquipmentSlot.Neck => "项链",
            EquipmentSlot.Shoulder => "护肩",
            EquipmentSlot.Back => "披风",
            EquipmentSlot.Chest => "胸甲",
            EquipmentSlot.Wrist => "护腕",
            EquipmentSlot.Hands => "手套",
            EquipmentSlot.Waist => "腰带",
            EquipmentSlot.Legs => "护腿",
            EquipmentSlot.Feet => "鞋子",
            EquipmentSlot.Finger1 => "戒指1",
            EquipmentSlot.Finger2 => "戒指2",
            EquipmentSlot.Trinket1 => "饰品1",
            EquipmentSlot.Trinket2 => "饰品2",
            EquipmentSlot.MainHand => "主手",
            EquipmentSlot.OffHand => "副手",
            EquipmentSlot.TwoHand => "双手武器",
            _ => slot.ToString()
        };
    }
}

/// <summary>
/// 装备请求
/// </summary>
public class EquipRequest
{
    public Guid GearInstanceId { get; set; }
}

/// <summary>
/// 分解请求
/// </summary>
public class DisenchantRequest
{
    public Guid GearInstanceId { get; set; }
}

/// <summary>
/// 批量分解请求
/// </summary>
public class DisenchantBatchRequest
{
    public List<Guid> GearInstanceIds { get; set; } = new();
}

/// <summary>
/// 重铸请求
/// </summary>
public class ReforgeRequest
{
    public Guid GearInstanceId { get; set; }
}

/// <summary>
/// 词条重置请求
/// </summary>
public class RerollRequest
{
    public Guid GearInstanceId { get; set; }
}

/// <summary>
/// 单词条重置请求
/// </summary>
public class RerollSingleRequest
{
    public Guid GearInstanceId { get; set; }
    public int AffixIndex { get; set; }
}
