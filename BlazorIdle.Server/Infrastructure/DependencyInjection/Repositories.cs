using BlazorIdle.Server.Application.Abstractions;
using BlazorIdle.Server.Infrastructure.Persistence.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace BlazorIdle.Server.Infrastructure;

public static class RepositoryRegistration
{
    public static IServiceCollection AddRepositories(this IServiceCollection services)
    {
        services.AddScoped<IBattleRepository, BattleRepository>();
        services.AddScoped<ICharacterRepository, CharacterRepository>();
        return services;
    }
}