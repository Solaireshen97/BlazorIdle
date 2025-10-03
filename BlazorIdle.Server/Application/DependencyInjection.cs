using BlazorIdle.Server.Application.Battles;
using BlazorWebGame.Application.Battles;
using Microsoft.Extensions.DependencyInjection;

namespace BlazorIdle.Server.Application;

/// <summary>
/// Application 层对外暴露的扩展方法：集中注册“用例级”服务。
/// 在 Program.cs 中调用 services.AddApplication() 即可完成本层依赖注入。
/// 作用：把所有应用层需要对外提供的服务集中在一个地方，避免在 Program.cs 四散写 AddScoped。
/// </summary>
public static class ApplicationDI
{
    /// <summary>
    /// 注册应用层服务。
    /// 返回 IServiceCollection 以支持链式调用（流式 API 风格）。
    /// </summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // BattleRunner:
        //  - 当前实现是纯算法/无状态（每次调用内部 new Clock/Scheduler），线程安全。
        //  - 使用 Transient：每次注入获取新的实例；即使改成 Scoped 也没问题。
        //  - 如果未来改造成“可配置策略 + 缓存”或带内部状态，就需要改为 Scoped 或 Singleton（视内部是否无共享可变数据）。
        services.AddTransient<BattleRunner>();

        // StartBattleService:
        //  - 典型“用例服务”（Application Service），组合：仓储接口 + BattleRunner。
        //  - 使用 Scoped：与请求生命周期绑定（同一个 HTTP 请求内只创建一次）。
        //  - 如果后续需要在一个请求中复用其内部缓存或跟踪信息，Scoped 是合适的。
        //  - 不适合 Singleton（依赖的仓储是 Scoped，提升会导致“捕获 Scoped 依赖”错误）。
        services.AddScoped<StartBattleService>();

        return services;
    }
}