using BlazorIdle.Server.Application.Equipment;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace BlazorIdle.Server.Api;

/// <summary>
/// 装备系统API控制器
/// 提供装备管理、属性查询等功能
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class EquipmentController : ControllerBase
{
    private readonly EquipmentService _equipmentService;
    private readonly StatsAggregationService _statsService;

    public EquipmentController(
        EquipmentService equipmentService,
        StatsAggregationService statsService)
    {
        _equipmentService = equipmentService;
        _statsService = statsService;
    }
    /// <summary>
    /// 获取角色的装备栏
    /// </summary>
    /// <param name="characterId">角色ID</param>
    /// <returns>装备栏信息，包含所有装备槽和总属性</returns>
    [HttpGet("{characterId:guid}")]
    public async Task<ActionResult<object>> GetEquipment(Guid characterId)
    {
        try
        {
            var equippedGear = await _equipmentService.GetEquippedGearAsync(characterId);
            var totalStats = await _statsService.GetTotalStatsAsync(characterId);
            var gearScore = await _statsService.GetTotalGearScoreAsync(characterId);

            var response = new
            {
                characterId,
                equippedGear = equippedGear.Select(g => new
                {
                    id = g.Id,
                    definitionId = g.DefinitionId,
                    slot = g.SlotType.ToString(),
                    rarity = g.Rarity.ToString(),
                    itemLevel = g.ItemLevel,
                    qualityScore = g.QualityScore,
                    stats = g.RolledStats,
                    affixes = g.Affixes.Select(a => new
                    {
                        affixId = a.AffixId,
                        displayText = a.DisplayText,
                        value = a.RolledValue
                    })
                }),
                totalStats = totalStats.ToDictionary(
                    kv => kv.Key.ToString(),
                    kv => kv.Value
                ),
                gearScore
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// 装备物品
    /// </summary>
    /// <param name="characterId">角色ID</param>
    /// <param name="request">装备请求（包含装备实例ID）</param>
    /// <returns>操作结果</returns>
    [HttpPost("{characterId:guid}/equip")]
    public async Task<ActionResult<object>> EquipItem(Guid characterId, [FromBody] EquipRequest request)
    {
        try
        {
            var (success, message) = await _equipmentService.EquipAsync(characterId, request.GearInstanceId);
            
            if (!success)
            {
                return BadRequest(new { error = message });
            }

            return Ok(new { message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    public record EquipRequest(Guid GearInstanceId);

    /// <summary>
    /// 卸下指定槽位的装备
    /// </summary>
    /// <param name="characterId">角色ID</param>
    /// <param name="slot">槽位类型</param>
    /// <returns>操作结果</returns>
    [HttpDelete("{characterId:guid}/{slot}")]
    public async Task<ActionResult<object>> UnequipItem(Guid characterId, string slot)
    {
        try
        {
            if (!Enum.TryParse<Domain.Equipment.Models.EquipmentSlot>(slot, true, out var slotEnum))
            {
                return BadRequest(new { error = "无效的槽位类型" });
            }

            var (success, message) = await _equipmentService.UnequipAsync(characterId, slotEnum);
            
            if (!success)
            {
                return BadRequest(new { error = message });
            }

            return Ok(new { message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// 获取角色装备的总属性加成
    /// </summary>
    /// <param name="characterId">角色ID</param>
    /// <returns>总属性字典</returns>
    [HttpGet("{characterId:guid}/stats")]
    public async Task<ActionResult<object>> GetEquipmentStats(Guid characterId)
    {
        try
        {
            var stats = await _statsService.GetTotalStatsAsync(characterId);
            var gearScore = await _statsService.GetTotalGearScoreAsync(characterId);

            return Ok(new
            {
                characterId,
                stats = stats.ToDictionary(kv => kv.Key.ToString(), kv => kv.Value),
                gearScore
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }
}
