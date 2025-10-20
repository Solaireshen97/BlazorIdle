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
using BlazorIdle.Server.Config.DatabaseOptimization;
using BlazorIdle.Server.Infrastructure.DatabaseOptimization;
using BlazorIdle.Server.Infrastructure.DatabaseOptimization.Abstractions;
using BlazorIdle.Server.Infrastructure.DatabaseOptimization.Caching;
using BlazorIdle.Server.Infrastructure.DatabaseOptimization.Caching.Abstractions;
using BlazorIdle.Server.Domain.Characters;
using BlazorIdle.Server.Domain.Records;
using BlazorIdle.Server.Domain.Activities;

namespace BlazorIdle.Server.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var conn = configuration.GetConnectionString("DefaultConnection") ?? "Data Source=gamedata.db";
        
        // 配置 SQLite 连接以提高并发性和稳定性
        var connectionStringBuilder = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder(conn)
        {
            Mode = Microsoft.Data.Sqlite.SqliteOpenMode.ReadWriteCreate,
            Cache = Microsoft.Data.Sqlite.SqliteCacheMode.Shared,
            // 设置繁忙超时为 30 秒，给予足够时间等待锁释放
            DefaultTimeout = 30
        };
        
        services.AddDbContext<GameDbContext>(opt => 
        {
            opt.UseSqlite(connectionStringBuilder.ToString(), sqliteOptions =>
            {
                // 启用连接池以重用连接
                sqliteOptions.CommandTimeout(30);
            });
            
            // 在开发环境启用敏感数据记录以便调试
            if (configuration.GetValue<bool>("Logging:EnableSensitiveDataLogging", false))
            {
                opt.EnableSensitiveDataLogging();
            }
        });

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
            var logger = sp.GetRequiredService<ILogger<OfflineSettlementService>>();
            
            // 传递 ActivityPlanService 的方法作为委托
            return new OfflineSettlementService(
                characters,
                simulator,
                plans,
                engine,
                db,
                equipmentStats,
                logger,
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
        services.AddSingleton<IShopConfigurationValidator, ShopConfigurationValidator>();
        services.AddSingleton<IShopConfigurationLoader, ShopConfigurationLoader>();
        
        // 商店系统缓存
        services.AddMemoryCache();
        services.AddSingleton<BlazorIdle.Server.Application.Shop.IShopCacheService, BlazorIdle.Server.Application.Shop.ShopCacheService>();
        
        // 库存系统服务
        services.AddScoped<BlazorIdle.Server.Application.Abstractions.IInventoryService, BlazorIdle.Server.Application.Inventory.InventoryService>();

        // ===== 数据库优化组件注册 =====
        // Database Optimization Components Registration
        
        // 配置选项（从 appsettings.json 加载）
        // Configuration options (loaded from appsettings.json)
        services.Configure<PersistenceOptions>(configuration.GetSection("Persistence"));
        services.Configure<ShutdownOptions>(configuration.GetSection("Shutdown"));
        services.Configure<MemoryCacheOptions>(configuration.GetSection("MemoryCache"));
        
        // 配置验证（确保配置值在合理范围内）
        // Configuration validation (ensure values are in valid range)
        services.AddOptions<PersistenceOptions>()
            .Bind(configuration.GetSection("Persistence"))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        
        services.AddOptions<ShutdownOptions>()
            .Bind(configuration.GetSection("Shutdown"))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        
        services.AddOptions<MemoryCacheOptions>()
            .Bind(configuration.GetSection("MemoryCache"))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        
        services.AddOptions<MonitoringOptions>()
            .Bind(configuration.GetSection("Monitoring"))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        
        // ===== 读缓存配置选项 =====
        // Read Cache Configuration Options
        services.Configure<ReadCacheOptions>(configuration.GetSection("ReadCache"));
        
        services.AddOptions<ReadCacheOptions>()
            .Bind(configuration.GetSection("ReadCache"))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        
        // ===== 读缓存核心服务 =====
        // Read Cache Core Services
        
        // 多层缓存管理器（单例 - 全局共享）
        // Multi-tier cache manager (singleton - globally shared)
        services.AddSingleton<IMultiTierCacheManager, MultiTierCacheManager>();
        
        // 缓存失效协调器（单例 - 管理缓存失效逻辑）
        // Cache invalidation coordinator (singleton - manages cache invalidation)
        services.AddSingleton<ICacheInvalidationCoordinator, CacheInvalidationCoordinator>();
        
        // 静态配置加载器（单例 + HostedService - 启动时加载静态配置）
        // Static config loader (singleton + HostedService - loads static configs on startup)
        services.AddSingleton<IStaticConfigLoader, StaticConfigLoader>();
        services.AddHostedService(sp => (StaticConfigLoader)sp.GetRequiredService<IStaticConfigLoader>());
        
        // 内存状态管理器（单例 - 全局共享）
        // Memory state managers (singletons - globally shared)
        services.AddSingleton<IMemoryStateManager<Character>, MemoryStateManager<Character>>();
        services.AddSingleton<IMemoryStateManager<RunningBattleSnapshotRecord>, MemoryStateManager<RunningBattleSnapshotRecord>>();
        services.AddSingleton<IMemoryStateManager<ActivityPlan>, MemoryStateManager<ActivityPlan>>();
        
        // 数据库性能指标收集器（单例 - 用于监控和诊断）
        // Database metrics collector (singleton - for monitoring and diagnostics)
        services.AddSingleton<DatabaseMetricsCollector>();
        
        // 持久化协调器（后台服务 - 单例）
        // Persistence coordinator (background service - singleton)
        services.AddSingleton<IPersistenceCoordinator, PersistenceCoordinator>();
        services.AddHostedService(sp => (PersistenceCoordinator)sp.GetRequiredService<IPersistenceCoordinator>());

        return services;
    }
}