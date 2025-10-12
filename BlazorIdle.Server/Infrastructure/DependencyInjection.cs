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
using BlazorIdle.Server.Domain.Equipment.Services;
using BlazorIdle.Server.Infrastructure.Configuration;

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
            var equipmentStats = sp.GetRequiredService<EquipmentStatsIntegration>();
            
            // 传递 ActivityPlanService 的方法作为委托
            return new OfflineSettlementService(
                characters,
                simulator,
                plans,
                engine,
                db,
                equipmentStats,
                planService.TryStartNextPendingPlanAsync,
                planService.StartPlanAsync
            );
        });

        // 经济数据校验（应用启动时执行）
        services.AddEconomyValidation(throwOnError: true);

        // 装备系统服务
        services.AddScoped<GearGenerationService>();
        services.AddScoped<EquipmentService>();
        services.AddScoped<StatsAggregationService>();
        services.AddScoped<DisenchantService>();
        services.AddScoped<ReforgeService>();
        services.AddScoped<EquipmentStatsIntegration>();
        
        // 装备系统计算器服务（Phase 4-6）
        services.AddSingleton<ArmorCalculator>();           // 无状态，线程安全，可为单例
        services.AddSingleton<BlockCalculator>();           // 无状态，线程安全，可为单例
        services.AddSingleton<AttackSpeedCalculator>();     // 无状态，线程安全，可为单例
        services.AddSingleton<WeaponDamageCalculator>();    // 武器伤害计算（Phase 5）
        services.AddScoped<EquipmentValidator>();           // 验证服务，使用Scoped
        
        // 商店系统配置
        services.Configure<ShopOptions>(configuration.GetSection("Shop"));
        services.AddSingleton<IShopConfigurationLoader, ShopConfigurationLoader>();
        
        // 商店系统缓存
        services.AddMemoryCache();
        services.AddSingleton<BlazorIdle.Server.Application.Shop.IShopCacheService, BlazorIdle.Server.Application.Shop.ShopCacheService>();

        return services;
    }
}