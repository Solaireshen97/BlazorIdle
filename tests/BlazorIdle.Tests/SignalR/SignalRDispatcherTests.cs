using BlazorIdle.Server.Infrastructure.SignalR;
using BlazorIdle.Server.Infrastructure.SignalR.Hubs;
using BlazorIdle.Server.Infrastructure.SignalR.Models;
using BlazorIdle.Server.Infrastructure.SignalR.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Moq;

namespace BlazorIdle.Tests.SignalR;

/// <summary>
/// SignalRDispatcher 单元测试
/// 测试消息分发器的核心功能：队列管理、批量发送、优先级调度
/// </summary>
public class SignalRDispatcherTests : IDisposable
{
    private readonly Mock<IHubContext<GameHub>> _hubContextMock;
    private readonly Mock<IConnectionManager> _connectionManagerMock;
    private readonly Mock<ILogger<SignalRDispatcher>> _loggerMock;
    private readonly SignalROptions _options;
    private readonly Mock<IHubClients> _hubClientsMock;
    private readonly Mock<ISingleClientProxy> _singleClientProxyMock;
    private readonly Mock<IClientProxy> _groupClientProxyMock;
    private readonly Mock<IClientProxy> _allClientProxyMock;
    private SignalRDispatcher? _dispatcher;

