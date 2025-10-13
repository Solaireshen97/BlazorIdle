namespace BlazorIdle.Server.Application.SignalR.Configuration;

/// <summary>
/// SignalR配置选项
/// 从appsettings.json的SignalR配置节读取
/// </summary>
public class SignalROptions
{
    /// <summary>
    /// Hub端点路径，默认：/hubs/battle
    /// </summary>
    public string HubPath { get; set; } = "/hubs/battle";

    /// <summary>
    /// 是否启用详细错误信息（仅开发环境建议启用）
    /// </summary>
    public bool EnableDetailedErrors { get; set; } = false;

    /// <summary>
    /// 保持连接活动的间隔（秒），默认：15秒
    /// </summary>
    public int KeepAliveIntervalSeconds { get; set; } = 15;

    /// <summary>
    /// 客户端超时时间（秒），默认：30秒
    /// </summary>
    public int ClientTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// 握手超时时间（秒），默认：15秒
    /// </summary>
    public int HandshakeTimeoutSeconds { get; set; } = 15;

    /// <summary>
    /// 最大接收消息大小（字节），默认：32KB
    /// </summary>
    public int MaximumReceiveMessageSize { get; set; } = 32768;

    /// <summary>
    /// 流缓冲区容量，默认：10
    /// </summary>
    public int StreamBufferCapacity { get; set; } = 10;

    /// <summary>
    /// 客户端是否启用自动重连，默认：true
    /// </summary>
    public bool EnableReconnection { get; set; } = true;

    /// <summary>
    /// 重连延迟序列（秒），默认：[0, 2, 10, 30]
    /// </summary>
    public int[] ReconnectionDelaySeconds { get; set; } = new[] { 0, 2, 10, 30 };

    /// <summary>
    /// 最大重连尝试次数，默认：4
    /// </summary>
    public int MaxReconnectionAttempts { get; set; } = 4;
}
