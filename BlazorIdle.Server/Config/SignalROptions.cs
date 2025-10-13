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
    /// 是否启用 SignalR（默认：true）
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Hub 端点路径（默认：/hubs/battle）
    /// </summary>
    public string HubPath { get; set; } = "/hubs/battle";

    /// <summary>
    /// 重连延迟（秒，默认：5）
    /// </summary>
    public int ReconnectDelaySeconds { get; set; } = 5;

    /// <summary>
    /// 最大重连尝试次数（默认：5）
    /// </summary>
    public int MaxReconnectAttempts { get; set; } = 5;

    /// <summary>
    /// 连接超时时间（秒，默认：30）
    /// </summary>
    public int ConnectionTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// 保持连接心跳间隔（秒，默认：15）
    /// </summary>
    public int KeepAliveIntervalSeconds { get; set; } = 15;

    /// <summary>
    /// 是否启用详细错误信息（开发模式建议启用，默认：false）
    /// </summary>
    public bool EnableDetailedErrors { get; set; } = false;
}
