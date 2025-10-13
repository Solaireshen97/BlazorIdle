namespace BlazorIdle.Server.Config;

/// <summary>
/// SignalR 配置选项
/// </summary>
public sealed class SignalROptions
{
    /// <summary>
    /// 配置节名称
    /// </summary>
    public const string SectionName = "SignalR";
    
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
    /// 战斗组名称前缀（用于 SignalR 分组）
    /// </summary>
    public string BattleGroupPrefix { get; set; } = "battle_";
    
    /// <summary>
    /// 最大并发连接数（0 表示不限制）
    /// </summary>
    public int MaxConcurrentConnections { get; set; } = 0;
    
    /// <summary>
    /// 连接空闲超时时间（秒，0 表示不限制）
    /// </summary>
    public int ConnectionIdleTimeoutSeconds { get; set; } = 300;
    
    /// <summary>
    /// 通知配置选项
    /// </summary>
    public NotificationOptions Notification { get; set; } = new();
    
    /// <summary>
    /// 性能配置选项（预留给 Phase 4）
    /// </summary>
    public PerformanceOptions Performance { get; set; } = new();
    
    /// <summary>
    /// 监控配置选项
    /// </summary>
    public MonitoringOptions Monitoring { get; set; } = new();
}

/// <summary>
/// 通知相关配置
/// </summary>
public sealed class NotificationOptions
{
    /// <summary>
    /// 启用玩家死亡通知
    /// </summary>
    public bool EnablePlayerDeathNotification { get; set; } = true;
    
    /// <summary>
    /// 启用玩家复活通知
    /// </summary>
    public bool EnablePlayerReviveNotification { get; set; } = true;
    
    /// <summary>
    /// 启用敌人击杀通知
    /// </summary>
    public bool EnableEnemyKilledNotification { get; set; } = true;
    
    /// <summary>
    /// 启用目标切换通知
    /// </summary>
    public bool EnableTargetSwitchedNotification { get; set; } = true;
    
    /// <summary>
    /// 启用波次刷新通知（预留给 Phase 3）
    /// </summary>
    public bool EnableWaveSpawnNotification { get; set; } = false;
    
    /// <summary>
    /// 启用技能施放通知（预留给 Phase 3）
    /// </summary>
    public bool EnableSkillCastNotification { get; set; } = false;
    
    /// <summary>
    /// 启用 Buff 变化通知（预留给 Phase 3）
    /// </summary>
    public bool EnableBuffChangeNotification { get; set; } = false;
}

/// <summary>
/// 性能相关配置（预留给 Phase 4）
/// </summary>
public sealed class PerformanceOptions
{
    /// <summary>
    /// 启用通知节流
    /// </summary>
    public bool EnableThrottling { get; set; } = false;
    
    /// <summary>
    /// 节流窗口大小（毫秒）
    /// </summary>
    public int ThrottleWindowMs { get; set; } = 1000;
    
    /// <summary>
    /// 启用批量通知
    /// </summary>
    public bool EnableBatching { get; set; } = false;
    
    /// <summary>
    /// 批量通知延迟（毫秒）
    /// </summary>
    public int BatchDelayMs { get; set; } = 100;
    
    /// <summary>
    /// 移动端自动降级
    /// </summary>
    public bool AutoDegradeOnMobile { get; set; } = false;
}

/// <summary>
/// 监控相关配置
/// </summary>
public sealed class MonitoringOptions
{
    /// <summary>
    /// 启用性能监控指标
    /// </summary>
    public bool EnableMetrics { get; set; } = false;
    
    /// <summary>
    /// 指标收集间隔（秒）
    /// </summary>
    public int MetricsIntervalSeconds { get; set; } = 60;
    
    /// <summary>
    /// 启用连接追踪
    /// </summary>
    public bool EnableConnectionTracking { get; set; } = false;
    
    /// <summary>
    /// 启用通知延迟测量
    /// </summary>
    public bool EnableLatencyMeasurement { get; set; } = false;
    
    /// <summary>
    /// 记录慢速通知的阈值（毫秒）
    /// </summary>
    public int SlowNotificationThresholdMs { get; set; } = 1000;
}
