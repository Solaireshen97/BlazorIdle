using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;

namespace BlazorIdle.Client.Services.SignalR;

/// <summary>
/// SignalR连接管理器
/// 负责管理SignalR连接的生命周期，包括连接、断开、重连、心跳等
/// 这是一个全局单例服务，在整个应用程序中共享同一个连接
/// </summary>
public class SignalRConnectionManager : IAsyncDisposable
{
    private readonly ILogger<SignalRConnectionManager> _logger;
    private readonly SignalRClientOptions _options;
    private HubConnection? _connection;
    private PeriodicTimer? _heartbeatTimer;
    private Task? _heartbeatTask;
    private CancellationTokenSource? _heartbeatCts;
    private readonly Dictionary<string, List<Delegate>> _messageHandlers = new();
    private readonly object _handlersLock = new();
    private bool _isDisposed;

    /// <summary>
    /// 连接状态变化事件
    /// 当连接成功建立时触发
    /// </summary>
    public event Func<Task>? Connected;

    /// <summary>
    /// 连接断开事件
    /// 当连接断开时触发，包含断开原因
    /// </summary>
    public event Func<Exception?, Task>? Disconnected;

    /// <summary>
    /// 重连中事件
    /// 当开始尝试重连时触发
    /// </summary>
    public event Func<string, Task>? Reconnecting;

    /// <summary>
    /// 重连成功事件
    /// 当重连成功后触发，包含新的连接ID
    /// </summary>
    public event Func<string?, Task>? Reconnected;

    /// <summary>
    /// 获取当前连接状态
    /// </summary>
    public HubConnectionState State => _connection?.State ?? HubConnectionState.Disconnected;

    /// <summary>
    /// 检查是否已连接
    /// </summary>
    public bool IsConnected => _connection?.State == HubConnectionState.Connected;

    /// <summary>
    /// 获取当前连接ID
    /// 如果未连接则返回null
    /// </summary>
    public string? ConnectionId => _connection?.ConnectionId;

    public SignalRConnectionManager(
        ILogger<SignalRConnectionManager> logger,
        SignalRClientOptions options)
    {
        _logger = logger;
        _options = options;
        
        // 验证配置有效性
        _options.Validate();
        
        _logger.LogInformation("SignalRConnectionManager已创建，Hub URL: {HubUrl}", _options.HubUrl);
    }

    /// <summary>
    /// 初始化SignalR连接
    /// 配置连接参数、重连策略、事件处理等
    /// </summary>
    /// <param name="accessToken">访问令牌，用于身份验证（可选）</param>
    /// <returns>异步任务</returns>
    public async Task InitializeAsync(string? accessToken = null)
    {
        if (_isDisposed)
        {
            throw new ObjectDisposedException(nameof(SignalRConnectionManager));
        }

        // 如果已有连接，先释放
        if (_connection != null)
        {
            _logger.LogInformation("检测到现有连接，正在释放...");
            await DisposeConnectionAsync();
        }

        _logger.LogInformation("开始初始化SignalR连接...");

        // 创建连接构建器
        var builder = new HubConnectionBuilder()
            .WithUrl(_options.HubUrl, options =>
            {
                // 如果提供了访问令牌，配置身份验证
                if (!string.IsNullOrEmpty(accessToken))
                {
                    options.AccessTokenProvider = () => Task.FromResult<string?>(accessToken);
                }
            });

        // 配置自动重连策略
        if (_options.EnableAutoReconnect)
        {
            var delays = _options.ReconnectDelaysMs.Select(ms => TimeSpan.FromMilliseconds(ms)).ToArray();
            builder.WithAutomaticReconnect(delays);
            _logger.LogInformation("已启用自动重连，重连策略: {Delays}ms", string.Join(", ", _options.ReconnectDelaysMs));
        }

        // 配置日志级别
        builder.ConfigureLogging(logging =>
        {
            logging.SetMinimumLevel(_options.EnableDetailedLogging ? LogLevel.Debug : LogLevel.Information);
        });

        // 构建连接
        _connection = builder.Build();

        // 注册连接生命周期事件
        _connection.Closed += OnClosedAsync;
        _connection.Reconnecting += OnReconnectingAsync;
        _connection.Reconnected += OnReconnectedAsync;

        // 注册基础消息处理器
        // 这些是服务器发送的标准消息
        _connection.On<object>("Connected", OnConnectedMessageAsync);
        _connection.On<string>("Error", OnErrorMessageAsync);
        _connection.On<string, string>("Subscribed", OnSubscribedMessageAsync);
        _connection.On<string, string>("Unsubscribed", OnUnsubscribedMessageAsync);

        _logger.LogInformation("SignalR连接初始化完成");
    }

