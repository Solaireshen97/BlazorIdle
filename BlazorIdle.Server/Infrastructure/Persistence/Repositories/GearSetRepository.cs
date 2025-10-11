using BlazorIdle.Server.Application.Abstractions;
using BlazorIdle.Server.Domain.Equipment.Models;
using Microsoft.EntityFrameworkCore;

namespace BlazorIdle.Server.Infrastructure.Persistence.Repositories;

/// <summary>
/// 套装仓储实现
/// </summary>
public class GearSetRepository : IGearSetRepository
{
    private readonly GameDbContext _db;

    public GearSetRepository(GameDbContext db) => _db = db;

    public Task<GearSet?> GetByIdAsync(string id, CancellationToken ct = default) =>
        _db.GearSets.FirstOrDefaultAsync(s => s.Id == id, ct);

    public Task<List<GearSet>> GetAllAsync(CancellationToken ct = default) =>
        _db.GearSets.ToListAsync(ct);

    public async Task CreateAsync(GearSet gearSet, CancellationToken ct = default)
    {
        _db.GearSets.Add(gearSet);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(GearSet gearSet, CancellationToken ct = default)
    {
        gearSet.UpdatedAt = DateTime.UtcNow;
        _db.GearSets.Update(gearSet);
        await _db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(string id, CancellationToken ct = default)
    {
        var gearSet = await GetByIdAsync(id, ct);
        if (gearSet != null)
        {
            _db.GearSets.Remove(gearSet);
            await _db.SaveChangesAsync(ct);
        }
    }
}
