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

    // POST /api/battles/start?characterId=...&seconds=...&seed=...&enemyId=...&enemyCount=...
    [HttpPost("start")]
    public async Task<IActionResult> Start([FromQuery] Guid characterId, [FromQuery] double seconds = 15, [FromQuery] ulong? seed = null, [FromQuery] string? enemyId = null, [FromQuery] int enemyCount = 1)
    {
        var id = await _startSvc.StartAsync(characterId, seconds, seed, enemyId, enemyCount);
        return Ok(new { battleId = id, seed, enemyId, enemyCount });
    }

    // GET /api/battles/{id}/summary
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
            SegmentCount = battle.Segments.Count,
            battle.AttackIntervalSeconds,
            battle.SpecialIntervalSeconds,
            // 新增：敌人与击杀
            battle.EnemyId,
            battle.EnemyName,
            battle.EnemyLevel,
            battle.EnemyMaxHp,
            battle.EnemyArmor,
            battle.EnemyMagicResist,
            battle.Killed,
            battle.KillTimeSeconds,
            battle.OverkillDamage,
            // 新增：RNG
            battle.Seed,
            battle.SeedIndexStart,
            battle.SeedIndexEnd
        });
    }

    // GET /api/battles/{id}/segments
    [HttpGet("{id:guid}/segments")]
    public async Task<ActionResult<IEnumerable<object>>> Segments(Guid id)
    {
        var battle = await _battleRepo.GetWithSegmentsAsync(id);
        if (battle == null) return NotFound();

        var result = battle.Segments
            .OrderBy(s => s.StartTime)
            .Select(s => new
            {
                s.StartTime,
                s.EndTime,
                s.EventCount,
                s.TotalDamage,
                DamageBySource = JsonSerializer.Deserialize<Dictionary<string, int>>(s.DamageBySourceJson) ?? new(),
                DamageByType = JsonSerializer.Deserialize<Dictionary<string, int>>(s.DamageByTypeJson) ?? new(),
                ResourceFlow = JsonSerializer.Deserialize<Dictionary<string, int>>(s.ResourceFlowJson) ?? new(),
                // 新增：段级 RNG 区间
                s.RngIndexStart,
                s.RngIndexEnd
            });

        return Ok(result);
    }

    // GET /api/battles/{id}/debug
    // 最小版：返回持久化摘要 + 段统计 + RNG 段区间（实时状态将在异步 Step 上线后提供）
    [HttpGet("{id:guid}/debug")]
    public async Task<ActionResult<object>> Debug(Guid id)
    {
        var battle = await _battleRepo.GetWithSegmentsAsync(id);
        if (battle == null) return NotFound();

        var segs = battle.Segments.OrderBy(s => s.StartTime).Select(s => new
        {
            s.StartTime,
            s.EndTime,
            s.EventCount,
            s.TotalDamage,
            s.RngIndexStart,
            s.RngIndexEnd
        });

        return Ok(new
        {
            battle.Id,
            battle.CharacterId,
            battle.TotalDamage,
            battle.DurationSeconds,
            battle.Seed,
            battle.SeedIndexStart,
            battle.SeedIndexEnd,
            battle.AttackIntervalSeconds,
            battle.SpecialIntervalSeconds,
            battle.EnemyId,
            battle.EnemyName,
            battle.EnemyLevel,
            battle.EnemyMaxHp,
            battle.Killed,
            battle.KillTimeSeconds,
            battle.OverkillDamage,
            Segments = segs
        });
    }
}