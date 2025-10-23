using BlazorIdle.Client.Services.SignalR;
using Xunit;

namespace BlazorIdle.Tests.SignalR;

/// <summary>
/// SignalRClientOptions单元测试
/// 测试配置类的验证逻辑
/// </summary>
public class SignalRClientOptionsTests
{
    [Fact]
    public void Validate_ValidConfiguration_ShouldNotThrow()
    {
        // Arrange - 准备有效的配置
        var options = new SignalRClientOptions
        {
            HubUrl = "https://localhost:7056/hubs/game",
            EnableAutoReconnect = true,
            ReconnectDelaysMs = new[] { 0, 2000, 5000 },
            EnableHeartbeat = true,
            HeartbeatIntervalSeconds = 30,
            ConnectionTimeoutSeconds = 30,
            MessageHandlerTimeoutMs = 5000
        };

        // Act & Assert - 执行验证，不应抛出异常
        var exception = Record.Exception(() => options.Validate());
        Assert.Null(exception);
    }

    [Fact]
    public void Validate_EmptyHubUrl_ShouldThrow()
    {
        // Arrange - 准备空URL的配置
        var options = new SignalRClientOptions { HubUrl = "" };

        // Act & Assert - 验证应抛出异常
        var exception = Assert.Throws<InvalidOperationException>(() => options.Validate());
        Assert.Contains("Hub URL不能为空", exception.Message);
    }

    [Fact]
    public void Validate_InvalidHubUrl_ShouldThrow()
    {
        // Arrange - 准备无效URL的配置
        var options = new SignalRClientOptions { HubUrl = "not-a-valid-url" };

        // Act & Assert - 验证应抛出异常
        var exception = Assert.Throws<InvalidOperationException>(() => options.Validate());
        Assert.Contains("URL格式无效", exception.Message);
    }

    [Fact]
    public void Validate_NegativeHeartbeatInterval_ShouldThrow()
    {
        // Arrange - 准备负数心跳间隔的配置
        var options = new SignalRClientOptions
        {
            HubUrl = "https://localhost:7056/hubs/game",
            HeartbeatIntervalSeconds = -1
        };

        // Act & Assert - 验证应抛出异常
        var exception = Assert.Throws<InvalidOperationException>(() => options.Validate());
        Assert.Contains("心跳间隔必须大于0", exception.Message);
    }

    [Fact]
    public void Validate_NegativeConnectionTimeout_ShouldThrow()
    {
        // Arrange - 准备负数连接超时的配置
        var options = new SignalRClientOptions
        {
            HubUrl = "https://localhost:7056/hubs/game",
            ConnectionTimeoutSeconds = 0
        };

        // Act & Assert - 验证应抛出异常
        var exception = Assert.Throws<InvalidOperationException>(() => options.Validate());
        Assert.Contains("连接超时时间必须大于0", exception.Message);
    }

    [Fact]
    public void Validate_NegativeMessageHandlerTimeout_ShouldThrow()
    {
        // Arrange - 准备负数消息处理超时的配置
        var options = new SignalRClientOptions
        {
            HubUrl = "https://localhost:7056/hubs/game",
            MessageHandlerTimeoutMs = -100
        };

        // Act & Assert - 验证应抛出异常
        var exception = Assert.Throws<InvalidOperationException>(() => options.Validate());
        Assert.Contains("消息处理超时时间必须大于0", exception.Message);
    }

    [Fact]
    public void Validate_EmptyReconnectDelays_ShouldThrow()
    {
        // Arrange - 准备空重连延迟数组的配置
        var options = new SignalRClientOptions
        {
            HubUrl = "https://localhost:7056/hubs/game",
            ReconnectDelaysMs = Array.Empty<int>()
        };

        // Act & Assert - 验证应抛出异常
        var exception = Assert.Throws<InvalidOperationException>(() => options.Validate());
        Assert.Contains("重连延迟数组不能为空", exception.Message);
    }

    [Fact]
    public void Validate_NegativeReconnectDelay_ShouldThrow()
    {
        // Arrange - 准备包含负数的重连延迟数组
        var options = new SignalRClientOptions
        {
            HubUrl = "https://localhost:7056/hubs/game",
            ReconnectDelaysMs = new[] { 0, 2000, -1000, 5000 }
        };

        // Act & Assert - 验证应抛出异常
        var exception = Assert.Throws<InvalidOperationException>(() => options.Validate());
        Assert.Contains("重连延迟时间不能为负数", exception.Message);
    }

    [Fact]
    public void DefaultValues_ShouldBeValid()
    {
        // Arrange & Act - 使用默认值创建配置
        var options = new SignalRClientOptions();

        // Assert - 默认值应该通过验证
        var exception = Record.Exception(() => options.Validate());
        Assert.Null(exception);
        
        // 验证默认值
        Assert.Equal("https://localhost:7056/hubs/game", options.HubUrl);
        Assert.True(options.EnableAutoReconnect);
        Assert.True(options.EnableHeartbeat);
        Assert.Equal(30, options.HeartbeatIntervalSeconds);
        Assert.Equal(30, options.ConnectionTimeoutSeconds);
        Assert.Equal(5000, options.MessageHandlerTimeoutMs);
        Assert.False(options.EnableDetailedLogging);
        Assert.NotEmpty(options.ReconnectDelaysMs);
    }

    [Fact]
    public void SectionName_ShouldBeCorrect()
    {
        // Assert - 配置节名称应该正确
        Assert.Equal("SignalRClient", SignalRClientOptions.SectionName);
    }
}
