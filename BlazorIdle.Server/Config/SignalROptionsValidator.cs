using Microsoft.Extensions.Options;

namespace BlazorIdle.Server.Config;

/// <summary>
/// SignalR 配置选项验证器
/// 确保配置参数在合理范围内
/// </summary>
public sealed class SignalROptionsValidator : IValidateOptions<SignalROptions>
{
    public ValidateOptionsResult Validate(string? name, SignalROptions options)
    {
        var errors = new List<string>();

        // 验证基础配置
        if (string.IsNullOrWhiteSpace(options.HubEndpoint))
        {
            errors.Add("HubEndpoint cannot be null or empty");
        }
        else if (!options.HubEndpoint.StartsWith("/"))
        {
            errors.Add("HubEndpoint must start with '/'");
        }

        if (options.MaxReconnectAttempts < 0)
        {
            errors.Add("MaxReconnectAttempts must be >= 0");
        }

        if (options.ReconnectBaseDelayMs < 100)
        {
            errors.Add("ReconnectBaseDelayMs must be >= 100");
        }

        if (options.MaxReconnectDelayMs < options.ReconnectBaseDelayMs)
        {
            errors.Add("MaxReconnectDelayMs must be >= ReconnectBaseDelayMs");
        }

        if (options.ConnectionTimeoutSeconds <= 0)
        {
            errors.Add("ConnectionTimeoutSeconds must be > 0");
        }

        if (options.KeepAliveIntervalSeconds <= 0)
        {
            errors.Add("KeepAliveIntervalSeconds must be > 0");
        }

        if (options.ServerTimeoutSeconds <= 0)
        {
            errors.Add("ServerTimeoutSeconds must be > 0");
        }

        if (string.IsNullOrWhiteSpace(options.BattleGroupPrefix))
        {
            errors.Add("BattleGroupPrefix cannot be null or empty");
        }

        if (options.MaxConcurrentConnections < 0)
        {
            errors.Add("MaxConcurrentConnections must be >= 0");
        }

        if (options.ConnectionIdleTimeoutSeconds < 0)
        {
            errors.Add("ConnectionIdleTimeoutSeconds must be >= 0");
        }

        // 验证性能配置
        if (options.Performance.ThrottleWindowMs < 0)
        {
            errors.Add("Performance.ThrottleWindowMs must be >= 0");
        }

        if (options.Performance.BatchDelayMs < 0)
        {
            errors.Add("Performance.BatchDelayMs must be >= 0");
        }

        // 验证监控配置
        if (options.Monitoring.MetricsIntervalSeconds <= 0)
        {
            errors.Add("Monitoring.MetricsIntervalSeconds must be > 0");
        }

        if (options.Monitoring.SlowNotificationThresholdMs < 0)
        {
            errors.Add("Monitoring.SlowNotificationThresholdMs must be >= 0");
        }

        return errors.Count > 0
            ? ValidateOptionsResult.Fail(errors)
            : ValidateOptionsResult.Success;
    }
}
