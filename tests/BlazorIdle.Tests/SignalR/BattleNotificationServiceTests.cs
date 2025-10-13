using BlazorIdle.Shared.Models.Notifications;
using Xunit;

namespace BlazorIdle.Tests.SignalR;

/// <summary>
/// 测试战斗通知相关的数据模型和常量
/// </summary>
public class BattleNotificationServiceTests
{
    [Fact]
    public void BattleEventTypes_HasCorrectConstants()
    {
        // Assert - 验证事件类型常量定义正确
        Assert.Equal("PlayerDeath", BattleEventTypes.PlayerDeath);
        Assert.Equal("PlayerRevive", BattleEventTypes.PlayerRevive);
        Assert.Equal("EnemyKilled", BattleEventTypes.EnemyKilled);
        Assert.Equal("TargetSwitched", BattleEventTypes.TargetSwitched);
        Assert.Equal("WaveCleared", BattleEventTypes.WaveCleared);
        Assert.Equal("BattleCompleted", BattleEventTypes.BattleCompleted);
    }

    [Fact]
    public void StateChangedEvent_CanBeCreated()
    {
        // Arrange
        var eventType = BattleEventTypes.PlayerDeath;
        var timestamp = DateTime.UtcNow;

        // Act
        var evt = new StateChangedEvent(eventType, timestamp);

        // Assert
        Assert.NotNull(evt);
        Assert.Equal(eventType, evt.EventType);
        Assert.Equal(timestamp, evt.Timestamp);
    }

    [Fact]
    public void StateChangedEvent_SupportsAllEventTypes()
    {
        // Arrange
        var timestamp = DateTime.UtcNow;
        var eventTypes = new[]
        {
            BattleEventTypes.PlayerDeath,
            BattleEventTypes.PlayerRevive,
            BattleEventTypes.EnemyKilled,
            BattleEventTypes.TargetSwitched,
            BattleEventTypes.WaveCleared,
            BattleEventTypes.BattleCompleted
        };

        // Act & Assert
        foreach (var eventType in eventTypes)
        {
            var evt = new StateChangedEvent(eventType, timestamp);
            Assert.Equal(eventType, evt.EventType);
            Assert.Equal(timestamp, evt.Timestamp);
        }
    }

    [Fact]
    public void StateChangedEvent_IsImmutable()
    {
        // Arrange
        var eventType = BattleEventTypes.EnemyKilled;
        var timestamp = DateTime.UtcNow;
        var evt1 = new StateChangedEvent(eventType, timestamp);

        // Act - 创建另一个相同的事件
        var evt2 = new StateChangedEvent(eventType, timestamp);

        // Assert - record类型支持值相等性
        Assert.Equal(evt1, evt2);
        Assert.True(evt1 == evt2);
    }

    [Fact]
    public void StateChangedEvent_WithDifferentTimestamps_AreNotEqual()
    {
        // Arrange
        var eventType = BattleEventTypes.TargetSwitched;
        var timestamp1 = DateTime.UtcNow;
        var timestamp2 = timestamp1.AddSeconds(1);
        var evt1 = new StateChangedEvent(eventType, timestamp1);
        var evt2 = new StateChangedEvent(eventType, timestamp2);

        // Assert
        Assert.NotEqual(evt1, evt2);
        Assert.False(evt1 == evt2);
    }
}
