using Microsoft.AspNetCore.SignalR;

namespace BlazorIdle.Server.Hubs;

/// <summary>
/// 战斗通知Hub
/// 管理客户端连接和战斗事件订阅
/// </summary>
public class BattleNotificationHub : Hub
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
            "客户端 {ConnectionId} 订阅了战斗 {BattleId}",
            Context.ConnectionId,
            battleId);
    }

    /// <summary>
    /// 取消订阅特定战斗的通知
    /// </summary>
    /// <param name="battleId">战斗ID</param>
    public async Task UnsubscribeBattle(Guid battleId)
    {
        var groupName = GetBattleGroupName(battleId);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
        _logger.LogInformation(
            "客户端 {ConnectionId} 取消订阅战斗 {BattleId}",
            Context.ConnectionId,
            battleId);
    }

    /// <summary>
    /// 客户端连接时
    /// </summary>
    public override Task OnConnectedAsync()
    {
        _logger.LogInformation("客户端已连接: {ConnectionId}", Context.ConnectionId);
        return base.OnConnectedAsync();
    }

    /// <summary>
    /// 客户端断开连接时
    /// </summary>
    public override Task OnDisconnectedAsync(Exception? exception)
    {
        if (exception != null)
        {
            _logger.LogWarning(
                exception,
                "客户端异常断开: {ConnectionId}",
                Context.ConnectionId);
        }
        else
        {
            _logger.LogInformation("客户端正常断开: {ConnectionId}", Context.ConnectionId);
        }
        return base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// 获取战斗组名称
    /// </summary>
    private static string GetBattleGroupName(Guid battleId) => $"battle_{battleId}";
}
