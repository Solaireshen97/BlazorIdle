using BlazorIdle.Server.Application.Abstractions;
using BlazorIdle.Server.Application.Battles.Step;
using BlazorIdle.Server.Domain.Characters;
using BlazorIdle.Server.Domain.Combat;
using BlazorIdle.Server.Domain.Combat.Enemies;
using BlazorIdle.Server.Domain.Combat.Professions;
using BlazorIdle.Server.Domain.Combat.Rng;
using BlazorIdle.Server.Domain.Economy;
using BlazorIdle.Server.Domain.Records;
using System.Text.Json;

namespace BlazorIdle.Server.Application.Battles;

public class StartBattleService
{
    private readonly ICharacterRepository _characters;
    private readonly IBattleRepository _battles;
    private readonly BattleRunner _runner;
    private readonly BattleSimulator _simulator;
    private readonly string _defaultDropMode; // "expected" | "sampled"

    public StartBattleService(ICharacterRepository characters, IBattleRepository battles, BattleRunner runner, BattleSimulator simulator, IConfiguration cfg)
    {
        _characters = characters;
        _battles = battles;
        _runner = runner;
        _simulator = simulator;
        _defaultDropMode = cfg.GetValue<string>("Economy:DefaultDropMode")?.Trim().ToLowerInvariant() == "sampled"
            ? "sampled" : "expected";
    }

