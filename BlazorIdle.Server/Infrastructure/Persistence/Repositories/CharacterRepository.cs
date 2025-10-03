using BlazorIdle.Server.Application.Abstractions;
using BlazorIdle.Server.Domain.Characters;
using Microsoft.EntityFrameworkCore;

namespace BlazorIdle.Server.Infrastructure.Persistence.Repositories;

/// <summary>
/// 角色数据仓储实现：封装对 Character 实体的最小读取操作。
/// 当前仅提供按 Id 获取单个角色的方法。
/// </summary>
public class CharacterRepository : ICharacterRepository
{
    private readonly GameDbContext _db;

    /// <summary>
    /// 通过依赖注入获得上下文（通常在 ASP.NET Core 中是 Scoped 生命周期）。
    /// </summary>
    public CharacterRepository(GameDbContext db) => _db = db;

    /// <summary>
    /// 按角色 Id 异步查询一个角色；找不到返回 null。
    /// 使用 FirstOrDefaultAsync：主键唯一，结果至多一条，与 SingleOrDefaultAsync 实际效果等价。
    /// （当前未使用 AsNoTracking，意味着若需要纯读操作并且不更新，可考虑后续优化。）
    /// </summary>
    public Task<Character?> GetAsync(Guid id, CancellationToken ct = default) =>
        _db.Characters.FirstOrDefaultAsync(c => c.Id == id, ct);
}