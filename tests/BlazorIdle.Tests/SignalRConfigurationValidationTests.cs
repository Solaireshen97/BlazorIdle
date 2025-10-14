using Xunit;
using BlazorIdle.Server.Config;

namespace BlazorIdle.Tests;

/// <summary>
/// SignalR 配置验证测试
/// </summary>
public sealed class SignalRConfigurationValidationTests
{
    [Fact]
    public void Validator_WithValidConfiguration_ReturnsSuccess()
    {
        // Arrange
        var options = new SignalROptions
        {
            HubEndpoint = "/hubs/battle",
            MaxReconnectAttempts = 5,
            ReconnectBaseDelayMs = 1000,
            MaxReconnectDelayMs = 30000,
            ConnectionTimeoutSeconds = 30,
            KeepAliveIntervalSeconds = 15,
            ServerTimeoutSeconds = 30
        };

        // Act
        var result = SignalROptionsValidator.Validate(options);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Validator_WithEmptyHubEndpoint_ReturnsFalse()
    {
        // Arrange
        var options = new SignalROptions
        {
            HubEndpoint = ""
        };

        // Act
        var result = SignalROptionsValidator.Validate(options);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("HubEndpoint 不能为空"));
    }

    [Fact]
    public void Validator_WithInvalidHubEndpoint_ReturnsFalse()
    {
        // Arrange
        var options = new SignalROptions
        {
            HubEndpoint = "hubs/battle" // 缺少前导斜杠
        };

        // Act
        var result = SignalROptionsValidator.Validate(options);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("HubEndpoint 必须以 '/' 开头"));
    }

    [Fact]
    public void Validator_WithNegativeMaxReconnectAttempts_ReturnsFalse()
    {
        // Arrange
        var options = new SignalROptions
        {
            HubEndpoint = "/hubs/battle",
            MaxReconnectAttempts = -1
        };

        // Act
        var result = SignalROptionsValidator.Validate(options);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("MaxReconnectAttempts 不能为负数"));
    }

    [Fact]
    public void Validator_WithTooLargeMaxReconnectAttempts_ReturnsFalse()
    {
        // Arrange
        var options = new SignalROptions
        {
            HubEndpoint = "/hubs/battle",
            MaxReconnectAttempts = 150
        };

        // Act
        var result = SignalROptionsValidator.Validate(options);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("MaxReconnectAttempts 不应超过 100"));
    }

    [Fact]
    public void Validator_WithInvalidReconnectBaseDelay_ReturnsFalse()
    {
        // Arrange
        var options = new SignalROptions
        {
            HubEndpoint = "/hubs/battle",
            ReconnectBaseDelayMs = 50 // 太小
        };

        // Act
        var result = SignalROptionsValidator.Validate(options);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("ReconnectBaseDelayMs 不应小于 100ms"));
    }

    [Fact]
    public void Validator_WithMaxDelayLessThanBaseDelay_ReturnsFalse()
    {
        // Arrange
        var options = new SignalROptions
        {
            HubEndpoint = "/hubs/battle",
            ReconnectBaseDelayMs = 5000,
            MaxReconnectDelayMs = 3000 // 小于基础延迟
        };

        // Act
        var result = SignalROptionsValidator.Validate(options);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("MaxReconnectDelayMs 不应小于 ReconnectBaseDelayMs"));
    }

    [Fact]
    public void Validator_WithInvalidConnectionTimeout_ReturnsFalse()
    {
        // Arrange
        var options = new SignalROptions
        {
            HubEndpoint = "/hubs/battle",
            ConnectionTimeoutSeconds = 2 // 太小
        };

        // Act
        var result = SignalROptionsValidator.Validate(options);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("ConnectionTimeoutSeconds 不应小于 5秒"));
    }

    [Fact]
    public void Validator_WithInvalidKeepAliveInterval_ReturnsFalse()
    {
        // Arrange
        var options = new SignalROptions
        {
            HubEndpoint = "/hubs/battle",
            KeepAliveIntervalSeconds = 0
        };

        // Act
        var result = SignalROptionsValidator.Validate(options);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("KeepAliveIntervalSeconds 不应小于 1秒"));
    }

    [Fact]
    public void Validator_WithServerTimeoutTooSmall_ReturnsFalse()
    {
        // Arrange
        var options = new SignalROptions
        {
            HubEndpoint = "/hubs/battle",
            KeepAliveIntervalSeconds = 15,
            ServerTimeoutSeconds = 20 // 应该至少是 30 (15 * 2)
        };

        // Act
        var result = SignalROptionsValidator.Validate(options);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("ServerTimeoutSeconds 应至少是 KeepAliveIntervalSeconds 的 2 倍"));
    }

    [Fact]
    public void Validator_WithThrottlingEnabled_ValidatesThrottleWindow()
    {
        // Arrange
        var options = new SignalROptions
        {
            HubEndpoint = "/hubs/battle",
            Performance = new PerformanceOptions
            {
                EnableThrottling = true,
                ThrottleWindowMs = 50 // 太小
            }
        };

        // Act
        var result = SignalROptionsValidator.Validate(options);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("ThrottleWindowMs 不应小于 100ms"));
    }

    [Fact]
    public void Validator_WithBatchingEnabled_ValidatesBatchDelay()
    {
        // Arrange
        var options = new SignalROptions
        {
            HubEndpoint = "/hubs/battle",
            Performance = new PerformanceOptions
            {
                EnableBatching = true,
                BatchDelayMs = 5 // 太小
            }
        };

        // Act
        var result = SignalROptionsValidator.Validate(options);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("BatchDelayMs 不应小于 10ms"));
    }

    [Fact]
    public void Validator_WithMultipleErrors_ReturnsAllErrors()
    {
        // Arrange
        var options = new SignalROptions
        {
            HubEndpoint = "",
            MaxReconnectAttempts = -1,
            ReconnectBaseDelayMs = 50
        };

        // Act
        var result = SignalROptionsValidator.Validate(options);

        // Assert
        Assert.False(result.IsValid);
        Assert.True(result.Errors.Count >= 3); // 至少 3 个错误
    }

    [Fact]
    public void ValidationResult_GetErrorMessage_ReturnsFormattedString()
    {
        // Arrange
        var options = new SignalROptions
        {
            HubEndpoint = "",
            MaxReconnectAttempts = -1
        };

        // Act
        var result = SignalROptionsValidator.Validate(options);
        var message = result.GetErrorMessage();

        // Assert
        Assert.False(string.IsNullOrEmpty(message));
        Assert.Contains(";", message); // 多个错误用分号分隔
    }

    [Fact]
    public void NotificationOptions_DamageAppliedNotification_CanBeEnabled()
    {
        // Arrange - 模拟从配置文件读取的设置
        var options = new SignalROptions
        {
            HubEndpoint = "/hubs/battle",
            Notification = new NotificationOptions
            {
                EnableDamageAppliedNotification = true,
                EnableAttackStartedNotification = true,
                EnableDamageReceivedNotification = true
            }
        };

        // Act
        var result = SignalROptionsValidator.Validate(options);

        // Assert
        Assert.True(result.IsValid);
        Assert.True(options.Notification.EnableDamageAppliedNotification);
        Assert.True(options.Notification.EnableAttackStartedNotification);
        Assert.True(options.Notification.EnableDamageReceivedNotification);
    }

    [Fact]
    public void NotificationOptions_DefaultValues_DamageAppliedDisabled()
    {
        // Arrange - 测试默认值
        var options = new SignalROptions
        {
            HubEndpoint = "/hubs/battle"
        };

        // Act & Assert - 验证默认值
        Assert.False(options.Notification.EnableDamageAppliedNotification); // 默认为 false，需要显式启用
        Assert.True(options.Notification.EnableAttackStartedNotification); // 默认为 true
        Assert.True(options.Notification.EnableDamageReceivedNotification); // 默认为 true
    }
}
