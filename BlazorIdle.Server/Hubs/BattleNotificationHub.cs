using Microsoft.AspNetCore.SignalR;

namespace BlazorIdle.Server.Hubs;

/// <summary>
/// SignalR Hub for real-time battle notifications
/// 管理客户端连接并提供订阅/取消订阅战斗通知的接口
/// </summary>
public sealed class BattleNotificationHub : Hub
{
    private readonly ILogger<BattleNotificationHub> _logger;

    public BattleNotificationHub(ILogger<BattleNotificationHub> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 订阅特定战斗的通知
    /// </summary>
    /// <param name="battleId">战斗ID</param>
    public async Task SubscribeBattle(Guid battleId)
    {
        var groupName = GetBattleGroupName(battleId);
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        _logger.LogInformation(
            "Client {ConnectionId} subscribed to battle {BattleId}",
            Context.ConnectionId,
            battleId
        );
    }

    /// <summary>
    /// 取消订阅特定战斗
    /// </summary>
    /// <param name="battleId">战斗ID</param>
    public async Task UnsubscribeBattle(Guid battleId)
    {
        var groupName = GetBattleGroupName(battleId);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
        _logger.LogInformation(
            "Client {ConnectionId} unsubscribed from battle {BattleId}",
            Context.ConnectionId,
            battleId
        );
    }

    /// <summary>
    /// 客户端连接时触发
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation(
            "Client {ConnectionId} connected to BattleNotificationHub",
            Context.ConnectionId
        );
        await base.OnConnectedAsync();
    }

    /// <summary>
    /// 客户端断开连接时触发
    /// </summary>
    /// <param name="exception">断开连接的异常信息（如果有）</param>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (exception != null)
        {
            _logger.LogWarning(
                exception,
                "Client {ConnectionId} disconnected with error",
                Context.ConnectionId
            );
        }
        else
        {
            _logger.LogInformation(
                "Client {ConnectionId} disconnected normally",
                Context.ConnectionId
            );
        }
        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// 获取战斗组名称（用于 SignalR Groups）
    /// </summary>
    private static string GetBattleGroupName(Guid battleId) => $"battle_{battleId}";
}
