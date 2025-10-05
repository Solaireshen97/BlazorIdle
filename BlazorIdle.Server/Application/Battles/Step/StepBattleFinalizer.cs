using System.Text.Json;
using BlazorIdle.Server.Application.Abstractions;
using BlazorIdle.Server.Domain.Combat.Enemies;
using BlazorIdle.Server.Domain.Records;
using Microsoft.Extensions.DependencyInjection;

namespace BlazorIdle.Server.Application.Battles.Step;

/// <summary>
/// 将 RunningBattle 的聚合结果落库为 BattleRecord + BattleSegmentRecord。
/// 使用 IServiceScopeFactory 以便在单例环境中安全获取 Scoped 仓储。
/// </summary>
public sealed class StepBattleFinalizer
{
    private readonly IServiceScopeFactory _scopeFactory;

    public StepBattleFinalizer(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public async Task<Guid> FinalizeAsync(RunningBattle rb, CancellationToken ct = default)
    {
        // 已持久化则直接返回
        if (rb.Persisted && rb.PersistedBattleId.HasValue)
            return rb.PersistedBattleId.Value;

        using var scope = _scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IBattleRepository>();

        var enemy = EnemyRegistry.Resolve(rb.EnemyId);

        var totalDamage = rb.Segments.Sum(s => s.TotalDamage);
        // 已完成用 KillTime 或 EndedAt 或 Target；未完成用当前模拟时长（防御）
        var duration = rb.Completed
            ? Math.Min(rb.TargetDurationSeconds, rb.Battle.EndedAt ?? (rb.KillTime ?? rb.Clock.CurrentTime))
            : rb.Clock.CurrentTime;

        var record = new BattleRecord
        {
            // 沿用运行时 Battle 的 Id，便于日志追踪
            Id = rb.Battle.Id,
            CharacterId = rb.CharacterId,
            StartedAt = DateTime.UtcNow,   // 若需精确，可改为 Coordinator 记录的开始时间
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
                // 若表结构包含段级 RNG，可赋值（你若尚未迁移到该列，可去掉下面两行）
                RngIndexStart = s.RngIndexStart,
                RngIndexEnd = s.RngIndexEnd
            }).ToList()
        };

        await repo.AddAsync(record, ct);

        rb.Persisted = true;
        rb.PersistedBattleId = record.Id;
        return record.Id;
    }
}