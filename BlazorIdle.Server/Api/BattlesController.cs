using BlazorIdle.Server.Application.Abstractions;
using BlazorIdle.Server.Application.Battles;
using BlazorIdle.Server.Domain.Combat.Enemies;
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

    [HttpGet("{id:guid}/summary")]
    public async Task<ActionResult<object>> Summary(Guid id, [FromQuery] string? dropMode = null)
    {
        var battle = await _battleRepo.GetWithSegmentsAsync(id);
        if (battle == null) return NotFound();

        var dps = battle.TotalDamage / Math.Max(0.0001, battle.DurationSeconds);
        var requested = (dropMode ?? "expected").Trim().ToLowerInvariant();
        if (requested != "expected" && requested != "sampled") requested = "expected";

        // 1) 若记录已持久化该 dropMode 的收益，直接返回
        if (!string.IsNullOrWhiteSpace(battle.RewardType)
            && string.Equals(battle.RewardType, requested, StringComparison.OrdinalIgnoreCase))
        {
            var lootObj = string.IsNullOrWhiteSpace(battle.LootJson)
                ? new Dictionary<string, double>()
                : JsonSerializer.Deserialize<Dictionary<string, double>>(battle.LootJson!) ?? new();

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

                DropMode = battle.RewardType,
                Gold = battle.Gold ?? 0,
                Exp = battle.Exp ?? 0,
                LootExpected = battle.RewardType == "expected" ? lootObj : new Dictionary<string, double>(),
                LootSampled = battle.RewardType == "sampled"
                    ? lootObj.ToDictionary(kv => kv.Key, kv => (int)Math.Round(kv.Value))
                    : new Dictionary<string, int>(),
                DungeonId = battle.DungeonId,
                DungeonRuns = battle.DungeonRuns ?? 0
            });
        }

        // 2) 否则：从段动态重算
        var killCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        int runCompleted = 0;
        string? dungeonId = battle.DungeonId;

        foreach (var s in battle.Segments)
        {
            var tags = JsonSerializer.Deserialize<Dictionary<string, int>>(s.TagCountersJson ?? "{}") ?? new();
            foreach (var (tag, val) in tags)
            {
                if (tag.StartsWith("kill.", StringComparison.Ordinal))
                {
                    if (!killCounts.ContainsKey(tag)) killCounts[tag] = 0;
                    killCounts[tag] += val;
                }
                else if (tag == "dungeon_run_complete") runCompleted += val;
                else if (dungeonId is null && tag.StartsWith("ctx.dungeonId.", StringComparison.Ordinal))
                    dungeonId = tag.Substring("ctx.dungeonId.".Length);
            }
        }

        // 解析 seed，避免在对象初始化器里重复 TryParse
        ulong? seedValue = ulong.TryParse(battle.Seed ?? "0", out var parsedSeed) ? parsedSeed : null;

        // 用对象初始化器一次性创建 EconomyContext（init-only 属性不可二次赋值）
        EconomyContext ctx;
        if (!string.IsNullOrWhiteSpace(dungeonId))
        {
            var d = DungeonRegistry.Resolve(dungeonId!);
            // Phase 6: 应用强化掉落倍率
            var finalDropMultiplier = d.DropChanceMultiplier * d.EnhancedDropMultiplier;
            ctx = new EconomyContext
            {
                Seed = seedValue,
                RunCompletedCount = runCompleted,
                GoldMultiplier = d.GoldMultiplier,
                ExpMultiplier = d.ExpMultiplier,
                DropChanceMultiplier = finalDropMultiplier,
                RunRewardGold = d.RunRewardGold,
                RunRewardExp = d.RunRewardExp,
                RunRewardLootTableId = d.RunRewardLootTableId,
                RunRewardLootRolls = d.RunRewardLootRolls
            };
        }
        else
        {
            ctx = new EconomyContext
            {
                Seed = seedValue,
                RunCompletedCount = runCompleted,
                GoldMultiplier = 1.0,
                ExpMultiplier = 1.0,
                DropChanceMultiplier = 1.0
            };
        }

        long gold; long exp; Dictionary<string, double>? lootExp = null; Dictionary<string, int>? lootSampled = null;

        if (requested == "sampled" && ctx.Seed is not null)
        {
            var r = EconomyCalculator.ComputeSampledWithContext(killCounts, ctx);
            gold = r.Gold; exp = r.Exp;
            lootSampled = r.Items.ToDictionary(kv => kv.Key, kv => (int)Math.Round(kv.Value));
        }
        else
        {
            var r = EconomyCalculator.ComputeExpectedWithContext(killCounts, ctx);
            gold = r.Gold; exp = r.Exp;
            lootExp = r.Items;
            requested = "expected";
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

            DropMode = requested,
            Gold = gold,
            Exp = exp,
            LootExpected = lootExp ?? new Dictionary<string, double>(),
            LootSampled = lootSampled ?? new Dictionary<string, int>(),
            DungeonId = dungeonId,
            DungeonRuns = runCompleted
        });
    }

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