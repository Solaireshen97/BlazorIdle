using BlazorIdle.Server.Application.Abstractions;
using BlazorIdle.Server.Domain.Records;
using Microsoft.EntityFrameworkCore;

namespace BlazorIdle.Server.Infrastructure.Persistence.Repositories;

public class BattleRepository : IBattleRepository
{
    private readonly GameDbContext _db;

    public BattleRepository(GameDbContext db) => _db = db;

    public async Task AddAsync(BattleRecord battle, CancellationToken ct = default)
    {
        _db.Battles.Add(battle);
        await _db.SaveChangesAsync(ct);
    }

    public Task<BattleRecord?> GetWithSegmentsAsync(Guid id, CancellationToken ct = default) =>
        _db.Battles
            .Include(b => b.Segments)
            .FirstOrDefaultAsync(b => b.Id == id, ct);

    // 新增：存在性检查（避免重复插入）
    public Task<bool> ExistsAsync(Guid id, CancellationToken ct = default) =>
        _db.Battles.AnyAsync(b => b.Id == id, ct);

    public async Task UpdateRewardsAsync(Guid id, string rewardType, long gold, long exp, string lootJson, string? dungeonId, int runs, CancellationToken ct = default)
    {
        var rec = await _db.Battles.FirstAsync(b => b.Id == id, ct);
        rec.RewardType = rewardType;
        rec.Gold = gold;
        rec.Exp = exp;
        rec.LootJson = lootJson;
        rec.DungeonId = dungeonId;
        rec.DungeonRuns = runs;
        await _db.SaveChangesAsync(ct);
    }
}