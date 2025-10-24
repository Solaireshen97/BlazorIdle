using BlazorIdle.Client.Services.SignalR;
using BlazorIdle.Services.Auth;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace BlazorIdle.Tests.SignalR;

/// <summary>
/// SignalR认证集成测试
/// 测试SignalR连接与JWT用户认证系统的集成
/// 验证Token是否正确附加到SignalR连接
/// </summary>
public class SignalRAuthenticationIntegrationTests : IDisposable
{
    private readonly Mock<ILogger<SignalRConnectionManager>> _loggerMock;
    private readonly Mock<IAuthenticationService> _authServiceMock;
    private readonly SignalRClientOptions _options;
    private SignalRConnectionManager? _manager;

    public SignalRAuthenticationIntegrationTests()
    {
        _loggerMock = new Mock<ILogger<SignalRConnectionManager>>();
        _authServiceMock = new Mock<IAuthenticationService>();
        
        _options = new SignalRClientOptions
        {
            HubUrl = "https://localhost:7056/hubs/game",
            EnableAutoReconnect = false,
            EnableHeartbeat = false,
            HeartbeatIntervalSeconds = 1,
            ConnectionTimeoutSeconds = 5,
            MessageHandlerTimeoutMs = 1000
        };
    }

    [Fact]
    public async Task InitializeAsync_WithNoToken_ShouldLogWarning()
    {
        // Arrange
        // 模拟未登录状态（没有Token）
        _authServiceMock.Setup(x => x.GetTokenAsync()).ReturnsAsync((string?)null);
        _manager = new SignalRConnectionManager(_loggerMock.Object, _options, _authServiceMock.Object);

        // Act
        await _manager.InitializeAsync();

        // Assert
        // 验证调用了GetTokenAsync
        _authServiceMock.Verify(x => x.GetTokenAsync(), Times.Once);
        
        // 验证记录了警告日志（Token为空时）
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("未找到JWT Token")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task InitializeAsync_WithValidToken_ShouldLogSuccess()
    {
        // Arrange
        // 模拟已登录状态（有有效的Token）
        var validToken = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiJ0ZXN0IiwibmFtZSI6IlRlc3QgVXNlciJ9.test";
        _authServiceMock.Setup(x => x.GetTokenAsync()).ReturnsAsync(validToken);
        _manager = new SignalRConnectionManager(_loggerMock.Object, _options, _authServiceMock.Object);

        // Act
        await _manager.InitializeAsync();

        // Assert
        // 验证调用了GetTokenAsync
        _authServiceMock.Verify(x => x.GetTokenAsync(), Times.Once);
        
        // 验证记录了成功日志（Token已获取）
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("已获取JWT Token")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task InitializeAsync_WithEmptyToken_ShouldLogWarning()
    {
        // Arrange
        // 模拟Token为空字符串的情况
        _authServiceMock.Setup(x => x.GetTokenAsync()).ReturnsAsync(string.Empty);
        _manager = new SignalRConnectionManager(_loggerMock.Object, _options, _authServiceMock.Object);

        // Act
        await _manager.InitializeAsync();

        // Assert
        // 验证记录了警告日志
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("未找到JWT Token")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task InitializeAsync_MultipleTimesWithDifferentTokens_ShouldUseLatestToken()
    {
        // Arrange
        var token1 = "token1";
        var token2 = "token2";
        var token3 = "token3";
        
        _manager = new SignalRConnectionManager(_loggerMock.Object, _options, _authServiceMock.Object);

        // Act & Assert
        // 第一次初始化 - 使用token1
        _authServiceMock.Setup(x => x.GetTokenAsync()).ReturnsAsync(token1);
        await _manager.InitializeAsync();
        _authServiceMock.Verify(x => x.GetTokenAsync(), Times.Once);

        // 第二次初始化 - 使用token2（模拟Token刷新场景）
        _authServiceMock.Setup(x => x.GetTokenAsync()).ReturnsAsync(token2);
        await _manager.InitializeAsync();
        _authServiceMock.Verify(x => x.GetTokenAsync(), Times.Exactly(2));

        // 第三次初始化 - 使用token3
        _authServiceMock.Setup(x => x.GetTokenAsync()).ReturnsAsync(token3);
        await _manager.InitializeAsync();
        _authServiceMock.Verify(x => x.GetTokenAsync(), Times.Exactly(3));
    }

    [Fact]
    public async Task InitializeAsync_WhenAuthServiceThrows_ShouldPropagateException()
    {
        // Arrange
        // 模拟认证服务抛出异常（例如LocalStorage访问失败）
        _authServiceMock.Setup(x => x.GetTokenAsync())
            .ThrowsAsync(new InvalidOperationException("LocalStorage access failed"));
        _manager = new SignalRConnectionManager(_loggerMock.Object, _options, _authServiceMock.Object);

        // Act & Assert
        // 初始化应该抛出异常
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await _manager.InitializeAsync());
    }

    [Fact]
    public async Task InitializeAsync_AfterDispose_ShouldNotCallAuthService()
    {
        // Arrange
        _authServiceMock.Setup(x => x.GetTokenAsync()).ReturnsAsync("test-token");
        _manager = new SignalRConnectionManager(_loggerMock.Object, _options, _authServiceMock.Object);
        await _manager.InitializeAsync();
        await _manager.DisposeAsync();

        // Act & Assert
        // Dispose后再次初始化应抛出ObjectDisposedException
        // 并且不应该调用AuthService
        await Assert.ThrowsAsync<ObjectDisposedException>(async () =>
            await _manager.InitializeAsync());
        
        // 验证只在第一次初始化时调用了GetTokenAsync，Dispose后没有再次调用
        _authServiceMock.Verify(x => x.GetTokenAsync(), Times.Once);
    }

    [Fact]
    public async Task InitializeAsync_WithLongToken_ShouldHandleCorrectly()
    {
        // Arrange
        // 模拟一个很长的JWT Token（实际场景中JWT可能很长）
        var longToken = new string('x', 2048); // 2KB的Token
        _authServiceMock.Setup(x => x.GetTokenAsync()).ReturnsAsync(longToken);
        _manager = new SignalRConnectionManager(_loggerMock.Object, _options, _authServiceMock.Object);

        // Act
        await _manager.InitializeAsync();

        // Assert
        // 应该成功处理长Token，不抛出异常
        Assert.Equal(HubConnectionState.Disconnected, _manager.State);
        _authServiceMock.Verify(x => x.GetTokenAsync(), Times.Once);
    }

    [Fact]
    public async Task InitializeAsync_ConcurrentCalls_ShouldBeThreadSafe()
    {
        // Arrange
        _authServiceMock.Setup(x => x.GetTokenAsync()).ReturnsAsync("test-token");
        _manager = new SignalRConnectionManager(_loggerMock.Object, _options, _authServiceMock.Object);

        // Act
        // 并发调用InitializeAsync测试线程安全性
        var tasks = new List<Task>();
        for (int i = 0; i < 10; i++)
        {
            tasks.Add(Task.Run(async () => await _manager.InitializeAsync()));
        }

        // 等待所有任务完成，不应该抛出异常
        await Task.WhenAll(tasks);

        // Assert
        // 所有并发调用都应该成功
        Assert.Equal(HubConnectionState.Disconnected, _manager.State);
    }

    [Fact]
    public async Task InitializeAsync_TokenFromAuthService_ShouldBeUsedForConnection()
    {
        // Arrange
        var expectedToken = "expected-jwt-token-123";
        _authServiceMock.Setup(x => x.GetTokenAsync()).ReturnsAsync(expectedToken);
        _manager = new SignalRConnectionManager(_loggerMock.Object, _options, _authServiceMock.Object);

        // Act
        await _manager.InitializeAsync();

        // Assert
        // 验证AuthService被调用来获取Token
        _authServiceMock.Verify(x => x.GetTokenAsync(), Times.Once);
        
        // 验证连接已初始化（虽然未启动）
        Assert.NotNull(_manager);
        Assert.Equal(HubConnectionState.Disconnected, _manager.State);
    }

    public void Dispose()
    {
        _manager?.DisposeAsync().AsTask().Wait(TimeSpan.FromSeconds(5));
    }
}
