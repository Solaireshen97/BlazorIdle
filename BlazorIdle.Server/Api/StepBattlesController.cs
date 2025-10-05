using System.Text.Json;
using BlazorIdle.Server.Application.Battles.Step;
using BlazorIdle.Server.Application.Abstractions;
using BlazorIdle.Server.Domain.Characters;
using BlazorIdle.Server.Domain.Combat.Professions;
using BlazorIdle.Server.Domain.Combat.Rng;
using Microsoft.AspNetCore.Mvc;

namespace BlazorIdle.Server.Api;

[ApiController]
[Route("api/battles/step")]
public class StepBattlesController : ControllerBase
{
    private readonly StepBattleCoordinator _coord;
    private readonly ICharacterRepository _characters;

    public StepBattlesController(StepBattleCoordinator coord, ICharacterRepository characters)
    {
        _coord = coord;
        _characters = characters;
    }

    // POST /api/battles/step/start?characterId=...&seconds=...&seed=...&enemyId=...&enemyCount=...
    [HttpPost("start")]
    public async Task<IActionResult> Start([FromQuery] Guid characterId, [FromQuery] double seconds = 30, [FromQuery] ulong? seed = null, [FromQuery] string? enemyId = null, [FromQuery] int enemyCount = 1)
    {
        var c = await _characters.GetAsync(characterId);
        if (c is null) return NotFound("Character not found.");
        var profession = c.Profession;

        // 职业基础 + 主属性转换（与同步路径一致）
        var baseStats = ProfessionBaseStatsRegistry.Resolve(profession);
        var attrs = new PrimaryAttributes(c.Strength, c.Agility, c.Intellect, c.Stamina);
        var derived = StatsBuilder.BuildDerived(profession, attrs);
        var stats = StatsBuilder.Combine(baseStats, derived);

        ulong finalSeed = seed ?? DeriveSeed(characterId);

        var id = _coord.Start(characterId, profession, stats, seconds, finalSeed, enemyId, enemyCount);
        return Ok(new { battleId = id, seed = finalSeed, enemyId = enemyId ?? "dummy", enemyCount });
    }

    // GET /api/battles/step/{id}/status
    [HttpGet("{id:guid}/status")]
    public ActionResult<object> Status(Guid id)
    {
        var (found, s) = _coord.GetStatus(id);
        if (!found) return NotFound();
        return Ok(s);
    }

    // GET /api/battles/step/{id}/segments?since=0
    [HttpGet("{id:guid}/segments")]
    public ActionResult<IEnumerable<object>> Segments(Guid id, [FromQuery] int since = 0)
    {
        var (found, segments) = _coord.GetSegments(id, since);
        if (!found) return NotFound();
        return Ok(segments);
    }

    private static ulong DeriveSeed(Guid characterId)
    {
        var baseRng = RngContext.FromGuid(characterId);
        baseRng.Skip(4);
        ulong salt = (ulong)DateTime.UtcNow.Ticks;
        return RngContext.Hash64(baseRng.NextUInt64() ^ salt);
    }
}