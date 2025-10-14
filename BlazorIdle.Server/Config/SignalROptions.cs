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
    /// 通知配置选项
    /// </summary>
    public NotificationOptions Notification { get; set; } = new();
    
    /// <summary>
    /// 性能配置选项（预留给 Phase 4）
    /// </summary>
    public PerformanceOptions Performance { get; set; } = new();
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
    
    /// <summary>
    /// 启用攻击触发轻量事件通知（用于前端进度条增量更新）
    /// </summary>
    public bool EnableAttackTickNotification { get; set; } = true;
    
    /// <summary>
    /// 启用技能施放完成轻量事件通知（用于前端进度条增量更新）
    /// </summary>
    public bool EnableSkillCastCompleteNotification { get; set; } = true;
    
    /// <summary>
    /// 启用伤害应用轻量事件通知（用于前端实时反馈）
    /// </summary>
    public bool EnableDamageAppliedNotification { get; set; } = false;
    
    /// <summary>
    /// 启用攻击开始事件通知（用于显示战斗日志）
    /// </summary>
    public bool EnableAttackStartedNotification { get; set; } = true;
    
    /// <summary>
    /// 启用敌人攻击开始事件通知（用于显示战斗日志）
    /// </summary>
    public bool EnableEnemyAttackStartedNotification { get; set; } = true;
    
    /// <summary>
    /// 启用伤害接收事件通知（用于显示战斗日志）
    /// </summary>
    public bool EnableDamageReceivedNotification { get; set; } = true;
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
