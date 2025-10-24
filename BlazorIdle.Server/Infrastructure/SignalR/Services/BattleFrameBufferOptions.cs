namespace BlazorIdle.Server.Infrastructure.SignalR.Services;

/// <summary>
/// 战斗帧缓冲区配置选项
/// 定义BattleFrameBuffer的所有可配置参数
/// </summary>
public class BattleFrameBufferOptions
{
    /// <summary>
    /// 配置节名称，用于从appsettings.json读取配置
    /// </summary>
    public const string SectionName = "BattleFrameBuffer";

    /// <summary>
    /// 缓冲区最大容量（帧数）
    /// 每个战斗缓存的最大帧数，超出后将清理最旧的帧
    /// 默认值：300帧（在8Hz下约37.5秒的历史）
    /// 建议值：根据断线重连时间需求调整
    /// - 15秒断线容忍：120帧（8Hz）
    /// - 30秒断线容忍：240帧（8Hz）
    /// - 60秒断线容忍：480帧（8Hz）
    /// </summary>
    public int MaxSize { get; set; } = 300;

    /// <summary>
    /// 是否启用统计信息
    /// true: 记录缓冲区的命中率、查询次数等统计信息
    /// false: 不记录统计信息，节省内存和CPU
    /// 默认值：false
    /// </summary>
    public bool EnableStatistics { get; set; } = false;

    /// <summary>
    /// 是否在清理时压缩缓冲区
    /// true: 清理旧帧后立即压缩内存（适合内存紧张环境）
    /// false: 延迟压缩，提高性能
    /// 默认值：false
    /// </summary>
    public bool CompactOnCleanup { get; set; } = false;

    /// <summary>
    /// 自动清理触发阈值（帧数）
    /// 当缓冲区容量超过此阈值时触发自动清理
    /// 0表示达到MaxSize立即清理
    /// 默认值：0（立即清理）
    /// </summary>
    public int CleanupThreshold { get; set; } = 0;

    /// <summary>
    /// 验证配置有效性
    /// </summary>
    /// <exception cref="InvalidOperationException">配置无效时抛出异常</exception>
    public void Validate()
    {
        if (MaxSize <= 0)
            throw new InvalidOperationException($"缓冲区最大容量必须大于0: {MaxSize}");

        if (MaxSize > 10000)
            throw new InvalidOperationException($"缓冲区最大容量不能超过10000: {MaxSize}（避免内存溢出）");

        if (CleanupThreshold < 0)
            throw new InvalidOperationException($"清理阈值不能为负数: {CleanupThreshold}");

        if (CleanupThreshold > MaxSize)
            throw new InvalidOperationException(
                $"清理阈值({CleanupThreshold})不能大于最大容量({MaxSize})");
    }
}
