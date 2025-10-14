using BlazorIdle.Client.Config;
using System.Text.Json;
using Xunit;

namespace BlazorIdle.Tests;

/// <summary>
/// 测试 Battle UI 配置加载和验证
/// Tests for Battle UI configuration loading and validation
/// </summary>
public class BattleUIConfigTests
{
    /// <summary>
    /// 测试默认配置值
    /// </summary>
    [Fact]
    public void DefaultConfig_ShouldHaveValidValues()
    {
        // Arrange & Act
        var config = new BattleUIConfig();
        
        // Assert - ProgressBar
        Assert.True(config.ProgressBar.EnableCycling);
        Assert.Equal(0.1, config.ProgressBar.MinIntervalSeconds);
        Assert.Equal(100.0, config.ProgressBar.MaxIntervalSeconds);
        
        // Assert - Polling
        Assert.Equal(500, config.Polling.Step.NormalIntervalMs);
        Assert.Equal(2000, config.Polling.Plan.NormalIntervalMs);
        Assert.True(config.Polling.Adaptive.Enabled);
        Assert.Equal(150, config.Polling.Adaptive.TriggerLeadTimeMs);
        
        // Assert - Animation
        Assert.Equal(100, config.Animation.ProgressBarUpdateIntervalMs);
        Assert.Equal(120, config.Animation.HpBarTransitionMs);
        
        // Assert - Debug
        Assert.False(config.Debug.EnableLogging);
    }
    
    /// <summary>
    /// 测试 JSON 序列化和反序列化
    /// </summary>
    [Fact]
    public void JsonSerialization_ShouldWorkCorrectly()
    {
        // Arrange
        var config = new BattleUIConfig
        {
            ProgressBar = new ProgressBarConfig
            {
                EnableCycling = false,
                MinIntervalSeconds = 0.2,
                MaxIntervalSeconds = 50.0
            },
            Polling = new PollingConfig
            {
                Step = new PollingModeConfig
                {
                    NormalIntervalMs = 300,
                    FastIntervalMs = 100
                },
                Adaptive = new AdaptivePollingConfig
                {
                    Enabled = false,
                    TriggerLeadTimeMs = 200
                }
            }
        };
        
        // Act - Serialize
        string json = JsonSerializer.Serialize(config);
        
        // Act - Deserialize
        var deserialized = JsonSerializer.Deserialize<BattleUIConfig>(json);
        
        // Assert
        Assert.NotNull(deserialized);
        Assert.False(deserialized!.ProgressBar.EnableCycling);
        Assert.Equal(0.2, deserialized.ProgressBar.MinIntervalSeconds);
        Assert.Equal(300, deserialized.Polling.Step.NormalIntervalMs);
        Assert.False(deserialized.Polling.Adaptive.Enabled);
        Assert.Equal(200, deserialized.Polling.Adaptive.TriggerLeadTimeMs);
    }
    
    /// <summary>
    /// 测试配置验证 - 轮询间隔
    /// </summary>
    [Fact]
    public void PollingInterval_ShouldBeWithinValidRange()
    {
        // Arrange
        var config = new BattleUIConfig();
        
        // Act & Assert
        Assert.True(config.Polling.Step.NormalIntervalMs >= config.Polling.Step.MinIntervalMs);
        Assert.True(config.Polling.Step.NormalIntervalMs <= config.Polling.Step.MaxIntervalMs);
        Assert.True(config.Polling.Plan.NormalIntervalMs >= config.Polling.Plan.MinIntervalMs);
        Assert.True(config.Polling.Plan.NormalIntervalMs <= config.Polling.Plan.MaxIntervalMs);
    }
    
    /// <summary>
    /// 测试配置验证 - 进度条间隔
    /// </summary>
    [Fact]
    public void ProgressBarInterval_ShouldBePositive()
    {
        // Arrange
        var config = new BattleUIConfig();
        
        // Act & Assert
        Assert.True(config.ProgressBar.MinIntervalSeconds > 0);
        Assert.True(config.ProgressBar.MaxIntervalSeconds > config.ProgressBar.MinIntervalSeconds);
    }
    
