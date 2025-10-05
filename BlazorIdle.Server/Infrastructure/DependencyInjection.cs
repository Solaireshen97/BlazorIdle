using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using BlazorIdle.Server.Infrastructure.Persistence;
using BlazorIdle.Server.Application.Abstractions;
using BlazorIdle.Server.Infrastructure.Persistence.Repositories;
using BlazorIdle.Server.Application.Battles.Step;

namespace BlazorIdle.Server.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var conn = configuration.GetConnectionString("DefaultConnection") ?? "Data Source=gamedata.db";
        services.AddDbContext<GameDbContext>(opt => opt.UseSqlite(conn));

        services.AddRepositories();

        // Step 模式后台推进 + 落库
        services.AddSingleton<StepBattleCoordinator>();
        services.AddSingleton<StepBattleFinalizer>();   // 使用 ScopeFactory 内部创建作用域
        services.AddHostedService<StepBattleHostedService>();

        return services;
    }
}