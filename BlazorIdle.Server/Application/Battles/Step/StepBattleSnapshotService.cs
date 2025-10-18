using System.Text.Json;
using BlazorIdle.Server.Application.Abstractions;
using BlazorIdle.Server.Domain.Characters;
using BlazorIdle.Server.Domain.Equipment.Services;
using BlazorIdle.Server.Domain.Records;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BlazorIdle.Server.Application.Battles.Step;

// 快照 JSON 结构（最小）
internal sealed record StepBattleSnapshotDto(
    Guid StepBattleId,
    Guid CharacterId,
    int Profession,
    string EnemyId,
    int EnemyCount,
    ulong Seed,
    double TargetSeconds,
    double SimulatedSeconds,
    List<BlazorIdle.Server.Domain.Combat.CombatSegment> Segments
);

public sealed class StepBattleSnapshotService
{
    private readonly IServiceScopeFactory _scopeFactory;

    public StepBattleSnapshotService(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    // Upsert：每场仅一行
    public async Task SaveAsync(RunningBattle rb, CancellationToken ct = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Infrastructure.Persistence.GameDbContext>();
        var logger = scope.ServiceProvider.GetService<Microsoft.Extensions.Logging.ILogger<StepBattleSnapshotService>>();
        var configuration = scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>();

        var dto = new StepBattleSnapshotDto(
            rb.Id,
            rb.CharacterId,
            (int)rb.Profession,
            rb.EnemyId,
            rb.EnemyCount,
            rb.Seed,
            rb.TargetDurationSeconds,
            rb.Clock.CurrentTime,
            rb.Segments.ToList() // 保存已生成的段（便于重启后直接展示）
        );

        var json = JsonSerializer.Serialize(dto);

        var row = await db.Set<RunningBattleSnapshotRecord>()
            .FirstOrDefaultAsync(x => x.StepBattleId == rb.Id, ct);

        if (row is null)
        {
            row = new RunningBattleSnapshotRecord
            {
                Id = Guid.NewGuid(),
                StepBattleId = rb.Id,
                CharacterId = rb.CharacterId,
                Profession = (int)rb.Profession,
                EnemyId = rb.EnemyId,
                EnemyCount = rb.EnemyCount,
                Seed = rb.Seed.ToString(),
                TargetSeconds = rb.TargetDurationSeconds,
                SimulatedSeconds = rb.Clock.CurrentTime,
                UpdatedAtUtc = DateTime.UtcNow,
                SnapshotJson = json
            };
            db.Set<RunningBattleSnapshotRecord>().Add(row);
        }
        else
        {
            row.CharacterId = rb.CharacterId;
            row.Profession = (int)rb.Profession;
            row.EnemyId = rb.EnemyId;
            row.EnemyCount = rb.EnemyCount;
            row.Seed = rb.Seed.ToString();
            row.TargetSeconds = rb.TargetDurationSeconds;
            row.SimulatedSeconds = rb.Clock.CurrentTime;
            row.UpdatedAtUtc = DateTime.UtcNow;
            row.SnapshotJson = json;
        }

        // 检查是否启用内存缓冲
        var enableMemoryBuffering = configuration.GetValue<bool>("Persistence:EnableMemoryBuffering", false);
        if (enableMemoryBuffering)
        {
            // 使用内存缓冲：只更新内存中的实体，标记为dirty
            // PersistenceCoordinator 会根据配置的SaveIntervalMs定期批量保存
            var snapshotManager = scope.ServiceProvider
                .GetService<Infrastructure.DatabaseOptimization.Abstractions.IMemoryStateManager<RunningBattleSnapshotRecord>>();
                
            if (snapshotManager != null)
            {
                // 如果是新记录，先添加到内存；否则更新
                if (row.Id == Guid.Empty || !await db.Set<RunningBattleSnapshotRecord>().AnyAsync(x => x.Id == row.Id, ct))
                {
                    // 确保ID已设置
                    if (row.Id == Guid.Empty)
                        row.Id = Guid.NewGuid();
                    snapshotManager.Add(row);
                }
                else
                {
                    snapshotManager.Update(row);
                }
                // 不再调用 SaveChangesAsync，让后台服务批量保存
            }
            else
            {
                // Fallback: 如果无法获取MemoryStateManager，直接保存
                await Infrastructure.Persistence.DatabaseRetryPolicy.SaveChangesWithRetryAsync(db, ct, logger);
            }
        }
        else
        {
            // 未启用内存缓冲：保持原有的立即保存行为
            await Infrastructure.Persistence.DatabaseRetryPolicy.SaveChangesWithRetryAsync(db, ct, logger);
        }
    }

    public async Task DeleteAsync(Guid stepBattleId, CancellationToken ct = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Infrastructure.Persistence.GameDbContext>();
        var logger = scope.ServiceProvider.GetService<Microsoft.Extensions.Logging.ILogger<StepBattleSnapshotService>>();
        
        var row = await db.Set<RunningBattleSnapshotRecord>().FirstOrDefaultAsync(x => x.StepBattleId == stepBattleId, ct);
        if (row is not null)
        {
            db.Remove(row);
            // 使用重试策略删除，防止数据库锁定导致失败
            await Infrastructure.Persistence.DatabaseRetryPolicy.SaveChangesWithRetryAsync(db, ct, logger);
        }
    }

    // 启动时恢复所有快照
    public async Task RecoverAllAsync(StepBattleCoordinator coord, CancellationToken ct = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Infrastructure.Persistence.GameDbContext>();
        var characters = scope.ServiceProvider.GetRequiredService<ICharacterRepository>();

        var rows = await db.Set<RunningBattleSnapshotRecord>()
            .OrderBy(x => x.UpdatedAtUtc)
            .ToListAsync(ct);

        foreach (var row in rows)
        {
            try
            {
                var dto = JsonSerializer.Deserialize<StepBattleSnapshotDto>(row.SnapshotJson);
                if (dto is null) continue;

                // 重建 Stats（与 Start 时一致，包含装备加成）
                var ch = await characters.GetAsync(dto.CharacterId, ct);
                if (ch is null) continue;

                var profession = (Shared.Models.Profession)dto.Profession;
                var attrs = new PrimaryAttributes(ch.Strength, ch.Agility, ch.Intellect, ch.Stamina);
                
                // 使用装备集成服务构建完整属性
                var equipmentStats = scope.ServiceProvider.GetRequiredService<EquipmentStatsIntegration>();
                var stats = await equipmentStats.BuildStatsWithEquipmentAsync(dto.CharacterId, profession, attrs);

                // 通过 Coordinator.Start 重建一个全新的 RunningBattle
                var newId = coord.Start(dto.CharacterId, profession, stats, dto.TargetSeconds, dto.Seed, dto.EnemyId, dto.EnemyCount);

                // 取回实例并快速“追帧到”快照时刻，然后替换 Segments
                if (coord.TryGet(newId, out var rb) && rb is not null)
                {
                    rb.FastForwardTo(dto.SimulatedSeconds); // 快速推进到相同模拟时间
                    rb.Segments.Clear();
                    rb.Segments.AddRange(dto.Segments);
                }
            }
            catch
            {
                // 忽略个别损坏快照
            }
        }
    }
}