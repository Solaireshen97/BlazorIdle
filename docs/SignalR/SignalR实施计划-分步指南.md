# SignalR 实施计划 - 分步指南

**文档版本**: 1.0  
**生成日期**: 2025年10月21日  
**状态**: 实施指导  
**目标**: 为开发人员提供详细的、可按步骤实施的SignalR统一管理系统开发计划

---

## 📚 目录

1. [实施概览](#实施概览)
2. [阶段一：基础架构搭建](#阶段一基础架构搭建)
3. [阶段二：战斗系统集成](#阶段二战斗系统集成)
4. [阶段三：活动与生产系统](#阶段三活动与生产系统)
5. [阶段四：组队与社交系统](#阶段四组队与社交系统)
6. [阶段五：优化与监控](#阶段五优化与监控)
7. [验收标准](#验收标准)
8. [故障排查](#故障排查)

---

## 实施概览

### 总体时间规划

```
阶段一：基础架构搭建        [████████░░░░░░░░░░░░] 2周
阶段二：战斗系统集成        [░░░░░░░░████████░░░░] 2-3周
阶段三：活动与生产系统      [░░░░░░░░░░░░░░██████] 2-3周
阶段四：组队与社交系统      [░░░░░░░░░░░░░░░░░░██] 2-3周（可选）
阶段五：优化与监控          [░░░░░░░░░░░░░░░░░░░█] 1-2周

总计：9-13周（约2-3个月）
```

### 依赖关系

```
阶段一（基础架构）
    │
    ├──→ 阶段二（战斗系统）
    │       │
    │       └──→ 阶段四（组队战斗）
    │
    └──→ 阶段三（活动与生产）
            │
            └──→ 阶段五（优化与监控）
```

**关键路径**: 阶段一 → 阶段二 → 阶段五

---

## 阶段一：基础架构搭建

**目标**: 建立SignalR统一管理框架，实现连接管理和消息分发

**时间**: 2周  
**人员**: 1-2名后端开发 + 1名前端开发

---

### 第1步：环境准备（第1天）

#### 任务清单

- [ ] 安装SignalR依赖包
- [ ] 配置开发环境
- [ ] 创建项目结构

#### 详细步骤

**1.1 服务端安装依赖**

```bash
cd BlazorIdle.Server

# SignalR核心包（ASP.NET Core已包含）
dotnet add package Microsoft.AspNetCore.SignalR.Core

# MessagePack序列化（可选，性能更好）
dotnet add package Microsoft.AspNetCore.SignalR.Protocols.MessagePack
```

**1.2 客户端安装依赖**

```bash
cd BlazorIdle

# SignalR客户端
dotnet add package Microsoft.AspNetCore.SignalR.Client
```

**1.3 创建目录结构**

```bash
# 服务端
mkdir -p BlazorIdle.Server/Infrastructure/SignalR/Hubs
mkdir -p BlazorIdle.Server/Infrastructure/SignalR/Broadcasters
mkdir -p BlazorIdle.Server/Infrastructure/SignalR/Services
mkdir -p BlazorIdle.Server/Infrastructure/SignalR/Models

# 共享
mkdir -p BlazorIdle.Shared/Messages

# 客户端
mkdir -p BlazorIdle/Services/SignalR
```

#### 验收标准

- ✅ 所有依赖包安装成功
- ✅ 项目编译无错误
- ✅ 目录结构创建完成

---

### 第2步：实现GameHub（第1-2天）

#### 任务清单

- [ ] 创建GameHub基类
- [ ] 实现连接管理方法
- [ ] 实现Group订阅方法
- [ ] 配置SignalR中间件

#### 详细步骤

**2.1 创建GameHub**

创建文件：`BlazorIdle.Server/Infrastructure/SignalR/Hubs/GameHub.cs`

```csharp
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace BlazorIdle.Server.Infrastructure.SignalR.Hubs;

public class GameHub : Hub
{
    private readonly IConnectionManager _connectionManager;
    private readonly ILogger<GameHub> _logger;

    public GameHub(
        IConnectionManager connectionManager,
        ILogger<GameHub> logger)
    {
        _connectionManager = connectionManager;
        _logger = logger;
    }

    /// <summary>
    /// 连接建立时调用
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        var userId = GetUserId();
        if (string.IsNullOrEmpty(userId))
        {
            _logger.LogWarning("Unauthorized connection attempt: {ConnectionId}", Context.ConnectionId);
            await Clients.Caller.SendAsync("Error", "Unauthorized");
            Context.Abort();
            return;
        }

        await _connectionManager.RegisterConnectionAsync(userId, Context.ConnectionId);
        
        _logger.LogInformation(
            "User {UserId} connected with ConnectionId {ConnectionId}",
            userId, Context.ConnectionId);

        await Clients.Caller.SendAsync("Connected", new
        {
            userId,
            connectionId = Context.ConnectionId,
            serverTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        });

        await base.OnConnectedAsync();
    }

    /// <summary>
    /// 连接断开时调用
    /// </summary>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = GetUserId();
        if (!string.IsNullOrEmpty(userId))
        {
            await _connectionManager.UnregisterConnectionAsync(userId, Context.ConnectionId);
            
            _logger.LogInformation(
                "User {UserId} disconnected. Exception: {Exception}",
                userId, exception?.Message ?? "None");
        }

        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// 订阅战斗更新
    /// </summary>
    public async Task SubscribeToBattle(string battleId)
    {
        var userId = GetUserId();
        if (string.IsNullOrEmpty(userId))
        {
            await Clients.Caller.SendAsync("Error", "Unauthorized");
            return;
        }

        var groupName = $"battle:{battleId}";
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        
        _logger.LogInformation(
            "User {UserId} subscribed to battle {BattleId}",
            userId, battleId);

        await Clients.Caller.SendAsync("Subscribed", "battle", battleId);
    }

    /// <summary>
    /// 取消订阅战斗更新
    /// </summary>
    public async Task UnsubscribeFromBattle(string battleId)
    {
        var userId = GetUserId();
        if (string.IsNullOrEmpty(userId))
        {
            return;
        }

        var groupName = $"battle:{battleId}";
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
        
        _logger.LogInformation(
            "User {UserId} unsubscribed from battle {BattleId}",
            userId, battleId);

        await Clients.Caller.SendAsync("Unsubscribed", "battle", battleId);
    }

    /// <summary>
    /// 订阅队伍更新
    /// </summary>
    public async Task SubscribeToParty(string partyId)
    {
        var userId = GetUserId();
        if (string.IsNullOrEmpty(userId))
        {
            await Clients.Caller.SendAsync("Error", "Unauthorized");
            return;
        }

        var groupName = $"party:{partyId}";
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        
        _logger.LogInformation(
            "User {UserId} subscribed to party {PartyId}",
            userId, partyId);

        await Clients.Caller.SendAsync("Subscribed", "party", partyId);
    }

    /// <summary>
    /// 心跳检测
    /// </summary>
    public async Task Heartbeat()
    {
        var userId = GetUserId();
        if (!string.IsNullOrEmpty(userId))
        {
            var session = await _connectionManager.GetSessionAsync(userId);
            if (session != null)
            {
                session.LastHeartbeat = DateTime.UtcNow;
            }
        }
    }

    /// <summary>
    /// 请求战斗状态同步（断线重连）
    /// </summary>
    public async Task RequestBattleSync(string battleId, long lastVersion)
    {
        var userId = GetUserId();
        if (string.IsNullOrEmpty(userId))
        {
            await Clients.Caller.SendAsync("Error", "Unauthorized");
            return;
        }

        _logger.LogInformation(
            "User {UserId} requested battle sync for {BattleId} from version {Version}",
            userId, battleId, lastVersion);

        // 这里会路由到CombatBroadcaster处理
        await Clients.Caller.SendAsync("SyncRequested", battleId, lastVersion);
    }

    private string? GetUserId()
    {
        return Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    }
}
```

**2.2 配置SignalR服务**

修改文件：`BlazorIdle.Server/Program.cs`

```csharp
// 添加SignalR服务
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
    options.MaximumReceiveMessageSize = 102400; // 100KB
    options.HandshakeTimeout = TimeSpan.FromSeconds(15);
    options.KeepAliveInterval = TimeSpan.FromSeconds(15);
    options.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
})
.AddMessagePackProtocol(options =>
{
    // 可选：使用MessagePack提升性能
    options.SerializerOptions = MessagePack.MessagePackSerializerOptions.Standard
        .WithCompression(MessagePack.MessagePackCompression.Lz4Block);
});

// ... 其他配置 ...

var app = builder.Build();

// ... 其他中间件 ...

// 映射Hub端点
app.MapHub<GameHub>("/hubs/game");

app.Run();
```

**2.3 CORS配置（开发环境）**

```csharp
// 在Program.cs中添加
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowBlazorClient", policy =>
    {
        policy.WithOrigins("https://localhost:7001")  // Blazor客户端地址
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// 使用CORS
app.UseCors("AllowBlazorClient");
```

#### 验收标准

- ✅ GameHub编译无错误
- ✅ SignalR服务配置完成
- ✅ Hub端点可访问（/hubs/game）
- ✅ 日志正常输出

---

### 第3步：实现ConnectionManager（第2-3天）

#### 任务清单

- [ ] 创建IConnectionManager接口
- [ ] 实现ConnectionManager
- [ ] 创建UserSession模型
- [ ] 注册服务

#### 详细步骤

**3.1 创建UserSession模型**

创建文件：`BlazorIdle.Server/Infrastructure/SignalR/Models/UserSession.cs`

```csharp
namespace BlazorIdle.Server.Infrastructure.SignalR.Models;

public class UserSession
{
    public string UserId { get; set; } = string.Empty;
    public List<string> ConnectionIds { get; set; } = new();
    public Dictionary<string, HashSet<string>> Subscriptions { get; set; } = new();
    public DateTime ConnectedAt { get; set; }
    public DateTime LastHeartbeat { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}
```

**3.2 创建IConnectionManager接口**

创建文件：`BlazorIdle.Server/Infrastructure/SignalR/IConnectionManager.cs`

```csharp
using BlazorIdle.Server.Infrastructure.SignalR.Models;

namespace BlazorIdle.Server.Infrastructure.SignalR;

public interface IConnectionManager
{
    Task RegisterConnectionAsync(string userId, string connectionId);
    Task UnregisterConnectionAsync(string userId, string connectionId);
    Task<string?> GetConnectionIdAsync(string userId);
    Task<IEnumerable<string>> GetConnectionIdsAsync(string userId);
    Task<bool> IsConnectedAsync(string userId);
    Task<UserSession?> GetSessionAsync(string userId);
    Task AddSubscriptionAsync(string userId, string type, string id);
    Task RemoveSubscriptionAsync(string userId, string type, string id);
    IEnumerable<UserSession> GetIdleSessions(TimeSpan idleThreshold);
}
```

**3.3 实现ConnectionManager**

创建文件：`BlazorIdle.Server/Infrastructure/SignalR/Services/ConnectionManager.cs`

```csharp
using System.Collections.Concurrent;
using BlazorIdle.Server.Infrastructure.SignalR.Models;
using Microsoft.Extensions.Caching.Memory;

namespace BlazorIdle.Server.Infrastructure.SignalR.Services;

public class ConnectionManager : IConnectionManager
{
    private readonly ConcurrentDictionary<string, UserSession> _sessions = new();
    private readonly ILogger<ConnectionManager> _logger;

    public ConnectionManager(ILogger<ConnectionManager> logger)
    {
        _logger = logger;
    }

    public Task RegisterConnectionAsync(string userId, string connectionId)
    {
        var session = _sessions.GetOrAdd(userId, _ => new UserSession
        {
            UserId = userId,
            ConnectedAt = DateTime.UtcNow,
            LastHeartbeat = DateTime.UtcNow
        });

        lock (session.ConnectionIds)
        {
            if (!session.ConnectionIds.Contains(connectionId))
            {
                session.ConnectionIds.Add(connectionId);
                _logger.LogDebug(
                    "Added connection {ConnectionId} for user {UserId}. Total connections: {Count}",
                    connectionId, userId, session.ConnectionIds.Count);
            }
        }

        session.LastHeartbeat = DateTime.UtcNow;
        return Task.CompletedTask;
    }

    public Task UnregisterConnectionAsync(string userId, string connectionId)
    {
        if (_sessions.TryGetValue(userId, out var session))
        {
            lock (session.ConnectionIds)
            {
                session.ConnectionIds.Remove(connectionId);
                
                _logger.LogDebug(
                    "Removed connection {ConnectionId} for user {UserId}. Remaining: {Count}",
                    connectionId, userId, session.ConnectionIds.Count);

                // 如果没有活跃连接了，移除会话
                if (session.ConnectionIds.Count == 0)
                {
                    _sessions.TryRemove(userId, out _);
                    _logger.LogInformation("Session removed for user {UserId}", userId);
                }
            }
        }

        return Task.CompletedTask;
    }

    public Task<string?> GetConnectionIdAsync(string userId)
    {
        if (_sessions.TryGetValue(userId, out var session))
        {
            lock (session.ConnectionIds)
            {
                return Task.FromResult(session.ConnectionIds.FirstOrDefault());
            }
        }

        return Task.FromResult<string?>(null);
    }

    public Task<IEnumerable<string>> GetConnectionIdsAsync(string userId)
    {
        if (_sessions.TryGetValue(userId, out var session))
        {
            lock (session.ConnectionIds)
            {
                return Task.FromResult<IEnumerable<string>>(session.ConnectionIds.ToList());
            }
        }

        return Task.FromResult(Enumerable.Empty<string>());
    }

    public Task<bool> IsConnectedAsync(string userId)
    {
        return Task.FromResult(_sessions.ContainsKey(userId));
    }

    public Task<UserSession?> GetSessionAsync(string userId)
    {
        _sessions.TryGetValue(userId, out var session);
        return Task.FromResult(session);
    }

    public Task AddSubscriptionAsync(string userId, string type, string id)
    {
        if (_sessions.TryGetValue(userId, out var session))
        {
            if (!session.Subscriptions.ContainsKey(type))
            {
                session.Subscriptions[type] = new HashSet<string>();
            }

            session.Subscriptions[type].Add(id);
            _logger.LogDebug(
                "User {UserId} subscribed to {Type}:{Id}",
                userId, type, id);
        }

        return Task.CompletedTask;
    }

    public Task RemoveSubscriptionAsync(string userId, string type, string id)
    {
        if (_sessions.TryGetValue(userId, out var session))
        {
            if (session.Subscriptions.TryGetValue(type, out var subscriptions))
            {
                subscriptions.Remove(id);
                _logger.LogDebug(
                    "User {UserId} unsubscribed from {Type}:{Id}",
                    userId, type, id);
            }
        }

        return Task.CompletedTask;
    }

    public IEnumerable<UserSession> GetIdleSessions(TimeSpan idleThreshold)
    {
        var cutoffTime = DateTime.UtcNow - idleThreshold;
        return _sessions.Values.Where(s => s.LastHeartbeat < cutoffTime).ToList();
    }
}
```

**3.4 注册服务**

在`Program.cs`中添加：

```csharp
// 注册SignalR服务
builder.Services.AddSingleton<IConnectionManager, ConnectionManager>();
```

#### 验收标准

- ✅ 所有接口方法实现完成
- ✅ 服务注册成功
- ✅ 编译无错误
- ✅ 单元测试通过（可选）

---

### 第4步：实现SignalRDispatcher（第3-5天）

#### 任务清单

- [ ] 创建ISignalRDispatcher接口
- [ ] 实现SignalRDispatcher
- [ ] 实现消息队列和批量发送
- [ ] 实现优先级调度

#### 详细步骤

**4.1 创建消息优先级枚举**

创建文件：`BlazorIdle.Server/Infrastructure/SignalR/Models/MessagePriority.cs`

```csharp
namespace BlazorIdle.Server.Infrastructure.SignalR.Models;

public enum MessagePriority
{
    Low = 0,
    Normal = 1,
    High = 2,
    Critical = 3
}
```

**4.2 创建ISignalRDispatcher接口**

创建文件：`BlazorIdle.Server/Infrastructure/SignalR/ISignalRDispatcher.cs`

```csharp
using BlazorIdle.Server.Infrastructure.SignalR.Models;

namespace BlazorIdle.Server.Infrastructure.SignalR;

public interface ISignalRDispatcher
{
    Task SendToUserAsync(
        string userId,
        string method,
        object message,
        MessagePriority priority = MessagePriority.Normal);

    Task SendToGroupAsync(
        string groupName,
        string method,
        object message,
        MessagePriority priority = MessagePriority.Normal);

    Task SendToAllAsync(
        string method,
        object message,
        MessagePriority priority = MessagePriority.Normal);

    Task<DispatcherMetrics> GetMetricsAsync();
}

public class DispatcherMetrics
{
    public int QueueDepth { get; set; }
    public int TotalMessagesSent { get; set; }
    public int FailedMessages { get; set; }
    public double AverageLatency { get; set; }
}
```

**4.3 实现SignalRDispatcher**

创建文件：`BlazorIdle.Server/Infrastructure/SignalR/Services/SignalRDispatcher.cs`

```csharp
using System.Threading.Channels;
using Microsoft.AspNetCore.SignalR;
using BlazorIdle.Server.Infrastructure.SignalR.Hubs;
using BlazorIdle.Server.Infrastructure.SignalR.Models;

namespace BlazorIdle.Server.Infrastructure.SignalR.Services;

public class SignalRDispatcher : ISignalRDispatcher, IDisposable
{
    private readonly IHubContext<GameHub> _hubContext;
    private readonly IConnectionManager _connectionManager;
    private readonly ILogger<SignalRDispatcher> _logger;
    private readonly Channel<PendingMessage> _messageChannel;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _processingTask;

    // 监控指标
    private int _totalMessagesSent = 0;
    private int _failedMessages = 0;
    private readonly List<long> _latencyHistory = new();

    public SignalRDispatcher(
        IHubContext<GameHub> hubContext,
        IConnectionManager connectionManager,
        ILogger<SignalRDispatcher> logger)
    {
        _hubContext = hubContext;
        _connectionManager = connectionManager;
        _logger = logger;

        // 创建有界通道（背压控制）
        _messageChannel = Channel.CreateBounded<PendingMessage>(new BoundedChannelOptions(10000)
        {
            FullMode = BoundedChannelFullMode.Wait
        });

        // 启动后台消费者
        _processingTask = Task.Run(() => ProcessMessagesAsync(_cts.Token));
    }

    public async Task SendToUserAsync(
        string userId,
        string method,
        object message,
        MessagePriority priority = MessagePriority.Normal)
    {
        var pendingMessage = new PendingMessage
        {
            Type = MessageType.User,
            Target = userId,
            Method = method,
            Message = message,
            Priority = priority,
            EnqueuedAt = DateTime.UtcNow
        };

        await _messageChannel.Writer.WriteAsync(pendingMessage, _cts.Token);
    }

    public async Task SendToGroupAsync(
        string groupName,
        string method,
        object message,
        MessagePriority priority = MessagePriority.Normal)
    {
        var pendingMessage = new PendingMessage
        {
            Type = MessageType.Group,
            Target = groupName,
            Method = method,
            Message = message,
            Priority = priority,
            EnqueuedAt = DateTime.UtcNow
        };

        await _messageChannel.Writer.WriteAsync(pendingMessage, _cts.Token);
    }

    public async Task SendToAllAsync(
        string method,
        object message,
        MessagePriority priority = MessagePriority.Normal)
    {
        var pendingMessage = new PendingMessage
        {
            Type = MessageType.All,
            Target = "all",
            Method = method,
            Message = message,
            Priority = priority,
            EnqueuedAt = DateTime.UtcNow
        };

        await _messageChannel.Writer.WriteAsync(pendingMessage, _cts.Token);
    }

    public Task<DispatcherMetrics> GetMetricsAsync()
    {
        var metrics = new DispatcherMetrics
        {
            QueueDepth = _messageChannel.Reader.Count,
            TotalMessagesSent = _totalMessagesSent,
            FailedMessages = _failedMessages,
            AverageLatency = _latencyHistory.Count > 0 ? _latencyHistory.Average() : 0
        };

        return Task.FromResult(metrics);
    }

    private async Task ProcessMessagesAsync(CancellationToken cancellationToken)
    {
        var batch = new List<PendingMessage>();
        var batchTimer = DateTime.UtcNow;

        try
        {
            await foreach (var message in _messageChannel.Reader.ReadAllAsync(cancellationToken))
            {
                batch.Add(message);

                // 达到批量阈值或时间窗口
                var shouldFlush = batch.Count >= 100 || 
                                (batch.Count > 0 && (DateTime.UtcNow - batchTimer).TotalMilliseconds > 50);

                if (shouldFlush)
                {
                    await SendBatchAsync(batch);
                    batch.Clear();
                    batchTimer = DateTime.UtcNow;
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Message processing cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in message processing loop");
        }
    }

    private async Task SendBatchAsync(List<PendingMessage> messages)
    {
        if (messages.Count == 0) return;

        // 按优先级排序
        messages.Sort((a, b) => b.Priority.CompareTo(a.Priority));

        foreach (var msg in messages)
        {
            var startTime = DateTime.UtcNow;

            try
            {
                switch (msg.Type)
                {
                    case MessageType.User:
                        var connectionIds = await _connectionManager.GetConnectionIdsAsync(msg.Target);
                        foreach (var connectionId in connectionIds)
                        {
                            await _hubContext.Clients.Client(connectionId).SendAsync(msg.Method, msg.Message);
                        }
                        break;

                    case MessageType.Group:
                        await _hubContext.Clients.Group(msg.Target).SendAsync(msg.Method, msg.Message);
                        break;

                    case MessageType.All:
                        await _hubContext.Clients.All.SendAsync(msg.Method, msg.Message);
                        break;
                }

                Interlocked.Increment(ref _totalMessagesSent);

                // 记录延迟
                var latency = (DateTime.UtcNow - startTime).TotalMilliseconds;
                lock (_latencyHistory)
                {
                    _latencyHistory.Add((long)latency);
                    if (_latencyHistory.Count > 1000)
                    {
                        _latencyHistory.RemoveAt(0);
                    }
                }
            }
            catch (Exception ex)
            {
                Interlocked.Increment(ref _failedMessages);
                _logger.LogError(ex, 
                    "Failed to send message: {Method} to {Target} (Type: {Type})",
                    msg.Method, msg.Target, msg.Type);
            }
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        _messageChannel.Writer.Complete();
        _processingTask.Wait(TimeSpan.FromSeconds(5));
        _cts.Dispose();
    }
}

internal class PendingMessage
{
    public MessageType Type { get; set; }
    public string Target { get; set; } = string.Empty;
    public string Method { get; set; } = string.Empty;
    public object Message { get; set; } = null!;
    public MessagePriority Priority { get; set; }
    public DateTime EnqueuedAt { get; set; }
}

internal enum MessageType
{
    User,
    Group,
    All
}
```

**4.4 注册服务**

在`Program.cs`中添加：

```csharp
builder.Services.AddSingleton<ISignalRDispatcher, SignalRDispatcher>();
```

#### 验收标准

- ✅ 消息队列正常工作
- ✅ 批量发送功能正常
- ✅ 优先级调度正确
- ✅ 监控指标可获取
- ✅ 异常不影响其他消息

---

### 第5步：客户端连接管理（第5-7天）

#### 任务清单

- [ ] 创建SignalRConnectionManager
- [ ] 实现自动重连
- [ ] 实现心跳检测
- [ ] 实现消息路由

#### 详细步骤

**5.1 创建SignalRConnectionManager**

创建文件：`BlazorIdle/Services/SignalR/SignalRConnectionManager.cs`

```csharp
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;

namespace BlazorIdle.Services.SignalR;

public class SignalRConnectionManager : IAsyncDisposable
{
    private readonly ILogger<SignalRConnectionManager> _logger;
    private HubConnection? _connection;
    private PeriodicTimer? _heartbeatTimer;
    private Task? _heartbeatTask;
    private CancellationTokenSource? _cts;

    public event Func<Task>? Connected;
    public event Func<Exception?, Task>? Disconnected;
    public event Func<string, Task>? Reconnecting;
    public event Func<string?, Task>? Reconnected;

    public HubConnectionState State => _connection?.State ?? HubConnectionState.Disconnected;
    public bool IsConnected => _connection?.State == HubConnectionState.Connected;

    public SignalRConnectionManager(ILogger<SignalRConnectionManager> logger)
    {
        _logger = logger;
    }

    public async Task InitializeAsync(string hubUrl, string? accessToken = null)
    {
        if (_connection != null)
        {
            await DisposeAsync();
        }

        _connection = new HubConnectionBuilder()
            .WithUrl(hubUrl, options =>
            {
                if (!string.IsNullOrEmpty(accessToken))
                {
                    options.AccessTokenProvider = () => Task.FromResult(accessToken);
                }
            })
            .WithAutomaticReconnect(new[] 
            { 
                TimeSpan.Zero, 
                TimeSpan.FromSeconds(2), 
                TimeSpan.FromSeconds(5), 
                TimeSpan.FromSeconds(10),
                TimeSpan.FromSeconds(20),
                TimeSpan.FromSeconds(30)
            })
            .ConfigureLogging(logging =>
            {
                logging.SetMinimumLevel(LogLevel.Information);
            })
            .Build();

        // 连接事件
        _connection.Closed += OnClosedAsync;
        _connection.Reconnecting += OnReconnectingAsync;
        _connection.Reconnected += OnReconnectedAsync;

        // 注册基础消息处理
        _connection.On<string, object>("Connected", OnConnectedMessageAsync);
        _connection.On<string>("Error", OnErrorMessageAsync);
    }

    public async Task StartAsync()
    {
        if (_connection == null)
        {
            throw new InvalidOperationException("Connection not initialized");
        }

        try
        {
            await _connection.StartAsync();
            _logger.LogInformation("SignalR connection started successfully");

            // 启动心跳
            StartHeartbeat();

            // 触发Connected事件
            if (Connected != null)
            {
                await Connected.Invoke();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start SignalR connection");
            throw;
        }
    }

    public async Task StopAsync()
    {
        if (_connection != null)
        {
            StopHeartbeat();
            await _connection.StopAsync();
            _logger.LogInformation("SignalR connection stopped");
        }
    }

    public async Task<T?> InvokeAsync<T>(string methodName, params object[] args)
    {
        if (_connection == null || _connection.State != HubConnectionState.Connected)
        {
            _logger.LogWarning("Cannot invoke {Method}: connection not established", methodName);
            return default;
        }

        try
        {
            return await _connection.InvokeAsync<T>(methodName, args);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error invoking {Method}", methodName);
            throw;
        }
    }

    public async Task SendAsync(string methodName, params object[] args)
    {
        if (_connection == null || _connection.State != HubConnectionState.Connected)
        {
            _logger.LogWarning("Cannot send {Method}: connection not established", methodName);
            return;
        }

        try
        {
            await _connection.SendAsync(methodName, args);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending {Method}", methodName);
            throw;
        }
    }

    public IDisposable On<T>(string methodName, Func<T, Task> handler)
    {
        if (_connection == null)
        {
            throw new InvalidOperationException("Connection not initialized");
        }

        return _connection.On(methodName, handler);
    }

    public IDisposable On<T1, T2>(string methodName, Func<T1, T2, Task> handler)
    {
        if (_connection == null)
        {
            throw new InvalidOperationException("Connection not initialized");
        }

        return _connection.On(methodName, handler);
    }

    private void StartHeartbeat()
    {
        _cts = new CancellationTokenSource();
        _heartbeatTimer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        _heartbeatTask = Task.Run(async () =>
        {
            while (await _heartbeatTimer.WaitForNextTickAsync(_cts.Token))
            {
                try
                {
                    if (_connection?.State == HubConnectionState.Connected)
                    {
                        await _connection.SendAsync("Heartbeat", _cts.Token);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Heartbeat failed");
                }
            }
        }, _cts.Token);
    }

    private void StopHeartbeat()
    {
        _cts?.Cancel();
        _heartbeatTimer?.Dispose();
        _heartbeatTask?.Wait(TimeSpan.FromSeconds(5));
        _cts?.Dispose();
    }

    private Task OnClosedAsync(Exception? exception)
    {
        _logger.LogWarning(exception, "SignalR connection closed");
        StopHeartbeat();
        
        if (Disconnected != null)
        {
            return Disconnected.Invoke(exception);
        }

        return Task.CompletedTask;
    }

    private Task OnReconnectingAsync(Exception? exception)
    {
        _logger.LogInformation("SignalR reconnecting...");
        
        if (Reconnecting != null)
        {
            return Reconnecting.Invoke(exception?.Message ?? "Unknown");
        }

        return Task.CompletedTask;
    }

    private Task OnReconnectedAsync(string? connectionId)
    {
        _logger.LogInformation("SignalR reconnected with ConnectionId: {ConnectionId}", connectionId);
        StartHeartbeat();
        
        if (Reconnected != null)
        {
            return Reconnected.Invoke(connectionId);
        }

        return Task.CompletedTask;
    }

    private Task OnConnectedMessageAsync(string userId, object data)
    {
        _logger.LogInformation("Received Connected message for user {UserId}", userId);
        return Task.CompletedTask;
    }

    private Task OnErrorMessageAsync(string error)
    {
        _logger.LogError("Received error from server: {Error}", error);
        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        StopHeartbeat();
        
        if (_connection != null)
        {
            await _connection.DisposeAsync();
            _connection = null;
        }
    }
}
```

**5.2 注册服务**

在`BlazorIdle/Program.cs`中添加：

```csharp
builder.Services.AddScoped<SignalRConnectionManager>();
```

**5.3 使用示例**

```csharp
// 在Blazor组件中使用
@inject SignalRConnectionManager SignalR

protected override async Task OnInitializedAsync()
{
    // 订阅事件
    SignalR.Connected += OnSignalRConnected;
    SignalR.Disconnected += OnSignalRDisconnected;

    // 初始化并连接
    await SignalR.InitializeAsync("https://localhost:7000/hubs/game", accessToken);
    await SignalR.StartAsync();

    // 订阅消息
    SignalR.On<string>("SystemAnnouncement", async (message) =>
    {
        await InvokeAsync(() =>
        {
            // 更新UI
            StateHasChanged();
        });
    });
}

private async Task OnSignalRConnected()
{
    Console.WriteLine("Connected to SignalR");
}

private async Task OnSignalRDisconnected(Exception? ex)
{
    Console.WriteLine($"Disconnected from SignalR: {ex?.Message}");
}
```

#### 验收标准

- ✅ 连接成功建立
- ✅ 自动重连工作正常
- ✅ 心跳检测正常
- ✅ 消息接收正常
- ✅ 事件回调触发

---

### 阶段一验收

#### 功能验收

- [ ] 客户端可以成功连接到GameHub
- [ ] 连接断开后自动重连
- [ ] 心跳检测正常工作
- [ ] 消息分发器正常运行
- [ ] 连接管理器正确追踪会话

#### 性能验收

- [ ] 连接建立时间 < 1秒
- [ ] 心跳延迟 < 100ms
- [ ] 消息队列无积压（正常负载下）
- [ ] 内存使用稳定（无泄漏）

#### 测试验收

- [ ] 单元测试覆盖率 > 70%
- [ ] 集成测试通过
- [ ] 手动测试通过

---

## 阶段二：战斗系统集成

**目标**: 集成现有战斗系统的SignalR推送

**时间**: 2-3周  
**人员**: 1-2名后端开发 + 1名前端开发

（由于篇幅限制，这里提供概要）

### 主要任务

1. **创建CombatBroadcaster**（2-3天）
   - 实现FrameTick推送
   - 实现KeyEvent推送
   - 实现Snapshot推送

2. **集成BattleFrameBuffer**（2-3天）
   - 实现帧缓冲
   - 实现补发逻辑
   - 实现快照生成

3. **修改BattleInstance**（2-3天）
   - 添加帧生成逻辑
   - 发布领域事件
   - 集成Broadcaster

4. **客户端战斗状态管理**（3-5天）
   - 创建BattleFrameReceiver
   - 实现版本管理
   - 实现状态更新

5. **测试与优化**（2-3天）
   - 压力测试
   - 性能优化
   - Bug修复

详细步骤请参考现有的 [战斗帧广播系统实现指南.md](./战斗帧广播系统实现指南.md)

---

## 阶段三：活动与生产系统

**目标**: 实现活动和生产系统的SignalR推送

**时间**: 2-3周

### 主要任务

1. **ActivityBroadcaster**（3-4天）
2. **CraftingBroadcaster**（2-3天）
3. **GatheringBroadcaster**（2-3天）
4. **客户端集成**（4-5天）

---

## 阶段四：组队与社交系统

**目标**: 实现组队和社交功能的SignalR推送（可选）

**时间**: 2-3周

### 主要任务

1. **PartyBroadcaster**（4-5天）
2. **多人战斗同步**（5-6天）
3. **客户端集成**（4-5天）

---

## 阶段五：优化与监控

**目标**: 性能优化和监控系统完善

**时间**: 1-2周

### 主要任务

1. **性能优化**（3-4天）
2. **监控面板**（2-3天）
3. **文档完善**（1-2天）

---

## 验收标准

### 整体验收标准

#### 功能性

- ✅ 所有计划的消息类型都能正常推送
- ✅ 断线重连后状态正确恢复
- ✅ 多用户场景下消息不串号
- ✅ 优先级调度正确工作

#### 性能

- ✅ 战斗帧推送延迟 < 200ms（P95）
- ✅ 消息队列深度 < 1000（正常负载）
- ✅ 连接数支持 > 100（单服务器）
- ✅ CPU使用率 < 50%（正常负载）

#### 可靠性

- ✅ 7×24小时稳定运行
- ✅ 错误率 < 0.1%
- ✅ 自动重连成功率 > 95%
- ✅ 消息丢失率 < 0.01%

#### 可维护性

- ✅ 代码质量良好（无严重代码异味）
- ✅ 文档齐全
- ✅ 日志清晰
- ✅ 监控完善

---

## 故障排查

### 常见问题

#### 问题1: 连接建立失败

**症状**: 客户端无法连接到Hub

**排查步骤**:
1. 检查Hub端点配置是否正确
2. 检查CORS配置
3. 检查防火墙设置
4. 查看服务器日志

**解决方案**:
```csharp
// 确保端点映射正确
app.MapHub<GameHub>("/hubs/game");

// 确保CORS允许
app.UseCors("AllowBlazorClient");
```

#### 问题2: 消息队列积压

**症状**: 消息延迟越来越大

**排查步骤**:
1. 检查DispatcherMetrics
2. 检查CPU使用率
3. 检查网络带宽

**解决方案**:
- 增加批量大小
- 降低推送频率
- 横向扩展服务器

#### 问题3: 内存泄漏

**症状**: 内存使用持续增长

**排查步骤**:
1. 检查会话是否正确清理
2. 检查事件订阅是否正确释放
3. 使用内存分析工具

**解决方案**:
- 确保Dispose正确调用
- 使用WeakReference
- 定期清理过期数据

---

## 总结

本实施计划提供了详细的、分步骤的SignalR统一管理系统开发指南。

### 关键成功因素

1. **严格按阶段推进**: 不跳过基础架构阶段
2. **充分测试**: 每个阶段完成后进行测试
3. **持续监控**: 及时发现性能问题
4. **文档同步**: 随代码更新文档

### 下一步

1. ✅ 开始阶段一实施
2. ✅ 建立项目看板跟踪进度
3. ✅ 定期review代码质量
4. ✅ 保持团队沟通

---

**文档状态**: ✅ 完成  
**最后更新**: 2025年10月21日  
**作者**: GitHub Copilot
