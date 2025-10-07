using BlazorIdle.Server.Application.Abstractions;
using BlazorIdle.Server.Domain.Activities;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace BlazorIdle.Server.Infrastructure.Persistence.Repositories;

/// <summary>
/// 活动计划仓储实现
/// </summary>
public class ActivityPlanRepository : IActivityPlanRepository
{
    private readonly GameDbContext _db;

    public ActivityPlanRepository(GameDbContext db) => _db = db;

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
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(ActivityPlan plan, CancellationToken ct = default)
    {
        _db.ActivityPlans.Update(plan);
        await _db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var plan = await GetAsync(id, ct);
        if (plan is not null)
        {
            _db.ActivityPlans.Remove(plan);
            await _db.SaveChangesAsync(ct);
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
