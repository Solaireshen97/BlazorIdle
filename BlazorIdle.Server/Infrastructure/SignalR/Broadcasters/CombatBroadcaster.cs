using System.Collections.Concurrent;
using BlazorIdle.Server.Infrastructure.SignalR.Models;
using BlazorIdle.Shared.Messages.Battle;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BlazorIdle.Server.Infrastructure.SignalR.Broadcasters;

/// <summary>
/// 战斗帧广播服务
/// 负责定时生成和推送战斗帧数据到所有订阅的客户端
/// 作为后台服务运行，管理所有活跃战斗的帧广播
/// </summary>
public class CombatBroadcaster : BackgroundService
{
    private readonly ISignalRDispatcher _dispatcher;
    private readonly ILogger<CombatBroadcaster> _logger;
    private readonly CombatBroadcasterOptions _options;
    
    // 活跃战斗配置字典
    // Key: 战斗ID, Value: 战斗帧配置
    private readonly ConcurrentDictionary<string, BattleFrameConfig> _activeBattles = new();

    public CombatBroadcaster(
        ISignalRDispatcher dispatcher,
        ILogger<CombatBroadcaster> logger,
        IOptions<CombatBroadcasterOptions> options)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        
        // 验证配置
        _options.Validate();
    }

    /// <summary>
    /// 后台服务执行方法
    /// 持续运行，定时检查并广播所有活跃战斗的帧
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "CombatBroadcaster服务已启动，定时器精度={TickIntervalMs}ms，默认频率={DefaultFrequency}Hz",
            _options.TickIntervalMs, _options.DefaultFrequency);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // 广播所有活跃战斗的帧
                    await BroadcastActiveFrames(stoppingToken);
                    
                    // 等待下一个检查周期
                    await Task.Delay(_options.TickIntervalMs, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    // 正常停止，不记录错误
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "CombatBroadcaster执行循环出错");
                    // 出错后等待一段时间，避免错误循环消耗CPU
                    await Task.Delay(1000, stoppingToken);
                }
            }
        }
        finally
        {
            _logger.LogInformation("CombatBroadcaster服务已停止，活跃战斗数量: {Count}", _activeBattles.Count);
        }
    }

    /// <summary>
    /// 广播所有活跃战斗的帧
    /// 检查每个战斗是否到了广播时间，如果是则生成并发送帧
    /// </summary>
    private async Task BroadcastActiveFrames(CancellationToken ct)
    {
        var now = DateTime.UtcNow;

        foreach (var (battleId, config) in _activeBattles)
        {
            if (ct.IsCancellationRequested)
                break;

            try
            {
                // 计算距离上次广播的时间间隔
                var intervalMs = 1000.0 / config.Frequency;
                var elapsed = (now - config.LastBroadcast).TotalMilliseconds;

                // 如果达到广播间隔，执行广播
                if (elapsed >= intervalMs)
                {
                    await BroadcastBattleFrame(battleId, config);
                    config.LastBroadcast = now;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "广播战斗 {BattleId} 时出错", battleId);
            }
        }
    }

    /// <summary>
    /// 广播单个战斗的帧
    /// 生成帧数据并通过SignalR推送到订阅的客户端
    /// </summary>
    private async Task BroadcastBattleFrame(string battleId, BattleFrameConfig config)
    {
        try
        {
            // TODO: 从BattleManager获取战斗实例
            // 当前阶段暂时不实现，等待与战斗系统集成
            // var battle = await _battleManager.GetBattleAsync(battleId);
            // if (battle == null)
            // {
            //     // 战斗已结束，停止广播
            //     if (_options.AutoCleanupFinishedBattles)
            //     {
            //         _ = Task.Delay(TimeSpan.FromSeconds(_options.CleanupDelaySeconds))
            //             .ContinueWith(_ => StopBroadcast(battleId));
            //     }
            //     return;
            // }

            // TODO: 生成帧数据
            // var frame = battle.GenerateFrameTick();
            
            // TODO: 缓存帧用于补发（下一步骤实现）
            // config.FrameBuffer?.AddFrame(frame);

            // 推送到战斗组
            var groupName = $"battle:{battleId}";
            // await _dispatcher.SendToGroupAsync(groupName, "BattleFrame", frame, MessagePriority.High);

            // 更新帧计数
            config.FrameCount++;

            // 定期生成快照
            if (config.FrameCount % _options.SnapshotIntervalFrames == 0)
            {
                await GenerateAndBroadcastSnapshot(battleId, config);
            }

            // 详细日志（仅在启用时）
            if (_options.EnableDetailedLogging)
            {
                _logger.LogDebug(
                    "已广播战斗 {BattleId} 的第 {FrameCount} 帧，频率={Frequency}Hz",
                    battleId, config.FrameCount, config.Frequency);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "生成或广播战斗 {BattleId} 的帧数据时出错", battleId);
        }
    }

    /// <summary>
    /// 生成并广播战斗快照
    /// 快照包含完整的战斗状态，用于断线重连
    /// </summary>
    private async Task GenerateAndBroadcastSnapshot(string battleId, BattleFrameConfig config)
    {
        try
        {
            // TODO: 生成快照
            // var snapshot = battle.GenerateSnapshot();
            // config.LastSnapshot = snapshot;

            // 推送快照
            var groupName = $"battle:{battleId}";
            // await _dispatcher.SendToGroupAsync(groupName, "BattleSnapshot", snapshot, MessagePriority.High);

            _logger.LogInformation(
                "为战斗 {BattleId} 生成并广播快照，帧计数={FrameCount}",
                battleId, config.FrameCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "生成或广播战斗 {BattleId} 的快照时出错", battleId);
        }
    }

    /// <summary>
    /// 开始广播指定战斗
    /// 将战斗添加到活跃列表，开始定时推送帧数据
    /// </summary>
    /// <param name="battleId">战斗ID</param>
    /// <param name="frequency">广播频率（Hz），如果未指定则使用默认频率</param>
    public void StartBroadcast(string battleId, int? frequency = null)
    {
        if (string.IsNullOrWhiteSpace(battleId))
            throw new ArgumentException("战斗ID不能为空", nameof(battleId));

        // 检查并发数量限制
        if (_options.MaxConcurrentBattles > 0 && 
            _activeBattles.Count >= _options.MaxConcurrentBattles)
        {
            _logger.LogWarning(
                "已达到最大并发战斗数量 {MaxCount}，无法开始广播战斗 {BattleId}",
                _options.MaxConcurrentBattles, battleId);
            return;
        }

        // 限制频率在有效范围内
        var actualFrequency = frequency ?? _options.DefaultFrequency;
        actualFrequency = Math.Clamp(actualFrequency, _options.MinFrequency, _options.MaxFrequency);
        
        // 创建或更新战斗配置
        var config = new BattleFrameConfig
        {
            Frequency = actualFrequency,
            LastBroadcast = DateTime.UtcNow.AddSeconds(-1), // 立即触发第一帧
            FrameCount = 0
        };

        if (_activeBattles.TryAdd(battleId, config))
        {
            _logger.LogInformation(
                "开始广播战斗 {BattleId}，频率={Frequency}Hz，当前活跃战斗数={Count}",
                battleId, actualFrequency, _activeBattles.Count);
        }
        else
        {
            // 战斗已存在，更新频率
            _activeBattles[battleId] = config;
            _logger.LogInformation(
                "更新战斗 {BattleId} 的广播配置，新频率={Frequency}Hz",
                battleId, actualFrequency);
        }
    }

    /// <summary>
    /// 停止广播指定战斗
    /// 将战斗从活跃列表移除，停止推送帧数据
    /// </summary>
    /// <param name="battleId">战斗ID</param>
    public void StopBroadcast(string battleId)
    {
        if (string.IsNullOrWhiteSpace(battleId))
            return;

        if (_activeBattles.TryRemove(battleId, out var config))
        {
            _logger.LogInformation(
                "停止广播战斗 {BattleId}，共广播了 {FrameCount} 帧，剩余活跃战斗数={Count}",
                battleId, config.FrameCount, _activeBattles.Count);
        }
        else
        {
            _logger.LogDebug("尝试停止不存在的战斗广播: {BattleId}", battleId);
        }
    }

    /// <summary>
    /// 设置指定战斗的广播频率
    /// 动态调整帧推送频率，适应不同场景需求
    /// </summary>
    /// <param name="battleId">战斗ID</param>
    /// <param name="frequency">新的广播频率（Hz）</param>
    public void SetFrequency(string battleId, int frequency)
    {
        if (string.IsNullOrWhiteSpace(battleId))
            return;

        if (_activeBattles.TryGetValue(battleId, out var config))
        {
            // 限制频率在有效范围内
            var actualFrequency = Math.Clamp(frequency, _options.MinFrequency, _options.MaxFrequency);
            config.Frequency = actualFrequency;
            
            _logger.LogDebug(
                "更新战斗 {BattleId} 的广播频率为 {Frequency}Hz",
                battleId, actualFrequency);
        }
        else
        {
            _logger.LogWarning("尝试设置不存在的战斗 {BattleId} 的频率", battleId);
        }
    }

    /// <summary>
    /// 推送关键事件
    /// 关键事件使用Critical优先级立即推送，不等待下一个帧周期
    /// </summary>
    /// <param name="battleId">战斗ID</param>
    /// <param name="keyEvent">关键事件数据</param>
    public async Task BroadcastKeyEvent(string battleId, KeyEvent keyEvent)
    {
        if (string.IsNullOrWhiteSpace(battleId))
            throw new ArgumentException("战斗ID不能为空", nameof(battleId));
        
        if (keyEvent == null)
            throw new ArgumentNullException(nameof(keyEvent));

        try
        {
            var groupName = $"battle:{battleId}";
            await _dispatcher.SendToGroupAsync(
                groupName,
                "KeyEvent",
                keyEvent,
                MessagePriority.Critical);

            _logger.LogDebug(
                "广播关键事件到战斗 {BattleId}，类型={EventType}，版本={Version}",
                battleId, keyEvent.Type, keyEvent.Version);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "广播关键事件到战斗 {BattleId} 时出错，事件类型={EventType}",
                battleId, keyEvent.Type);
        }
    }

    /// <summary>
    /// 推送快照
    /// 手动触发快照推送，用于特殊场景（如客户端请求完整状态）
    /// </summary>
    /// <param name="battleId">战斗ID</param>
    /// <param name="snapshot">快照数据</param>
    public async Task BroadcastSnapshot(string battleId, BattleSnapshot snapshot)
    {
        if (string.IsNullOrWhiteSpace(battleId))
            throw new ArgumentException("战斗ID不能为空", nameof(battleId));
        
        if (snapshot == null)
            throw new ArgumentNullException(nameof(snapshot));

        try
        {
            var groupName = $"battle:{battleId}";
            await _dispatcher.SendToGroupAsync(
                groupName,
                "BattleSnapshot",
                snapshot,
                MessagePriority.High);

            _logger.LogInformation(
                "广播快照到战斗 {BattleId}，版本={Version}",
                battleId, snapshot.Version);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "广播快照到战斗 {BattleId} 时出错",
                battleId);
        }
    }

    /// <summary>
    /// 获取活跃战斗数量
    /// 用于监控和统计
    /// </summary>
    /// <returns>当前正在广播的战斗数量</returns>
    public int GetActiveBattleCount() => _activeBattles.Count;

    /// <summary>
    /// 获取指定战斗的配置
    /// 用于查询战斗的当前状态和统计信息
    /// </summary>
    /// <param name="battleId">战斗ID</param>
    /// <returns>战斗配置，如果战斗不存在则返回null</returns>
    public BattleFrameConfig? GetBattleConfig(string battleId)
    {
        _activeBattles.TryGetValue(battleId, out var config);
        return config;
    }
}

/// <summary>
/// 战斗帧配置
/// 存储单个战斗的广播配置和状态
/// </summary>
public class BattleFrameConfig
{
    /// <summary>
    /// 广播频率（Hz）
    /// 每秒推送的帧数
    /// </summary>
    public int Frequency { get; set; } = 8;
    
    /// <summary>
    /// 上次广播时间
    /// 用于计算下次广播的时机
    /// </summary>
    public DateTime LastBroadcast { get; set; }
    
    /// <summary>
    /// 已广播的帧数
    /// 从战斗开始累计的帧计数
    /// </summary>
    public long FrameCount { get; set; }
    
    /// <summary>
    /// 最后一次快照
    /// 用于断线重连时的状态恢复
    /// </summary>
    public BattleSnapshot? LastSnapshot { get; set; }
}
