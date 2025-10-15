using BlazorIdle.Server.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BlazorIdle.Server.Api;

/// <summary>
/// 背包系统 API 控制器
/// </summary>
/// <remarks>
/// 提供的功能：
/// - 查询角色背包物品列表
/// - 显示物品数量和更新时间
/// - 同时返回角色基本信息（金币、经验）
/// 
/// 背包特性：
/// - 物品按数量倒序排列（数量多的在前）
/// - 相同数量的物品按ID排序
/// - 包含物品的最后更新时间
/// - 无背包容量限制
/// 
/// 认证要求：
/// - 所有接口均无需认证（基于角色ID操作）
/// 
/// 基础路由：/api/inventory
/// </remarks>
[ApiController]
[Route("api/[controller]")]
public class InventoryController : ControllerBase
{
    private readonly GameDbContext _db;

    public InventoryController(GameDbContext db) => _db = db;

    /// <summary>
    /// 获取角色的背包物品列表
    /// </summary>
    /// <param name="characterId">角色ID</param>
    /// <returns>物品列表，包含物品ID、数量等信息</returns>
    /// <response code="200">返回背包信息</response>
    /// <response code="404">角色不存在</response>
    /// <remarks>
    /// 功能说明：
    /// - 返回角色的所有背包物品
    /// - 物品按数量倒序排列（便于查看稀缺资源）
    /// - 同时返回角色的金币和经验值
    /// - 包含物品的最后更新时间戳
    /// 
    /// 物品类型：
    /// - 制造材料（矿石、皮革、布料等）
    /// - 消耗品（药水、食物等）
    /// - 任务物品
    /// - 其他杂项物品
    /// 
    /// 排序规则：
    /// 1. 数量倒序（数量多的在前）
    /// 2. 相同数量按ItemId字母顺序
    /// 
    /// 请求示例：
    /// <code>
    /// GET /api/inventory/123e4567-e89b-12d3-a456-426614174000
    /// </code>
    /// 
    /// 响应示例：
    /// <code>
    /// {
    ///   "characterId": "123e4567-e89b-12d3-a456-426614174000",
    ///   "characterName": "我的战士",
    ///   "gold": 5000,
    ///   "experience": 12500,
    ///   "items": [
    ///     {
    ///       "itemId": "iron_ore",
    ///       "quantity": 150,
    ///       "updatedAt": "2025-10-15T10:30:00Z"
    ///     },
    ///     {
    ///       "itemId": "health_potion",
    ///       "quantity": 25,
    ///       "updatedAt": "2025-10-15T09:15:00Z"
    ///     }
    ///   ]
    /// }
    /// </code>
    /// </remarks>
    [HttpGet("{characterId:guid}")]
    public async Task<ActionResult<object>> GetInventory(Guid characterId)
    {
        var character = await _db.Characters.FindAsync(characterId);
        if (character == null)
            return NotFound(new { error = "Character not found" });

        var items = await _db.InventoryItems
            .Where(x => x.CharacterId == characterId)
            .OrderByDescending(x => x.Quantity)
            .ThenBy(x => x.ItemId)
            .Select(x => new
            {
                x.ItemId,
                x.Quantity,
                x.UpdatedAt
            })
            .ToListAsync();

        return Ok(new
        {
            characterId,
            characterName = character.Name,
            gold = character.Gold,
            experience = character.Experience,
            items
        });
    }
}
