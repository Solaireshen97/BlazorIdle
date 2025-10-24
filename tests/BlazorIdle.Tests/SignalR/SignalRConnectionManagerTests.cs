using BlazorIdle.Client.Services.SignalR;
using BlazorIdle.Services.Auth;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace BlazorIdle.Tests.SignalR;

/// <summary>
/// SignalRConnectionManager单元测试
/// 测试连接管理器的核心功能和JWT认证集成
/// </summary>
public class SignalRConnectionManagerTests : IDisposable
{
    private readonly Mock<ILogger<SignalRConnectionManager>> _loggerMock;
    private readonly Mock<IAuthenticationService> _authServiceMock;
    private readonly SignalRClientOptions _options;
    private SignalRConnectionManager? _manager;

    public SignalRConnectionManagerTests()
    {
        _loggerMock = new Mock<ILogger<SignalRConnectionManager>>();
        _authServiceMock = new Mock<IAuthenticationService>();
        
        // 默认设置：模拟未登录状态（无Token）
        _authServiceMock.Setup(x => x.GetTokenAsync()).ReturnsAsync((string?)null);
        
        // 使用测试配置
        _options = new SignalRClientOptions
        {
            HubUrl = "https://localhost:7056/hubs/game",
            EnableAutoReconnect = false, // 测试中禁用自动重连
            EnableHeartbeat = false, // 测试中禁用心跳
            HeartbeatIntervalSeconds = 1,
            ConnectionTimeoutSeconds = 5,
            MessageHandlerTimeoutMs = 1000
        };
    }

    [Fact]
    public void Constructor_ValidOptions_ShouldCreateInstance()
    {
        // Arrange & Act - 使用有效配置创建实例
        _manager = new SignalRConnectionManager(_loggerMock.Object, _options, _authServiceMock.Object);

        // Assert - 实例应成功创建
        Assert.NotNull(_manager);
        Assert.Equal(HubConnectionState.Disconnected, _manager.State);
        Assert.False(_manager.IsConnected);
        Assert.Null(_manager.ConnectionId);
    }

    [Fact]
    public void Constructor_InvalidOptions_ShouldThrow()
    {
        // Arrange - 准备无效配置
        var invalidOptions = new SignalRClientOptions { HubUrl = "" };

        // Act & Assert - 构造函数应抛出异常
        Assert.Throws<InvalidOperationException>(() =>
            new SignalRConnectionManager(_loggerMock.Object, invalidOptions, _authServiceMock.Object));
    }

    [Fact]
    public async Task InitializeAsync_ShouldConfigureConnection()
    {
        // Arrange
        _manager = new SignalRConnectionManager(_loggerMock.Object, _options, _authServiceMock.Object);

        // Act - 初始化连接
        await _manager.InitializeAsync();

        // Assert - 连接应该被配置，但尚未启动
        Assert.Equal(HubConnectionState.Disconnected, _manager.State);
        Assert.False(_manager.IsConnected);
        
        // 验证已调用GetTokenAsync获取认证Token
        _authServiceMock.Verify(x => x.GetTokenAsync(), Times.Once);
    }

    [Fact]
    public async Task InitializeAsync_WithValidToken_ShouldConfigureAuth()
    {
        // Arrange
        _manager = new SignalRConnectionManager(_loggerMock.Object, _options, _authServiceMock.Object);
        var testToken = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.test";
        
        // 模拟已登录状态（有Token）
        _authServiceMock.Setup(x => x.GetTokenAsync()).ReturnsAsync(testToken);

        // Act - 初始化连接
        await _manager.InitializeAsync();

        // Assert - 连接应该被配置，并附加了Token
        Assert.Equal(HubConnectionState.Disconnected, _manager.State);
        _authServiceMock.Verify(x => x.GetTokenAsync(), Times.Once);
    }

