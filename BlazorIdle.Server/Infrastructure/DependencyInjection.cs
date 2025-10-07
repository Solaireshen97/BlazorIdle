using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using BlazorIdle.Server.Infrastructure.Persistence;
using BlazorIdle.Server.Application.Abstractions;
using BlazorIdle.Server.Infrastructure.Persistence.Repositories;
using BlazorIdle.Server.Application.Battles.Step;
using BlazorIdle.Server.Application.Battles.Simulation;
using BlazorIdle.Server.Application.Battles.Offline;
using BlazorIdle.Server.Application.Battles;
using BlazorIdle.Server.Infrastructure.Startup;

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

        // 离线快进引擎
        services.AddSingleton<OfflineFastForwardEngine>();

        // 离线结算（集成活动计划自动衔接）
        services.AddTransient<OfflineSettlementService>(sp =>
        {
            var characters = sp.GetRequiredService<ICharacterRepository>();
            var simulator = sp.GetRequiredService<BattleSimulator>();
            var plans = sp.GetRequiredService<IActivityPlanRepository>();
            var engine = sp.GetRequiredService<OfflineFastForwardEngine>();
            var db = sp.GetRequiredService<GameDbContext>();
            
            // 创建回调函数：尝试启动下一个待执行的计划
            async Task<Domain.Activities.ActivityPlan?> TryStartNextPlan(Guid characterId, CancellationToken ct)
            {
                var planService = sp.GetRequiredService<Application.Activities.ActivityPlanService>();
                return await planService.TryStartNextPendingPlanAsync(characterId, ct);
            }
            
            return new OfflineSettlementService(characters, simulator, plans, engine, db, TryStartNextPlan);
        });

        // 经济数据校验（应用启动时执行）
        services.AddEconomyValidation(throwOnError: true);

        return services;
    }
}