    /// <summary>
    /// 测试配置验证 - JIT 轮询参数
    /// </summary>
    [Fact]
    public void AdaptivePolling_ShouldHaveValidParameters()
    {
        // Arrange
        var config = new BattleUIConfig();
        
        // Act & Assert
        Assert.True(config.Polling.Adaptive.TriggerLeadTimeMs > 0);
        Assert.True(config.Polling.Adaptive.MinLeadTimeMs > 0);
        Assert.True(config.Polling.Adaptive.MaxLeadTimeMs > config.Polling.Adaptive.MinLeadTimeMs);
        Assert.True(config.Polling.Adaptive.CooldownAfterTriggerMs > 0);
        Assert.True(config.Polling.Adaptive.MaxJitPollsPerSecond > 0);
    }
    
    /// <summary>
    /// 测试动画配置参数
    /// </summary>
    [Fact]
    public void AnimationConfig_ShouldHaveValidParameters()
    {
        // Arrange
        var config = new BattleUIConfig();
        
        // Act & Assert
        Assert.True(config.Animation.ProgressBarUpdateIntervalMs > 0);
        Assert.True(config.Animation.HpBarTransitionMs > 0);
        Assert.True(config.Animation.AttackProgressTransitionMs > 0);
    }
    
    /// <summary>
    /// 测试 JSON 配置文件格式
    /// </summary>
    [Fact]
    public void ConfigFile_ShouldHaveValidJsonFormat()
    {
        // Arrange
        string sampleJson = @"{
            ""progressBar"": {
                ""enableCycling"": true,
                ""minIntervalSeconds"": 0.1,
                ""maxIntervalSeconds"": 100.0
            },
            ""polling"": {
                ""step"": {
                    ""normalIntervalMs"": 500,
                    ""slowIntervalMs"": 2000,
                    ""fastIntervalMs"": 200,
                    ""minIntervalMs"": 100,
                    ""maxIntervalMs"": 5000
                },
                ""plan"": {
                    ""normalIntervalMs"": 2000,
                    ""slowIntervalMs"": 5000,
                    ""fastIntervalMs"": 500,
                    ""minIntervalMs"": 200,
                    ""maxIntervalMs"": 10000
                },
                ""adaptive"": {
                    ""enabled"": true,
                    ""triggerLeadTimeMs"": 150,
                    ""minLeadTimeMs"": 50,
                    ""maxLeadTimeMs"": 500,
                    ""cooldownAfterTriggerMs"": 300,
                    ""maxJitPollsPerSecond"": 5
                }
            },
            ""animation"": {
                ""progressBarUpdateIntervalMs"": 100,
                ""hpBarTransitionMs"": 120,
                ""attackProgressTransitionMs"": 100
            },
            ""debug"": {
                ""enableLogging"": false,
                ""logProgressCalculations"": false,
                ""logPollingEvents"": false,
                ""logJitPolls"": false
            }
        }";
        
        // Act
        var config = JsonSerializer.Deserialize<BattleUIConfig>(sampleJson);
        
        // Assert
        Assert.NotNull(config);
        Assert.True(config!.ProgressBar.EnableCycling);
        Assert.Equal(500, config.Polling.Step.NormalIntervalMs);
        Assert.Equal(150, config.Polling.Adaptive.TriggerLeadTimeMs);
        Assert.Equal(120, config.Animation.HpBarTransitionMs);
        Assert.False(config.Debug.EnableLogging);
    }
    
    /// <summary>
    /// 测试配置属性命名约定
    /// </summary>
    [Fact]
    public void ConfigProperties_ShouldFollowNamingConventions()
    {
        // Arrange & Act
        var config = new BattleUIConfig();
        
        // Assert - 验证所有嵌套配置对象都已初始化
        Assert.NotNull(config.ProgressBar);
        Assert.NotNull(config.Polling);
        Assert.NotNull(config.Polling.Step);
        Assert.NotNull(config.Polling.Plan);
        Assert.NotNull(config.Polling.Adaptive);
        Assert.NotNull(config.Animation);
        Assert.NotNull(config.Debug);
    }
    
    /// <summary>
    /// 测试空配置处理
    /// 当配置文件为空或缺失时，应该使用默认值
    /// </summary>
    [Fact]
    public void EmptyConfig_ShouldUseDefaults()
    {
        // Arrange
        string emptyJson = "{}";
        
        // Act
        var config = JsonSerializer.Deserialize<BattleUIConfig>(emptyJson);
        
        // Assert - 应该使用默认值
        Assert.NotNull(config);
        Assert.NotNull(config!.ProgressBar);
        Assert.NotNull(config.Polling);
        Assert.NotNull(config.Animation);
        Assert.NotNull(config.Debug);
    }
}
