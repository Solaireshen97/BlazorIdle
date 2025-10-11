using BlazorIdle.Server.Application.Abstractions;
using BlazorIdle.Server.Domain.Equipment.Models;
using Microsoft.EntityFrameworkCore;

namespace BlazorIdle.Server.Infrastructure.Persistence.Repositories;

/// <summary>
/// 装备定义仓储实现
/// </summary>
public class GearDefinitionRepository : IGearDefinitionRepository
{
    private readonly GameDbContext _db;

    public GearDefinitionRepository(GameDbContext db) => _db = db;

    public Task<GearDefinition?> GetByIdAsync(string id, CancellationToken ct = default) =>
        _db.GearDefinitions.FirstOrDefaultAsync(g => g.Id == id, ct);

    public Task<List<GearDefinition>> GetBySlotAsync(EquipmentSlot slot, CancellationToken ct = default) =>
        _db.GearDefinitions.Where(g => g.Slot == slot).ToListAsync(ct);

    public Task<List<GearDefinition>> GetAllAsync(CancellationToken ct = default) =>
        _db.GearDefinitions.ToListAsync(ct);

    public async Task CreateAsync(GearDefinition definition, CancellationToken ct = default)
    {
        _db.GearDefinitions.Add(definition);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(GearDefinition definition, CancellationToken ct = default)
    {
        definition.UpdatedAt = DateTime.UtcNow;
        _db.GearDefinitions.Update(definition);
        await _db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(string id, CancellationToken ct = default)
    {
        var definition = await GetByIdAsync(id, ct);
        if (definition != null)
        {
            _db.GearDefinitions.Remove(definition);
            await _db.SaveChangesAsync(ct);
        }
    }
}
