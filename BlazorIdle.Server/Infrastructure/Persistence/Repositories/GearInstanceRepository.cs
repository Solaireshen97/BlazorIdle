using BlazorIdle.Server.Application.Abstractions;
using BlazorIdle.Server.Domain.Equipment.Models;
using Microsoft.EntityFrameworkCore;

namespace BlazorIdle.Server.Infrastructure.Persistence.Repositories;

/// <summary>
/// 装备实例仓储实现
/// </summary>
public class GearInstanceRepository : IGearInstanceRepository
{
    private readonly GameDbContext _db;

    public GearInstanceRepository(GameDbContext db) => _db = db;

    public Task<GearInstance?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        _db.GearInstances
            .Include(g => g.Definition)
            .FirstOrDefaultAsync(g => g.Id == id, ct);

    public Task<List<GearInstance>> GetEquippedGearAsync(Guid characterId, CancellationToken ct = default) =>
        _db.GearInstances
            .Include(g => g.Definition)
            .Where(g => g.CharacterId == characterId && g.IsEquipped)
            .ToListAsync(ct);

    public Task<List<GearInstance>> GetGearByCharacterAsync(Guid characterId, CancellationToken ct = default) =>
        _db.GearInstances
            .Include(g => g.Definition)
            .Where(g => g.CharacterId == characterId)
            .ToListAsync(ct);

    public Task<GearInstance?> GetEquippedGearBySlotAsync(Guid characterId, EquipmentSlot slot, CancellationToken ct = default) =>
        _db.GearInstances
            .Include(g => g.Definition)
            .FirstOrDefaultAsync(g => g.CharacterId == characterId && g.IsEquipped && g.SlotType == slot, ct);

    public async Task CreateAsync(GearInstance instance, CancellationToken ct = default)
    {
        instance.UpdatedAt = DateTime.UtcNow;
        _db.GearInstances.Add(instance);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(GearInstance instance, CancellationToken ct = default)
    {
        instance.UpdatedAt = DateTime.UtcNow;
        _db.GearInstances.Update(instance);
        await _db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var instance = await _db.GearInstances.FirstOrDefaultAsync(g => g.Id == id, ct);
        if (instance != null)
        {
            _db.GearInstances.Remove(instance);
            await _db.SaveChangesAsync(ct);
        }
    }

    public async Task CreateBatchAsync(IEnumerable<GearInstance> instances, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        foreach (var instance in instances)
        {
            instance.UpdatedAt = now;
        }
        _db.GearInstances.AddRange(instances);
        await _db.SaveChangesAsync(ct);
    }
}
