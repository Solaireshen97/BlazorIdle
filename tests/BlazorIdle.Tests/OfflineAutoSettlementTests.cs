using BlazorIdle.Server.Application.Battles.Offline;
using BlazorIdle.Server.Domain.Characters;
using BlazorIdle.Shared.Models;
using Microsoft.Extensions.Options;
using System;
using Xunit;

namespace BlazorIdle.Tests;

/// <summary>
/// 测试新的自动离线结算功能
/// 包括：离线检测、自动应用收益、心跳触发结算
/// </summary>
public class OfflineAutoSettlementTests
{
    private readonly OfflineOptions _defaultOptions;

    public OfflineAutoSettlementTests()
    {
        // 配置离线选项（60秒阈值）
        _defaultOptions = new OfflineOptions
        {
            OfflineThresholdSeconds = 60,
            MaxOfflineSeconds = 43200,
            EnableAutoSettlement = true
        };
    }

    private OfflineSettlementService CreateService(OfflineOptions? options = null)
    {
        var opts = options ?? _defaultOptions;
        var optionsWrapper = Options.Create(opts);
        
        // 创建基本依赖（实际测试中可以使用更完整的mock）
        var simulator = new Server.Application.Battles.BattleSimulator();
        var engine = new OfflineFastForwardEngine(simulator);
        
        // 这里简化测试，只测试IsPlayerOffline方法，不需要完整的依赖
        return new OfflineSettlementService(
            characters: null!,
            simulator: simulator,
            plans: null!,
            engine: engine,
            db: null!,
            options: optionsWrapper,
            tryStartNextPlan: null
        );
    }

    [Fact]
    public void IsPlayerOffline_WithRecentHeartbeat_ReturnsFalse()
    {
        // Arrange - 30秒前的心跳，低于60秒阈值
        var service = CreateService();
        var character = new Character
        {
            Id = Guid.NewGuid(),
            Name = "OnlinePlayer",
            Profession = Profession.Warrior,
            Level = 1,
            LastSeenAtUtc = DateTime.UtcNow.AddSeconds(-30)
        };

        // Act
        var isOffline = service.IsPlayerOffline(character);

        // Assert
        Assert.False(isOffline);
    }

    [Fact]
    public void IsPlayerOffline_WithOldHeartbeat_ReturnsTrue()
    {
        // Arrange - 90秒前的心跳，超过60秒阈值
        var service = CreateService();
        var character = new Character
        {
            Id = Guid.NewGuid(),
            Name = "OfflinePlayer",
            Profession = Profession.Warrior,
            Level = 1,
            LastSeenAtUtc = DateTime.UtcNow.AddSeconds(-90)
        };

        // Act
        var isOffline = service.IsPlayerOffline(character);

        // Assert
        Assert.True(isOffline);
    }

    [Fact]
    public void IsPlayerOffline_WithNoHeartbeat_ReturnsFalse()
    {
        // Arrange - 从未心跳
        var service = CreateService();
        var character = new Character
        {
            Id = Guid.NewGuid(),
            Name = "NewPlayer",
            Profession = Profession.Warrior,
            Level = 1,
            LastSeenAtUtc = null
        };

        // Act
        var isOffline = service.IsPlayerOffline(character);

        // Assert
        Assert.False(isOffline);
    }

    [Theory]
    [InlineData(30, 60, false)]  // 30秒离线，60秒阈值 => 不离线
    [InlineData(60, 60, true)]   // 正好60秒 => 离线
    [InlineData(90, 60, true)]   // 90秒离线，60秒阈值 => 离线
    [InlineData(120, 90, true)]  // 120秒离线，90秒阈值 => 离线
    [InlineData(45, 90, false)]  // 45秒离线，90秒阈值 => 不离线
    public void IsPlayerOffline_WithVariousThresholds_ReturnsCorrectResult(
        int secondsSinceLastSeen, 
        int thresholdSeconds, 
        bool expectedOffline)
    {
        // Arrange
        var options = new OfflineOptions
        {
            OfflineThresholdSeconds = thresholdSeconds,
            MaxOfflineSeconds = 43200,
            EnableAutoSettlement = true
        };
        var service = CreateService(options);
        
        var character = new Character
        {
            Id = Guid.NewGuid(),
            Name = "TestPlayer",
            Profession = Profession.Warrior,
            Level = 1,
            LastSeenAtUtc = DateTime.UtcNow.AddSeconds(-secondsSinceLastSeen)
        };

        // Act
        var isOffline = service.IsPlayerOffline(character);

        // Assert
        Assert.Equal(expectedOffline, isOffline);
    }

    [Fact]
    public void OfflineOptions_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var options = new OfflineOptions();

        // Assert
        Assert.Equal(60, options.OfflineThresholdSeconds);
        Assert.Equal(43200, options.MaxOfflineSeconds);
        Assert.True(options.EnableAutoSettlement);
    }

    [Fact]
    public void OfflineOptions_CustomValues_CanBeSet()
    {
        // Arrange & Act
        var options = new OfflineOptions
        {
            OfflineThresholdSeconds = 120,
            MaxOfflineSeconds = 86400,
            EnableAutoSettlement = false
        };

        // Assert
        Assert.Equal(120, options.OfflineThresholdSeconds);
        Assert.Equal(86400, options.MaxOfflineSeconds);
        Assert.False(options.EnableAutoSettlement);
    }
}
