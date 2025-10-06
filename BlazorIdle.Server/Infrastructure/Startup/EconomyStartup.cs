using BlazorIdle.Server.Domain.Economy;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BlazorIdle.Server.Infrastructure.Startup;

public static class EconomyStartup
{
    public static IServiceCollection AddEconomyValidation(this IServiceCollection services, bool throwOnError = true)
    {
        // 使用构造时的工厂，延后到 ServiceProvider 构建完成后执行校验并记录日志
        services.AddSingleton<IStartupFilter, EconomyValidationStartupFilter>(sp => new EconomyValidationStartupFilter(throwOnError));
        return services;
    }

    private sealed class EconomyValidationStartupFilter : IStartupFilter
    {
        private readonly bool _throwOnError;
        public EconomyValidationStartupFilter(bool throwOnError) => _throwOnError = throwOnError;

        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
        {
            return app =>
            {
                var logger = app.ApplicationServices.GetRequiredService<ILoggerFactory>().CreateLogger("EconomyValidation");
                var issues = EconomyValidator.ValidateAll();

                var errors = issues.Where(i => i.Severity == "error").ToList();
                var warnings = issues.Where(i => i.Severity == "warning").ToList();

                foreach (var w in warnings)
                    logger.LogWarning("[{Code}] {Message}", w.Code, w.Message);

                if (errors.Count > 0)
                {
                    foreach (var e in errors)
                        logger.LogError("[{Code}] {Message}", e.Code, e.Message);

                    if (_throwOnError)
                    {
                        throw new InvalidOperationException($"Economy validation failed with {errors.Count} error(s). See logs for details.");
                    }
                }
                else
                {
                    logger.LogInformation("Economy validation passed with {Warnings} warning(s).", warnings.Count);
                }

                next(app);
            };
        }
    }
}