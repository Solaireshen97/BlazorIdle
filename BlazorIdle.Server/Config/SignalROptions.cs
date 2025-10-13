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
    
    // ==================== Phase 2.5 增强配置 ====================
    
    /// <summary>
    /// SignalR 组名前缀（用于战斗订阅分组）
    /// </summary>
    public string GroupNamePrefix { get; set; } = "battle_";
    
    /// <summary>
    /// 方法名配置
    /// </summary>
    public MethodNames Methods { get; set; } = new();
    
    /// <summary>
    /// 通知节流配置（防止高频通知）
    /// </summary>
    public ThrottlingOptions Throttling { get; set; } = new();
    
    /// <summary>
    /// 监控和诊断配置
    /// </summary>
    public MonitoringOptions Monitoring { get; set; } = new();
}

/// <summary>
/// SignalR 方法名配置
/// </summary>
public sealed class MethodNames
{
    /// <summary>
    /// 状态变更通知方法名
    /// </summary>
    public string StateChanged { get; set; } = "StateChanged";
    
    /// <summary>
    /// 详细事件通知方法名
    /// </summary>
    public string BattleEvent { get; set; } = "BattleEvent";
}

/// <summary>
/// 通知节流配置
/// </summary>
public sealed class ThrottlingOptions
{
    /// <summary>
    /// 是否启用通知节流
    /// </summary>
    public bool EnableThrottling { get; set; } = false;
    
    /// <summary>
    /// 最小通知间隔（毫秒）
    /// </summary>
    public int MinNotificationIntervalMs { get; set; } = 100;
    
    /// <summary>
    /// 批量通知最大延迟（毫秒）
    /// </summary>
    public int MaxBatchDelayMs { get; set; } = 500;
    
    /// <summary>
    /// 每批最大事件数
    /// </summary>
    public int MaxEventsPerBatch { get; set; } = 10;
}

/// <summary>
/// 监控和诊断配置
/// </summary>
public sealed class MonitoringOptions
{
    /// <summary>
    /// 是否启用性能指标收集
    /// </summary>
    public bool EnableMetrics { get; set; } = false;
    
    /// <summary>
    /// 是否记录连接事件
    /// </summary>
    public bool LogConnectionEvents { get; set; } = true;
    
    /// <summary>
    /// 是否记录通知发送详情
    /// </summary>
    public bool LogNotificationDetails { get; set; } = false;
    
    /// <summary>
    /// 慢通知阈值（毫秒），超过此时间记录警告
    /// </summary>
    public int SlowNotificationThresholdMs { get; set; } = 1000;
}
