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

        if (rows.Count == 0)
        {
            logger?.LogInformation("没有需要恢复的战斗快照");
            return;
        }

        logger?.LogInformation("开始恢复 {Count} 个战斗快照", rows.Count);
        
        var recoveredCount = 0;
        var failedCount = 0;
        var deletedCount = 0;

        foreach (var row in rows)
        {
            try
            {
                var dto = JsonSerializer.Deserialize<StepBattleSnapshotDto>(row.SnapshotJson);
                if (dto is null)
                {
                    logger?.LogWarning("快照 {SnapshotId} 反序列化失败，将删除", row.Id);
                    db.Set<RunningBattleSnapshotRecord>().Remove(row);
                    deletedCount++;
                    continue;
                }

                // 重建 Stats（与 Start 时一致，包含装备加成）
                var ch = await characters.GetAsync(dto.CharacterId, ct);
                if (ch is null)
                {
                    logger?.LogWarning("快照 {SnapshotId} 的角色 {CharacterId} 不存在，将删除快照", row.Id, dto.CharacterId);
                    db.Set<RunningBattleSnapshotRecord>().Remove(row);
                    deletedCount++;
                    continue;
                }

                var profession = (Shared.Models.Profession)dto.Profession;
                var attrs = new PrimaryAttributes(ch.Strength, ch.Agility, ch.Intellect, ch.Stamina);
                
                // 使用装备集成服务构建完整属性
                var equipmentStats = scope.ServiceProvider.GetRequiredService<EquipmentStatsIntegration>();
                var stats = await equipmentStats.BuildStatsWithEquipmentAsync(dto.CharacterId, profession, attrs);

                // 通过 Coordinator.Start 重建一个全新的 RunningBattle
                var newId = coord.Start(dto.CharacterId, profession, stats, dto.TargetSeconds, dto.Seed, dto.EnemyId, dto.EnemyCount);

                // 取回实例并快速"追帧到"快照时刻，然后替换 Segments
                if (coord.TryGet(newId, out var rb) && rb is not null)
                {
                    rb.FastForwardTo(dto.SimulatedSeconds); // 快速推进到相同模拟时间
                    rb.Segments.Clear();
                    rb.Segments.AddRange(dto.Segments);
                    
                    logger?.LogInformation(
                        "成功恢复战斗快照: 旧ID={OldBattleId}, 新ID={NewBattleId}, 角色={CharacterId}, 进度={Progress:F1}秒",
                        dto.StepBattleId,
                        newId,
                        dto.CharacterId,
                        dto.SimulatedSeconds);
                    
                    recoveredCount++;
                    
                    // 关键修复：删除旧快照，避免重复恢复
                    db.Set<RunningBattleSnapshotRecord>().Remove(row);
                    deletedCount++;
                }
                else
                {
                    logger?.LogWarning("恢复战斗快照失败: 无法获取新建的战斗实例 {NewBattleId}", newId);
                    failedCount++;
                }
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "恢复快照 {SnapshotId} 时发生错误", row.Id);
                failedCount++;
                
                // 出错的快照也应该被删除，避免下次启动时再次尝试恢复
                try
                {
                    db.Set<RunningBattleSnapshotRecord>().Remove(row);
                    deletedCount++;
                }
                catch
                {
                    // 忽略删除失败
                }
            }
        }

        // 保存所有更改（删除旧快照）
        try
        {
            await Infrastructure.Persistence.DatabaseRetryPolicy.SaveChangesWithRetryAsync(db, ct, logger);
            
            logger?.LogInformation(
                "战斗快照恢复完成: 成功 {RecoveredCount} 个，失败 {FailedCount} 个，删除旧快照 {DeletedCount} 个",
                recoveredCount,
                failedCount,
                deletedCount);
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "保存快照清理更改时发生错误");
        }
    }
}