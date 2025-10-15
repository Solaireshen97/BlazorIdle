using BlazorIdle.Server.Application.Abstractions;
using BlazorIdle.Server.Application.Battles;
using BlazorIdle.Server.Domain.Combat.Enemies;
using BlazorIdle.Server.Domain.Economy;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace BlazorIdle.Server.Api;

/// <summary>
/// 战斗系统 API 控制器
/// </summary>
/// <remarks>
/// 提供的功能：
/// - 开始新战斗（持续时间模式/击杀模式/地下城模式）
/// - 查询战斗结果摘要（包含伤害统计、经济收益）
/// - 查询战斗段详细数据（伤害来源、资源流动）
/// - 查询战斗调试信息（RNG种子、事件计数）
/// 
/// 战斗模式：
/// - duration: 持续时间模式（战斗固定秒数）
/// - kill: 击杀模式（击杀指定数量敌人）
/// - dungeon: 地下城模式（完成地下城波次）
/// 
/// 认证要求：
/// - 所有接口均无需认证（基于角色ID操作）
/// 
/// 基础路由：/api/battles
/// </remarks>
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

    /// <summary>
    /// 开始新战斗
    /// </summary>
    /// <param name="characterId">角色ID</param>
    /// <param name="seconds">战斗持续时间（秒），默认15秒</param>
    /// <param name="seed">可选的随机数种子，用于战斗复现</param>
    /// <param name="enemyId">可选的敌人ID，不指定则使用默认敌人</param>
    /// <param name="enemyCount">敌人数量，默认1个</param>
    /// <param name="mode">战斗模式（duration/kill/dungeon），默认duration</param>
    /// <param name="dungeonId">地下城ID，仅在dungeon模式下使用</param>
    /// <returns>战斗ID和战斗参数</returns>
    /// <response code="200">战斗开始成功，返回战斗ID</response>
    /// <remarks>
    /// 功能说明：
    /// - 创建新战斗实例并开始模拟
    /// - 根据mode参数决定战斗结束条件
    /// - 使用角色当前装备和属性进行战斗
    /// - 战斗结果异步计算并存储
    /// 
    /// 战斗模式详解：
    /// - duration: 战斗指定秒数后结束
    /// - kill: 击杀指定数量敌人后结束
    /// - dungeon: 完成地下城所有波次后结束
    /// 
    /// 随机数种子：
    /// - 如果提供seed，战斗结果可以完全复现
    /// - 不提供seed则使用随机生成的种子
    /// - 用于战斗回放和调试
    /// 
    /// 请求示例：
    /// <code>
    /// POST /api/battles/start?characterId=123e4567-e89b-12d3-a456-426614174000&amp;seconds=30&amp;mode=duration
    /// POST /api/battles/start?characterId=123e4567-e89b-12d3-a456-426614174000&amp;enemyId=goblin&amp;enemyCount=10&amp;mode=kill
    /// POST /api/battles/start?characterId=123e4567-e89b-12d3-a456-426614174000&amp;dungeonId=forest_depths&amp;mode=dungeon
    /// </code>
    /// 
    /// 响应示例：
    /// <code>
    /// {
    ///   "battleId": "789e0123-e89b-12d3-a456-426614174000",
    ///   "seed": 12345678901234,
    ///   "enemyId": "goblin",
    ///   "enemyCount": 1,
    ///   "mode": "duration",
    ///   "dungeonId": null
    /// }
    /// </code>
    /// </remarks>
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

    /// <summary>
    /// 获取战斗结果摘要
    /// </summary>
    /// <param name="id">战斗ID</param>
    /// <param name="dropMode">掉落计算模式（expected/sampled），默认expected</param>
    /// <returns>战斗统计数据和经济收益</returns>
    /// <response code="200">返回战斗摘要</response>
    /// <response code="404">战斗不存在</response>
    /// <remarks>
    /// 功能说明：
    /// - 返回战斗的完整统计数据
    /// - 包含伤害统计、DPS、击杀信息
    /// - 计算经济收益（金币、经验、掉落物品）
    /// - 支持两种掉落计算模式
    /// 
    /// 掉落模式说明：
    /// - expected: 期望值模式，返回物品掉落的期望数量（小数）
    /// - sampled: 采样模式，使用RNG种子实际模拟掉落（整数）
    /// 
    /// 性能优化：
    /// - 如果战斗记录已缓存指定dropMode的收益，直接返回
    /// - 否则从战斗段动态重新计算
    /// 
    /// 地下城奖励：
    /// - 包含地下城特殊奖励（完成奖励、强化掉落倍率）
    /// - 显示地下城完成次数（DungeonRuns）
    /// 
    /// 请求示例：
    /// <code>
    /// GET /api/battles/789e0123-e89b-12d3-a456-426614174000/summary
    /// GET /api/battles/789e0123-e89b-12d3-a456-426614174000/summary?dropMode=sampled
    /// </code>
    /// 
    /// 响应示例：
    /// <code>
    /// {
    ///   "id": "789e0123-e89b-12d3-a456-426614174000",
    ///   "characterId": "123e4567-e89b-12d3-a456-426614174000",
    ///   "totalDamage": 50000,
    ///   "durationSeconds": 30.5,
    ///   "dps": 1639.34,
    ///   "segmentCount": 3,
    ///   "killed": 5,
    ///   "dropMode": "expected",
    ///   "gold": 150,
    ///   "exp": 500,
    ///   "lootExpected": { "iron_ore": 2.5, "leather": 1.8 },
    ///   "lootSampled": {},
    ///   "dungeonId": null,
    ///   "dungeonRuns": 0
    /// }
    /// </code>
    /// </remarks>
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

    /// <summary>
    /// 获取战斗段详细数据
    /// </summary>
    /// <param name="id">战斗ID</param>
    /// <returns>战斗段列表，包含伤害来源和资源流动统计</returns>
    /// <response code="200">返回战斗段列表</response>
    /// <response code="404">战斗不存在</response>
    /// <remarks>
    /// 功能说明：
    /// - 返回战斗的所有时间段数据
    /// - 每个段包含详细的伤害统计和资源流动
    /// - 用于战斗分析和回放
    /// 
    /// 段数据包含：
    /// - 时间范围（StartTime/EndTime）
    /// - 事件数量和总伤害
    /// - 按来源分类的伤害（攻击/技能/Buff）
    /// - 按类型分类的伤害（物理/魔法/真实）
    /// - 资源流动（怒气/能量等）
    /// - RNG索引范围（用于复现）
    /// 
    /// 请求示例：
    /// <code>
    /// GET /api/battles/789e0123-e89b-12d3-a456-426614174000/segments
    /// </code>
    /// 
    /// 响应示例：
    /// <code>
    /// [
    ///   {
    ///     "startTime": 0.0,
    ///     "endTime": 10.0,
    ///     "eventCount": 25,
    ///     "totalDamage": 15000,
    ///     "damageBySource": { "Attack": 12000, "Skill": 3000 },
    ///     "damageByType": { "Physical": 15000 },
    ///     "resourceFlow": { "Rage": 50 },
    ///     "rngIndexStart": 0,
    ///     "rngIndexEnd": 100
    ///   }
    /// ]
    /// </code>
    /// </remarks>
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

    /// <summary>
    /// 获取战斗调试信息
    /// </summary>
    /// <param name="id">战斗ID</param>
    /// <returns>战斗调试数据，包含RNG种子和段摘要</returns>
    /// <response code="200">返回调试信息</response>
    /// <response code="404">战斗不存在</response>
    /// <remarks>
    /// 功能说明：
    /// - 返回战斗的核心调试数据
    /// - 包含RNG种子信息（用于战斗复现）
    /// - 包含敌人配置和击杀统计
    /// - 包含简化的段摘要（不含详细统计）
    /// 
    /// 用途：
    /// - 调试战斗逻辑问题
    /// - 复现特定战斗场景
    /// - 验证RNG一致性
    /// - 性能分析（事件计数）
    /// 
    /// 请求示例：
    /// <code>
    /// GET /api/battles/789e0123-e89b-12d3-a456-426614174000/debug
    /// </code>
    /// 
    /// 响应示例：
    /// <code>
    /// {
    ///   "id": "789e0123-e89b-12d3-a456-426614174000",
    ///   "characterId": "123e4567-e89b-12d3-a456-426614174000",
    ///   "totalDamage": 50000,
    ///   "durationSeconds": 30.5,
    ///   "seed": "12345678901234",
    ///   "seedIndexStart": 0,
    ///   "seedIndexEnd": 500,
    ///   "attackIntervalSeconds": 2.5,
    ///   "specialIntervalSeconds": 8.0,
    ///   "enemyId": "goblin",
    ///   "enemyName": "哥布林",
    ///   "enemyLevel": 10,
    ///   "enemyMaxHp": 1000,
    ///   "killed": 5,
    ///   "killTimeSeconds": 25.3,
    ///   "overkillDamage": 150,
    ///   "segments": [
    ///     { "startTime": 0.0, "endTime": 10.0, "eventCount": 25, "totalDamage": 15000 }
    ///   ]
    /// }
    /// </code>
    /// </remarks>
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