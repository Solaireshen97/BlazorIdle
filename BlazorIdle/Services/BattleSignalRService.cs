using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;

namespace BlazorIdle.Client.Services;

/// <summary>
/// SignalR 客户端服务 - 管理实时战斗通知连接
/// </summary>
public sealed class BattleSignalRService : IAsyncDisposable
{
    private readonly ILogger<BattleSignalRService> _logger;
    private readonly IConfiguration _configuration;
    private HubConnection? _connection;
    private bool _isConnected;
    private readonly List<Action<StateChangedEvent>> _stateChangedHandlers = new();
    private readonly List<Action<object>> _battleEventHandlers = new();

    // 配置参数
    private readonly bool _enabled;
    private readonly string _hubUrl;
    private readonly int _reconnectDelaySeconds;
    private readonly int _maxReconnectAttempts;

    public BattleSignalRService(
        ILogger<BattleSignalRService> logger,
        IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;

        // 读取配置
        _enabled = configuration.GetValue<bool>("SignalR:Enabled", true);
        var apiBase = configuration["ApiBaseUrl"] ?? "https://localhost:7056";
        var hubPath = configuration.GetValue<string>("SignalR:HubPath", "/hubs/battle");
        _hubUrl = $"{apiBase.TrimEnd('/')}{hubPath}";
        _reconnectDelaySeconds = configuration.GetValue<int>("SignalR:ReconnectDelaySeconds", 5);
        _maxReconnectAttempts = configuration.GetValue<int>("SignalR:MaxReconnectAttempts", 5);
    }

    /// <summary>
    /// 连接到 SignalR Hub
    /// </summary>
    public async Task<bool> ConnectAsync()
    {
        if (!_enabled)
        {
            _logger.LogInformation("SignalR is disabled in configuration");
            return false;
        }

        if (_isConnected)
        {
            _logger.LogDebug("Already connected to SignalR Hub");
            return true;
        }

        try
        {
            _connection = new HubConnectionBuilder()
                .WithUrl(_hubUrl)
                .WithAutomaticReconnect(new RetryPolicy(_reconnectDelaySeconds, _maxReconnectAttempts))
                .Build();

            // 注册事件处理器
            _connection.On<StateChangedEvent>("StateChanged", evt =>
            {
                _logger.LogDebug("Received StateChanged event: {EventType}", evt.EventType);
                foreach (var handler in _stateChangedHandlers)
                {
                    try
                    {
                        handler(evt);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error in StateChanged handler");
                    }
                }
            });

            _connection.On<object>("BattleEvent", evt =>
            {
                _logger.LogDebug("Received BattleEvent");
                foreach (var handler in _battleEventHandlers)
                {
                    try
                    {
                        handler(evt);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error in BattleEvent handler");
                    }
                }
            });

            // 连接关闭事件
            _connection.Closed += (error) =>
            {
                _isConnected = false;
                if (error != null)
                {
                    _logger.LogWarning(error, "SignalR connection closed with error");
                }
                else
                {
                    _logger.LogInformation("SignalR connection closed");
                }
                return Task.CompletedTask;
            };

            // 重连事件
            _connection.Reconnecting += (error) =>
            {
                _isConnected = false;
                _logger.LogWarning(error, "SignalR connection lost, reconnecting...");
                return Task.CompletedTask;
            };

            _connection.Reconnected += (connectionId) =>
            {
                _isConnected = true;
                _logger.LogInformation("SignalR reconnected with connection ID: {ConnectionId}", connectionId);
                return Task.CompletedTask;
            };

            await _connection.StartAsync();
            _isConnected = true;
            _logger.LogInformation("Connected to SignalR Hub at {HubUrl}", _hubUrl);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to SignalR Hub at {HubUrl}", _hubUrl);
            return false;
        }
    }

    /// <summary>
    /// 订阅战斗通知
    /// </summary>
    public async Task SubscribeBattleAsync(Guid battleId)
    {
        if (_connection == null || !_isConnected)
        {
            _logger.LogWarning("Cannot subscribe to battle {BattleId}: not connected", battleId);
            return;
        }

        try
        {
            await _connection.InvokeAsync("SubscribeBattle", battleId);
            _logger.LogDebug("Subscribed to battle {BattleId}", battleId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to subscribe to battle {BattleId}", battleId);
        }
    }

    /// <summary>
    /// 取消订阅战斗通知
    /// </summary>
    public async Task UnsubscribeBattleAsync(Guid battleId)
    {
        if (_connection == null || !_isConnected)
        {
            return;
        }

        try
        {
            await _connection.InvokeAsync("UnsubscribeBattle", battleId);
            _logger.LogDebug("Unsubscribed from battle {BattleId}", battleId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to unsubscribe from battle {BattleId}", battleId);
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
    /// 注册战斗事件处理器（Phase 2 扩展）
    /// </summary>
    public void OnBattleEvent(Action<object> handler)
    {
        _battleEventHandlers.Add(handler);
    }

    /// <summary>
    /// 获取连接状态
    /// </summary>
    public bool IsConnected => _isConnected;

    /// <summary>
    /// 断开连接并释放资源
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_connection != null)
        {
            await _connection.DisposeAsync();
            _connection = null;
        }
        _isConnected = false;
    }

    /// <summary>
    /// 自定义重试策略
    /// </summary>
    private class RetryPolicy : IRetryPolicy
    {
        private readonly int _delaySeconds;
        private readonly int _maxAttempts;

        public RetryPolicy(int delaySeconds, int maxAttempts)
        {
            _delaySeconds = delaySeconds;
            _maxAttempts = maxAttempts;
        }

        public TimeSpan? NextRetryDelay(RetryContext retryContext)
        {
            if (retryContext.PreviousRetryCount >= _maxAttempts)
            {
                return null; // 停止重试
            }

            // 指数退避策略
            return TimeSpan.FromSeconds(_delaySeconds * Math.Pow(2, retryContext.PreviousRetryCount));
        }
    }
}

/// <summary>
/// 状态变更事件（与服务器端匹配）
/// </summary>
public sealed class StateChangedEvent
{
    public string EventType { get; set; } = "";
    public DateTime Timestamp { get; set; }
}