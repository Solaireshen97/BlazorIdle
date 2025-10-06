using System.Text.Json;
using BlazorIdle.Server.Application.Abstractions;
using BlazorIdle.Server.Domain.Combat.Enemies;
using BlazorIdle.Server.Domain.Economy;
using BlazorIdle.Server.Domain.Records;
using Microsoft.EntityFrameworkCore;

namespace BlazorIdle.Server.Application.Battles.Step;

public sealed class StepBattleFinalizer
{
    private readonly IBattleRepository _repo;
    private readonly string _defaultDropMode; // "expected" | "sampled"

    public StepBattleFinalizer(IBattleRepository repo, IConfiguration cfg)
    {
        _repo = repo;
        _defaultDropMode = cfg.GetValue<string>("Economy:DefaultDropMode")?.Trim().ToLowerInvariant() == "sampled"
            ? "sampled" : "expected";
    }

    public async Task<Guid> FinalizeAsync(RunningBattle rb, CancellationToken ct = default)
    {
        // 幂等：内存标记
        if (rb.Persisted && rb.PersistedBattleId.HasValue)
            return rb.PersistedBattleId.Value;

        // 幂等：数据库已存在
        if (await _repo.ExistsAsync(rb.Battle.Id, ct))
        {
            rb.Persisted = true;
            rb.PersistedBattleId = rb.Battle.Id;
            return rb.Battle.Id;
        }

        var enemy = EnemyRegistry.Resolve(rb.EnemyId);

        var totalDamage = rb.Segments.Sum(s => s.TotalDamage);
        var simulated = rb.Clock.CurrentTime;
        var duration = rb.Completed
            ? Math.Min(rb.TargetDurationSeconds, rb.Battle.EndedAt ?? (rb.KillTime ?? simulated))
            : simulated;

        // 1) 聚合经济相关计数
        var killCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        int runCompleted = 0;
        string? dungeonId = rb.DungeonId;

        foreach (var s in rb.Segments)
        {
            if (s.TagCounters.TryGetValue("dungeon_run_complete", out var rc))
                runCompleted += rc;

            foreach (var (tag, val) in s.TagCounters)
            {
                if (tag.StartsWith("kill.", StringComparison.Ordinal))
                {
                    if (!killCounts.ContainsKey(tag)) killCounts[tag] = 0;
                    killCounts[tag] += val;
                }
                else if (dungeonId is null && tag.StartsWith("ctx.dungeonId.", StringComparison.Ordinal))
                {
                    dungeonId = tag.Substring("ctx.dungeonId.".Length);
                }
            }
        }

        // 2) 构建上下文（对象初始化器一次性赋值）
        EconomyContext ctx;
        if (!string.IsNullOrWhiteSpace(dungeonId))
        {
            var d = DungeonRegistry.Resolve(dungeonId!);
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
                Seed = rb.Seed
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
                Seed = rb.Seed
            };
        }

        // 3) 计算奖励（默认 dropMode 来自配置）
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
            lootJson = JsonSerializer.Serialize(r.Items); // itemId -> double
            rewardType = "expected";
        }

        // 4) 组装记录并落库（一次性包含奖励）
        var record = new BattleRecord
        {
            Id = rb.Battle.Id,
            CharacterId = rb.CharacterId,
            StartedAt = DateTime.UtcNow,
            EndedAt = DateTime.UtcNow,
            TotalDamage = totalDamage,
            DurationSeconds = duration,
            AttackIntervalSeconds = rb.Battle.AttackIntervalSeconds,
            SpecialIntervalSeconds = rb.Battle.SpecialIntervalSeconds,

            Seed = rb.Seed.ToString(),
            SeedIndexStart = rb.SeedIndexStart,
            SeedIndexEnd = rb.SeedIndexEnd,

            EnemyId = enemy.Id,
            EnemyName = enemy.Name,
            EnemyLevel = enemy.Level,
            EnemyMaxHp = enemy.MaxHp,
            EnemyArmor = enemy.Armor,
            EnemyMagicResist = enemy.MagicResist,

            Killed = rb.Killed,
            KillTimeSeconds = rb.KillTime,
            OverkillDamage = rb.Overkill,

            RewardType = rewardType,
            Gold = gold,
            Exp = exp,
            LootJson = lootJson,
            DungeonId = dungeonId,
            DungeonRuns = runCompleted,

            Segments = rb.Segments.Select(s => new BattleSegmentRecord
            {
                Id = Guid.NewGuid(),
                BattleId = rb.Battle.Id,
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

        try
        {
            await _repo.AddAsync(record, ct);
            rb.Persisted = true;
            rb.PersistedBattleId = record.Id;
            return record.Id;
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            rb.Persisted = true;
            rb.PersistedBattleId = rb.Battle.Id;
            return rb.Battle.Id;
        }
    }

    private static bool IsUniqueViolation(DbUpdateException ex)
        => ex.InnerException?.Message.Contains("UNIQUE constraint failed", StringComparison.OrdinalIgnoreCase) == true;
}