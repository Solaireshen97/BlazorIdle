using BlazorIdle.Server.Domain.Records;

namespace BlazorIdle.Server.Application.Abstractions;

public interface IBattleRepository
{
    Task AddAsync(BattleRecord battle, CancellationToken ct = default);
    Task<BattleRecord?> GetWithSegmentsAsync(Guid id, CancellationToken ct = default);
}