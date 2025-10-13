namespace BlazorIdle.Server.Config;

/// <summary>
/// SignalR 配置选项
/// </summary>
public sealed class SignalROptions
{
    /// <summary>
    /// SignalR Hub 端点路径
    /// </summary>
    public string HubEndpoint { get; set; } = "/hubs/battle";
    
    /// <summary>
    /// 是否启用 SignalR（可用于降级到纯轮询）
    /// </summary>
    public bool EnableSignalR { get; set; } = true;
    
    /// <summary>
    /// 自动重连尝试次数
    /// </summary>
    public int MaxReconnectAttempts { get; set; } = 5;
    
    /// <summary>
    /// 重连基础延迟（毫秒）
    /// </summary>
    public int ReconnectBaseDelayMs { get; set; } = 1000;
    
    /// <summary>
    /// 是否启用详细日志（开发环境）
    /// </summary>
    public bool EnableDetailedLogging { get; set; } = false;
    
    /// <summary>
    /// 连接超时时间（秒）
    /// </summary>
    public int ConnectionTimeoutSeconds { get; set; } = 30;
    
    /// <summary>
    /// 保持连接间隔（秒）
    /// </summary>
    public int KeepAliveIntervalSeconds { get; set; } = 15;
    
    /// <summary>
    /// 服务器超时时间（秒）
    /// </summary>
    public int ServerTimeoutSeconds { get; set; } = 30;
}
