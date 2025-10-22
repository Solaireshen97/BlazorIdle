namespace BlazorIdle.Server.Infrastructure.SignalR;

/// <summary>
/// SignalR配置选项
/// 从appsettings.json的SignalR节点读取配置
/// </summary>
public class SignalROptions
{
    /// <summary>
    /// 配置节名称
    /// </summary>
    public const string SectionName = "SignalR";

    /// <summary>
    /// 消息队列容量
    /// 当队列满时，新消息将等待直到有空间（背压控制）
    /// 默认值：10000
    /// </summary>
    public int QueueCapacity { get; set; } = 10000;

    /// <summary>
    /// 批量发送大小
    /// 当待发送消息达到此数量时触发批量发送
    /// 默认值：100
    /// </summary>
    public int BatchSize { get; set; } = 100;

    /// <summary>
    /// 批量发送时间间隔（毫秒）
    /// 即使未达到批量大小，超过此时间也会触发发送
    /// 默认值：50毫秒
    /// </summary>
    public int BatchIntervalMs { get; set; } = 50;

    /// <summary>
    /// 是否启用详细错误信息
    /// 仅在开发环境建议启用
    /// 默认值：false
    /// </summary>
    public bool EnableDetailedErrors { get; set; } = false;

    /// <summary>
    /// 最大消息接收大小（字节）
    /// 默认值：102400 (100KB)
    /// </summary>
    public long MaximumReceiveMessageSize { get; set; } = 102400;

    /// <summary>
    /// 握手超时时间（秒）
    /// 默认值：15秒
    /// </summary>
    public int HandshakeTimeoutSeconds { get; set; } = 15;

    /// <summary>
    /// 保活间隔（秒）
    /// 服务器向客户端发送ping的间隔
    /// 默认值：15秒
    /// </summary>
    public int KeepAliveIntervalSeconds { get; set; } = 15;

    /// <summary>
    /// 客户端超时时间（秒）
    /// 如果在此时间内未收到客户端消息，连接将被关闭
    /// 默认值：30秒
    /// </summary>
    public int ClientTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// 是否启用MessagePack压缩
    /// MessagePack提供更好的性能但需要客户端支持
    /// 默认值：false
    /// </summary>
    public bool EnableMessagePackCompression { get; set; } = false;

    /// <summary>
    /// 验证配置有效性
    /// </summary>
    public void Validate()
    {
        if (QueueCapacity <= 0)
            throw new ArgumentException("QueueCapacity must be greater than 0", nameof(QueueCapacity));

        if (BatchSize <= 0)
            throw new ArgumentException("BatchSize must be greater than 0", nameof(BatchSize));

        if (BatchIntervalMs <= 0)
            throw new ArgumentException("BatchIntervalMs must be greater than 0", nameof(BatchIntervalMs));

        if (MaximumReceiveMessageSize <= 0)
            throw new ArgumentException("MaximumReceiveMessageSize must be greater than 0", nameof(MaximumReceiveMessageSize));

        if (HandshakeTimeoutSeconds <= 0)
            throw new ArgumentException("HandshakeTimeoutSeconds must be greater than 0", nameof(HandshakeTimeoutSeconds));

        if (KeepAliveIntervalSeconds <= 0)
            throw new ArgumentException("KeepAliveIntervalSeconds must be greater than 0", nameof(KeepAliveIntervalSeconds));

        if (ClientTimeoutSeconds <= 0)
            throw new ArgumentException("ClientTimeoutSeconds must be greater than 0", nameof(ClientTimeoutSeconds));

        if (ClientTimeoutSeconds <= KeepAliveIntervalSeconds)
            throw new ArgumentException("ClientTimeoutSeconds must be greater than KeepAliveIntervalSeconds", nameof(ClientTimeoutSeconds));
    }
}
