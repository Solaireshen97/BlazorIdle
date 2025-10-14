using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using BlazorIdle.Shared.Models;

namespace BlazorIdle.Client.Services;

/// <summary>
/// SignalR 客户端服务，管理战斗通知连接
/// </summary>
public sealed class BattleSignalRService : IAsyncDisposable
{
    private readonly ILogger<BattleSignalRService> _logger;
    private readonly AuthService _authService;
    private HubConnection? _connection;
    private bool _isConnecting;
    private readonly string _hubUrl;

    // 配置选项（从配置文件读取）
    private readonly bool _enableSignalR;
    private readonly int _maxReconnectAttempts;
    private readonly int _reconnectBaseDelayMs;
    private readonly int _maxReconnectDelayMs;
    private readonly bool _enableDetailedLogging;
    
    // 事件处理器
    private readonly List<Action<StateChangedEvent>> _stateChangedHandlers = new();
    private readonly List<Action<object>> _battleEventHandlers = new();

    public BattleSignalRService(
        ILogger<BattleSignalRService> logger,
        AuthService authService,
        IConfiguration configuration)
    {
        _logger = logger;
        _authService = authService;
        
        // 从配置读取 SignalR 设置
        var signalRConfig = configuration.GetSection("SignalR");
        _enableSignalR = signalRConfig.GetValue<bool>("EnableSignalR", true);
        _maxReconnectAttempts = signalRConfig.GetValue<int>("MaxReconnectAttempts", 5);
        _reconnectBaseDelayMs = signalRConfig.GetValue<int>("ReconnectBaseDelayMs", 1000);
        _maxReconnectDelayMs = signalRConfig.GetValue<int>("MaxReconnectDelayMs", 30000);
        _enableDetailedLogging = signalRConfig.GetValue<bool>("EnableDetailedLogging", false);
        
        // 构建 Hub URL
        var apiBaseUrl = configuration["ApiBaseUrl"] ?? "https://localhost:7001";
        var hubEndpoint = signalRConfig.GetValue<string>("HubEndpoint", "/hubs/battle");
        _hubUrl = $"{apiBaseUrl}{hubEndpoint}";
    }

    /// <summary>
    /// 连接状态
    /// </summary>
    public bool IsConnected => _connection?.State == HubConnectionState.Connected;

    /// <summary>
    /// SignalR 是否可用
    /// </summary>
    public bool IsAvailable => _enableSignalR && _connection != null;

    /// <summary>
    /// 连接到 SignalR Hub
    /// </summary>
    public async Task<bool> ConnectAsync()
    {
        if (!_enableSignalR)
        {
            _logger.LogDebug("SignalR is disabled in configuration");
            return false;
        }

        if (_isConnecting)
        {
            _logger.LogDebug("Already connecting to SignalR");
            return false;
        }

        if (IsConnected)
        {
            _logger.LogDebug("Already connected to SignalR");
            return true;
        }

        try
        {
            _isConnecting = true;

            // 获取认证令牌
            var token = _authService.GetToken();
            if (string.IsNullOrEmpty(token))
            {
                _logger.LogWarning("No authentication token available, cannot connect to SignalR");
                return false;
            }

            // 构建连接
            _connection = new HubConnectionBuilder()
                .WithUrl(_hubUrl, options =>
                {
                    options.AccessTokenProvider = () => Task.FromResult<string?>(token);
                })
                .WithAutomaticReconnect(new SignalRRetryPolicy(_maxReconnectAttempts, _reconnectBaseDelayMs, _maxReconnectDelayMs))
                .ConfigureLogging(logging =>
                {
                    if (_enableDetailedLogging)
                    {
                        logging.AddFilter("Microsoft.AspNetCore.SignalR", LogLevel.Debug);
                        logging.SetMinimumLevel(LogLevel.Debug);
                    }
                    else
                    {
                        logging.AddFilter("Microsoft.AspNetCore.SignalR", LogLevel.Information);
                        logging.SetMinimumLevel(LogLevel.Information);
                    }
                })
                .Build();

            // 注册事件处理器
            _connection.On<StateChangedEvent>("StateChanged", OnStateChanged);
            _connection.On<object>("BattleEvent", OnBattleEvent);

            // 连接管理事件
            _connection.Closed += OnConnectionClosed;
            _connection.Reconnecting += OnReconnecting;
            _connection.Reconnected += OnReconnected;

            // 启动连接
            await _connection.StartAsync();
            
            _logger.LogInformation("Connected to SignalR Hub: {HubUrl}", _hubUrl);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to SignalR Hub: {HubUrl}", _hubUrl);
            return false;
        }
        finally
        {
            _isConnecting = false;
        }
    }

