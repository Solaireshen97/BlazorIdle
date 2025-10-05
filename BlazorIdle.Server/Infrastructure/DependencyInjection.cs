using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using BlazorIdle.Server.Infrastructure.Persistence;
using BlazorIdle.Server.Application.Abstractions;
using BlazorIdle.Server.Infrastructure.Persistence.Repositories;
using BlazorIdle.Server.Application.Battles.Step;
using BlazorIdle.Server.Application.Battles.Simulation;
using BlazorIdle.Server.Application.Battles.Offline;

namespace BlazorIdle.Server.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var conn = configuration.GetConnectionString("DefaultConnection") ?? "Data Source=gamedata.db";
        services.AddDbContext<GameDbContext>(opt => opt.UseSqlite(conn));

        services.AddRepositories();

        // Step 异步战斗后台
        services.AddSingleton<StepBattleCoordinator>();
        services.AddSingleton<StepBattleFinalizer>();
        services.AddSingleton<StepBattleSnapshotService>();
        services.AddHostedService<StepBattleHostedService>();

        // 批量模拟
        services.AddTransient<BatchSimulator>();

        // 离线结算
        services.AddTransient<OfflineSettlementService>();

        return services;
    }
}