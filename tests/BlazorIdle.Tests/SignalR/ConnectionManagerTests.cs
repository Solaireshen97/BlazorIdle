using BlazorIdle.Server.Infrastructure.SignalR;
using BlazorIdle.Server.Infrastructure.SignalR.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace BlazorIdle.Tests.SignalR;

/// <summary>
/// ConnectionManager 单元测试
/// 测试连接管理器的核心功能和线程安全性
/// </summary>
public class ConnectionManagerTests
{
    private readonly IConnectionManager _connectionManager;
    private readonly Mock<ILogger<ConnectionManager>> _loggerMock;

    public ConnectionManagerTests()
    {
        _loggerMock = new Mock<ILogger<ConnectionManager>>();
        _connectionManager = new ConnectionManager(_loggerMock.Object);
    }

    [Fact]
    public async Task RegisterConnectionAsync_ShouldCreateNewSession()
    {
        // Arrange
        var userId = "user1";
        var connectionId = "conn1";

        // Act
        await _connectionManager.RegisterConnectionAsync(userId, connectionId);

        // Assert
        var isConnected = await _connectionManager.IsConnectedAsync(userId);
        Assert.True(isConnected);
    }

    [Fact]
    public async Task RegisterConnectionAsync_ShouldAddMultipleConnections()
    {
        // Arrange
        var userId = "user1";
        var connectionId1 = "conn1";
        var connectionId2 = "conn2";

        // Act
        await _connectionManager.RegisterConnectionAsync(userId, connectionId1);
        await _connectionManager.RegisterConnectionAsync(userId, connectionId2);

        // Assert
        var connectionIds = await _connectionManager.GetConnectionIdsAsync(userId);
        Assert.Equal(2, connectionIds.Count());
        Assert.Contains(connectionId1, connectionIds);
        Assert.Contains(connectionId2, connectionIds);
    }

    [Fact]
    public async Task UnregisterConnectionAsync_ShouldRemoveConnection()
    {
        // Arrange
        var userId = "user1";
        var connectionId = "conn1";
        await _connectionManager.RegisterConnectionAsync(userId, connectionId);

        // Act
        await _connectionManager.UnregisterConnectionAsync(userId, connectionId);

        // Assert
        var isConnected = await _connectionManager.IsConnectedAsync(userId);
        Assert.False(isConnected);
    }

    [Fact]
    public async Task UnregisterConnectionAsync_ShouldKeepSessionIfOtherConnectionsExist()
    {
        // Arrange
        var userId = "user1";
        var connectionId1 = "conn1";
        var connectionId2 = "conn2";
        await _connectionManager.RegisterConnectionAsync(userId, connectionId1);
        await _connectionManager.RegisterConnectionAsync(userId, connectionId2);

        // Act
        await _connectionManager.UnregisterConnectionAsync(userId, connectionId1);

        // Assert
        var isConnected = await _connectionManager.IsConnectedAsync(userId);
        Assert.True(isConnected);
        
        var connectionIds = await _connectionManager.GetConnectionIdsAsync(userId);
        Assert.Single(connectionIds);
        Assert.Contains(connectionId2, connectionIds);
    }

    [Fact]
    public async Task GetConnectionIdAsync_ShouldReturnFirstConnection()
    {
        // Arrange
        var userId = "user1";
        var connectionId1 = "conn1";
        var connectionId2 = "conn2";
        await _connectionManager.RegisterConnectionAsync(userId, connectionId1);
        await _connectionManager.RegisterConnectionAsync(userId, connectionId2);

        // Act
        var connectionId = await _connectionManager.GetConnectionIdAsync(userId);

        // Assert
        Assert.NotNull(connectionId);
        Assert.True(connectionId == connectionId1 || connectionId == connectionId2);
    }

    [Fact]
    public async Task GetConnectionIdAsync_ShouldReturnNullForNonExistentUser()
    {
        // Act
        var connectionId = await _connectionManager.GetConnectionIdAsync("nonexistent");

        // Assert
        Assert.Null(connectionId);
    }

    [Fact]
    public async Task IsConnectedAsync_ShouldReturnFalseForNonExistentUser()
    {
        // Act
        var isConnected = await _connectionManager.IsConnectedAsync("nonexistent");

        // Assert
        Assert.False(isConnected);
    }

