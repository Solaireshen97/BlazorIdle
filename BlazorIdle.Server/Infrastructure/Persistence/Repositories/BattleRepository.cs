using BlazorIdle.Server.Application.Abstractions;
using BlazorIdle.Server.Domain.Records;
using Microsoft.EntityFrameworkCore;

namespace BlazorIdle.Server.Infrastructure.Persistence.Repositories;

/// <summary>
/// 战斗数据仓储实现：封装对 BattleRecord 聚合（含其 Segments）的基本持久化操作。
/// 当前职责很轻，只提供新增和按 Id（含分段）查询。
/// </summary>
public class BattleRepository : IBattleRepository
{
    private readonly GameDbContext _db;

    /// <summary>
    /// 通过依赖注入获取 DbContext（在 ASP.NET Core 中通常是 Scoped 生命周期）。
    /// </summary>
    public BattleRepository(GameDbContext db) => _db = db;

    /// <summary>
    /// 新增一条 BattleRecord（含其 Segments，如果已填充导航集合）。
    /// 立即调用 SaveChangesAsync 提交到数据库。
    /// 注意：这里直接提交会使“批量写/多聚合事务”不易组合。
    /// </summary>
    public async Task AddAsync(BattleRecord battle, CancellationToken ct = default)
    {
        _db.Battles.Add(battle);          // 等价 _db.Add(battle)，但语义更明确
        await _db.SaveChangesAsync(ct);   // 立即保存（无显式事务则由 EF 内部/数据库自动事务包裹）
    }

    /// <summary>
    /// 按战斗 Id 查询，并 Eager Load 关联 Segments。
    /// 若未找到返回 null。
    /// </summary>
    public Task<BattleRecord?> GetWithSegmentsAsync(Guid id, CancellationToken ct = default) =>
        _db.Battles
            .Include(b => b.Segments)     // 立即加载分段集合（避免延迟加载未知开销）
            .FirstOrDefaultAsync(b => b.Id == id, ct);
}