using Microsoft.AspNetCore.Mvc;

namespace BlazorIdle.Server.Api;

/// <summary>
/// 装备系统API控制器（Step 5: 装备系统UI预留）
/// 当前为占位实现，返回空槽数据
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class EquipmentController : ControllerBase
{
    /// <summary>
    /// 获取角色的装备栏
    /// </summary>
    /// <param name="characterId">角色ID</param>
    /// <returns>装备栏信息，包含所有装备槽和总属性</returns>
    [HttpGet("{characterId:guid}")]
    public ActionResult<object> GetEquipment(Guid characterId)
    {
        // 占位实现：返回空装备槽
        var slots = new[]
        {
            new { SlotType = "head", SlotName = "头盔", Item = (object?)null, IsLocked = false },
            new { SlotType = "weapon", SlotName = "武器", Item = (object?)null, IsLocked = false },
            new { SlotType = "chest", SlotName = "胸甲", Item = (object?)null, IsLocked = false },
            new { SlotType = "offhand", SlotName = "副手", Item = (object?)null, IsLocked = false },
            new { SlotType = "waist", SlotName = "腰带", Item = (object?)null, IsLocked = false },
            new { SlotType = "legs", SlotName = "腿部", Item = (object?)null, IsLocked = false },
            new { SlotType = "feet", SlotName = "鞋子", Item = (object?)null, IsLocked = false },
            new { SlotType = "trinket1", SlotName = "饰品1", Item = (object?)null, IsLocked = false },
            new { SlotType = "trinket2", SlotName = "饰品2", Item = (object?)null, IsLocked = false }
        };

        var response = new
        {
            characterId,
            characterName = "角色名称",
            slots,
            totalStats = new Dictionary<string, double>
            {
                { "AttackPower", 0 },
                { "Armor", 0 },
                { "HastePercent", 0 },
                { "CritChance", 0 }
            }
        };

        return Ok(response);
    }

    /// <summary>
    /// 装备物品到指定槽位
    /// </summary>
    /// <param name="characterId">角色ID</param>
    /// <param name="slot">槽位类型</param>
    /// <param name="request">装备请求（包含物品ID）</param>
    /// <returns>装备后的槽位信息</returns>
    [HttpPost("{characterId:guid}/{slot}")]
    public ActionResult<object> EquipItem(Guid characterId, string slot, [FromBody] object request)
    {
        // 占位实现：返回未实现错误
        return StatusCode(501, new { error = "装备系统尚未实现" });
    }

    /// <summary>
    /// 卸下指定槽位的装备
    /// </summary>
    /// <param name="characterId">角色ID</param>
    /// <param name="slot">槽位类型</param>
    /// <returns>卸下后的槽位信息</returns>
    [HttpDelete("{characterId:guid}/{slot}")]
    public ActionResult<object> UnequipItem(Guid characterId, string slot)
    {
        // 占位实现：返回未实现错误
        return StatusCode(501, new { error = "装备系统尚未实现" });
    }

    /// <summary>
    /// 获取角色装备的总属性加成
    /// </summary>
    /// <param name="characterId">角色ID</param>
    /// <returns>总属性字典</returns>
    [HttpGet("{characterId:guid}/stats")]
    public ActionResult<object> GetEquipmentStats(Guid characterId)
    {
        // 占位实现：返回空属性
        var stats = new Dictionary<string, double>
        {
            { "AttackPower", 0 },
            { "Armor", 0 },
            { "HastePercent", 0 },
            { "CritChance", 0 }
        };

        return Ok(new { characterId, stats });
    }
}
