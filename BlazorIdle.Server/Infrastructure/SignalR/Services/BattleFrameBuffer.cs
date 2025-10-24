using System.Collections.Concurrent;
using BlazorIdle.Shared.Messages.Battle;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BlazorIdle.Server.Infrastructure.SignalR.Services;

/// <summary>
/// 战斗帧缓冲区
/// 用于存储历史帧数据，支持断线重连后的补发和状态同步
/// 实现线程安全的帧存储、查询和自动清理功能
/// </summary>
public class BattleFrameBuffer
{
    // 帧数据存储字典
    // Key: 帧版本号, Value: 帧数据
    private readonly ConcurrentDictionary<long, FrameTick> _frames = new();
    
    private readonly BattleFrameBufferOptions _options;
    private readonly ILogger<BattleFrameBuffer>? _logger;
    
    // 版本号范围跟踪
    // 用于快速判断帧是否在缓冲区范围内
    private long _minVersion = 0;
    private long _maxVersion = 0;
    
    // 锁对象，用于保护版本号更新
    private readonly object _versionLock = new();
    
    // 统计信息（可选）
    private BufferStatistics _statistics = new();

    /// <summary>
    /// 构造函数 - 使用配置选项
    /// </summary>
    /// <param name="options">缓冲区配置选项</param>
    /// <param name="logger">日志记录器（可选）</param>
    public BattleFrameBuffer(IOptions<BattleFrameBufferOptions> options, ILogger<BattleFrameBuffer>? logger = null)
        : this(options?.Value ?? throw new ArgumentNullException(nameof(options)), logger)
    {
    }

    /// <summary>
    /// 构造函数 - 直接使用配置对象
    /// </summary>
    /// <param name="options">缓冲区配置选项</param>
    /// <param name="logger">日志记录器（可选）</param>
    public BattleFrameBuffer(BattleFrameBufferOptions options, ILogger<BattleFrameBuffer>? logger = null)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _options.Validate();
        _logger = logger;
        
