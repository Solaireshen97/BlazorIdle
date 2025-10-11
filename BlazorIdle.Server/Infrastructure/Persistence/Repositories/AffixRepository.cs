using BlazorIdle.Server.Application.Abstractions;
using BlazorIdle.Server.Domain.Equipment.Models;
using Microsoft.EntityFrameworkCore;

namespace BlazorIdle.Server.Infrastructure.Persistence.Repositories;

/// <summary>
/// 词条定义仓储实现
/// </summary>
public class AffixRepository : IAffixRepository
{
    private readonly GameDbContext _db;

    public AffixRepository(GameDbContext db) => _db = db;

    public Task<Affix?> GetByIdAsync(string id, CancellationToken ct = default) =>
        _db.Affixes.FirstOrDefaultAsync(a => a.Id == id, ct);

    public Task<List<Affix>> GetAllAsync(CancellationToken ct = default) =>
        _db.Affixes.ToListAsync(ct);

    public Task<List<Affix>> GetBySlotAsync(EquipmentSlot slot, CancellationToken ct = default) =>
        _db.Affixes
            .Where(a => a.AllowedSlots == null || a.AllowedSlots.Contains(slot))
            .ToListAsync(ct);

    public async Task CreateAsync(Affix affix, CancellationToken ct = default)
    {
        affix.UpdatedAt = DateTime.UtcNow;
        _db.Affixes.Add(affix);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Affix affix, CancellationToken ct = default)
    {
        affix.UpdatedAt = DateTime.UtcNow;
        _db.Affixes.Update(affix);
        await _db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(string id, CancellationToken ct = default)
    {
        var affix = await GetByIdAsync(id, ct);
        if (affix != null)
        {
            _db.Affixes.Remove(affix);
            await _db.SaveChangesAsync(ct);
        }
    }
}
