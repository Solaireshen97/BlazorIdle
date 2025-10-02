using BlazorIdle.Server.Application.Battles;
using BlazorIdle.Server.Application.Abstractions;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace BlazorIdle.Server.Api;

[ApiController]
[Route("api/[controller]")]
public class BattlesController : ControllerBase
{
    private readonly StartBattleService _startSvc;
    private readonly IBattleRepository _battleRepo;

    public BattlesController(StartBattleService startSvc, IBattleRepository battleRepo)
    {
        _startSvc = startSvc;
        _battleRepo = battleRepo;
    }

    [HttpPost("start")]
    public async Task<ActionResult<object>> Start([FromQuery] Guid characterId, [FromQuery] double seconds = 15)
    {
        var id = await _startSvc.StartAsync(characterId, seconds);
        return Ok(new { battleId = id });
    }

    [HttpGet("{id:guid}/summary")]
    public async Task<ActionResult<object>> Summary(Guid id)
    {
        var battle = await _battleRepo.GetWithSegmentsAsync(id);
        if (battle == null) return NotFound();
        var dps = battle.TotalDamage / Math.Max(0.0001, battle.DurationSeconds);
        return Ok(new
        {
            battle.Id,
            battle.CharacterId,
            battle.TotalDamage,
            battle.DurationSeconds,
            Dps = Math.Round(dps, 2),
            SegmentCount = battle.Segments.Count
        });
    }

    [HttpGet("{id:guid}/segments")]
    public async Task<ActionResult<IEnumerable<object>>> Segments(Guid id)
    {
        var battle = await _battleRepo.GetWithSegmentsAsync(id);
        if (battle == null) return NotFound();
        var result = battle.Segments
            .OrderBy(s => s.StartTime)
            .Select(s => new {
                s.StartTime,
                s.EndTime,
                s.EventCount,
                s.TotalDamage,
                DamageBySource = JsonSerializer.Deserialize<Dictionary<string, int>>(s.DamageBySourceJson) ?? new()
            });
        return Ok(result);
    }
}