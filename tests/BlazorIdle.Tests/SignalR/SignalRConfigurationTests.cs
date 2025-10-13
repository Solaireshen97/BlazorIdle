using BlazorIdle.Server.Application.SignalR.Configuration;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace BlazorIdle.Tests.SignalR;

/// <summary>
/// 测试SignalR配置加载和默认值
/// </summary>
public class SignalRConfigurationTests
{
    [Fact]
    public void SignalROptions_HasCorrectDefaultValues()
    {
        // Arrange & Act
        var options = new SignalROptions();

        // Assert
        Assert.Equal("/hubs/battle", options.HubPath);
        Assert.False(options.EnableDetailedErrors);
        Assert.Equal(15, options.KeepAliveIntervalSeconds);
        Assert.Equal(30, options.ClientTimeoutSeconds);
        Assert.Equal(15, options.HandshakeTimeoutSeconds);
        Assert.Equal(32768, options.MaximumReceiveMessageSize);
        Assert.Equal(10, options.StreamBufferCapacity);
        Assert.True(options.EnableReconnection);
        Assert.Equal(4, options.MaxReconnectionAttempts);
        Assert.Equal(new[] { 0, 2, 10, 30 }, options.ReconnectionDelaySeconds);
    }

    [Fact]
    public void SignalROptions_LoadsFromConfiguration()
    {
        // Arrange
        var configData = new Dictionary<string, string?>
        {
            { "SignalR:HubPath", "/custom/hub" },
            { "SignalR:EnableDetailedErrors", "true" },
            { "SignalR:KeepAliveIntervalSeconds", "20" },
            { "SignalR:ClientTimeoutSeconds", "45" },
            { "SignalR:HandshakeTimeoutSeconds", "10" },
            { "SignalR:MaximumReceiveMessageSize", "65536" },
            { "SignalR:StreamBufferCapacity", "20" },
            { "SignalR:EnableReconnection", "false" },
            { "SignalR:MaxReconnectionAttempts", "5" }
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        // Act
        var options = configuration.GetSection("SignalR").Get<SignalROptions>();

        // Assert
        Assert.NotNull(options);
        Assert.Equal("/custom/hub", options.HubPath);
        Assert.True(options.EnableDetailedErrors);
        Assert.Equal(20, options.KeepAliveIntervalSeconds);
        Assert.Equal(45, options.ClientTimeoutSeconds);
        Assert.Equal(10, options.HandshakeTimeoutSeconds);
        Assert.Equal(65536, options.MaximumReceiveMessageSize);
        Assert.Equal(20, options.StreamBufferCapacity);
        Assert.False(options.EnableReconnection);
        Assert.Equal(5, options.MaxReconnectionAttempts);
    }

    [Fact]
    public void SignalROptions_DefaultReconnectionDelayArray()
    {
        // Arrange & Act
        var options = new SignalROptions();

        // Assert - 验证默认的重连延迟序列
        Assert.NotNull(options.ReconnectionDelaySeconds);
        Assert.Equal(4, options.ReconnectionDelaySeconds.Length);
        Assert.Equal(0, options.ReconnectionDelaySeconds[0]);
        Assert.Equal(2, options.ReconnectionDelaySeconds[1]);
        Assert.Equal(10, options.ReconnectionDelaySeconds[2]);
        Assert.Equal(30, options.ReconnectionDelaySeconds[3]);
    }

    [Fact]
    public void SignalROptions_PartialConfiguration_UsesDefaultsForMissing()
    {
        // Arrange
        var configData = new Dictionary<string, string?>
        {
            { "SignalR:HubPath", "/api/hub" },
            { "SignalR:EnableDetailedErrors", "true" }
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        // Act
        var options = configuration.GetSection("SignalR").Get<SignalROptions>() ?? new SignalROptions();

        // Assert - 自定义值
        Assert.Equal("/api/hub", options.HubPath);
        Assert.True(options.EnableDetailedErrors);
        
        // Assert - 默认值
        Assert.Equal(15, options.KeepAliveIntervalSeconds);
        Assert.Equal(30, options.ClientTimeoutSeconds);
        Assert.True(options.EnableReconnection);
    }

    [Fact]
    public void SignalROptions_ValidatesTimeoutValues()
    {
        // Arrange
        var options = new SignalROptions
        {
            KeepAliveIntervalSeconds = 15,
            ClientTimeoutSeconds = 30,
            HandshakeTimeoutSeconds = 15
        };

        // Assert
        Assert.True(options.ClientTimeoutSeconds > options.KeepAliveIntervalSeconds);
        Assert.True(options.HandshakeTimeoutSeconds > 0);
    }
}
