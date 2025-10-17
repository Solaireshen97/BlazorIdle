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

        // 使用重试策略保存，防止数据库锁定导致失败
        await Infrastructure.Persistence.DatabaseRetryPolicy.SaveChangesWithRetryAsync(db, ct, logger);
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
        var logger = scope.ServiceProvider.GetService<Microsoft.Extensions.Logging.ILogger<StepBattleSnapshotService>>();

        var rows = await db.Set<RunningBattleSnapshotRecord>()
            .OrderBy(x => x.UpdatedAtUtc)
            .ToListAsync(ct);

        logger?.LogInformation("开始恢复战斗快照，共 {Count} 个快照", rows.Count);

        int recoveredCount = 0;
        int failedCount = 0;
        var failedSnapshotIds = new List<Guid>();

        foreach (var row in rows)
        {
            try
            {
                // 反序列化快照数据
                var dto = JsonSerializer.Deserialize<StepBattleSnapshotDto>(row.SnapshotJson);
                if (dto is null)
                {
                    logger?.LogWarning("快照 {SnapshotId} 数据为空，将被删除", row.Id);
                    failedSnapshotIds.Add(row.StepBattleId);
                    failedCount++;
                    continue;
                }

                // 验证角色是否存在
                var ch = await characters.GetAsync(dto.CharacterId, ct);
                if (ch is null)
                {
                    logger?.LogWarning(
                        "快照 {SnapshotId} 引用的角色 {CharacterId} 不存在，将被删除",
                        row.Id, dto.CharacterId);
                    failedSnapshotIds.Add(row.StepBattleId);
                    failedCount++;
                    continue;
                }

                var profession = (Shared.Models.Profession)dto.Profession;
                var attrs = new PrimaryAttributes(ch.Strength, ch.Agility, ch.Intellect, ch.Stamina);
                
                // 使用装备集成服务构建完整属性
                var equipmentStats = scope.ServiceProvider.GetRequiredService<EquipmentStatsIntegration>();
                CharacterStats stats;
                
                try
                {
                    stats = await equipmentStats.BuildStatsWithEquipmentAsync(dto.CharacterId, profession, attrs);
                }
                catch (Exception ex)
                {
                    logger?.LogWarning(ex,
                        "快照 {SnapshotId} 构建角色属性失败（可能装备缺失），将被删除",
                        row.Id);
                    failedSnapshotIds.Add(row.StepBattleId);
                    failedCount++;
                    continue;
                }

                // 通过 Coordinator.Start 重建一个全新的 RunningBattle
                Guid newId;
                try
                {
                    newId = coord.Start(dto.CharacterId, profession, stats, dto.TargetSeconds, dto.Seed, dto.EnemyId, dto.EnemyCount);
                }
                catch (Exception ex)
                {
                    logger?.LogWarning(ex,
                        "快照 {SnapshotId} 启动战斗失败（可能敌人配置错误），将被删除",
                        row.Id);
                    failedSnapshotIds.Add(row.StepBattleId);
                    failedCount++;
                    continue;
                }

                // 取回实例并快速"追帧到"快照时刻，然后替换 Segments
                if (coord.TryGet(newId, out var rb) && rb is not null)
                {
                    try
                    {
                        rb.FastForwardTo(dto.SimulatedSeconds); // 快速推进到相同模拟时间
                        rb.Segments.Clear();
                        rb.Segments.AddRange(dto.Segments);
                        
                        recoveredCount++;
                        logger?.LogInformation(
                            "成功恢复快照 {SnapshotId}，战斗ID: {BattleId}，角色: {CharacterId}",
                            row.Id, newId, dto.CharacterId);
                    }
                    catch (Exception ex)
                    {
                        logger?.LogWarning(ex,
                            "快照 {SnapshotId} 快进到模拟时间失败，将被删除",
                            row.Id);
                        failedSnapshotIds.Add(row.StepBattleId);
                        failedCount++;
                    }
                }
                else
                {
                    logger?.LogWarning(
                        "快照 {SnapshotId} 无法获取运行中的战斗实例，将被删除",
                        row.Id);
                    failedSnapshotIds.Add(row.StepBattleId);
                    failedCount++;
                }
            }
            catch (Exception ex)
            {
                // 捕获所有未处理的异常
                logger?.LogError(ex,
                    "恢复快照 {SnapshotId} 时发生意外错误，将被删除",
                    row.Id);
                failedSnapshotIds.Add(row.StepBattleId);
                failedCount++;
            }
        }

        // 删除所有失败的快照
        if (failedSnapshotIds.Count > 0)
        {
            logger?.LogWarning("清理 {Count} 个损坏的快照", failedSnapshotIds.Count);
            
            foreach (var failedId in failedSnapshotIds)
            {
                try
                {
                    await DeleteAsync(failedId, ct);
                }
                catch (Exception ex)
                {
                    logger?.LogError(ex, "删除损坏快照 {SnapshotId} 失败", failedId);
                }
            }
        }

        logger?.LogInformation(
            "战斗快照恢复完成: 成功 {RecoveredCount} 个，失败 {FailedCount} 个",
            recoveredCount, failedCount);
    }
}
