using BlazorIdle.Server.Application.Abstractions;
using BlazorIdle.Server.Infrastructure.Persistence.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace BlazorIdle.Server.Infrastructure;

/// <summary>
/// 仓储（Repository）实现的集中注册扩展。
/// 目的：把所有“数据访问实现”在一个地方统一注入到 DI 容器，
/// 避免在 Program.cs 里到处写 AddScoped。
/// </summary>
public static class RepositoryRegistration
{
    /// <summary>
    /// 向 IServiceCollection 注册仓储实现。
    /// 调用位置：Program.cs -> services.AddRepositories()
    /// 返回自身以便链式调用（Fluent API）。
    /// </summary>
    public static IServiceCollection AddRepositories(this IServiceCollection services)
    {
        // AddScoped：每个 HTTP 请求创建一个实例（与 DbContext 生命周期匹配）。
        // IBattleRepository -> BattleRepository:
        //   * 负责战斗记录(BattleRecord + Segments)的持久化与读取（含 Include Segments）。
        services.AddScoped<IBattleRepository, BattleRepository>();

        // ICharacterRepository -> CharacterRepository:
        //   * 封装获取角色（Character）的数据访问。保持上层不用直接依赖 DbContext。
        services.AddScoped<ICharacterRepository, CharacterRepository>();

        return services;
    }
}