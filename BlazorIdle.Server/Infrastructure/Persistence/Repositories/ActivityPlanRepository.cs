using BlazorIdle.Server.Application.Abstractions;
using BlazorIdle.Server.Domain.Activities;
using BlazorIdle.Server.Infrastructure.DatabaseOptimization.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace BlazorIdle.Server.Infrastructure.Persistence.Repositories;

/// <summary>
/// 活动计划仓储实现
/// Activity plan repository implementation
/// </summary>
public class ActivityPlanRepository : IActivityPlanRepository
{
    private readonly GameDbContext _db;
    private readonly IConfiguration _configuration;
    private readonly IMemoryStateManager<ActivityPlan>? _memoryStateManager;

    public ActivityPlanRepository(
        GameDbContext db,
        IConfiguration configuration,
        IMemoryStateManager<ActivityPlan>? memoryStateManager = null)
    {
        _db = db;
        _configuration = configuration;
        _memoryStateManager = memoryStateManager;
    }

    public Task<ActivityPlan?> GetAsync(Guid id, CancellationToken ct = default) =>
        _db.ActivityPlans.FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task<List<ActivityPlan>> GetByCharacterAsync(Guid characterId, CancellationToken ct = default) =>
        await _db.ActivityPlans
            .Where(p => p.CharacterId == characterId)
            .OrderBy(p => p.SlotIndex)
            .ThenBy(p => p.CreatedAt)
            .ToListAsync(ct);

    public async Task<List<ActivityPlan>> GetByCharacterAndSlotAsync(Guid characterId, int slotIndex, CancellationToken ct = default) =>
        await _db.ActivityPlans
            .Where(p => p.CharacterId == characterId && p.SlotIndex == slotIndex)
            .OrderBy(p => p.CreatedAt)
            .ToListAsync(ct);

    public async Task AddAsync(ActivityPlan plan, CancellationToken ct = default)
    {
        _db.ActivityPlans.Add(plan);
        
        // 检查是否启用内存缓冲
        var enableMemoryBuffering = _configuration.GetValue<bool>("Persistence:EnableMemoryBuffering", false);
        if (enableMemoryBuffering && _memoryStateManager != null)
        {
            // 使用内存缓冲：将实体添加到内存，标记为dirty
            _memoryStateManager.Add(plan);
            // 不立即调用 SaveChangesAsync，由 PersistenceCoordinator 批量保存
        }
        else
        {
            // 未启用内存缓冲：保持原有的立即保存行为
            await Persistence.DatabaseRetryPolicy.SaveChangesWithRetryAsync(_db, ct);
        }
    }

    public async Task UpdateAsync(ActivityPlan plan, CancellationToken ct = default)
    {
        _db.ActivityPlans.Update(plan);
        
        // 检查是否启用内存缓冲
        var enableMemoryBuffering = _configuration.GetValue<bool>("Persistence:EnableMemoryBuffering", false);
        if (enableMemoryBuffering && _memoryStateManager != null)
        {
            // 使用内存缓冲：更新内存中的实体，标记为dirty
            _memoryStateManager.Update(plan);
            // 不立即调用 SaveChangesAsync，由 PersistenceCoordinator 批量保存
        }
        else
        {
            // 未启用内存缓冲：保持原有的立即保存行为
            await Persistence.DatabaseRetryPolicy.SaveChangesWithRetryAsync(_db, ct);
        }
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var plan = await GetAsync(id, ct);
        if (plan is not null)
        {
            _db.ActivityPlans.Remove(plan);
            
            // 检查是否启用内存缓冲
            var enableMemoryBuffering = _configuration.GetValue<bool>("Persistence:EnableMemoryBuffering", false);
            if (enableMemoryBuffering && _memoryStateManager != null)
            {
                // 使用内存缓冲：从内存中移除
                _memoryStateManager.Remove(id);
                // 标记 EF Core 实体为删除状态，但不立即保存
                // 由 PersistenceCoordinator 批量保存删除操作
            }
            else
            {
                // 未启用内存缓冲：保持原有的立即保存行为
                await Persistence.DatabaseRetryPolicy.SaveChangesWithRetryAsync(_db, ct);
            }
        }
    }

    public Task<ActivityPlan?> GetRunningPlanAsync(Guid characterId, CancellationToken ct = default) =>
        _db.ActivityPlans
            .Where(p => p.CharacterId == characterId && p.State == ActivityState.Running)
            .FirstOrDefaultAsync(ct);

    public Task<ActivityPlan?> GetNextPendingPlanAsync(Guid characterId, CancellationToken ct = default) =>
        _db.ActivityPlans
            .Where(p => p.CharacterId == characterId && p.State == ActivityState.Pending)
            .OrderBy(p => p.SlotIndex)
            .ThenBy(p => p.CreatedAt)
            .FirstOrDefaultAsync(ct);

    public async Task<List<ActivityPlan>> GetAllRunningPlansAsync(CancellationToken ct = default) =>
        await _db.ActivityPlans
            .Where(p => p.State == ActivityState.Running)
            .ToListAsync(ct);
}
