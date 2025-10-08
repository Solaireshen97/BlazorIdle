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
    /// 检查角色离线时间并返回结算预览
    /// GET /api/offline/check?characterId={id}
    /// 注意：默认不自动发放收益，如需自动发放请使用心跳端点
    /// </summary>
    [HttpGet("check")]
    public async Task<ActionResult<OfflineCheckResult>> CheckOffline(
        [FromQuery] Guid characterId,
        [FromQuery] bool autoApply = false,
        CancellationToken ct = default)
    {
        try
        {
            // 允许通过查询参数控制是否自动应用
            var result = await _offline.CheckAndSettleAsync(characterId, autoApply, ct);
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
    /// 注意：此端点已被废弃。离线结算现在在心跳更新时自动触发和应用。
    /// 保留此端点仅为向后兼容，建议使用心跳机制自动处理。
    /// </summary>
    [HttpPost("apply")]
    [Obsolete("此端点已废弃，离线结算现在通过心跳自动处理")]
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
            return Ok(new { success = true, message = "结算应用成功（注意：建议使用心跳自动结算）" });
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