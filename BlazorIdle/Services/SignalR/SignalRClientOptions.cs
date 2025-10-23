namespace BlazorIdle.Client.Services.SignalR;

/// <summary>
/// SignalR客户端配置选项
/// 包含连接管理、重连策略、心跳检测等配置参数
/// </summary>
public class SignalRClientOptions
{
    /// <summary>
    /// 配置节名称，用于从appsettings.json读取配置
    /// </summary>
    public const string SectionName = "SignalRClient";

    /// <summary>
    /// SignalR Hub的URL地址
    /// 默认值：https://localhost:7056/hubs/game
    /// </summary>
    public string HubUrl { get; set; } = "https://localhost:7056/hubs/game";

    /// <summary>
    /// 是否启用自动重连
    /// 默认值：true
    /// </summary>
    public bool EnableAutoReconnect { get; set; } = true;

    /// <summary>
    /// 自动重连延迟数组（毫秒），定义重连策略
    /// 例如：[0, 2000, 5000, 10000, 20000, 30000]
    /// 表示立即重连、2秒后、5秒后、10秒后、20秒后、30秒后
    /// 默认值：渐进式延迟策略
    /// </summary>
    public int[] ReconnectDelaysMs { get; set; } = new[] { 0, 2000, 5000, 10000, 20000, 30000 };

    /// <summary>
    /// 是否启用心跳检测
    /// 心跳用于保持连接活跃并及时发现连接异常
    /// 默认值：true
    /// </summary>
    public bool EnableHeartbeat { get; set; } = true;

    /// <summary>
    /// 心跳间隔时间（秒）
    /// 客户端会定期向服务器发送心跳消息
    /// 默认值：30秒
    /// </summary>
    public int HeartbeatIntervalSeconds { get; set; } = 30;

    /// <summary>
    /// 是否启用详细日志
    /// 开发环境建议启用以便调试
    /// 默认值：false
    /// </summary>
    public bool EnableDetailedLogging { get; set; } = false;

    /// <summary>
    /// 连接超时时间（秒）
    /// 超过此时间连接仍未建立则视为失败
    /// 默认值：30秒
    /// </summary>
    public int ConnectionTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// 消息处理超时时间（毫秒）
    /// 消息处理器超过此时间未完成则记录警告
    /// 默认值：5000毫秒（5秒）
    /// </summary>
    public int MessageHandlerTimeoutMs { get; set; } = 5000;

    /// <summary>
    /// 验证配置有效性
    /// </summary>
    /// <exception cref="InvalidOperationException">配置无效时抛出异常</exception>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(HubUrl))
        {
            throw new InvalidOperationException("SignalR Hub URL不能为空");
        }

        if (!Uri.TryCreate(HubUrl, UriKind.Absolute, out _))
        {
            throw new InvalidOperationException($"SignalR Hub URL格式无效: {HubUrl}");
        }

        if (HeartbeatIntervalSeconds <= 0)
        {
            throw new InvalidOperationException($"心跳间隔必须大于0: {HeartbeatIntervalSeconds}");
        }

        if (ConnectionTimeoutSeconds <= 0)
        {
            throw new InvalidOperationException($"连接超时时间必须大于0: {ConnectionTimeoutSeconds}");
        }

        if (MessageHandlerTimeoutMs <= 0)
        {
            throw new InvalidOperationException($"消息处理超时时间必须大于0: {MessageHandlerTimeoutMs}");
        }

        if (ReconnectDelaysMs == null || ReconnectDelaysMs.Length == 0)
        {
            throw new InvalidOperationException("重连延迟数组不能为空");
        }

        if (ReconnectDelaysMs.Any(d => d < 0))
        {
            throw new InvalidOperationException("重连延迟时间不能为负数");
        }
    }
}
