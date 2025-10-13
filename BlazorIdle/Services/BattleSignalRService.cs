using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using BlazorIdle.Shared.Models;
using BlazorIdle.Client.Config;

namespace BlazorIdle.Client.Services;

/// <summary>
/// SignalR 客户端服务，管理战斗通知连接
/// </summary>
public sealed class BattleSignalRService : IAsyncDisposable
{
    private readonly ILogger<BattleSignalRService> _logger;
    private readonly AuthService _authService;
    private readonly SignalRClientOptions _options;
    private HubConnection? _connection;
    private bool _isConnecting;
    private readonly string _hubUrl;
    
    // 事件处理器
    private readonly List<Action<StateChangedEvent>> _stateChangedHandlers = new();
    
    // 连接状态事件
    public event Func<Task>? Connected;
    public event Func<Exception?, Task>? Disconnected;
    public event Func<Exception?, Task>? Reconnecting;
    public event Func<string?, Task>? Reconnected;

    public BattleSignalRService(
        ILogger<BattleSignalRService> logger,
        AuthService authService,
        IConfiguration configuration,
        IOptions<SignalRClientOptions>? options = null)
    {
        _logger = logger;
        _authService = authService;
        
        // 优先使用依赖注入的选项，否则从配置读取
        if (options?.Value != null)
        {
            _options = options.Value;
        }
        else
        {
            // 向后兼容：从配置直接读取
            var signalRConfig = configuration.GetSection("SignalR");
            _options = new SignalRClientOptions
            {
                EnableSignalR = signalRConfig.GetValue<bool>("EnableSignalR", true),
                MaxReconnectAttempts = signalRConfig.GetValue<int>("MaxReconnectAttempts", 5),
                ReconnectBaseDelayMs = signalRConfig.GetValue<int>("ReconnectBaseDelayMs", 1000),
                MaxReconnectDelayMs = signalRConfig.GetValue<int>("MaxReconnectDelayMs", 30000),
                EnableDetailedLogging = signalRConfig.GetValue<bool>("EnableDetailedLogging", false),
                ConnectionTimeoutSeconds = signalRConfig.GetValue<int>("ConnectionTimeoutSeconds", 30),
                KeepAliveIntervalSeconds = signalRConfig.GetValue<int>("KeepAliveIntervalSeconds", 15),
                ServerTimeoutSeconds = signalRConfig.GetValue<int>("ServerTimeoutSeconds", 30),
                EnableAutomaticReconnect = signalRConfig.GetValue<bool>("EnableAutomaticReconnect", true),
                HubEndpoint = signalRConfig.GetValue<string>("HubEndpoint", "/hubs/battle") ?? "/hubs/battle"
            };
        }
        
        // 构建 Hub URL
        var apiBaseUrl = configuration["ApiBaseUrl"] ?? "https://localhost:7001";
        _hubUrl = $"{apiBaseUrl}{_options.HubEndpoint}";
    }

    /// <summary>
    /// 连接状态
    /// </summary>
    public bool IsConnected => _connection?.State == HubConnectionState.Connected;

    /// <summary>
    /// SignalR 是否可用
    /// </summary>
    public bool IsAvailable => _options.EnableSignalR && _connection != null;
    
    /// <summary>
    /// 当前连接状态
    /// </summary>
    public HubConnectionState? ConnectionState => _connection?.State;

    /// <summary>
    /// 连接到 SignalR Hub
    /// </summary>
    public async Task<bool> ConnectAsync()
    {
        if (!_options.EnableSignalR)
        {
            if (_options.EnableDetailedLogging)
            {
                _logger.LogDebug("SignalR is disabled in configuration");
            }
            return false;
        }

        if (_isConnecting)
        {
            if (_options.EnableDetailedLogging)
            {
                _logger.LogDebug("Already connecting to SignalR");
            }
            return false;
        }

        if (IsConnected)
        {
            if (_options.EnableDetailedLogging)
            {
                _logger.LogDebug("Already connected to SignalR");
            }
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
            var builder = new HubConnectionBuilder()
                .WithUrl(_hubUrl, options =>
                {
                    options.AccessTokenProvider = () => Task.FromResult<string?>(token);
                });

            // 配置自动重连（如果启用）
            if (_options.EnableAutomaticReconnect)
            {
                builder.WithAutomaticReconnect(
                    new SignalRRetryPolicy(
                        _options.MaxReconnectAttempts, 
                        _options.ReconnectBaseDelayMs,
                        _options.MaxReconnectDelayMs
                    )
                );
            }
            
            // 配置日志级别
            builder.ConfigureLogging(logging =>
            {
                if (_options.EnableDetailedLogging)
                {
                    logging.AddFilter("Microsoft.AspNetCore.SignalR", LogLevel.Debug);
                    logging.SetMinimumLevel(LogLevel.Debug);
                }
                else
                {
                    logging.AddFilter("Microsoft.AspNetCore.SignalR", LogLevel.Information);
                    logging.SetMinimumLevel(LogLevel.Information);
                }
            });
            
            _connection = builder.Build();

            // 注册事件处理器
            _connection.On<StateChangedEvent>("StateChanged", OnStateChanged);

            // 连接管理事件
            _connection.Closed += OnConnectionClosed;
            _connection.Reconnecting += OnReconnecting;
            _connection.Reconnected += OnReconnected;

            // 启动连接
            await _connection.StartAsync();
            
            _logger.LogInformation("Connected to SignalR Hub: {HubUrl}", _hubUrl);
            
            // 触发连接事件
            if (Connected != null)
            {
                await Connected.Invoke();
            }
            
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

    private async Task OnConnectionClosed(Exception? exception)
    {
        if (exception != null)
        {
            _logger.LogWarning(exception, "SignalR connection closed with error");
        }
        else
        {
            _logger.LogInformation("SignalR connection closed");
        }
        
        // 触发断开连接事件
        if (Disconnected != null)
        {
            await Disconnected.Invoke(exception);
        }
    }

    private async Task OnReconnecting(Exception? exception)
    {
        _logger.LogWarning(exception, "SignalR reconnecting...");
        
        // 触发重连事件
        if (Reconnecting != null)
        {
            await Reconnecting.Invoke(exception);
        }
    }

    private async Task OnReconnected(string? connectionId)
    {
        _logger.LogInformation("SignalR reconnected: {ConnectionId}", connectionId);
        
        // 触发重连成功事件
        if (Reconnected != null)
        {
            await Reconnected.Invoke(connectionId);
        }
    }
}

/// <summary>
/// SignalR 重连策略
/// 实现指数退避算法，防止过度重连
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
        // 达到最大重试次数，停止重连
        if (retryContext.PreviousRetryCount >= _maxAttempts)
        {
            return null;
        }

        // 指数退避策略：1s, 2s, 4s, 8s, 16s...
        var delayMs = _baseDelayMs * Math.Pow(2, retryContext.PreviousRetryCount);
        
        // 限制最大延迟
        var clampedDelayMs = Math.Min(delayMs, _maxDelayMs);
        
        return TimeSpan.FromMilliseconds(clampedDelayMs);
    }
}