        _logger?.LogDebug(
            "创建BattleFrameBuffer，最大容量={MaxSize}，统计功能={EnableStats}",
            _options.MaxSize, _options.EnableStatistics);
    }

    /// <summary>
    /// 构造函数 - 简化版本，仅指定容量
    /// 用于测试或简单场景
    /// </summary>
    /// <param name="maxSize">最大容量</param>
    public BattleFrameBuffer(int maxSize = 300)
        : this(new BattleFrameBufferOptions { MaxSize = maxSize })
    {
    }

    /// <summary>
    /// 添加帧到缓冲区
    /// 线程安全，支持并发添加
    /// </summary>
    /// <param name="frame">要添加的帧数据</param>
    /// <exception cref="ArgumentNullException">frame为null时抛出</exception>
    public void AddFrame(FrameTick frame)
    {
        if (frame == null)
            throw new ArgumentNullException(nameof(frame));

        // 存储帧数据
        _frames[frame.Version] = frame;
        
        // 更新版本号范围
        lock (_versionLock)
        {
            if (frame.Version > _maxVersion)
                _maxVersion = frame.Version;
            
            if (_minVersion == 0 || frame.Version < _minVersion)
                _minVersion = frame.Version;
        }

        // 更新统计信息
        if (_options.EnableStatistics)
        {
            _statistics.TotalFramesAdded++;
            _statistics.CurrentSize = _frames.Count;
        }

        // 检查是否需要清理
        var threshold = _options.CleanupThreshold > 0 
            ? _options.CleanupThreshold 
            : _options.MaxSize;
        
        if (_frames.Count > threshold)
        {
            CleanupOldFrames();
        }

        _logger?.LogTrace(
            "添加帧到缓冲区，版本={Version}，当前容量={Count}/{MaxSize}",
            frame.Version, _frames.Count, _options.MaxSize);
    }

    /// <summary>
    /// 获取指定范围的帧
    /// 用于断线重连时的增量补发
    /// </summary>
    /// <param name="fromVersion">起始版本号（包含）</param>
    /// <param name="toVersion">结束版本号（包含）</param>
    /// <returns>
    /// 帧列表（按版本号升序）
    /// 如果有任何帧缺失或超出缓冲区范围，返回空列表，表示需要使用快照
    /// </returns>
    public List<FrameTick> GetFrames(long fromVersion, long toVersion)
    {
        // 参数验证
        if (fromVersion > toVersion)
        {
            _logger?.LogWarning(
                "查询参数无效: fromVersion({From}) > toVersion({To})",
                fromVersion, toVersion);
            return new List<FrameTick>();
        }

        // 检查版本范围是否在缓冲区范围内
        if (fromVersion < _minVersion)
        {
            _logger?.LogDebug(
                "查询版本范围过旧: fromVersion={From}, 缓冲区最小版本={Min}",
                fromVersion, _minVersion);
            
            if (_options.EnableStatistics)
                _statistics.OutOfRangeQueries++;
            
            return new List<FrameTick>();
        }

        // 尝试收集所有请求的帧
        var frames = new List<FrameTick>((int)(toVersion - fromVersion + 1));

        for (long v = fromVersion; v <= toVersion; v++)
        {
            if (_frames.TryGetValue(v, out var frame))
            {
                frames.Add(frame);
            }
            else
            {
                // 缺少某些帧，返回空表示需要快照
                _logger?.LogWarning(
                    "帧范围不完整: 版本{Version}缺失，请求范围={From}-{To}",
                    v, fromVersion, toVersion);
                
                if (_options.EnableStatistics)
                    _statistics.IncompleteQueries++;
                
                return new List<FrameTick>();
            }
        }

        // 更新统计信息
        if (_options.EnableStatistics)
        {
            _statistics.SuccessfulQueries++;
            _statistics.TotalFramesRetrieved += frames.Count;
        }

        _logger?.LogDebug(
            "成功获取帧范围: {From}-{To}，共{Count}帧",
            fromVersion, toVersion, frames.Count);

        return frames;
    }

    /// <summary>
    /// 获取指定版本的单个帧
    /// </summary>
    /// <param name="version">帧版本号</param>
    /// <returns>帧数据，如果不存在则返回null</returns>
    public FrameTick? GetFrame(long version)
    {
        _frames.TryGetValue(version, out var frame);
        
        if (_options.EnableStatistics)
        {
            if (frame != null)
                _statistics.SuccessfulQueries++;
            else
                _statistics.IncompleteQueries++;
        }
        
        return frame;
    }

    /// <summary>
    /// 检查指定版本的帧是否存在
    /// </summary>
    /// <param name="version">帧版本号</param>
    /// <returns>true表示存在，false表示不存在</returns>
    public bool HasFrame(long version)
    {
        return _frames.ContainsKey(version);
    }

    /// <summary>
    /// 检查指定范围的帧是否完整
    /// 用于判断是否可以使用增量补发
    /// </summary>
    /// <param name="fromVersion">起始版本号</param>
    /// <param name="toVersion">结束版本号</param>
    /// <returns>true表示范围内所有帧都存在，false表示有缺失</returns>
    public bool HasCompleteRange(long fromVersion, long toVersion)
    {
        if (fromVersion > toVersion)
            return false;

        if (fromVersion < _minVersion || toVersion > _maxVersion)
            return false;

        for (long v = fromVersion; v <= toVersion; v++)
        {
            if (!_frames.ContainsKey(v))
                return false;
        }

        return true;
    }

    /// <summary>
    /// 清理过旧的帧
    /// 保持缓冲区容量在限制范围内
    /// </summary>
    private void CleanupOldFrames()
    {
        var currentCount = _frames.Count;
        var targetCount = _options.MaxSize;

        if (currentCount <= targetCount)
            return;

        // 计算需要移除的帧数
        var toRemoveCount = currentCount - targetCount;

        // 获取所有版本号并排序
        var versions = _frames.Keys.OrderBy(v => v).ToList();
        var toRemove = versions.Take(toRemoveCount).ToList();

        // 移除最旧的帧
        foreach (var version in toRemove)
        {
            _frames.TryRemove(version, out _);
        }

        // 更新最小版本号
        if (toRemove.Count > 0)
        {
            lock (_versionLock)
            {
                var newMinVersion = toRemove.Last() + 1;
                if (newMinVersion > _minVersion)
                    _minVersion = newMinVersion;
            }

            _logger?.LogDebug(
                "清理了{Count}个旧帧，新的版本范围: {Min}-{Max}",
                toRemove.Count, _minVersion, _maxVersion);

            if (_options.EnableStatistics)
            {
                _statistics.TotalFramesRemoved += toRemove.Count;
                _statistics.CleanupCount++;
                _statistics.CurrentSize = _frames.Count;
            }
        }

        // 可选：压缩内存
        if (_options.CompactOnCleanup)
        {
            GC.Collect(2, GCCollectionMode.Optimized);
            _logger?.LogTrace("执行了内存压缩");
        }
    }

    /// <summary>
    /// 获取缓冲区统计信息
    /// 用于监控和调试
    /// </summary>
    /// <returns>统计信息对象</returns>
    public BufferStatistics GetStatistics()
    {
        var stats = _statistics.Clone();
        stats.CurrentSize = _frames.Count;
        stats.MinVersion = _minVersion;
        stats.MaxVersion = _maxVersion;
        stats.MaxSize = _options.MaxSize;
        return stats;
    }

    /// <summary>
    /// 清空缓冲区
    /// 删除所有缓存的帧并重置状态
    /// </summary>
    public void Clear()
    {
        _frames.Clear();
        
        lock (_versionLock)
        {
            _minVersion = 0;
            _maxVersion = 0;
        }

        if (_options.EnableStatistics)
        {
            _statistics.CurrentSize = 0;
        }

        _logger?.LogInformation("缓冲区已清空");
    }

    /// <summary>
    /// 获取缓冲区当前容量
    /// </summary>
    public int Count => _frames.Count;

    /// <summary>
    /// 获取最小版本号
    /// </summary>
    public long MinVersion => _minVersion;

    /// <summary>
    /// 获取最大版本号
    /// </summary>
    public long MaxVersion => _maxVersion;
}

