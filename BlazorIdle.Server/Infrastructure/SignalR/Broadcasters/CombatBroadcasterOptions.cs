namespace BlazorIdle.Server.Infrastructure.SignalR.Broadcasters;

/// <summary>
/// 战斗广播器配置选项
/// 定义CombatBroadcaster的所有可配置参数
/// </summary>
public class CombatBroadcasterOptions
{
    /// <summary>
    /// 配置节名称，用于从appsettings.json读取配置
    /// </summary>
    public const string SectionName = "CombatBroadcaster";

    /// <summary>
    /// 定时器精度（毫秒）
    /// 后台服务的轮询间隔，决定了广播的最小时间精度
    /// 默认值：10毫秒（足够的精度用于2-10Hz的广播频率）
    /// </summary>
    public int TickIntervalMs { get; set; } = 10;

    /// <summary>
    /// 默认广播频率（Hz）
    /// 每秒推送的帧数，范围：2-10Hz
    /// 默认值：8Hz（每125毫秒一帧）
    /// 建议值：低端设备2-4Hz，中端设备6-8Hz，高端设备8-10Hz
    /// </summary>
    public int DefaultFrequency { get; set; } = 8;

    /// <summary>
    /// 最小广播频率（Hz）
    /// 防止频率设置过低导致体验不佳
    /// 默认值：2Hz
    /// </summary>
    public int MinFrequency { get; set; } = 2;

    /// <summary>
    /// 最大广播频率（Hz）
    /// 防止频率设置过高导致带宽和CPU消耗过大
    /// 默认值：10Hz
    /// </summary>
    public int MaxFrequency { get; set; } = 10;

    /// <summary>
    /// 快照生成间隔（帧数）
    /// 每隔多少帧生成一次完整快照
    /// 快照用于断线重连时的完整状态恢复
    /// 默认值：300帧（在8Hz下约37.5秒）
    /// </summary>
    public int SnapshotIntervalFrames { get; set; } = 300;

    /// <summary>
    /// 是否自动清理结束的战斗
    /// true: 战斗结束后自动停止广播并清理资源
    /// false: 需要手动调用StopBroadcast
    /// 默认值：true
    /// </summary>
    public bool AutoCleanupFinishedBattles { get; set; } = true;

    /// <summary>
    /// 战斗结束后延迟清理时间（秒）
    /// 在战斗结束后等待多久才清理资源
    /// 给客户端留出时间接收最后的数据和结算信息
    /// 默认值：5秒
    /// </summary>
    public int CleanupDelaySeconds { get; set; } = 5;

    /// <summary>
    /// 最大并发战斗数量
    /// 限制同时进行广播的战斗数量，防止资源耗尽
    /// 0表示不限制
    /// 默认值：0（不限制）
    /// </summary>
    public int MaxConcurrentBattles { get; set; } = 0;

    /// <summary>
    /// 是否启用详细日志
    /// true: 记录每一帧的广播详情
    /// false: 仅记录启动、停止等重要事件
    /// 默认值：false（减少日志量）
    /// </summary>
    public bool EnableDetailedLogging { get; set; } = false;

    /// <summary>
    /// 验证配置有效性
    /// </summary>
    /// <exception cref="InvalidOperationException">配置无效时抛出异常</exception>
    public void Validate()
    {
        if (TickIntervalMs <= 0)
            throw new InvalidOperationException($"定时器精度必须大于0毫秒: {TickIntervalMs}");

        if (TickIntervalMs > 1000)
            throw new InvalidOperationException($"定时器精度不能超过1000毫秒: {TickIntervalMs}");

        if (MinFrequency <= 0)
            throw new InvalidOperationException($"最小广播频率必须大于0Hz: {MinFrequency}");

        if (MaxFrequency < MinFrequency)
            throw new InvalidOperationException(
                $"最大广播频率({MaxFrequency}Hz)不能小于最小广播频率({MinFrequency}Hz)");

        if (DefaultFrequency < MinFrequency || DefaultFrequency > MaxFrequency)
            throw new InvalidOperationException(
                $"默认广播频率({DefaultFrequency}Hz)必须在{MinFrequency}-{MaxFrequency}Hz范围内");

        if (SnapshotIntervalFrames <= 0)
            throw new InvalidOperationException($"快照生成间隔必须大于0帧: {SnapshotIntervalFrames}");

        if (CleanupDelaySeconds < 0)
            throw new InvalidOperationException($"清理延迟时间不能为负数: {CleanupDelaySeconds}");

        if (MaxConcurrentBattles < 0)
            throw new InvalidOperationException($"最大并发战斗数量不能为负数: {MaxConcurrentBattles}");
    }
}
