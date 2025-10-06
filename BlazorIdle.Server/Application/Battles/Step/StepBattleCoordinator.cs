using BlazorIdle.Server.Domain.Characters;
using BlazorIdle.Server.Domain.Combat.Enemies;
using BlazorIdle.Server.Domain.Combat.Professions;
using BlazorIdle.Server.Domain.Economy;
using BlazorIdle.Shared.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using static BlazorIdle.Server.Domain.Economy.EconomyCalculator;

namespace BlazorIdle.Server.Application.Battles.Step;

public sealed class StepBattleCoordinator
{
    private readonly ConcurrentDictionary<Guid, RunningBattle> _running = new();
    private readonly ConcurrentDictionary<Guid, DateTime> _completedAtUtc = new();

    private readonly IServiceScopeFactory _scopeFactory; // 改为作用域工厂

    public StepBattleCoordinator(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    // 新增覆盖：continuousRespawnDelaySeconds / dungeonWaveDelaySeconds / dungeonRunDelaySeconds
    public Guid Start(Guid characterId, Profession profession, CharacterStats stats, double seconds, ulong seed, string? enemyId, int enemyCount,
        StepBattleMode mode = StepBattleMode.Duration, string? dungeonId = null,
        double? continuousRespawnDelaySeconds = null, double? dungeonWaveDelaySeconds = null, double? dungeonRunDelaySeconds = null)
    {
        var eid = EnemyRegistry.Resolve(enemyId).Id;
        var enemy = EnemyRegistry.Resolve(eid);
        var id = Guid.NewGuid();

        var rb = new RunningBattle(
            id: id,
            characterId: characterId,
            profession: profession,
            seed: seed,
            targetSeconds: seconds,
            enemyDef: enemy,
            enemyCount: enemyCount,
            stats: stats,
            mode: mode,
            dungeonId: dungeonId,
            continuousRespawnDelaySeconds: continuousRespawnDelaySeconds,
            dungeonWaveDelaySeconds: dungeonWaveDelaySeconds,
            dungeonRunDelaySeconds: dungeonRunDelaySeconds
        );

        if (!_running.TryAdd(id, rb))
            throw new InvalidOperationException("Failed to register running battle.");

        return id;
    }

    internal IEnumerable<Guid> InternalIdsSnapshot() => _running.Keys.ToArray();

    public (bool found, StepBattleStatusDto status) GetStatus(Guid id, string? dropMode = null)
    {
        if (!_running.TryGetValue(id, out var rb))
            return (false, default!);

        var totalDamage = rb.Segments.Sum(s => s.TotalDamage);
        var simulated = rb.Clock.CurrentTime;
        var effectiveDuration = rb.Completed
            ? Math.Min(rb.TargetDurationSeconds, rb.Battle.EndedAt ?? (rb.KillTime ?? simulated))
            : simulated;

        var dps = totalDamage / Math.Max(0.0001, effectiveDuration);

        // 聚合 kill.* 与 dungeon_run_complete
        var killCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        int runCompleted = 0;
        foreach (var seg in rb.Segments)
        {
            if (seg.TagCounters.TryGetValue("dungeon_run_complete", out var rc))
                runCompleted += rc;

            foreach (var (tag, val) in seg.TagCounters)
            {
                if (!tag.StartsWith("kill.", StringComparison.Ordinal)) continue;
                if (!killCounts.ContainsKey(tag)) killCounts[tag] = 0;
                killCounts[tag] += val;
            }
        }

        // 构建经济上下文（若是地城模式，读取 dungeon 配置；否则倍率=1）
        var ctx = new EconomyContext { Seed = rb.Seed };
        if (!string.IsNullOrWhiteSpace(rb.DungeonId))
        {
            var d = DungeonRegistry.Resolve(rb.DungeonId!);
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

        var mode = (dropMode ?? "expected").Trim().ToLowerInvariant();
        long gold; long exp; Dictionary<string, double>? lootExp = null; Dictionary<string, int>? lootSampled = null;
        if (mode == "sampled")
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
            mode = "expected";
        }

        return (true, new StepBattleStatusDto
        {
            Id = rb.Id,
            CharacterId = rb.CharacterId,
            Profession = rb.Profession,
            EnemyId = rb.EnemyId,
            EnemyCount = rb.EnemyCount,
            SimulatedSeconds = rb.Clock.CurrentTime,
            TargetSeconds = rb.TargetDurationSeconds,
            Completed = rb.Completed,
            TotalDamage = totalDamage,
            Dps = Math.Round(dps, 2),
            SegmentCount = rb.Segments.Count,
            Seed = rb.Seed.ToString(),
            SeedIndexStart = rb.SeedIndexStart,
            SeedIndexEnd = rb.SeedIndexEnd,
            Killed = rb.Killed,
            KillTimeSeconds = rb.KillTime,
            OverkillDamage = rb.Overkill,
            PersistedBattleId = rb.PersistedBattleId,
            Mode = rb.Mode.ToString().ToLowerInvariant(),
            WaveIndex = rb.WaveIndex,
            RunCount = rb.RunCount,
            DungeonId = rb.DungeonId,

            // 奖励
            DropMode = mode,
            Gold = gold,
            Exp = exp,
            LootExpected = lootExp ?? new(),
            LootSampled = lootSampled ?? new()
        });
    }

    public (bool found, List<StepBattleSegmentDto> segments) GetSegments(Guid id, int sinceIndex)
    {
        if (!_running.TryGetValue(id, out var rb))
            return (false, new());

        var list = new List<StepBattleSegmentDto>();
        for (int i = sinceIndex; i < rb.Segments.Count; i++)
        {
            var s = rb.Segments[i];
            list.Add(new StepBattleSegmentDto
            {
                Index = i,
                StartTime = s.StartTime,
                EndTime = s.EndTime,
                EventCount = s.EventCount,
                TotalDamage = s.TotalDamage,
                DamageBySource = s.DamageBySource,
                DamageByType = s.DamageByType,
                ResourceFlow = s.ResourceFlow
            });
        }
        return (true, list);
    }

    public void AdvanceAll(int maxEventsPerBattle = 500, double maxSliceSeconds = 0.25, CancellationToken ct = default)
    {
        foreach (var kv in _running.ToArray())
        {
            if (ct.IsCancellationRequested) break;

            var rb = kv.Value;
            if (!rb.Completed)
            {
                rb.Advance(maxEvents: maxEventsPerBattle, maxSimSecondsSlice: maxSliceSeconds);

                if (rb.Completed)
                {
                    _completedAtUtc.TryAdd(rb.Id, DateTime.UtcNow);
                }
            }

            if (rb.Completed && !rb.Persisted)
            {
                try
                {
                    // 在需要用到仓储/Finalizer 时，临时创建一个作用域，解析 Scoped 的 Finalizer
                    using var scope = _scopeFactory.CreateScope();
                    var finalizer = scope.ServiceProvider.GetRequiredService<StepBattleFinalizer>();
                    var persistedId = finalizer.FinalizeAsync(rb, ct).GetAwaiter().GetResult();

                    rb.Persisted = true;
                    rb.PersistedBattleId = persistedId;
                }
                catch
                {
                    // TODO: log
                }
            }
        }
    }

    public bool TryGet(Guid id, out RunningBattle? rb) => _running.TryGetValue(id, out rb);

    public int PruneCompleted(TimeSpan ttl)
    {
        var now = DateTime.UtcNow;
        int removed = 0;
        foreach (var kv in _completedAtUtc.ToArray())
        {
            if ((now - kv.Value) > ttl)
            {
                if (_running.TryRemove(kv.Key, out _))
                {
                    _completedAtUtc.TryRemove(kv.Key, out _);
                    removed++;
                }
            }
        }
        return removed;
    }

    public async Task<(bool ok, Guid persistedId)> StopAndFinalizeAsync(Guid id, CancellationToken ct = default)
    {
        if (!_running.TryGetValue(id, out var rb))
            return (false, Guid.Empty);

        lock (rb)
        {
            if (!rb.Completed)
                rb.ForceStopAndSeal();
        }

        if (!rb.Persisted)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var finalizer = scope.ServiceProvider.GetRequiredService<StepBattleFinalizer>();
                var persistedId = await finalizer.FinalizeAsync(rb, ct);

                rb.Persisted = true;
                rb.PersistedBattleId = persistedId;
            }
            catch
            {
                return (false, Guid.Empty);
            }
        }

