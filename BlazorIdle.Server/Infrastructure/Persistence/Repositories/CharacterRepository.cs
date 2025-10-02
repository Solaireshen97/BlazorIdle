using BlazorIdle.Server.Application.Abstractions;
using BlazorIdle.Server.Domain.Characters;
using Microsoft.EntityFrameworkCore;

namespace BlazorIdle.Server.Infrastructure.Persistence.Repositories;

public class CharacterRepository : ICharacterRepository
{
    private readonly GameDbContext _db;
    public CharacterRepository(GameDbContext db) => _db = db;

    public Task<Character?> GetAsync(Guid id, CancellationToken ct = default) =>
        _db.Characters.FirstOrDefaultAsync(c => c.Id == id, ct);
}