    /// <summary>
    /// 启动SignalR连接
    /// 建立与服务器的WebSocket连接
    /// </summary>
    /// <returns>异步任务</returns>
    /// <exception cref="InvalidOperationException">连接未初始化时抛出</exception>
    public async Task StartAsync()
    {
        if (_isDisposed)
        {
            throw new ObjectDisposedException(nameof(SignalRConnectionManager));
        }

        if (_connection == null)
        {
            throw new InvalidOperationException("连接未初始化，请先调用InitializeAsync");
        }

        try
        {
            _logger.LogInformation("正在启动SignalR连接...");
            
            // 使用超时机制防止长时间阻塞
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(_options.ConnectionTimeoutSeconds));
            await _connection.StartAsync(cts.Token);
            
            _logger.LogInformation("SignalR连接启动成功，ConnectionId: {ConnectionId}", _connection.ConnectionId);

            // 启动心跳检测
            if (_options.EnableHeartbeat)
            {
                StartHeartbeat();
            }

            // 触发连接成功事件
            if (Connected != null)
            {
                await InvokeEventHandlerAsync(Connected);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogError("SignalR连接启动超时（{Timeout}秒）", _options.ConnectionTimeoutSeconds);
            throw new TimeoutException($"连接超时，未能在{_options.ConnectionTimeoutSeconds}秒内建立连接");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SignalR连接启动失败");
            throw;
        }
    }

