using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BlazorIdle.Server.Domain.Characters;
using BlazorIdle.Server.Domain.Combat.Enemies;
using BlazorIdle.Server.Domain.Combat.Professions;
using BlazorIdle.Shared.Models;

namespace BlazorIdle.Server.Application.Battles.Step;

public sealed class StepBattleCoordinator
{
    private readonly ConcurrentDictionary<Guid, RunningBattle> _running = new();
    private readonly ConcurrentDictionary<Guid, DateTime> _completedAtUtc = new();

    private readonly StepBattleFinalizer _finalizer;

    public StepBattleCoordinator(StepBattleFinalizer finalizer)
    {
        _finalizer = finalizer;
    }

    public Guid Start(Guid characterId, Profession profession, CharacterStats stats, double seconds, ulong seed, string? enemyId, int enemyCount)
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
            stats: stats
        );

        if (!_running.TryAdd(id, rb))
            throw new InvalidOperationException("Failed to register running battle.");

        return id;
    }

    public (bool found, StepBattleStatusDto status) GetStatus(Guid id)
    {
        if (!_running.TryGetValue(id, out var rb))
            return (false, default!);

        var totalDamage = rb.Segments.Sum(s => s.TotalDamage);

        var simulated = rb.Clock.CurrentTime;
        var effectiveDuration = rb.Completed
            ? Math.Min(rb.TargetDurationSeconds, rb.Battle.EndedAt ?? (rb.KillTime ?? simulated))
            : simulated;

        var dps = totalDamage / Math.Max(0.0001, effectiveDuration);

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
            PersistedBattleId = rb.PersistedBattleId
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
                    var persistedId = _finalizer.FinalizeAsync(rb, ct).GetAwaiter().GetResult();
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

    // 手动终止并结算（并发安全）
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
                var persistedId = await _finalizer.FinalizeAsync(rb, ct);
                rb.Persisted = true;
                rb.PersistedBattleId = persistedId;
            }
            catch
            {
                // TODO: log
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