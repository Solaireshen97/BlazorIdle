namespace BlazorIdle.Client.Config;

/// <summary>
/// SignalR 客户端配置选项
/// 遵循项目配置化设计原则，所有参数从 appsettings.json 读取
/// </summary>
public sealed class SignalRClientOptions
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
    /// 最大重连延迟（毫秒）
    /// 防止指数退避延迟过长
    /// </summary>
    public int MaxReconnectDelayMs { get; set; } = 30000;
    
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
    
    /// <summary>
    /// 是否自动重连（断线后）
    /// </summary>
    public bool EnableAutomaticReconnect { get; set; } = true;
    
    /// <summary>
    /// 重连失败后等待时间（毫秒）
    /// 用于手动重连策略
    /// </summary>
    public int ReconnectFailedWaitMs { get; set; } = 5000;
    
    /// <summary>
    /// 是否在启动时自动连接
    /// </summary>
    public bool AutoConnectOnStartup { get; set; } = false;
    
    /// <summary>
    /// 连接状态检查间隔（毫秒）
    /// </summary>
    public int ConnectionCheckIntervalMs { get; set; } = 10000;
}