    /// <summary>
    /// 订阅战斗事件
    /// </summary>
    public async Task<bool> SubscribeBattleAsync(Guid battleId)
    {
        if (!IsConnected || _connection == null)
        {
            _logger.LogWarning("Cannot subscribe to battle {BattleId}, not connected", battleId);
            return false;
        }

        try
        {
            await _connection.InvokeAsync("SubscribeBattle", battleId);
            _logger.LogDebug("Subscribed to battle {BattleId}", battleId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to subscribe to battle {BattleId}", battleId);
            return false;
        }
    }

    /// <summary>
    /// 取消订阅战斗
    /// </summary>
    public async Task<bool> UnsubscribeBattleAsync(Guid battleId)
    {
        if (!IsConnected || _connection == null)
        {
            return false;
        }

        try
        {
            await _connection.InvokeAsync("UnsubscribeBattle", battleId);
            _logger.LogDebug("Unsubscribed from battle {BattleId}", battleId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to unsubscribe from battle {BattleId}", battleId);
            return false;
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
    /// 注册战斗事件处理器（用于轻量级增量事件）
    /// </summary>
    public void OnBattleEvent(Action<object> handler)
    {
        _battleEventHandlers.Add(handler);
    }

    /// <summary>
    /// 断开连接
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_connection != null)
        {
            await _connection.DisposeAsync();
            _connection = null;
        }
    }

    // 内部事件处理
    private void OnStateChanged(StateChangedEvent evt)
    {
        _logger.LogDebug(
            "Received StateChanged event: BattleId={BattleId}, EventType={EventType}",
            evt.BattleId,
            evt.EventType
        );

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
    }

    private Task OnConnectionClosed(Exception? exception)
    {
        if (exception != null)
        {
            _logger.LogWarning(exception, "SignalR connection closed with error");
        }
        else
        {
            _logger.LogInformation("SignalR connection closed");
        }
        return Task.CompletedTask;
    }

    private Task OnReconnecting(Exception? exception)
    {
        _logger.LogWarning(exception, "SignalR reconnecting...");
        return Task.CompletedTask;
    }

    private Task OnReconnected(string? connectionId)
    {
        _logger.LogInformation("SignalR reconnected: {ConnectionId}", connectionId);
        return Task.CompletedTask;
    }
    
    private void OnBattleEvent(object eventData)
    {
        if (_enableDetailedLogging)
        {
            _logger.LogDebug(
                "Received BattleEvent: {EventType}",
                eventData.GetType().Name
            );
        }

        foreach (var handler in _battleEventHandlers)
        {
            try
            {
                handler(eventData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in BattleEvent handler");
            }
        }
    }
}

/// <summary>
/// SignalR 重连策略
/// </summary>
internal sealed class SignalRRetryPolicy : IRetryPolicy
{
    private readonly int _maxAttempts;
    private readonly int _baseDelayMs;
    private readonly int _maxDelayMs;

    public SignalRRetryPolicy(int maxAttempts, int baseDelayMs, int maxDelayMs)
    {
        _maxAttempts = maxAttempts;
        _baseDelayMs = baseDelayMs;
        _maxDelayMs = maxDelayMs;
    }

    public TimeSpan? NextRetryDelay(RetryContext retryContext)
    {
        // 指数退避策略
        if (retryContext.PreviousRetryCount >= _maxAttempts)
        {
            return null; // 停止重连
        }

        // 指数退避：1s, 2s, 4s, 8s, 16s...
        var delayMs = _baseDelayMs * Math.Pow(2, retryContext.PreviousRetryCount);
        return TimeSpan.FromMilliseconds(Math.Min(delayMs, _maxDelayMs));
    }
}
