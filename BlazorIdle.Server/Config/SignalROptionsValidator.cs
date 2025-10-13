namespace BlazorIdle.Server.Config;

/// <summary>
/// SignalR 配置验证器
/// 确保配置参数在合理范围内
/// </summary>
public sealed class SignalROptionsValidator
{
    /// <summary>
    /// 验证配置选项
    /// </summary>
    /// <param name="options">要验证的配置选项</param>
    /// <returns>验证结果，包含错误信息列表</returns>
    public static ValidationResult Validate(SignalROptions options)
    {
        var errors = new List<string>();

        // 验证基础配置
        if (string.IsNullOrWhiteSpace(options.HubEndpoint))
        {
            errors.Add("HubEndpoint 不能为空");
        }
        else if (!options.HubEndpoint.StartsWith("/"))
        {
            errors.Add("HubEndpoint 必须以 '/' 开头");
        }

        if (options.MaxReconnectAttempts < 0)
        {
            errors.Add("MaxReconnectAttempts 不能为负数");
        }
        else if (options.MaxReconnectAttempts > 100)
        {
            errors.Add("MaxReconnectAttempts 不应超过 100");
        }

        if (options.ReconnectBaseDelayMs < 100)
        {
            errors.Add("ReconnectBaseDelayMs 不应小于 100ms");
        }
        else if (options.ReconnectBaseDelayMs > 60000)
        {
            errors.Add("ReconnectBaseDelayMs 不应超过 60000ms (1分钟)");
        }

        if (options.MaxReconnectDelayMs < options.ReconnectBaseDelayMs)
        {
            errors.Add("MaxReconnectDelayMs 不应小于 ReconnectBaseDelayMs");
        }
        else if (options.MaxReconnectDelayMs > 300000)
        {
            errors.Add("MaxReconnectDelayMs 不应超过 300000ms (5分钟)");
        }

        if (options.ConnectionTimeoutSeconds < 5)
        {
            errors.Add("ConnectionTimeoutSeconds 不应小于 5秒");
        }
        else if (options.ConnectionTimeoutSeconds > 300)
        {
            errors.Add("ConnectionTimeoutSeconds 不应超过 300秒");
        }

        if (options.KeepAliveIntervalSeconds < 1)
        {
            errors.Add("KeepAliveIntervalSeconds 不应小于 1秒");
        }
        else if (options.KeepAliveIntervalSeconds > 60)
        {
            errors.Add("KeepAliveIntervalSeconds 不应超过 60秒");
        }

        if (options.ServerTimeoutSeconds < options.KeepAliveIntervalSeconds * 2)
        {
            errors.Add("ServerTimeoutSeconds 应至少是 KeepAliveIntervalSeconds 的 2 倍");
        }

        // 验证性能配置
        if (options.Performance.EnableThrottling)
        {
            if (options.Performance.ThrottleWindowMs < 100)
            {
                errors.Add("ThrottleWindowMs 不应小于 100ms");
            }
            else if (options.Performance.ThrottleWindowMs > 10000)
            {
                errors.Add("ThrottleWindowMs 不应超过 10000ms");
            }
        }

        if (options.Performance.EnableBatching)
        {
            if (options.Performance.BatchDelayMs < 10)
            {
                errors.Add("BatchDelayMs 不应小于 10ms");
            }
            else if (options.Performance.BatchDelayMs > 5000)
            {
                errors.Add("BatchDelayMs 不应超过 5000ms");
            }
        }

        return new ValidationResult
        {
            IsValid = errors.Count == 0,
            Errors = errors
        };
    }

    /// <summary>
    /// 验证结果
    /// </summary>
    public sealed class ValidationResult
    {
        /// <summary>
        /// 是否验证通过
        /// </summary>
        public bool IsValid { get; set; }

        /// <summary>
        /// 错误信息列表
        /// </summary>
        public List<string> Errors { get; set; } = new();

        /// <summary>
        /// 获取格式化的错误信息
        /// </summary>
        public string GetErrorMessage()
        {
            return string.Join("; ", Errors);
        }
    }
}
