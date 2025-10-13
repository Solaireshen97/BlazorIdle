namespace BlazorIdle.Server.Config;

/// <summary>
/// SignalR 配置验证器
/// 确保配置值在合理范围内，防止配置错误导致运行时问题
/// </summary>
public static class SignalROptionsValidator
{
    /// <summary>
    /// 验证 SignalR 配置选项
    /// </summary>
    /// <param name="options">要验证的配置选项</param>
    /// <returns>验证结果</returns>
    public static ValidationResult Validate(SignalROptions options)
    {
        var errors = new List<string>();
        
        // 验证端点路径
        if (string.IsNullOrWhiteSpace(options.HubEndpoint))
        {
            errors.Add("HubEndpoint cannot be empty");
        }
        else if (!options.HubEndpoint.StartsWith('/'))
        {
            errors.Add("HubEndpoint must start with '/'");
        }
        
        // 验证重连参数
        if (options.MaxReconnectAttempts < 0)
        {
            errors.Add("MaxReconnectAttempts cannot be negative");
        }
        else if (options.MaxReconnectAttempts > 20)
        {
            errors.Add("MaxReconnectAttempts should not exceed 20 to avoid excessive retries");
        }
        
        if (options.ReconnectBaseDelayMs < 100)
        {
            errors.Add("ReconnectBaseDelayMs should be at least 100ms");
        }
        else if (options.ReconnectBaseDelayMs > 10000)
        {
            errors.Add("ReconnectBaseDelayMs should not exceed 10000ms (10 seconds)");
        }
        
        // 验证超时参数
        if (options.ConnectionTimeoutSeconds < 1)
        {
            errors.Add("ConnectionTimeoutSeconds must be at least 1 second");
        }
        else if (options.ConnectionTimeoutSeconds > 300)
        {
            errors.Add("ConnectionTimeoutSeconds should not exceed 300 seconds (5 minutes)");
        }
        
        if (options.KeepAliveIntervalSeconds < 1)
        {
            errors.Add("KeepAliveIntervalSeconds must be at least 1 second");
        }
        else if (options.KeepAliveIntervalSeconds > options.ServerTimeoutSeconds)
        {
            errors.Add("KeepAliveIntervalSeconds should not exceed ServerTimeoutSeconds");
        }
        
        if (options.ServerTimeoutSeconds < 1)
        {
            errors.Add("ServerTimeoutSeconds must be at least 1 second");
        }
        else if (options.ServerTimeoutSeconds > 600)
        {
            errors.Add("ServerTimeoutSeconds should not exceed 600 seconds (10 minutes)");
        }
        
        // 验证 KeepAlive 与 ServerTimeout 的关系
        if (options.KeepAliveIntervalSeconds * 2 > options.ServerTimeoutSeconds)
        {
            errors.Add("ServerTimeoutSeconds should be at least twice KeepAliveIntervalSeconds");
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
        public bool IsValid { get; init; }
        
        /// <summary>
        /// 验证错误列表
        /// </summary>
        public List<string> Errors { get; init; } = new();
        
        /// <summary>
        /// 获取格式化的错误消息
        /// </summary>
        public string GetErrorMessage()
        {
            return IsValid ? string.Empty : string.Join("; ", Errors);
        }
    }
}