    [Fact]
    public async Task StartAsync_WithoutInitialize_ShouldThrow()
    {
        // Arrange
        _manager = new SignalRConnectionManager(_loggerMock.Object, _options, _authServiceMock.Object);

        // Act & Assert - 未初始化就启动应抛出异常
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await _manager.StartAsync());
    }

    [Fact]
    public async Task SendAsync_WithoutConnection_ShouldThrow()
    {
        // Arrange
        _manager = new SignalRConnectionManager(_loggerMock.Object, _options, _authServiceMock.Object);
        await _manager.InitializeAsync();

        // Act & Assert - 未连接时发送消息应抛出异常
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await _manager.SendAsync("TestMethod", "arg1"));
    }

    [Fact]
    public async Task InvokeAsync_WithoutConnection_ShouldThrow()
    {
        // Arrange
        _manager = new SignalRConnectionManager(_loggerMock.Object, _options, _authServiceMock.Object);
        await _manager.InitializeAsync();

        // Act & Assert - 未连接时调用方法应抛出异常
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await _manager.InvokeAsync<string>("TestMethod", "arg1"));
    }

    [Fact]
    public async Task On_WithoutInitialize_ShouldThrow()
    {
        // Arrange
        _manager = new SignalRConnectionManager(_loggerMock.Object, _options, _authServiceMock.Object);

        // Act & Assert - 未初始化就注册处理器应抛出异常
        Assert.Throws<InvalidOperationException>(() =>
            _manager.On<string>("TestMessage", msg => Task.CompletedTask));
    }

    [Fact]
    public async Task On_AfterInitialize_ShouldRegisterHandler()
    {
        // Arrange
        _manager = new SignalRConnectionManager(_loggerMock.Object, _options, _authServiceMock.Object);
        await _manager.InitializeAsync();
        var handlerCalled = false;

        // Act - 注册消息处理器
        var subscription = _manager.On<string>("TestMessage", msg =>
        {
            handlerCalled = true;
            return Task.CompletedTask;
        });

        // Assert - 订阅应该成功
        Assert.NotNull(subscription);
        // 注意：由于无法触发实际的SignalR消息，这里只验证注册不抛异常
    }

    [Fact]
    public async Task DisposeAsync_ShouldCleanupResources()
    {
        // Arrange
        _manager = new SignalRConnectionManager(_loggerMock.Object, _options, _authServiceMock.Object);
        await _manager.InitializeAsync();

        // Act - 释放资源
        await _manager.DisposeAsync();

        // Assert - 再次调用应该安全（幂等性）
        await _manager.DisposeAsync();
    }

    [Fact]
    public async Task DisposeAsync_AfterDispose_OperationsShouldThrow()
    {
        // Arrange
        _manager = new SignalRConnectionManager(_loggerMock.Object, _options, _authServiceMock.Object);
        await _manager.InitializeAsync();
        await _manager.DisposeAsync();

        // Act & Assert - 释放后的操作应抛出异常
        await Assert.ThrowsAsync<ObjectDisposedException>(async () =>
            await _manager.InitializeAsync());
    }

    [Fact]
    public void State_BeforeInitialize_ShouldBeDisconnected()
    {
        // Arrange & Act
        _manager = new SignalRConnectionManager(_loggerMock.Object, _options, _authServiceMock.Object);

        // Assert
        Assert.Equal(HubConnectionState.Disconnected, _manager.State);
    }

    [Fact]
    public void IsConnected_BeforeConnection_ShouldBeFalse()
    {
        // Arrange & Act
        _manager = new SignalRConnectionManager(_loggerMock.Object, _options, _authServiceMock.Object);

        // Assert
        Assert.False(_manager.IsConnected);
    }

    [Fact]
    public void ConnectionId_BeforeConnection_ShouldBeNull()
    {
        // Arrange & Act
        _manager = new SignalRConnectionManager(_loggerMock.Object, _options, _authServiceMock.Object);

        // Assert
        Assert.Null(_manager.ConnectionId);
    }

    [Fact]
    public async Task SubscribeToBattleAsync_WithoutConnection_ShouldThrow()
    {
        // Arrange
        _manager = new SignalRConnectionManager(_loggerMock.Object, _options, _authServiceMock.Object);
        await _manager.InitializeAsync();

        // Act & Assert - 未连接时订阅应抛出异常
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await _manager.SubscribeToBattleAsync("battle-123"));
    }

    [Fact]
    public async Task UnsubscribeFromBattleAsync_WithoutConnection_ShouldThrow()
    {
        // Arrange
        _manager = new SignalRConnectionManager(_loggerMock.Object, _options, _authServiceMock.Object);
        await _manager.InitializeAsync();

        // Act & Assert - 未连接时取消订阅应抛出异常
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await _manager.UnsubscribeFromBattleAsync("battle-123"));
    }

    [Fact]
    public async Task SubscribeToPartyAsync_WithoutConnection_ShouldThrow()
    {
        // Arrange
        _manager = new SignalRConnectionManager(_loggerMock.Object, _options, _authServiceMock.Object);
        await _manager.InitializeAsync();

        // Act & Assert - 未连接时订阅应抛出异常
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await _manager.SubscribeToPartyAsync("party-123"));
    }

    [Fact]
    public async Task RequestBattleSyncAsync_WithoutConnection_ShouldThrow()
    {
        // Arrange
        _manager = new SignalRConnectionManager(_loggerMock.Object, _options, _authServiceMock.Object);
        await _manager.InitializeAsync();

        // Act & Assert - 未连接时请求同步应抛出异常
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await _manager.RequestBattleSyncAsync("battle-123", 100));
    }

    [Fact]
    public async Task MultipleInitialize_ShouldDisposeOldConnection()
    {
        // Arrange
        _manager = new SignalRConnectionManager(_loggerMock.Object, _options, _authServiceMock.Object);
        await _manager.InitializeAsync();

        // Act - 多次初始化应该正确处理旧连接
        await _manager.InitializeAsync();
        await _manager.InitializeAsync();

        // Assert - 连接应该被重新配置
        Assert.Equal(HubConnectionState.Disconnected, _manager.State);
        // 验证每次初始化都会获取Token
        _authServiceMock.Verify(x => x.GetTokenAsync(), Times.Exactly(3));
    }

    [Fact]
    public async Task Events_ShouldBeSubscribable()
    {
        // Arrange
        _manager = new SignalRConnectionManager(_loggerMock.Object, _options, _authServiceMock.Object);
        var connectedCalled = false;
        var disconnectedCalled = false;
        var reconnectingCalled = false;
        var reconnectedCalled = false;

        // Act - 注册事件处理器（应该不抛出异常）
        _manager.Connected += () => { connectedCalled = true; return Task.CompletedTask; };
        _manager.Disconnected += _ => { disconnectedCalled = true; return Task.CompletedTask; };
        _manager.Reconnecting += _ => { reconnectingCalled = true; return Task.CompletedTask; };
        _manager.Reconnected += _ => { reconnectedCalled = true; return Task.CompletedTask; };

        // Assert - 事件订阅应该成功（无异常）
        // 由于无法直接验证事件是否为null，我们通过能够成功订阅来证明事件存在
        Assert.False(connectedCalled); // 事件尚未触发
        Assert.False(disconnectedCalled);
        Assert.False(reconnectingCalled);
        Assert.False(reconnectedCalled);
    }

    public void Dispose()
    {
        _manager?.DisposeAsync().AsTask().Wait(TimeSpan.FromSeconds(5));
    }
}
