using BlazorIdle.Server.Application.Battles.Offline;
using Microsoft.AspNetCore.Mvc;

namespace BlazorIdle.Server.Api;

[ApiController]
[Route("api/[controller]")]
public class OfflineController : ControllerBase
{
    private readonly OfflineSettlementService _offline;

    public OfflineController(OfflineSettlementService offline) => _offline = offline;

    /// <summary>
    /// 检查角色离线时间并返回结算预览（不发放收益）
    /// GET /api/offline/check?characterId={id}
    /// </summary>
    [HttpGet("check")]
    public async Task<ActionResult<OfflineCheckResult>> CheckOffline(
        [FromQuery] Guid characterId,
        CancellationToken ct = default)
    {
        try
        {
            var result = await _offline.CheckAndSettleAsync(characterId, ct);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>
    /// 应用离线结算，实际发放收益到角色
    /// POST /api/offline/apply
    /// </summary>
    [HttpPost("apply")]
    public async Task<ActionResult> ApplySettlement(
        [FromBody] ApplySettlementRequest request,
        CancellationToken ct = default)
    {
        try
        {
            await _offline.ApplySettlementAsync(
                request.CharacterId,
                request.Settlement,
                ct);
            return Ok(new { success = true });
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    // 示例：POST /api/offline/settle?characterId=...&seconds=7200&mode=continuous&enemyId=dummy&enemyCount=1&dropMode=sampled
    [HttpPost("settle")]
    public async Task<ActionResult<object>> Settle(
        [FromQuery] Guid characterId,
        [FromQuery] double seconds,
        [FromQuery] string? mode = "continuous",
        [FromQuery] string? enemyId = "dummy",
        [FromQuery] int enemyCount = 1,
        [FromQuery] string? dungeonId = null,
        [FromQuery] ulong? seed = null,
        [FromQuery] string? dropMode = "expected",
        CancellationToken ct = default)
    {
        if (seconds <= 0) return BadRequest("seconds must be positive.");
        var res = await _offline.SimulateAsync(characterId, TimeSpan.FromSeconds(seconds), mode, enemyId, enemyCount, dungeonId, seed, dropMode, ct);
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
            res.DropMode,
            res.Gold,
            res.Exp,
            res.LootExpected,
            res.LootSampled
        });
    }
}

/// <summary>
/// 应用离线结算请求
/// </summary>
public record ApplySettlementRequest(
    Guid CharacterId,
    OfflineFastForwardResult Settlement
);