    [Fact]
    public async Task GetSessionAsync_ShouldReturnSessionData()
    {
        // Arrange
        var userId = "user1";
        var connectionId = "conn1";
        await _connectionManager.RegisterConnectionAsync(userId, connectionId);

        // Act
        var session = await _connectionManager.GetSessionAsync(userId);

        // Assert
        Assert.NotNull(session);
        Assert.Equal(userId, session.UserId);
        Assert.Contains(connectionId, session.ConnectionIds);
        Assert.True(session.ConnectedAt <= DateTime.UtcNow);
        Assert.True(session.LastHeartbeat <= DateTime.UtcNow);
    }

    [Fact]
    public async Task GetSessionAsync_ShouldReturnNullForNonExistentUser()
    {
        // Act
        var session = await _connectionManager.GetSessionAsync("nonexistent");

        // Assert
        Assert.Null(session);
    }

    [Fact]
    public async Task AddSubscriptionAsync_ShouldAddSubscription()
    {
        // Arrange
        var userId = "user1";
        var connectionId = "conn1";
        await _connectionManager.RegisterConnectionAsync(userId, connectionId);

        // Act
        await _connectionManager.AddSubscriptionAsync(userId, "battle", "battle1");

        // Assert
        var session = await _connectionManager.GetSessionAsync(userId);
        Assert.NotNull(session);
        Assert.True(session.Subscriptions.ContainsKey("battle"));
        Assert.Contains("battle1", session.Subscriptions["battle"]);
    }

    [Fact]
    public async Task AddSubscriptionAsync_ShouldAddMultipleSubscriptionsOfSameType()
    {
        // Arrange
        var userId = "user1";
        var connectionId = "conn1";
        await _connectionManager.RegisterConnectionAsync(userId, connectionId);

        // Act
        await _connectionManager.AddSubscriptionAsync(userId, "battle", "battle1");
        await _connectionManager.AddSubscriptionAsync(userId, "battle", "battle2");

        // Assert
        var session = await _connectionManager.GetSessionAsync(userId);
        Assert.NotNull(session);
        Assert.Equal(2, session.Subscriptions["battle"].Count);
        Assert.Contains("battle1", session.Subscriptions["battle"]);
        Assert.Contains("battle2", session.Subscriptions["battle"]);
    }

    [Fact]
    public async Task AddSubscriptionAsync_ShouldAddMultipleSubscriptionTypes()
    {
        // Arrange
        var userId = "user1";
        var connectionId = "conn1";
        await _connectionManager.RegisterConnectionAsync(userId, connectionId);

        // Act
        await _connectionManager.AddSubscriptionAsync(userId, "battle", "battle1");
        await _connectionManager.AddSubscriptionAsync(userId, "party", "party1");

        // Assert
        var session = await _connectionManager.GetSessionAsync(userId);
        Assert.NotNull(session);
        Assert.True(session.Subscriptions.ContainsKey("battle"));
        Assert.True(session.Subscriptions.ContainsKey("party"));
        Assert.Contains("battle1", session.Subscriptions["battle"]);
        Assert.Contains("party1", session.Subscriptions["party"]);
    }

    [Fact]
    public async Task RemoveSubscriptionAsync_ShouldRemoveSubscription()
    {
        // Arrange
        var userId = "user1";
        var connectionId = "conn1";
        await _connectionManager.RegisterConnectionAsync(userId, connectionId);
        await _connectionManager.AddSubscriptionAsync(userId, "battle", "battle1");

        // Act
        await _connectionManager.RemoveSubscriptionAsync(userId, "battle", "battle1");

        // Assert
        var session = await _connectionManager.GetSessionAsync(userId);
        Assert.NotNull(session);
        Assert.DoesNotContain("battle1", session.Subscriptions.GetValueOrDefault("battle") ?? new HashSet<string>());
    }

    [Fact]
    public async Task GetIdleSessions_ShouldReturnIdleSessions()
    {
        // Arrange
        var userId1 = "user1";
        var userId2 = "user2";
        var connectionId1 = "conn1";
        var connectionId2 = "conn2";
        
        await _connectionManager.RegisterConnectionAsync(userId1, connectionId1);
        await _connectionManager.RegisterConnectionAsync(userId2, connectionId2);
        
        // 让第一个用户的会话变得空闲
        var session1 = await _connectionManager.GetSessionAsync(userId1);
        if (session1 != null)
        {
            session1.LastHeartbeat = DateTime.UtcNow.AddMinutes(-10);
        }

        // Act
        var idleSessions = _connectionManager.GetIdleSessions(TimeSpan.FromMinutes(5));

        // Assert
        Assert.Single(idleSessions);
        Assert.Equal(userId1, idleSessions.First().UserId);
    }

