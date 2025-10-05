using System.Text.Json;
using BlazorIdle.Server.Application.Abstractions;
using BlazorIdle.Server.Domain.Combat.Enemies;
using BlazorIdle.Server.Domain.Records;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BlazorIdle.Server.Application.Battles.Step;

public sealed class StepBattleFinalizer
{
    private readonly IServiceScopeFactory _scopeFactory;

    public StepBattleFinalizer(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public async Task<Guid> FinalizeAsync(RunningBattle rb, CancellationToken ct = default)
    {
        // 若内存标记已持久化，直接返回
        if (rb.Persisted && rb.PersistedBattleId.HasValue)
            return rb.PersistedBattleId.Value;

        using var scope = _scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IBattleRepository>();

        // 幂等保护 1：DB 是否已存在（可能是此前自动完成或并发）
        if (await repo.ExistsAsync(rb.Battle.Id, ct))
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
            Id = rb.Battle.Id, // 使用运行时 BattleId，保证同一场仗只有一条记录
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
                // 若你还未迁移段级 RNG 列，则去掉下面两行
                RngIndexStart = s.RngIndexStart,
                RngIndexEnd = s.RngIndexEnd
            }).ToList()
        };

        try
        {
            await repo.AddAsync(record, ct);
            rb.Persisted = true;
            rb.PersistedBattleId = record.Id;
            return record.Id;
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            // 幂等保护 2：并发重复插入 -> 当作已存在处理
            rb.Persisted = true;
            rb.PersistedBattleId = rb.Battle.Id;
            return rb.Battle.Id;
        }
    }

    private static bool IsUniqueViolation(DbUpdateException ex)
        => ex.InnerException?.Message.Contains("UNIQUE constraint failed", StringComparison.OrdinalIgnoreCase) == true;
}