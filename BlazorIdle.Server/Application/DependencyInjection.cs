using BlazorIdle.Server.Application.Battles;
using BlazorWebGame.Application.Battles;
using Microsoft.Extensions.DependencyInjection;

namespace BlazorIdle.Server.Application;

public static class ApplicationDI
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddTransient<BattleRunner>();        // 无状态或轻量
        services.AddScoped<StartBattleService>();     // 用例
        return services;
    }
}