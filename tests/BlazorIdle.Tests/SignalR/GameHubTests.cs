using BlazorIdle.Server.Infrastructure.SignalR;
using BlazorIdle.Server.Infrastructure.SignalR.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Moq;
using System.Security.Claims;

namespace BlazorIdle.Tests.SignalR;

/// <summary>
/// GameHub 单元测试
/// 使用 Mock 测试 GameHub 的各种场景，包括授权、订阅和心跳等
/// </summary>
public class GameHubTests
{
    private readonly Mock<IConnectionManager> _connectionManagerMock;
    private readonly Mock<ILogger<GameHub>> _loggerMock;
    private readonly Mock<HubCallerContext> _contextMock;
    private readonly Mock<IHubCallerClients> _clientsMock;
    private readonly Mock<ISingleClientProxy> _callerMock;
    private readonly Mock<IGroupManager> _groupsMock;
    private readonly GameHub _hub;

    public GameHubTests()
    {
        _connectionManagerMock = new Mock<IConnectionManager>();
        _loggerMock = new Mock<ILogger<GameHub>>();
        _contextMock = new Mock<HubCallerContext>();
        _clientsMock = new Mock<IHubCallerClients>();
        _callerMock = new Mock<ISingleClientProxy>();
        _groupsMock = new Mock<IGroupManager>();

        _hub = new GameHub(_connectionManagerMock.Object, _loggerMock.Object)
        {
            Context = _contextMock.Object,
            Clients = _clientsMock.Object,
            Groups = _groupsMock.Object
        };

        // 默认设置：返回 Caller
        _clientsMock.Setup(c => c.Caller).Returns(_callerMock.Object);
    }