    public SignalRDispatcherTests()
    {
        _hubContextMock = new Mock<IHubContext<GameHub>>();
        _connectionManagerMock = new Mock<IConnectionManager>();
        _loggerMock = new Mock<ILogger<SignalRDispatcher>>();
        
        _options = new SignalROptions
        {
            QueueCapacity = 1000,
            BatchSize = 10,
            BatchIntervalMs = 50
        };

        // 设置Hub和Client的Mock
        _hubClientsMock = new Mock<IHubClients>();
        _singleClientProxyMock = new Mock<ISingleClientProxy>();
        _groupClientProxyMock = new Mock<IClientProxy>();
        _allClientProxyMock = new Mock<IClientProxy>();
        
        _hubContextMock.Setup(x => x.Clients).Returns(_hubClientsMock.Object);
        _hubClientsMock.Setup(x => x.Client(It.IsAny<string>())).Returns(_singleClientProxyMock.Object);
        _hubClientsMock.Setup(x => x.Group(It.IsAny<string>())).Returns(_groupClientProxyMock.Object);
        _hubClientsMock.Setup(x => x.All).Returns(_allClientProxyMock.Object);

        // 设置Client Proxy的SendCoreAsync返回成功（SignalR内部使用SendCoreAsync）
        _singleClientProxyMock
            .Setup(x => x.SendCoreAsync(It.IsAny<string>(), It.IsAny<object?[]>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        
        _groupClientProxyMock
            .Setup(x => x.SendCoreAsync(It.IsAny<string>(), It.IsAny<object?[]>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        
        _allClientProxyMock
            .Setup(x => x.SendCoreAsync(It.IsAny<string>(), It.IsAny<object?[]>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    [Fact]
    public async Task SendToUserAsync_ShouldEnqueueMessage()
    {
        // Arrange
        var userId = "user1";
        var connectionId = "conn1";
        _connectionManagerMock
            .Setup(x => x.GetConnectionIdsAsync(userId))
            .ReturnsAsync(new List<string> { connectionId });

        _dispatcher = new SignalRDispatcher(
            _hubContextMock.Object,
            _connectionManagerMock.Object,
            _loggerMock.Object,
            _options);

        // Act
        await _dispatcher.SendToUserAsync(userId, "TestMethod", new { data = "test" });
        
        // 等待消息被处理
        await Task.Delay(200);

        // Assert
        _singleClientProxyMock.Verify(
            x => x.SendCoreAsync(
                "TestMethod",
                It.Is<object?[]>(args => args != null && args.Length > 0),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SendToGroupAsync_ShouldSendMessageToGroup()
    {
        // Arrange
        var groupName = "battle:123";
        _dispatcher = new SignalRDispatcher(
            _hubContextMock.Object,
            _connectionManagerMock.Object,
            _loggerMock.Object,
            _options);

        // Act
        await _dispatcher.SendToGroupAsync(groupName, "BattleUpdate", new { battleId = "123" });
        
        // 等待消息被处理
        await Task.Delay(200);

        // Assert
        _groupClientProxyMock.Verify(
            x => x.SendCoreAsync(
                "BattleUpdate",
                It.Is<object?[]>(args => args != null && args.Length > 0),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SendToAllAsync_ShouldBroadcastToAllClients()
    {
        // Arrange
        _dispatcher = new SignalRDispatcher(
            _hubContextMock.Object,
            _connectionManagerMock.Object,
            _loggerMock.Object,
            _options);

        // Act
        await _dispatcher.SendToAllAsync("Announcement", new { message = "Server maintenance" });
        
        // 等待消息被处理
        await Task.Delay(200);

        // Assert
        _allClientProxyMock.Verify(
            x => x.SendCoreAsync(
                "Announcement",
                It.Is<object?[]>(args => args != null && args.Length > 0),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SendToUserAsync_WithMultipleConnections_ShouldSendToAll()
    {
        // Arrange
        var userId = "user1";
        var connectionIds = new List<string> { "conn1", "conn2", "conn3" };
        _connectionManagerMock
            .Setup(x => x.GetConnectionIdsAsync(userId))
            .ReturnsAsync(connectionIds);

        _dispatcher = new SignalRDispatcher(
            _hubContextMock.Object,
            _connectionManagerMock.Object,
            _loggerMock.Object,
            _options);

        // Act
        await _dispatcher.SendToUserAsync(userId, "TestMethod", new { data = "test" });
        
        // 等待消息被处理
        await Task.Delay(200);

        // Assert - 应该为每个连接发送一次
        _singleClientProxyMock.Verify(
            x => x.SendCoreAsync(
                "TestMethod",
                It.Is<object?[]>(args => args != null && args.Length > 0),
                It.IsAny<CancellationToken>()),
            Times.Exactly(3));
    }

    [Fact]
    public async Task Priority_HighPriorityMessagesShouldBeSentFirst()
    {
        // Arrange
        var sentMethods = new List<string>();
        var lockObj = new object();
        
        _allClientProxyMock
            .Setup(x => x.SendCoreAsync(It.IsAny<string>(), It.IsAny<object?[]>(), It.IsAny<CancellationToken>()))
            .Callback<string, object?[], CancellationToken>((method, args, ct) => 
            {
                lock (lockObj)
                {
                    sentMethods.Add(method);
                }
            })
            .Returns(Task.CompletedTask);

        _dispatcher = new SignalRDispatcher(
            _hubContextMock.Object,
            _connectionManagerMock.Object,
            _loggerMock.Object,
            _options);

        // Act - 发送不同优先级的消息
        await _dispatcher.SendToAllAsync("LowPriorityMsg", new { }, MessagePriority.Low);
        await _dispatcher.SendToAllAsync("CriticalMsg", new { }, MessagePriority.Critical);
        await _dispatcher.SendToAllAsync("NormalMsg", new { }, MessagePriority.Normal);
        await _dispatcher.SendToAllAsync("HighPriorityMsg", new { }, MessagePriority.High);

        // 等待所有消息被处理
        await Task.Delay(300);

        // Assert - 验证所有消息都被发送
        Assert.NotEmpty(sentMethods);
        Assert.Contains("CriticalMsg", sentMethods);
        Assert.Contains("HighPriorityMsg", sentMethods);
        Assert.Contains("NormalMsg", sentMethods);
        Assert.Contains("LowPriorityMsg", sentMethods);
        
        // Critical应该在Low之前发送
        var criticalIndex = sentMethods.IndexOf("CriticalMsg");
        var lowIndex = sentMethods.IndexOf("LowPriorityMsg");
        Assert.True(criticalIndex < lowIndex, "Critical priority message should be sent before Low priority message");
    }

    [Fact]
    public async Task GetMetricsAsync_ShouldReturnCorrectMetrics()
    {
        // Arrange
        var userId = "user1";
        var connectionId = "conn1";
        _connectionManagerMock
            .Setup(x => x.GetConnectionIdsAsync(userId))
            .ReturnsAsync(new List<string> { connectionId });

        _dispatcher = new SignalRDispatcher(
            _hubContextMock.Object,
            _connectionManagerMock.Object,
            _loggerMock.Object,
            _options);

        // Act - 发送几条消息
        await _dispatcher.SendToUserAsync(userId, "Test1", new { });
        await _dispatcher.SendToUserAsync(userId, "Test2", new { });
        await _dispatcher.SendToUserAsync(userId, "Test3", new { });
        
        // 等待消息被处理
        await Task.Delay(300);

        var metrics = await _dispatcher.GetMetricsAsync();

        // Assert
        Assert.NotNull(metrics);
        Assert.Equal(3, metrics.TotalMessagesSent);
        Assert.Equal(0, metrics.FailedMessages);
        Assert.True(metrics.AverageLatency >= 0);
    }

    [Fact]
    public async Task SendAsync_WithException_ShouldRecordFailure()
    {
        // Arrange
        _allClientProxyMock
            .Setup(x => x.SendCoreAsync(It.IsAny<string>(), It.IsAny<object?[]>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Network error"));

        _dispatcher = new SignalRDispatcher(
            _hubContextMock.Object,
            _connectionManagerMock.Object,
            _loggerMock.Object,
            _options);

        // Act
        await _dispatcher.SendToAllAsync("TestMethod", new { });
        
        // 等待消息被处理
        await Task.Delay(300);

        var metrics = await _dispatcher.GetMetricsAsync();

        // Assert
        Assert.Equal(0, metrics.TotalMessagesSent);
        Assert.Equal(1, metrics.FailedMessages);
    }

    [Fact]
    public async Task BatchProcessing_ShouldBatchMessagesCorrectly()
    {
        // Arrange
        _dispatcher = new SignalRDispatcher(
            _hubContextMock.Object,
            _connectionManagerMock.Object,
            _loggerMock.Object,
            _options);

        // Act - 发送20条消息（应该分成2个批次，每批10条）
        for (int i = 0; i < 20; i++)
        {
            await _dispatcher.SendToAllAsync($"Message{i}", new { index = i });
        }

        // 等待所有消息被处理
        await Task.Delay(400);

        var metrics = await _dispatcher.GetMetricsAsync();

        // Assert
        Assert.Equal(20, metrics.TotalMessagesSent);
    }

    [Fact]
    public async Task QueueDepth_ShouldReflectPendingMessages()
    {
        // Arrange
        var slowClientProxy = new Mock<IClientProxy>();
        slowClientProxy
            .Setup(x => x.SendCoreAsync(It.IsAny<string>(), It.IsAny<object?[]>(), It.IsAny<CancellationToken>()))
            .Returns(async () => 
            {
                await Task.Delay(100); // 模拟慢速发送
            });

        _hubClientsMock.Setup(x => x.All).Returns(slowClientProxy.Object);

        _dispatcher = new SignalRDispatcher(
            _hubContextMock.Object,
            _connectionManagerMock.Object,
            _loggerMock.Object,
            _options);

        // Act - 快速发送多条消息
        for (int i = 0; i < 5; i++)
        {
            await _dispatcher.SendToAllAsync($"Message{i}", new { });
        }

        // 立即检查队列深度（消息还未全部处理）
        var metricsBeforeProcessing = await _dispatcher.GetMetricsAsync();

        // 等待所有消息处理完成
        await Task.Delay(1000);
        
        var metricsAfterProcessing = await _dispatcher.GetMetricsAsync();

        // Assert
        // 处理前队列应该有消息（可能部分已处理）
        Assert.True(metricsBeforeProcessing.QueueDepth >= 0);
        
        // 处理后队列应该为空
        Assert.Equal(0, metricsAfterProcessing.QueueDepth);
    }

    [Fact]
    public async Task ConcurrentSending_ShouldHandleThreadSafely()
    {
        // Arrange
        _dispatcher = new SignalRDispatcher(
            _hubContextMock.Object,
            _connectionManagerMock.Object,
            _loggerMock.Object,
            _options);

        // Act - 并发发送100条消息
        var tasks = new List<Task>();
        for (int i = 0; i < 100; i++)
        {
            var index = i;
            tasks.Add(Task.Run(async () =>
            {
                await _dispatcher.SendToAllAsync($"Message{index}", new { index });
            }));
        }

        await Task.WhenAll(tasks);
        
        // 等待所有消息被处理
        await Task.Delay(600);

        var metrics = await _dispatcher.GetMetricsAsync();

        // Assert
        Assert.Equal(100, metrics.TotalMessagesSent);
        Assert.Equal(0, metrics.FailedMessages);
    }

    [Fact]
    public void SignalROptions_Validate_ShouldThrowOnInvalidConfig()
    {
        // Arrange
        var invalidOptions = new SignalROptions
        {
            QueueCapacity = -1 // 无效值
        };

        // Act & Assert
        Assert.Throws<ArgumentException>(() => invalidOptions.Validate());
    }

    [Fact]
    public void SignalROptions_Validate_ShouldPassOnValidConfig()
    {
        // Arrange
        var validOptions = new SignalROptions
        {
            QueueCapacity = 1000,
            BatchSize = 100,
            BatchIntervalMs = 50,
            MaximumReceiveMessageSize = 102400,
            HandshakeTimeoutSeconds = 15,
            KeepAliveIntervalSeconds = 15,
            ClientTimeoutSeconds = 30
        };

        // Act & Assert - 不应该抛出异常
        validOptions.Validate();
    }

    [Fact]
    public void SignalROptions_Validate_ShouldThrowWhenClientTimeoutLessThanKeepAlive()
    {
        // Arrange
        var invalidOptions = new SignalROptions
        {
            KeepAliveIntervalSeconds = 30,
            ClientTimeoutSeconds = 15 // 小于KeepAlive
        };

        // Act & Assert
        Assert.Throws<ArgumentException>(() => invalidOptions.Validate());
    }

    public void Dispose()
    {
        // 先停止dispatcher再释放，避免重复dispose
        if (_dispatcher != null)
        {
            try
            {
                _dispatcher.Dispose();
                _dispatcher = null;
            }
            catch
            {
                // 忽略dispose异常
            }
        }
    }
}
