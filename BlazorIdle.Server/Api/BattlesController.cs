using BlazorIdle.Server.Application.Abstractions;
using BlazorIdle.Server.Application.Battles;
using BlazorIdle.Server.Domain.Economy;
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

    // POST /api/battles/start?characterId=...&seconds=...&seed=...&enemyId=...&enemyCount=...&mode=...&dungeonId=...
    // mode: duration(默认) | continuous | dungeon | dungeonloop
    [HttpPost("start")]
    public async Task<IActionResult> Start(
        [FromQuery] Guid characterId,
        [FromQuery] double seconds = 15,
        [FromQuery] ulong? seed = null,
        [FromQuery] string? enemyId = null,
        [FromQuery] int enemyCount = 1,
        [FromQuery] string? mode = null,
        [FromQuery] string? dungeonId = null)
    {
        var id = await _startSvc.StartAsync(characterId, seconds, seed, enemyId, enemyCount, mode, dungeonId);
        return Ok(new { battleId = id, seed, enemyId, enemyCount, mode = mode ?? "duration", dungeonId });
    }

    // GET /api/battles/{id}/summary
    [HttpGet("{id:guid}/summary")]
    public async Task<ActionResult<object>> Summary(Guid id)
    {
        var battle = await _battleRepo.GetWithSegmentsAsync(id);
        if (battle == null) return NotFound();

        var dps = battle.TotalDamage / Math.Max(0.0001, battle.DurationSeconds);

        // 新增：从段 TagCountersJson 聚合 kill.* → 奖励
        var killCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var s in battle.Segments)
        {
            var tags = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, int>>(s.TagCountersJson ?? "{}")
                       ?? new Dictionary<string, int>();
            foreach (var (tag, val) in tags)
            {
                if (!tag.StartsWith("kill.", StringComparison.Ordinal)) continue;
                if (!killCounts.ContainsKey(tag)) killCounts[tag] = 0;
                killCounts[tag] += val;
            }
        }
        var reward = EconomyCalculator.ComputeExpected(killCounts);

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
            battle.EnemyId,
            battle.EnemyName,
            battle.EnemyLevel,
            battle.EnemyMaxHp,
            battle.EnemyArmor,
            battle.EnemyMagicResist,
            battle.Killed,
            battle.KillTimeSeconds,
            battle.OverkillDamage,
            battle.Seed,
            battle.SeedIndexStart,
            battle.SeedIndexEnd,

            // 新增：奖励输出
            Gold = reward.Gold,
            Exp = reward.Exp,
            LootExpected = reward.Items
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
                s.RngIndexStart,
                s.RngIndexEnd
            });

        return Ok(result);
    }

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