    // 新增：mode/dungeonId + 刷新等待覆盖（可选）
    public async Task<Guid> StartAsync(
        Guid characterId,
        double simulateSeconds = 15,
        ulong? seed = null,
        string? enemyId = null,
        int enemyCount = 1,
        string? mode = null,
        string? dungeonId = null,
        double? respawnDelay = null,
        double? waveDelay = null,
        double? runDelay = null,
        CancellationToken ct = default)
    {
        var c = await _characters.GetAsync(characterId, ct) ?? throw new InvalidOperationException("Character not found");
        var module = ProfessionRegistry.Resolve(c.Profession);

        var enemyDef = EnemyRegistry.Resolve(enemyId);
        enemyCount = Math.Max(1, enemyCount);

        var battleDomain = new Battle
        {
            CharacterId = characterId,
            AttackIntervalSeconds = module.BaseAttackInterval,
            SpecialIntervalSeconds = module.BaseSpecialInterval,
            StartedAt = 0
        };

        var baseStats = ProfessionBaseStatsRegistry.Resolve(c.Profession);
        var attrs = new PrimaryAttributes(c.Strength, c.Agility, c.Intellect, c.Stamina);
        var derived = StatsBuilder.BuildDerived(c.Profession, attrs);
        var stats = StatsBuilder.Combine(baseStats, derived);

        ulong finalSeed = seed ?? DeriveSeed(characterId);
        var m = (mode ?? "duration").Trim().ToLowerInvariant();

        List<CombatSegment> segments;
        bool killed;
        double? killTime;
        int overkill;
        long seedIndexStart;
        long seedIndexEnd;

        string? dungeonIdForRecord = null;

        if (m == "continuous" || m == "dungeon" || m == "dungeonloop")
        {
            var rng = new RngContext(finalSeed);
            seedIndexStart = rng.Index;

            // 使用 BattleSimulator 统一创建
            var config = new BattleSimulator.BattleConfig
            {
                BattleId = battleDomain.Id,
                CharacterId = characterId,
                Profession = c.Profession,
                Stats = stats,
                Seed = finalSeed,
                EnemyDef = enemyDef,
                EnemyCount = enemyCount,
                Mode = m,
                DungeonId = dungeonId,
                ContinuousRespawnDelaySeconds = respawnDelay,
                DungeonWaveDelaySeconds = waveDelay,
                DungeonRunDelaySeconds = runDelay,
                Module = module
            };

            var rb = _simulator.CreateRunningBattle(config, simulateSeconds);
            rb.FastForwardTo(simulateSeconds);

            segments = rb.Segments.ToList();
            killed = rb.Killed;
            killTime = rb.KillTime;
            overkill = rb.Overkill;
            seedIndexEnd = rb.SeedIndexEnd;
            dungeonIdForRecord = rb.DungeonId;
        }
        else
        {
            var rng = new RngContext(finalSeed);
            seedIndexStart = rng.Index;

            var groupDefs = Enumerable.Range(0, enemyCount).Select(_ => enemyDef).ToList();
            var encounterGroup = new EncounterGroup(groupDefs);

            segments = _runner.RunForDuration(
                battleDomain, simulateSeconds, c.Profession, rng,
                out killed, out killTime, out overkill,
                module: module,
                encounter: null,
                encounterGroup: encounterGroup,
                stats: stats
            ).ToList();

            seedIndexEnd = rng.Index;
        }

        var totalDamage = segments.Sum(s => s.TotalDamage);

        // 1) 经济聚合：kill.* + run_complete
        var killCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        int runCompleted = 0;
        foreach (var s in segments)
        {
            if (s.TagCounters.TryGetValue("dungeon_run_complete", out var rc))
                runCompleted += rc;

            foreach (var (tag, val) in s.TagCounters)
            {
                if (!tag.StartsWith("kill.", StringComparison.Ordinal)) continue;
                if (!killCounts.ContainsKey(tag)) killCounts[tag] = 0;
                killCounts[tag] += val;
            }
        }

        // 2) 构建上下文（init-only 属性：分别用对象初始化器一次性构造）
        EconomyContext ctx;
        if (!string.IsNullOrWhiteSpace(dungeonIdForRecord))
        {
            var d = DungeonRegistry.Resolve(dungeonIdForRecord!);
            ctx = new EconomyContext
            {
                GoldMultiplier = d.GoldMultiplier,
                ExpMultiplier = d.ExpMultiplier,
                DropChanceMultiplier = d.DropChanceMultiplier,
                RunCompletedCount = runCompleted,
                RunRewardGold = d.RunRewardGold,
                RunRewardExp = d.RunRewardExp,
                RunRewardLootTableId = d.RunRewardLootTableId,
                RunRewardLootRolls = d.RunRewardLootRolls,
                Seed = finalSeed
            };
        }
        else
        {
            ctx = new EconomyContext
            {
                GoldMultiplier = 1.0,
                ExpMultiplier = 1.0,
                DropChanceMultiplier = 1.0,
                RunCompletedCount = runCompleted,
                Seed = finalSeed
            };
        }

        // 3) 计算奖励（默认 dropMode）
        string rewardType = _defaultDropMode;
        long gold; long exp; string lootJson;

        if (rewardType == "sampled")
        {
            var r = EconomyCalculator.ComputeSampledWithContext(killCounts, ctx);
            gold = r.Gold; exp = r.Exp;
            var items = r.Items.ToDictionary(kv => kv.Key, kv => (int)Math.Round(kv.Value));
            lootJson = JsonSerializer.Serialize(items);
        }
        else
        {
            var r = EconomyCalculator.ComputeExpectedWithContext(killCounts, ctx);
            gold = r.Gold; exp = r.Exp;
            lootJson = JsonSerializer.Serialize(r.Items);
            rewardType = "expected";
        }

        // 4) 记录并落库
        var record = new BattleRecord
        {
            Id = battleDomain.Id,
            CharacterId = characterId,
            StartedAt = DateTime.UtcNow,
            EndedAt = DateTime.UtcNow,
            TotalDamage = totalDamage,
            DurationSeconds = (killTime ?? simulateSeconds),
            AttackIntervalSeconds = battleDomain.AttackIntervalSeconds,
            SpecialIntervalSeconds = battleDomain.SpecialIntervalSeconds,
            Seed = finalSeed.ToString(),
            SeedIndexStart = seedIndexStart,
            SeedIndexEnd = seedIndexEnd,

            EnemyId = enemyDef.Id,
            EnemyName = enemyDef.Name,
            EnemyLevel = enemyDef.Level,
            EnemyMaxHp = enemyDef.MaxHp,
            EnemyArmor = enemyDef.Armor,
            EnemyMagicResist = enemyDef.MagicResist,
            Killed = killed,
            KillTimeSeconds = killTime,
            OverkillDamage = overkill,

            // 经济持久化
            RewardType = rewardType,
            Gold = gold,
            Exp = exp,
            LootJson = lootJson,
            DungeonId = dungeonIdForRecord,
            DungeonRuns = runCompleted,

            Segments = segments.Select(s => new BattleSegmentRecord
            {
                Id = Guid.NewGuid(),
                BattleId = battleDomain.Id,
                StartTime = s.StartTime,
                EndTime = s.EndTime,
                EventCount = s.EventCount,
                TotalDamage = s.TotalDamage,
                DamageBySourceJson = JsonSerializer.Serialize(s.DamageBySource),
                TagCountersJson = JsonSerializer.Serialize(s.TagCounters),
                ResourceFlowJson = JsonSerializer.Serialize(s.ResourceFlow),
                DamageByTypeJson = JsonSerializer.Serialize(s.DamageByType),
                RngIndexStart = s.RngIndexStart,
                RngIndexEnd = s.RngIndexEnd
            }).ToList()
        };

        await _battles.AddAsync(record, ct);
        return record.Id;
    }

    private static ulong DeriveSeed(Guid characterId)
    {
        var baseRng = RngContext.FromGuid(characterId);
        baseRng.Skip(4);
        ulong salt = (ulong)DateTime.UtcNow.Ticks;
        return RngContext.Hash64(baseRng.NextUInt64() ^ salt);
    }
}