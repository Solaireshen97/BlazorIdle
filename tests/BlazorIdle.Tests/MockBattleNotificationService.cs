using BlazorIdle.Server.Application.Abstractions;

namespace BlazorIdle.Tests;

/// <summary>
/// Mock implementation of IBattleNotificationService for testing
/// </summary>
public class MockBattleNotificationService : IBattleNotificationService
{
    public List<(Guid BattleId, string EventType)> Notifications { get; } = new();

    public Task NotifyStateChangeAsync(Guid battleId, string eventType)
    {
        Notifications.Add((battleId, eventType));
        return Task.CompletedTask;
    }

    public Task NotifyEventAsync(Guid battleId, object eventData)
    {
        return Task.CompletedTask;
    }

    public void Clear() => Notifications.Clear();
}