    [Fact]
    public async Task GetIdleSessions_ShouldReturnEmptyWhenNoIdleSessions()
    {
        // Arrange
        var userId = "user1";
        var connectionId = "conn1";
        await _connectionManager.RegisterConnectionAsync(userId, connectionId);

        // Act
        var idleSessions = _connectionManager.GetIdleSessions(TimeSpan.FromMinutes(5));

        // Assert
        Assert.Empty(idleSessions);
    }

    [Fact]
    public async Task ConcurrentRegistration_ShouldHandleThreadSafely()
    {
        // Arrange
        var userId = "user1";
        var tasks = new List<Task>();

        // Act - 并发注册10个连接
        for (int i = 0; i < 10; i++)
        {
            var connectionId = $"conn{i}";
            tasks.Add(_connectionManager.RegisterConnectionAsync(userId, connectionId));
        }
        await Task.WhenAll(tasks);

        // Assert
        var connectionIds = await _connectionManager.GetConnectionIdsAsync(userId);
        Assert.Equal(10, connectionIds.Count());
    }

    [Fact]
    public async Task ConcurrentUnregistration_ShouldHandleThreadSafely()
    {
        // Arrange
        var userId = "user1";
        var connectionIdList = new List<string>();
        
        for (int i = 0; i < 10; i++)
        {
            var connectionId = $"conn{i}";
            connectionIdList.Add(connectionId);
            await _connectionManager.RegisterConnectionAsync(userId, connectionId);
        }

        // Act - 并发注销10个连接
        var tasks = connectionIdList.Select(id => 
            _connectionManager.UnregisterConnectionAsync(userId, id));
        await Task.WhenAll(tasks);

        // Assert
        var isConnected = await _connectionManager.IsConnectedAsync(userId);
        Assert.False(isConnected);
    }

    [Fact]
    public async Task UpdateHeartbeat_ShouldUpdateTimestamp()
    {
        // Arrange
        var userId = "user1";
        var connectionId = "conn1";
        await _connectionManager.RegisterConnectionAsync(userId, connectionId);
        
        var session = await _connectionManager.GetSessionAsync(userId);
        Assert.NotNull(session);
        var initialHeartbeat = session.LastHeartbeat;
        
        // 等待1毫秒确保时间戳不同
        await Task.Delay(1);

        // Act
        session.LastHeartbeat = DateTime.UtcNow;

        // Assert
        var updatedSession = await _connectionManager.GetSessionAsync(userId);
        Assert.NotNull(updatedSession);
        Assert.True(updatedSession.LastHeartbeat > initialHeartbeat);
    }

    [Fact]
    public async Task RegisterConnectionAsync_ShouldNotAddDuplicateConnection()
    {
        // Arrange
        var userId = "user1";
        var connectionId = "conn1";

        // Act
        await _connectionManager.RegisterConnectionAsync(userId, connectionId);
        await _connectionManager.RegisterConnectionAsync(userId, connectionId);

        // Assert
        var connectionIds = await _connectionManager.GetConnectionIdsAsync(userId);
        Assert.Single(connectionIds);
    }

    [Fact]
    public async Task GetConnectionIdsAsync_ShouldReturnCopy()
    {
        // Arrange
        var userId = "user1";
        var connectionId1 = "conn1";
        var connectionId2 = "conn2";
        await _connectionManager.RegisterConnectionAsync(userId, connectionId1);

        // Act
        var connectionIds1 = await _connectionManager.GetConnectionIdsAsync(userId);
        var list1 = connectionIds1.ToList();
        
        await _connectionManager.RegisterConnectionAsync(userId, connectionId2);
        
        var connectionIds2 = await _connectionManager.GetConnectionIdsAsync(userId);
        var list2 = connectionIds2.ToList();

        // Assert
        Assert.Single(list1);
        Assert.Equal(2, list2.Count);
    }
}