        _completedAtUtc.TryAdd(rb.Id, DateTime.UtcNow);
        return (true, rb.PersistedBattleId!.Value);
    }
}

public sealed class StepBattleStatusDto
{
    public Guid Id { get; set; }
    public Guid CharacterId { get; set; }
    public Profession Profession { get; set; }
    public string EnemyId { get; set; } = "dummy";
    public int EnemyCount { get; set; }
    public double SimulatedSeconds { get; set; }
    public double TargetSeconds { get; set; }
    public bool Completed { get; set; }
    public int TotalDamage { get; set; }
    public double Dps { get; set; }
    public int SegmentCount { get; set; }
    public string Seed { get; set; } = "0";
    public long SeedIndexStart { get; set; }
    public long SeedIndexEnd { get; set; }
    public bool Killed { get; set; }
    public double? KillTimeSeconds { get; set; }
    public int OverkillDamage { get; set; }
    public Guid? PersistedBattleId { get; set; }

    // 附加：持续/地城展示（前端可忽略这些字段）
    public string? Mode { get; set; }            // "duration"|"continuous"|"dungeonsingle"|"dungeonloop"
    public int? WaveIndex { get; set; }
    public int? RunCount { get; set; }
    public string? DungeonId { get; set; }

    // 新增：期望值奖励
    public string? DropMode { get; set; } // "expected" | "sampled"
    public long Gold { get; set; }
    public long Exp { get; set; }
    public Dictionary<string, double> LootExpected { get; set; } = new();
    public Dictionary<string, int> LootSampled { get; set; } = new();
}

public sealed class StepBattleSegmentDto
{
    public int Index { get; set; }
    public double StartTime { get; set; }
    public double EndTime { get; set; }
    public int EventCount { get; set; }
    public int TotalDamage { get; set; }
    public Dictionary<string, int> DamageBySource { get; set; } = new();
    public Dictionary<string, int> DamageByType { get; set; } = new();
    public Dictionary<string, int> ResourceFlow { get; set; } = new();
}