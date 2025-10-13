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

        // 验证 Hub 端点
        if (string.IsNullOrWhiteSpace(options.HubEndpoint))
        {
            errors.Add("HubEndpoint cannot be null or empty");
        }
        else if (!options.HubEndpoint.StartsWith('/'))
        {
            errors.Add("HubEndpoint must start with '/'");
        }

        // 验证重连配置
        if (options.MaxReconnectAttempts < 0)
        {
            errors.Add("MaxReconnectAttempts must be non-negative");
        }
        if (options.MaxReconnectAttempts > 100)
        {
            errors.Add("MaxReconnectAttempts should not exceed 100");
        }

        if (options.ReconnectBaseDelayMs < 100)
        {
            errors.Add("ReconnectBaseDelayMs must be at least 100ms");
        }
        if (options.ReconnectBaseDelayMs > 10000)
        {
            errors.Add("ReconnectBaseDelayMs should not exceed 10000ms (10s)");
        }

        if (options.MaxReconnectDelayMs < options.ReconnectBaseDelayMs)
        {
            errors.Add("MaxReconnectDelayMs must be greater than or equal to ReconnectBaseDelayMs");
        }
        if (options.MaxReconnectDelayMs > 300000)
        {
            errors.Add("MaxReconnectDelayMs should not exceed 300000ms (5 minutes)");
        }

        // 验证超时配置
        if (options.ConnectionTimeoutSeconds < 5)
        {
            errors.Add("ConnectionTimeoutSeconds must be at least 5 seconds");
        }
        if (options.ConnectionTimeoutSeconds > 300)
        {
            errors.Add("ConnectionTimeoutSeconds should not exceed 300 seconds");
        }

        if (options.KeepAliveIntervalSeconds < 5)
        {
            errors.Add("KeepAliveIntervalSeconds must be at least 5 seconds");
        }
        if (options.KeepAliveIntervalSeconds > 60)
        {
            errors.Add("KeepAliveIntervalSeconds should not exceed 60 seconds");
        }

        if (options.ServerTimeoutSeconds < options.KeepAliveIntervalSeconds)
        {
            errors.Add("ServerTimeoutSeconds must be greater than or equal to KeepAliveIntervalSeconds");
        }
        if (options.ServerTimeoutSeconds > 600)
        {
            errors.Add("ServerTimeoutSeconds should not exceed 600 seconds");
        }

        // 验证通知配置
        if (options.Notification == null)
        {
            errors.Add("Notification configuration cannot be null");
        }

        // 验证性能配置
        if (options.Performance == null)
        {
            errors.Add("Performance configuration cannot be null");
        }
        else
        {
            if (options.Performance.ThrottleWindowMs < 100)
            {
                errors.Add("Performance.ThrottleWindowMs must be at least 100ms");
            }
            if (options.Performance.ThrottleWindowMs > 10000)
            {
                errors.Add("Performance.ThrottleWindowMs should not exceed 10000ms");
            }

            if (options.Performance.BatchDelayMs < 10)
            {
                errors.Add("Performance.BatchDelayMs must be at least 10ms");
            }
            if (options.Performance.BatchDelayMs > 5000)
            {
                errors.Add("Performance.BatchDelayMs should not exceed 5000ms");
            }
        }

        if (errors.Count > 0)
        {
            return ValidateOptionsResult.Fail(errors);
        }

        return ValidateOptionsResult.Success;
    }
}
