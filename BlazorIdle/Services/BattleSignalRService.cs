using BlazorIdle.Shared.Models.Notifications;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace BlazorIdle.Services;

/// <summary>
/// 战斗SignalR服务 - 客户端
/// 管理SignalR连接生命周期并处理战斗事件通知
/// </summary>
public class BattleSignalRService : IAsyncDisposable
{
    private readonly ILogger<BattleSignalRService> _logger;
    private readonly string _hubUrl;
    private HubConnection? _connection;
    private readonly List<Action<StateChangedEvent>> _stateChangedHandlers = new();
    private bool _isDisposed;

    public BattleSignalRService(
        IConfiguration configuration,
        ILogger<BattleSignalRService> logger)
    {
        _logger = logger;
        
        // 从配置读取API基础URL
        var apiBase = configuration["ApiBaseUrl"] ?? "https://localhost:7056";
        var hubPath = configuration["SignalR:HubPath"] ?? "/hubs/battle";
        _hubUrl = $"{apiBase}{hubPath}";
    }

    /// <summary>
    /// 连接状态
    /// </summary>
    public HubConnectionState ConnectionState => 
        _connection?.State ?? HubConnectionState.Disconnected;

    /// <summary>
    /// 连接到SignalR Hub
    /// </summary>
    public async Task ConnectAsync()
    {
        if (_connection != null && _connection.State == HubConnectionState.Connected)
        {
            _logger.LogDebug("SignalR已连接，跳过重复连接");
            return;
        }

        try
        {
            _connection = new HubConnectionBuilder()
                .WithUrl(_hubUrl)
                .WithAutomaticReconnect(new[] 
                { 
                    TimeSpan.Zero,
                    TimeSpan.FromSeconds(2),
                    TimeSpan.FromSeconds(10),
                    TimeSpan.FromSeconds(30)
                })
                .Build();

            // 注册状态变更事件处理器
            _connection.On<object>("StateChanged", HandleStateChanged);

            // 注册连接事件
            _connection.Closed += OnConnectionClosed;
            _connection.Reconnecting += OnReconnecting;
            _connection.Reconnected += OnReconnected;

            await _connection.StartAsync();
            _logger.LogInformation("SignalR连接成功: {HubUrl}", _hubUrl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SignalR连接失败: {HubUrl}", _hubUrl);
            throw;
        }
    }

    /// <summary>
    /// 订阅特定战斗的通知
    /// </summary>
    public async Task SubscribeBattleAsync(Guid battleId)
    {
        if (_connection?.State != HubConnectionState.Connected)
        {
            _logger.LogWarning("SignalR未连接，无法订阅战斗: {BattleId}", battleId);
            return;
        }

        try
        {
            await _connection.InvokeAsync("SubscribeBattle", battleId);
            _logger.LogInformation("已订阅战斗通知: {BattleId}", battleId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "订阅战斗通知失败: {BattleId}", battleId);
        }
    }

    /// <summary>
    /// 取消订阅特定战斗的通知
    /// </summary>
    public async Task UnsubscribeBattleAsync(Guid battleId)
    {
        if (_connection?.State != HubConnectionState.Connected)
        {
            return;
        }

        try
        {
            await _connection.InvokeAsync("UnsubscribeBattle", battleId);
            _logger.LogInformation("已取消订阅战斗通知: {BattleId}", battleId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "取消订阅战斗通知失败: {BattleId}", battleId);
        }
    }

    /// <summary>
    /// 注册状态变更事件处理器
    /// </summary>
    public void OnStateChanged(Action<StateChangedEvent> handler)
    {
        _stateChangedHandlers.Add(handler);
    }

    /// <summary>
    /// 移除状态变更事件处理器
    /// </summary>
    public void RemoveStateChangedHandler(Action<StateChangedEvent> handler)
    {
        _stateChangedHandlers.Remove(handler);
    }

    /// <summary>
    /// 处理状态变更通知
    /// </summary>
    private void HandleStateChanged(object notification)
    {
        try
        {
            // 解析通知数据
            var eventType = notification.GetType().GetProperty("eventType")?.GetValue(notification)?.ToString();
            var timestamp = notification.GetType().GetProperty("timestamp")?.GetValue(notification);

            if (string.IsNullOrEmpty(eventType))
            {
                _logger.LogWarning("收到无效的状态变更通知");
                return;
            }

            var evt = new StateChangedEvent(
                eventType,
                timestamp is DateTime dt ? dt : DateTime.UtcNow
            );

            _logger.LogDebug("收到状态变更通知: {EventType}", eventType);

            // 触发所有注册的处理器
            foreach (var handler in _stateChangedHandlers.ToList())
            {
                try
                {
                    handler(evt);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "处理状态变更事件失败: {EventType}", eventType);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "解析状态变更通知失败");
        }
    }

    private Task OnConnectionClosed(Exception? exception)
    {
        if (exception != null)
        {
            _logger.LogWarning(exception, "SignalR连接关闭");
        }
        else
        {
            _logger.LogInformation("SignalR连接正常关闭");
        }
        return Task.CompletedTask;
    }

    private Task OnReconnecting(Exception? exception)
    {
        _logger.LogWarning(exception, "SignalR正在重连...");
        return Task.CompletedTask;
    }

    private Task OnReconnected(string? connectionId)
    {
        _logger.LogInformation("SignalR重连成功: {ConnectionId}", connectionId);
        return Task.CompletedTask;
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        _stateChangedHandlers.Clear();

        if (_connection != null)
        {
            try
            {
                await _connection.DisposeAsync();
                _logger.LogInformation("SignalR连接已释放");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "释放SignalR连接失败");
            }
        }

        GC.SuppressFinalize(this);
    }
}
