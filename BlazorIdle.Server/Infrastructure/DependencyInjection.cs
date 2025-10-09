using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using BlazorIdle.Server.Infrastructure.Persistence;
using BlazorIdle.Server.Application.Abstractions;
using BlazorIdle.Server.Infrastructure.Persistence.Repositories;
using BlazorIdle.Server.Application.Battles.Step;
using BlazorIdle.Server.Application.Battles.Simulation;
using BlazorIdle.Server.Application.Battles.Offline;
using BlazorIdle.Server.Infrastructure.Startup;
using BlazorIdle.Server.Application.Battles;
using BlazorIdle.Server.Application.Activities;

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
        services.AddScoped<StepBattleFinalizer>();              // Scoped，避免 Singleton 依赖 Scoped
        services.AddSingleton<StepBattleSnapshotService>();     // 恢复注册（HostedService 依赖它）
        services.AddHostedService<StepBattleHostedService>();   // HostedService 为 Singleton

        // 批量模拟
        services.AddTransient<BatchSimulator>();

        // 离线战斗引擎
        services.AddTransient<OfflineFastForwardEngine>();

        // 离线结算（需要注入 ActivityPlanService 的 TryStartNextPendingPlanAsync 和 StartPlanAsync 委托）
        services.AddTransient<OfflineSettlementService>(sp =>
        {
            var characters = sp.GetRequiredService<ICharacterRepository>();
            var simulator = sp.GetRequiredService<BattleSimulator>();
            var plans = sp.GetRequiredService<IActivityPlanRepository>();
            var engine = sp.GetRequiredService<OfflineFastForwardEngine>();
            var db = sp.GetRequiredService<GameDbContext>();
            var planService = sp.GetRequiredService<ActivityPlanService>();
            
            // 传递 ActivityPlanService 的方法作为委托
            return new OfflineSettlementService(
                characters,
                simulator,
                plans,
                engine,
                db,
                planService.TryStartNextPendingPlanAsync,
                planService.StartPlanAsync
            );
        });

        // 经济数据校验（应用启动时执行）
        services.AddEconomyValidation(throwOnError: true);

        return services;
    }
}