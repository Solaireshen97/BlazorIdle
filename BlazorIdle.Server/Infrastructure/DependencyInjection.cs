using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using BlazorIdle.Server.Infrastructure.Persistence;
using BlazorIdle.Server.Application.Abstractions;
using BlazorIdle.Server.Infrastructure.Persistence.Repositories;

namespace BlazorIdle.Server.Infrastructure;

/// <summary>
/// 基础设施层的服务注册扩展：
/// 在 Program.cs 中可通过 services.AddInfrastructure(Configuration) 一次性注入：
///   * DbContext (EF Core)
///   * 仓储实现
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// 注册基础设施层需要的依赖。
    /// </summary>
    /// <param name="services">核心 DI 容器</param>
    /// <param name="configuration">用于读取连接串等配置来源（appsettings / 环境变量）</param>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // 读取连接串；若未配置 "ConnectionStrings:DefaultConnection" 则退回到本地文件 SQLite
        var conn = configuration.GetConnectionString("DefaultConnection") ?? "Data Source=gamedata.db";

        // 注册 EF Core 上下文，使用 SQLite 提供程序。
        // 生命周期：AddDbContext 默认 Scoped（适合 Web 请求）。
        services.AddDbContext<GameDbContext>(opt => opt.UseSqlite(conn));

        // 仓储注册：接口 → 实现（Scoped，与 DbContext 生命周期保持一致）
        services.AddRepositories();

        return services;
    }
}