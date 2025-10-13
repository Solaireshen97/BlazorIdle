using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Authorization;

namespace BlazorIdle.Server.Hubs;

/// <summary>
/// 战斗通知 SignalR Hub
/// 负责管理客户端连接和战斗事件订阅
/// </summary>
[Authorize]
public sealed class BattleNotificationHub : Hub
{
    private readonly ILogger<BattleNotificationHub> _logger;

    public BattleNotificationHub(ILogger<BattleNotificationHub> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 订阅指定战斗的事件通知
    /// </summary>
    /// <param name="battleId">战斗 ID</param>
    public async Task SubscribeBattle(Guid battleId)
    {
        var groupName = GetBattleGroupName(battleId);
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        
        _logger.LogDebug(
            "Client {ConnectionId} subscribed to battle {BattleId}",
            Context.ConnectionId,
            battleId
        );
    }

    /// <summary>
    /// 取消订阅指定战斗
    /// </summary>
    /// <param name="battleId">战斗 ID</param>
    public async Task UnsubscribeBattle(Guid battleId)
    {
        var groupName = GetBattleGroupName(battleId);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
        
        _logger.LogDebug(
            "Client {ConnectionId} unsubscribed from battle {BattleId}",
            Context.ConnectionId,
            battleId
        );
    }

    /// <summary>
    /// 客户端连接时调用
    /// </summary>
    public override Task OnConnectedAsync()
    {
        _logger.LogInformation(
            "Client connected: {ConnectionId}",
            Context.ConnectionId
        );
        return base.OnConnectedAsync();
    }

    /// <summary>
    /// 客户端断开连接时调用
    /// </summary>
    /// <param name="exception">断开连接的异常（如果有）</param>
    public override Task OnDisconnectedAsync(Exception? exception)
    {
        if (exception != null)
        {
            _logger.LogWarning(
                exception,
                "Client disconnected with error: {ConnectionId}",
                Context.ConnectionId
            );
        }
        else
        {
            _logger.LogInformation(
                "Client disconnected: {ConnectionId}",
                Context.ConnectionId
            );
        }
        
        return base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// 获取战斗的组名称
    /// </summary>
    private static string GetBattleGroupName(Guid battleId) => $"battle_{battleId}";
}