    private void SetupAuthenticatedUser(string userId)
    {
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, userId)
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        _contextMock.Setup(c => c.User).Returns(principal);
        _contextMock.Setup(c => c.ConnectionId).Returns("test-connection-id");
    }

    private void SetupUnauthenticatedUser()
    {
        _contextMock.Setup(c => c.User).Returns((ClaimsPrincipal?)null);
        _contextMock.Setup(c => c.ConnectionId).Returns("test-connection-id");
    }

    [Fact]
    public async Task OnConnectedAsync_WithAuthenticatedUser_ShouldRegisterConnection()
    {
        // Arrange
        var userId = "user1";
        SetupAuthenticatedUser(userId);

        // Act
        await _hub.OnConnectedAsync();

        // Assert
        _connectionManagerMock.Verify(
            cm => cm.RegisterConnectionAsync(userId, "test-connection-id"),
            Times.Once);
        
        _callerMock.Verify(
            c => c.SendCoreAsync(
                "Connected",
                It.Is<object[]>(args => args.Length == 1),
                default),
            Times.Once);
    }

    [Fact]
    public async Task OnConnectedAsync_WithUnauthenticatedUser_ShouldRejectConnection()
    {
        // Arrange
        SetupUnauthenticatedUser();

        // Act
        await _hub.OnConnectedAsync();

        // Assert
        _connectionManagerMock.Verify(
            cm => cm.RegisterConnectionAsync(It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
        
        _callerMock.Verify(
            c => c.SendCoreAsync(
                "Error",
                It.Is<object[]>(args => args.Length == 1 && args[0].ToString() == "Unauthorized"),
                default),
            Times.Once);
        
        _contextMock.Verify(c => c.Abort(), Times.Once);
    }

    [Fact]
    public async Task OnDisconnectedAsync_WithAuthenticatedUser_ShouldUnregisterConnection()
    {
        // Arrange
        var userId = "user1";
        SetupAuthenticatedUser(userId);

        // Act
        await _hub.OnDisconnectedAsync(null);

        // Assert
        _connectionManagerMock.Verify(
            cm => cm.UnregisterConnectionAsync(userId, "test-connection-id"),
            Times.Once);
    }

    [Fact]
    public async Task OnDisconnectedAsync_WithException_ShouldStillUnregisterConnection()
    {
        // Arrange
        var userId = "user1";
        SetupAuthenticatedUser(userId);
        var exception = new Exception("Test exception");

        // Act
        await _hub.OnDisconnectedAsync(exception);

        // Assert
        _connectionManagerMock.Verify(
            cm => cm.UnregisterConnectionAsync(userId, "test-connection-id"),
            Times.Once);
    }

    [Fact]
    public async Task SubscribeToBattle_WithAuthenticatedUser_ShouldAddToGroup()
    {
        // Arrange
        var userId = "user1";
        var battleId = "battle123";
        SetupAuthenticatedUser(userId);

        // Act
        await _hub.SubscribeToBattle(battleId);

        // Assert
        _groupsMock.Verify(
            g => g.AddToGroupAsync("test-connection-id", $"battle:{battleId}", default),
            Times.Once);
        
        _connectionManagerMock.Verify(
            cm => cm.AddSubscriptionAsync(userId, "battle", battleId),
            Times.Once);
        
        _callerMock.Verify(
            c => c.SendCoreAsync(
                "Subscribed",
                It.Is<object[]>(args => 
                    args.Length == 2 && 
                    args[0].ToString() == "battle" && 
                    args[1].ToString() == battleId),
                default),
            Times.Once);
    }

    [Fact]
    public async Task SubscribeToBattle_WithUnauthenticatedUser_ShouldSendError()
    {
        // Arrange
        var battleId = "battle123";
        SetupUnauthenticatedUser();

        // Act
        await _hub.SubscribeToBattle(battleId);

        // Assert
        _groupsMock.Verify(
            g => g.AddToGroupAsync(It.IsAny<string>(), It.IsAny<string>(), default),
            Times.Never);
        
        _callerMock.Verify(
            c => c.SendCoreAsync(
                "Error",
                It.Is<object[]>(args => args.Length == 1 && args[0].ToString() == "Unauthorized"),
                default),
            Times.Once);
    }

    [Fact]
    public async Task UnsubscribeFromBattle_WithAuthenticatedUser_ShouldRemoveFromGroup()
    {
        // Arrange
        var userId = "user1";
        var battleId = "battle123";
        SetupAuthenticatedUser(userId);

        // Act
        await _hub.UnsubscribeFromBattle(battleId);

        // Assert
        _groupsMock.Verify(
            g => g.RemoveFromGroupAsync("test-connection-id", $"battle:{battleId}", default),
            Times.Once);
        
        _connectionManagerMock.Verify(
            cm => cm.RemoveSubscriptionAsync(userId, "battle", battleId),
            Times.Once);
        
        _callerMock.Verify(
            c => c.SendCoreAsync(
                "Unsubscribed",
                It.Is<object[]>(args => 
                    args.Length == 2 && 
                    args[0].ToString() == "battle" && 
                    args[1].ToString() == battleId),
                default),
            Times.Once);
    }

    [Fact]
    public async Task UnsubscribeFromBattle_WithUnauthenticatedUser_ShouldDoNothing()
    {
        // Arrange
        var battleId = "battle123";
        SetupUnauthenticatedUser();

        // Act
        await _hub.UnsubscribeFromBattle(battleId);

        // Assert
        _groupsMock.Verify(
            g => g.RemoveFromGroupAsync(It.IsAny<string>(), It.IsAny<string>(), default),
            Times.Never);
    }

    [Fact]
    public async Task SubscribeToParty_WithAuthenticatedUser_ShouldAddToGroup()
    {
        // Arrange
        var userId = "user1";
        var partyId = "party123";
        SetupAuthenticatedUser(userId);

        // Act
        await _hub.SubscribeToParty(partyId);

        // Assert
        _groupsMock.Verify(
            g => g.AddToGroupAsync("test-connection-id", $"party:{partyId}", default),
            Times.Once);
        
        _connectionManagerMock.Verify(
            cm => cm.AddSubscriptionAsync(userId, "party", partyId),
            Times.Once);
        
        _callerMock.Verify(
            c => c.SendCoreAsync(
                "Subscribed",
                It.Is<object[]>(args => 
                    args.Length == 2 && 
                    args[0].ToString() == "party" && 
                    args[1].ToString() == partyId),
                default),
            Times.Once);
    }

    [Fact]
    public async Task SubscribeToParty_WithUnauthenticatedUser_ShouldSendError()
    {
        // Arrange
        var partyId = "party123";
        SetupUnauthenticatedUser();

        // Act
        await _hub.SubscribeToParty(partyId);

        // Assert
        _groupsMock.Verify(
            g => g.AddToGroupAsync(It.IsAny<string>(), It.IsAny<string>(), default),
            Times.Never);
        
        _callerMock.Verify(
            c => c.SendCoreAsync(
                "Error",
                It.Is<object[]>(args => args.Length == 1 && args[0].ToString() == "Unauthorized"),
                default),
            Times.Once);
    }

    [Fact]
    public async Task UnsubscribeFromParty_WithAuthenticatedUser_ShouldRemoveFromGroup()
    {
        // Arrange
        var userId = "user1";
        var partyId = "party123";
        SetupAuthenticatedUser(userId);

        // Act
        await _hub.UnsubscribeFromParty(partyId);

        // Assert
        _groupsMock.Verify(
            g => g.RemoveFromGroupAsync("test-connection-id", $"party:{partyId}", default),
            Times.Once);
        
        _connectionManagerMock.Verify(
            cm => cm.RemoveSubscriptionAsync(userId, "party", partyId),
            Times.Once);
    }

    [Fact]
    public async Task Heartbeat_WithAuthenticatedUser_ShouldUpdateLastHeartbeat()
    {
        // Arrange
        var userId = "user1";
        SetupAuthenticatedUser(userId);
        
        var mockSession = new BlazorIdle.Server.Infrastructure.SignalR.Models.UserSession
        {
            UserId = userId,
            LastHeartbeat = DateTime.UtcNow.AddMinutes(-1)
        };
        
        _connectionManagerMock
            .Setup(cm => cm.GetSessionAsync(userId))
            .ReturnsAsync(mockSession);

        var oldHeartbeat = mockSession.LastHeartbeat;

        // Act
        await _hub.Heartbeat();

        // Assert
        _connectionManagerMock.Verify(
            cm => cm.GetSessionAsync(userId),
            Times.Once);
        
        Assert.True(mockSession.LastHeartbeat > oldHeartbeat);
    }

    [Fact]
    public async Task Heartbeat_WithUnauthenticatedUser_ShouldDoNothing()
    {
        // Arrange
        SetupUnauthenticatedUser();

        // Act
        await _hub.Heartbeat();

        // Assert
        _connectionManagerMock.Verify(
            cm => cm.GetSessionAsync(It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task Heartbeat_WithNullSession_ShouldNotThrow()
    {
        // Arrange
        var userId = "user1";
        SetupAuthenticatedUser(userId);
        
        _connectionManagerMock
            .Setup(cm => cm.GetSessionAsync(userId))
            .ReturnsAsync((BlazorIdle.Server.Infrastructure.SignalR.Models.UserSession?)null);

        // Act & Assert - 应该不抛出异常
        await _hub.Heartbeat();
        
        _connectionManagerMock.Verify(
            cm => cm.GetSessionAsync(userId),
            Times.Once);
    }

    [Fact]
    public async Task RequestBattleSync_WithAuthenticatedUser_ShouldSendSyncRequested()
    {
        // Arrange
        var userId = "user1";
        var battleId = "battle123";
        var lastVersion = 42L;
        SetupAuthenticatedUser(userId);

        // Act
        await _hub.RequestBattleSync(battleId, lastVersion);

        // Assert
        _callerMock.Verify(
            c => c.SendCoreAsync(
                "SyncRequested",
                It.Is<object[]>(args => 
                    args.Length == 2 && 
                    args[0].ToString() == battleId && 
                    (long)args[1] == lastVersion),
                default),
            Times.Once);
    }

    [Fact]
    public async Task RequestBattleSync_WithUnauthenticatedUser_ShouldSendError()
    {
        // Arrange
        var battleId = "battle123";
        var lastVersion = 42L;
        SetupUnauthenticatedUser();

        // Act
        await _hub.RequestBattleSync(battleId, lastVersion);

        // Assert
        _callerMock.Verify(
            c => c.SendCoreAsync(
                "Error",
                It.Is<object[]>(args => args.Length == 1 && args[0].ToString() == "Unauthorized"),
                default),
            Times.Once);
        
        _callerMock.Verify(
            c => c.SendCoreAsync("SyncRequested", It.IsAny<object[]>(), default),
            Times.Never);
    }

    [Fact]
    public async Task MultipleSubscriptions_ShouldHandleCorrectly()
    {
        // Arrange
        var userId = "user1";
        var battleId1 = "battle1";
        var battleId2 = "battle2";
        var partyId = "party1";
        SetupAuthenticatedUser(userId);

        // Act
        await _hub.SubscribeToBattle(battleId1);
        await _hub.SubscribeToBattle(battleId2);
        await _hub.SubscribeToParty(partyId);

        // Assert
        _groupsMock.Verify(
            g => g.AddToGroupAsync("test-connection-id", $"battle:{battleId1}", default),
            Times.Once);
        _groupsMock.Verify(
            g => g.AddToGroupAsync("test-connection-id", $"battle:{battleId2}", default),
            Times.Once);
        _groupsMock.Verify(
            g => g.AddToGroupAsync("test-connection-id", $"party:{partyId}", default),
            Times.Once);
        
        _connectionManagerMock.Verify(
            cm => cm.AddSubscriptionAsync(userId, "battle", battleId1),
            Times.Once);
        _connectionManagerMock.Verify(
            cm => cm.AddSubscriptionAsync(userId, "battle", battleId2),
            Times.Once);
        _connectionManagerMock.Verify(
            cm => cm.AddSubscriptionAsync(userId, "party", partyId),
            Times.Once);
    }
}
