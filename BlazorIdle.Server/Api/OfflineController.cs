using BlazorIdle.Server.Application.Battles.Offline;
using Microsoft.AspNetCore.Mvc;

namespace BlazorIdle.Server.Api;

[ApiController]
[Route("api/[controller]")]
public class OfflineController : ControllerBase
{
    private readonly OfflineSettlementService _offline;

    public OfflineController(OfflineSettlementService offline) => _offline = offline;

    // 最小版离线结算：直接传 seconds（或由客户端根据下线时刻计算）
    // mode: continuous|dungeon|dungeonloop
    // 示例：POST /api/offline/settle?characterId=...&seconds=7200&mode=continuous&enemyId=dummy&enemyCount=1
    [HttpPost("settle")]
    public async Task<ActionResult<object>> Settle(
        [FromQuery] Guid characterId,
        [FromQuery] double seconds,
        [FromQuery] string? mode = "continuous",
        [FromQuery] string? enemyId = "dummy",
        [FromQuery] int enemyCount = 1,
        [FromQuery] string? dungeonId = null,
        [FromQuery] ulong? seed = null,
        CancellationToken ct = default)
    {
        if (seconds <= 0) return BadRequest("seconds must be positive.");
        var res = await _offline.SimulateAsync(characterId, TimeSpan.FromSeconds(seconds), mode, enemyId, enemyCount, dungeonId, seed, ct);
        return Ok(new
        {
            res.CharacterId,
            res.SimulatedSeconds,
            res.TotalDamage,
            res.TotalKills,
            res.Mode,
            res.EnemyId,
            res.EnemyCount,
            res.DungeonId,
            // 新增
            res.Gold,
            res.Exp,
            res.LootExpected
        });
    }
}