using Xunit;
using BlazorIdle.Server.Config;

namespace BlazorIdle.Tests;

/// <summary>
/// SignalR 配置验证测试
/// </summary>
public sealed class SignalRConfigurationTests
{
    [Fact]
    public void SignalROptionsValidator_ValidConfiguration_PassesValidation()
    {
        // Arrange
        var options = new SignalROptions
        {
            HubEndpoint = "/hubs/battle",
            EnableSignalR = true,
            MaxReconnectAttempts = 5,
            ReconnectBaseDelayMs = 1000,
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
    public void SignalROptionsValidator_EmptyHubEndpoint_FailsValidation()
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
        Assert.Contains(result.Errors, e => e.Contains("HubEndpoint cannot be empty"));
    }

    [Fact]
    public void SignalROptionsValidator_InvalidHubEndpoint_FailsValidation()
    {
        // Arrange
        var options = new SignalROptions
        {
            HubEndpoint = "hubs/battle" // Missing leading slash
        };

        // Act
        var result = SignalROptionsValidator.Validate(options);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("must start with '/'"));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(25)]
    public void SignalROptionsValidator_InvalidMaxReconnectAttempts_FailsValidation(int attempts)
    {
        // Arrange
        var options = new SignalROptions
        {
            MaxReconnectAttempts = attempts
        };

        // Act
        var result = SignalROptionsValidator.Validate(options);

        // Assert
        Assert.False(result.IsValid);
        Assert.NotEmpty(result.Errors);
    }

    [Theory]
    [InlineData(50)]    // Too low
    [InlineData(15000)] // Too high
    public void SignalROptionsValidator_InvalidReconnectDelay_FailsValidation(int delayMs)
    {
        // Arrange
        var options = new SignalROptions
        {
            ReconnectBaseDelayMs = delayMs
        };

        // Act
        var result = SignalROptionsValidator.Validate(options);

        // Assert
        Assert.False(result.IsValid);
        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public void SignalROptionsValidator_KeepAliveExceedsServerTimeout_FailsValidation()
    {
        // Arrange
        var options = new SignalROptions
        {
            KeepAliveIntervalSeconds = 40,
            ServerTimeoutSeconds = 30
        };

        // Act
        var result = SignalROptionsValidator.Validate(options);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("should not exceed ServerTimeoutSeconds"));
    }

    [Fact]
    public void SignalROptionsValidator_ServerTimeoutTooSmall_FailsValidation()
    {
        // Arrange
        var options = new SignalROptions
        {
            KeepAliveIntervalSeconds = 15,
            ServerTimeoutSeconds = 20 // Less than 2x KeepAlive
        };

        // Act
        var result = SignalROptionsValidator.Validate(options);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("at least twice KeepAliveIntervalSeconds"));
    }

    [Fact]
    public void SignalROptionsValidator_MultipleErrors_ReturnsAllErrors()
    {
        // Arrange
        var options = new SignalROptions
        {
            HubEndpoint = "",
            MaxReconnectAttempts = -1,
            ReconnectBaseDelayMs = 50,
            ConnectionTimeoutSeconds = 0,
            KeepAliveIntervalSeconds = 0,
            ServerTimeoutSeconds = 0
        };

        // Act
        var result = SignalROptionsValidator.Validate(options);

        // Assert
        Assert.False(result.IsValid);
        Assert.True(result.Errors.Count >= 5); // At least 5 errors
    }

    [Fact]
    public void SignalROptionsValidator_GetErrorMessage_ReturnsFormattedString()
    {
        // Arrange
        var options = new SignalROptions
        {
            HubEndpoint = "",
            MaxReconnectAttempts = -1
        };

        // Act
        var result = SignalROptionsValidator.Validate(options);
        var errorMessage = result.GetErrorMessage();

        // Assert
        Assert.NotEmpty(errorMessage);
        Assert.Contains(";", errorMessage); // Multiple errors separated by semicolon
    }

    [Fact]
    public void SignalROptions_DefaultValues_PassValidation()
    {
        // Arrange
        var options = new SignalROptions(); // Using default values

        // Act
        var result = SignalROptionsValidator.Validate(options);

        // Assert
        Assert.True(result.IsValid, 
            $"Default SignalROptions should be valid. Errors: {result.GetErrorMessage()}");
    }
}
