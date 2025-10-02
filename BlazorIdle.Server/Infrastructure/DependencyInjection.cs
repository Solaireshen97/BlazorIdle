using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using BlazorIdle.Server.Infrastructure.Persistence;
using BlazorIdle.Server.Application.Abstractions;
using BlazorIdle.Server.Infrastructure.Persistence.Repositories;

namespace BlazorIdle.Server.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var conn = configuration.GetConnectionString("DefaultConnection") ?? "Data Source=gamedata.db";
        services.AddDbContext<GameDbContext>(opt => opt.UseSqlite(conn));

        // 仓储注册直接写在这里
        services.AddScoped<IBattleRepository, BattleRepository>();
        services.AddScoped<ICharacterRepository, CharacterRepository>();

        return services;
    }
}