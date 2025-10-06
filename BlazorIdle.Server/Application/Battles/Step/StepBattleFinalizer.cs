using System.Text.Json;
using BlazorIdle.Server.Application.Abstractions;
using BlazorIdle.Server.Domain.Combat.Enemies;
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
        // 若内存标记已持久化，直接返回
        if (rb.Persisted && rb.PersistedBattleId.HasValue)
            return rb.PersistedBattleId.Value;

        // 幂等保护 1：DB 是否已存在（可能是此前自动完成或并发）
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

            // 奖励字段先不在 Finalizer 里计算，保持最小改动（同步路径已持久化；Step 的 summary 会动态算）
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