using BlazorIdle.Server.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BlazorIdle.Server.Api;

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
