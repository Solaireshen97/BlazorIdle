using BlazorIdle.Server.Domain.Records;

namespace BlazorIdle.Server.Application.Abstractions;

public interface IBattleRepository
{
    Task AddAsync(BattleRecord battle, CancellationToken ct = default);
    Task<BattleRecord?> GetWithSegmentsAsync(Guid id, CancellationToken ct = default);

    // 新增：幂等保护用，检查是否已存在
    Task<bool> ExistsAsync(Guid id, CancellationToken ct = default);
}