    /// <summary>
    /// 停止SignalR连接
    /// 优雅地关闭连接并停止心跳
    /// </summary>
    /// <returns>异步任务</returns>
    public async Task StopAsync()
    {
        if (_connection != null && _connection.State != HubConnectionState.Disconnected)
        {
            _logger.LogInformation("正在停止SignalR连接...");
            
            // 先停止心跳
            StopHeartbeat();
            
            try
            {
                await _connection.StopAsync();
                _logger.LogInformation("SignalR连接已停止");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "停止SignalR连接时发生错误");
            }
        }
    }

    /// <summary>
    /// 调用服务器方法并等待返回结果
    /// </summary>
    /// <typeparam name="T">返回值类型</typeparam>
    /// <param name="methodName">服务器方法名</param>
    /// <param name="args">方法参数</param>
    /// <returns>服务器返回的结果</returns>
    public async Task<T?> InvokeAsync<T>(string methodName, params object[] args)
    {
        EnsureConnected(methodName);

        try
        {
            _logger.LogDebug("调用服务器方法: {Method}", methodName);
            return await _connection!.InvokeAsync<T>(methodName, args);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "调用服务器方法 {Method} 失败", methodName);
            throw;
        }
    }

    /// <summary>
    /// 发送消息到服务器（不等待返回）
    /// </summary>
    /// <param name="methodName">服务器方法名</param>
    /// <param name="args">方法参数</param>
    /// <returns>异步任务</returns>
    public async Task SendAsync(string methodName, params object[] args)
    {
        EnsureConnected(methodName);

        try
        {
            _logger.LogDebug("发送消息到服务器: {Method}", methodName);
            await _connection!.SendAsync(methodName, args);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "发送消息 {Method} 失败", methodName);
            throw;
        }
    }

    /// <summary>
    /// 注册消息处理器（单个参数）
    /// 用于接收服务器推送的消息
    /// </summary>
    /// <typeparam name="T">消息参数类型</typeparam>
    /// <param name="methodName">消息方法名</param>
    /// <param name="handler">消息处理函数</param>
    /// <returns>可用于取消注册的IDisposable对象</returns>
    public IDisposable On<T>(string methodName, Func<T, Task> handler)
    {
        if (_connection == null)
        {
            throw new InvalidOperationException("连接未初始化，请先调用InitializeAsync");
        }

        _logger.LogDebug("注册消息处理器: {Method}", methodName);
        
        // 记录处理器以便管理
        lock (_handlersLock)
        {
            if (!_messageHandlers.ContainsKey(methodName))
            {
                _messageHandlers[methodName] = new List<Delegate>();
            }
            _messageHandlers[methodName].Add(handler);
        }

        // 注册到SignalR连接
        return _connection.On(methodName, handler);
    }

    /// <summary>
    /// 注册消息处理器（两个参数）
    /// 用于接收服务器推送的消息
    /// </summary>
    /// <typeparam name="T1">第一个参数类型</typeparam>
    /// <typeparam name="T2">第二个参数类型</typeparam>
    /// <param name="methodName">消息方法名</param>
    /// <param name="handler">消息处理函数</param>
    /// <returns>可用于取消注册的IDisposable对象</returns>
    public IDisposable On<T1, T2>(string methodName, Func<T1, T2, Task> handler)
    {
        if (_connection == null)
        {
            throw new InvalidOperationException("连接未初始化，请先调用InitializeAsync");
        }

        _logger.LogDebug("注册消息处理器: {Method}", methodName);
        
        // 记录处理器以便管理
        lock (_handlersLock)
        {
            if (!_messageHandlers.ContainsKey(methodName))
            {
                _messageHandlers[methodName] = new List<Delegate>();
            }
            _messageHandlers[methodName].Add(handler);
        }

        // 注册到SignalR连接
        return _connection.On(methodName, handler);
    }

    /// <summary>
    /// 注册消息处理器（三个参数）
    /// 用于接收服务器推送的消息
    /// </summary>
    /// <typeparam name="T1">第一个参数类型</typeparam>
    /// <typeparam name="T2">第二个参数类型</typeparam>
    /// <typeparam name="T3">第三个参数类型</typeparam>
    /// <param name="methodName">消息方法名</param>
    /// <param name="handler">消息处理函数</param>
    /// <returns>可用于取消注册的IDisposable对象</returns>
    public IDisposable On<T1, T2, T3>(string methodName, Func<T1, T2, T3, Task> handler)
    {
        if (_connection == null)
        {
            throw new InvalidOperationException("连接未初始化，请先调用InitializeAsync");
        }

        _logger.LogDebug("注册消息处理器: {Method}", methodName);
        
        // 记录处理器以便管理
        lock (_handlersLock)
        {
            if (!_messageHandlers.ContainsKey(methodName))
            {
                _messageHandlers[methodName] = new List<Delegate>();
            }
            _messageHandlers[methodName].Add(handler);
        }

        // 注册到SignalR连接
        return _connection.On(methodName, handler);
    }

    /// <summary>
    /// 订阅战斗更新
    /// 将客户端加入指定战斗的SignalR组，以接收战斗相关消息
    /// </summary>
    /// <param name="battleId">战斗ID</param>
    /// <returns>异步任务</returns>
    public async Task SubscribeToBattleAsync(string battleId)
    {
        EnsureConnected(nameof(SubscribeToBattleAsync));
        
        _logger.LogInformation("订阅战斗更新: {BattleId}", battleId);
        await _connection!.SendAsync("SubscribeToBattle", battleId);
    }

    /// <summary>
    /// 取消订阅战斗更新
    /// 将客户端从指定战斗的SignalR组中移除
    /// </summary>
    /// <param name="battleId">战斗ID</param>
    /// <returns>异步任务</returns>
    public async Task UnsubscribeFromBattleAsync(string battleId)
    {
        EnsureConnected(nameof(UnsubscribeFromBattleAsync));
        
        _logger.LogInformation("取消订阅战斗更新: {BattleId}", battleId);
        await _connection!.SendAsync("UnsubscribeFromBattle", battleId);
    }

    /// <summary>
    /// 订阅队伍更新
    /// 将客户端加入指定队伍的SignalR组，以接收队伍相关消息
    /// </summary>
    /// <param name="partyId">队伍ID</param>
    /// <returns>异步任务</returns>
    public async Task SubscribeToPartyAsync(string partyId)
    {
        EnsureConnected(nameof(SubscribeToPartyAsync));
        
        _logger.LogInformation("订阅队伍更新: {PartyId}", partyId);
        await _connection!.SendAsync("SubscribeToParty", partyId);
    }

    /// <summary>
    /// 请求战斗状态同步
    /// 用于断线重连后恢复战斗状态
    /// </summary>
    /// <param name="battleId">战斗ID</param>
    /// <param name="lastVersion">客户端最后接收的版本号</param>
    /// <returns>异步任务</returns>
    public async Task RequestBattleSyncAsync(string battleId, long lastVersion)
    {
        EnsureConnected(nameof(RequestBattleSyncAsync));
        
        _logger.LogInformation("请求战斗状态同步: BattleId={BattleId}, LastVersion={LastVersion}", 
            battleId, lastVersion);
        await _connection!.SendAsync("RequestBattleSync", battleId, lastVersion);
    }

    #region 心跳检测

    /// <summary>
    /// 启动心跳检测定时器
    /// 定期向服务器发送心跳消息以保持连接活跃
    /// </summary>
    private void StartHeartbeat()
    {
        // 如果已有心跳任务在运行，先停止
        if (_heartbeatTask != null)
        {
            StopHeartbeat();
        }

        _logger.LogInformation("启动心跳检测，间隔: {Interval}秒", _options.HeartbeatIntervalSeconds);

        _heartbeatCts = new CancellationTokenSource();
        _heartbeatTimer = new PeriodicTimer(TimeSpan.FromSeconds(_options.HeartbeatIntervalSeconds));
        
        _heartbeatTask = Task.Run(async () =>
        {
            try
            {
                // 定期执行心跳
                while (await _heartbeatTimer.WaitForNextTickAsync(_heartbeatCts.Token))
                {
                    try
                    {
                        // 只在连接状态下发送心跳
                        if (_connection?.State == HubConnectionState.Connected)
                        {
                            _logger.LogDebug("发送心跳");
                            await _connection.SendAsync("Heartbeat", _heartbeatCts.Token);
                        }
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        _logger.LogWarning(ex, "心跳发送失败");
                    }
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogDebug("心跳任务已取消");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "心跳任务异常");
            }
        }, _heartbeatCts.Token);
    }

    /// <summary>
    /// 停止心跳检测定时器
    /// </summary>
    private void StopHeartbeat()
    {
        if (_heartbeatCts != null)
        {
            _logger.LogDebug("停止心跳检测");
            
            _heartbeatCts.Cancel();
            _heartbeatTimer?.Dispose();
            
            // 等待心跳任务完成
            try
            {
                _heartbeatTask?.Wait(TimeSpan.FromSeconds(5));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "等待心跳任务完成时发生错误");
            }
            
            _heartbeatCts.Dispose();
            _heartbeatCts = null;
            _heartbeatTimer = null;
            _heartbeatTask = null;
        }
    }

    #endregion

    #region 事件处理器

    /// <summary>
    /// 连接关闭事件处理器
    /// 当连接断开时调用
    /// </summary>
    private async Task OnClosedAsync(Exception? exception)
    {
        if (exception != null)
        {
            _logger.LogWarning(exception, "SignalR连接已关闭");
        }
        else
        {
            _logger.LogInformation("SignalR连接已正常关闭");
        }
        
        // 停止心跳
        StopHeartbeat();
        
        // 触发断开事件
        if (Disconnected != null)
        {
            await InvokeEventHandlerAsync(() => Disconnected(exception));
        }
    }

    /// <summary>
    /// 重连中事件处理器
    /// 当开始尝试重连时调用
    /// </summary>
    private async Task OnReconnectingAsync(Exception? exception)
    {
        var message = exception?.Message ?? "未知原因";
        _logger.LogInformation("SignalR正在尝试重连，原因: {Reason}", message);
        
        // 触发重连中事件
        if (Reconnecting != null)
        {
            await InvokeEventHandlerAsync(() => Reconnecting(message));
        }
    }

    /// <summary>
    /// 重连成功事件处理器
    /// 当重连成功后调用
    /// </summary>
    private async Task OnReconnectedAsync(string? connectionId)
    {
        _logger.LogInformation("SignalR重连成功，新ConnectionId: {ConnectionId}", connectionId);
        
        // 重启心跳
        if (_options.EnableHeartbeat)
        {
            StartHeartbeat();
        }
        
        // 触发重连成功事件
        if (Reconnected != null)
        {
            await InvokeEventHandlerAsync(() => Reconnected(connectionId));
        }
    }

    /// <summary>
    /// 处理服务器发送的连接成功消息
    /// </summary>
    private Task OnConnectedMessageAsync(object data)
    {
        _logger.LogInformation("收到服务器连接确认消息: {Data}", data);
        return Task.CompletedTask;
    }

    /// <summary>
    /// 处理服务器发送的错误消息
    /// </summary>
    private Task OnErrorMessageAsync(string error)
    {
        _logger.LogError("收到服务器错误消息: {Error}", error);
        return Task.CompletedTask;
    }

    /// <summary>
    /// 处理服务器发送的订阅成功消息
    /// </summary>
    private Task OnSubscribedMessageAsync(string type, string id)
    {
        _logger.LogInformation("订阅成功: {Type}:{Id}", type, id);
        return Task.CompletedTask;
    }

    /// <summary>
    /// 处理服务器发送的取消订阅消息
    /// </summary>
    private Task OnUnsubscribedMessageAsync(string type, string id)
    {
        _logger.LogInformation("取消订阅: {Type}:{Id}", type, id);
        return Task.CompletedTask;
    }

    #endregion

    #region 辅助方法

    /// <summary>
    /// 确保连接已建立
    /// 如果未连接则抛出异常
    /// </summary>
    /// <param name="operation">操作名称（用于日志）</param>
    /// <exception cref="InvalidOperationException">连接未建立时抛出</exception>
    private void EnsureConnected(string operation)
    {
        if (_connection == null || _connection.State != HubConnectionState.Connected)
        {
            var message = $"无法执行 {operation}：SignalR连接未建立（当前状态: {State}）";
            _logger.LogWarning(message);
            throw new InvalidOperationException(message);
        }
    }

    /// <summary>
    /// 安全地调用事件处理器
    /// 捕获并记录处理器中的异常，避免影响其他处理器
    /// </summary>
    private async Task InvokeEventHandlerAsync(Func<Task> handler)
    {
        try
        {
            await handler();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "事件处理器执行失败");
        }
    }

    /// <summary>
    /// 释放连接资源
    /// </summary>
    private async Task DisposeConnectionAsync()
    {
        if (_connection != null)
        {
            try
            {
                // 停止心跳
                StopHeartbeat();
                
                // 取消事件注册
                _connection.Closed -= OnClosedAsync;
                _connection.Reconnecting -= OnReconnectingAsync;
                _connection.Reconnected -= OnReconnectedAsync;
                
                // 释放连接
                await _connection.DisposeAsync();
                _connection = null;
                
                // 清除消息处理器记录
                lock (_handlersLock)
                {
                    _messageHandlers.Clear();
                }
                
                _logger.LogDebug("连接资源已释放");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "释放连接资源时发生错误");
            }
        }
    }

    #endregion

    /// <summary>
    /// 异步释放资源
    /// 实现IAsyncDisposable接口
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_isDisposed)
        {
            return;
        }

        _logger.LogInformation("正在释放SignalRConnectionManager资源...");
        
        await DisposeConnectionAsync();
        
        _isDisposed = true;
        
        _logger.LogInformation("SignalRConnectionManager资源已释放");
    }
}
