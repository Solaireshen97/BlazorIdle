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
    public async Task<ActionResult<object>> Summary(Guid id, [FromQuery] string? dropMode = null)
    {
        var battle = await _battleRepo.GetWithSegmentsAsync(id);
        if (battle == null) return NotFound();

        var dps = battle.TotalDamage / Math.Max(0.0001, battle.DurationSeconds);

        var killCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var s in battle.Segments)
        {
            var tags = JsonSerializer.Deserialize<Dictionary<string, int>>(s.TagCountersJson ?? "{}")
                       ?? new Dictionary<string, int>();
            foreach (var (tag, val) in tags)
            {
                if (!tag.StartsWith("kill.", StringComparison.Ordinal)) continue;
                if (!killCounts.ContainsKey(tag)) killCounts[tag] = 0;
                killCounts[tag] += val;
            }
        }

        var mode = (dropMode ?? "expected").Trim().ToLowerInvariant();
        long gold; long exp; Dictionary<string, double>? lootExp = null; Dictionary<string, int>? lootSampled = null;

        if (mode == "sampled")
        {
            // 从记录的 seed 派生（record.Seed 是字符串）
            ulong seed = 0;
            _ = ulong.TryParse(battle.Seed ?? "0", out seed);
            var r = EconomyCalculator.ComputeSampled(killCounts, seed);
            gold = r.Gold; exp = r.Exp;
            lootSampled = r.Items.ToDictionary(kv => kv.Key, kv => (int)Math.Round(kv.Value));
        }
        else
        {
            var r = EconomyCalculator.ComputeExpected(killCounts);
            gold = r.Gold; exp = r.Exp;
            lootExp = r.Items;
            mode = "expected";
        }

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

            DropMode = mode,
            Gold = gold,
            Exp = exp,
            LootExpected = lootExp ?? new Dictionary<string, double>(),
            LootSampled = lootSampled ?? new Dictionary<string, int>()
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