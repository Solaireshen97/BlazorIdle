using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace BlazorIdle.Server.Infrastructure.SignalR.Hubs;

/// <summary>
/// 游戏主Hub
/// 统一的SignalR入口，处理所有游戏相关的实时通信
/// 支持连接管理、Group订阅、心跳检测和状态同步
/// </summary>
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
    /// 注册用户连接，验证身份，发送连接确认消息
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        var userId = GetUserId();
        
        // 验证用户身份
        if (string.IsNullOrEmpty(userId))
        {
            _logger.LogWarning("未授权的连接尝试：{ConnectionId}", Context.ConnectionId);
            await Clients.Caller.SendAsync("Error", "Unauthorized");
            Context.Abort();
            return;
        }

        // 注册连接
        await _connectionManager.RegisterConnectionAsync(userId, Context.ConnectionId);
        
        _logger.LogInformation(
            "用户 {UserId} 已连接，ConnectionId：{ConnectionId}",
            userId, Context.ConnectionId);

        // 发送连接确认消息，包含服务器时间用于时间同步
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
    /// 注销连接，清理订阅，如果是最后一个连接则清理会话
    /// </summary>
    /// <param name="exception">断开连接的异常信息，正常断开时为null</param>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = GetUserId();
        if (!string.IsNullOrEmpty(userId))
        {
            await _connectionManager.UnregisterConnectionAsync(userId, Context.ConnectionId);
            
            _logger.LogInformation(
                "用户 {UserId} 已断开连接。异常：{Exception}",
                userId, exception?.Message ?? "无");
        }

        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// 订阅战斗更新
    /// 将用户连接加入到指定战斗的SignalR Group
    /// </summary>
    /// <param name="battleId">战斗ID</param>
    public async Task SubscribeToBattle(string battleId)
    {
        var userId = GetUserId();
        if (string.IsNullOrEmpty(userId))
        {
            await Clients.Caller.SendAsync("Error", "Unauthorized");
            return;
        }

        // 将连接加入Group
        var groupName = $"battle:{battleId}";
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        
        // 记录订阅信息
        await _connectionManager.AddSubscriptionAsync(userId, "battle", battleId);
        
        _logger.LogInformation(
            "用户 {UserId} 订阅了战斗 {BattleId}",
            userId, battleId);

        // 发送订阅确认
        await Clients.Caller.SendAsync("Subscribed", "battle", battleId);
    }

    /// <summary>
    /// 取消订阅战斗更新
    /// 将用户连接从指定战斗的SignalR Group移除
    /// </summary>
    /// <param name="battleId">战斗ID</param>
    public async Task UnsubscribeFromBattle(string battleId)
    {
        var userId = GetUserId();
        if (string.IsNullOrEmpty(userId))
        {
            return;
        }

        // 从Group移除连接
        var groupName = $"battle:{battleId}";
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
        
        // 移除订阅记录
        await _connectionManager.RemoveSubscriptionAsync(userId, "battle", battleId);
        
        _logger.LogInformation(
            "用户 {UserId} 取消订阅战斗 {BattleId}",
            userId, battleId);

        // 发送取消订阅确认
        await Clients.Caller.SendAsync("Unsubscribed", "battle", battleId);
    }

    /// <summary>
    /// 订阅队伍更新
    /// 将用户连接加入到指定队伍的SignalR Group
    /// </summary>
    /// <param name="partyId">队伍ID</param>
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
        
        await _connectionManager.AddSubscriptionAsync(userId, "party", partyId);
        
        _logger.LogInformation(
            "用户 {UserId} 订阅了队伍 {PartyId}",
            userId, partyId);

        await Clients.Caller.SendAsync("Subscribed", "party", partyId);
    }

    /// <summary>
    /// 取消订阅队伍更新
    /// </summary>
    /// <param name="partyId">队伍ID</param>
    public async Task UnsubscribeFromParty(string partyId)
    {
        var userId = GetUserId();
        if (string.IsNullOrEmpty(userId))
        {
            return;
        }

        var groupName = $"party:{partyId}";
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
        
        await _connectionManager.RemoveSubscriptionAsync(userId, "party", partyId);
        
        _logger.LogInformation(
            "用户 {UserId} 取消订阅队伍 {PartyId}",
            userId, partyId);

        await Clients.Caller.SendAsync("Unsubscribed", "party", partyId);
    }

    /// <summary>
    /// 心跳检测
    /// 客户端定期调用此方法以维持连接活跃状态
    /// 用于检测断线和自动清理空闲连接
    /// </summary>
    public async Task Heartbeat()
    {
        var userId = GetUserId();
        if (!string.IsNullOrEmpty(userId))
        {
            var session = await _connectionManager.GetSessionAsync(userId);
            if (session != null)
            {
                // 更新最后心跳时间
                session.LastHeartbeat = DateTime.UtcNow;
            }
        }
    }

    /// <summary>
    /// 请求战斗状态同步
    /// 用于断线重连后的状态恢复
    /// </summary>
    /// <param name="battleId">战斗ID</param>
    /// <param name="lastVersion">客户端最后接收到的版本号</param>
    public async Task RequestBattleSync(string battleId, long lastVersion)
    {
        var userId = GetUserId();
        if (string.IsNullOrEmpty(userId))
        {
            await Clients.Caller.SendAsync("Error", "Unauthorized");
            return;
        }

        _logger.LogInformation(
            "用户 {UserId} 请求战斗 {BattleId} 的状态同步，最后版本：{Version}",
            userId, battleId, lastVersion);

        // 通知客户端同步请求已收到
        // 实际的状态同步会由CombatBroadcaster处理
        await Clients.Caller.SendAsync("SyncRequested", battleId, lastVersion);
    }

    /// <summary>
    /// 从当前用户的Claims中提取用户ID
    /// </summary>
    /// <returns>用户ID，如果未授权则返回null</returns>
    private string? GetUserId()
    {
        return Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    }
}
