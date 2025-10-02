using BlazorIdle.Server.Domain.Characters;

namespace BlazorIdle.Server.Application.Abstractions;

public interface ICharacterRepository
{
    Task<Character?> GetAsync(Guid id, CancellationToken ct = default);
}