/// <summary>
/// 缓冲区统计信息
/// 用于监控缓冲区的使用情况和性能
/// </summary>
public class BufferStatistics
{
    /// <summary>
    /// 当前缓存的帧数
    /// </summary>
    public int CurrentSize { get; set; }

    /// <summary>
    /// 最小版本号
    /// </summary>
    public long MinVersion { get; set; }

    /// <summary>
    /// 最大版本号
    /// </summary>
    public long MaxVersion { get; set; }

    /// <summary>
    /// 最大容量
    /// </summary>
    public int MaxSize { get; set; }

    /// <summary>
    /// 添加的总帧数（累计）
    /// </summary>
    public long TotalFramesAdded { get; set; }

    /// <summary>
    /// 移除的总帧数（累计）
    /// </summary>
    public long TotalFramesRemoved { get; set; }

    /// <summary>
    /// 成功获取的总帧数（累计）
    /// </summary>
    public long TotalFramesRetrieved { get; set; }

    /// <summary>
    /// 成功查询次数
    /// </summary>
    public long SuccessfulQueries { get; set; }

    /// <summary>
    /// 不完整查询次数（有帧缺失）
    /// </summary>
    public long IncompleteQueries { get; set; }

    /// <summary>
    /// 超出范围查询次数（请求的版本太旧）
    /// </summary>
    public long OutOfRangeQueries { get; set; }

    /// <summary>
    /// 清理次数
    /// </summary>
    public long CleanupCount { get; set; }

    /// <summary>
    /// 计算命中率
    /// </summary>
    public double HitRate
    {
        get
        {
            var totalQueries = SuccessfulQueries + IncompleteQueries + OutOfRangeQueries;
            return totalQueries > 0 ? (double)SuccessfulQueries / totalQueries : 0;
        }
    }

    /// <summary>
    /// 克隆统计信息
    /// </summary>
    public BufferStatistics Clone()
    {
        return new BufferStatistics
        {
            CurrentSize = CurrentSize,
            MinVersion = MinVersion,
            MaxVersion = MaxVersion,
            MaxSize = MaxSize,
            TotalFramesAdded = TotalFramesAdded,
            TotalFramesRemoved = TotalFramesRemoved,
            TotalFramesRetrieved = TotalFramesRetrieved,
            SuccessfulQueries = SuccessfulQueries,
            IncompleteQueries = IncompleteQueries,
            OutOfRangeQueries = OutOfRangeQueries,
            CleanupCount = CleanupCount
        };
    }
}
