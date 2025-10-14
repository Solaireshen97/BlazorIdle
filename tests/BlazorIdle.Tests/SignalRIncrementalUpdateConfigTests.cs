using Xunit;
using BlazorIdle.Models;

namespace BlazorIdle.Tests;

/// <summary>
/// 测试 SignalR 增量更新配置功能
/// </summary>
public class SignalRIncrementalUpdateConfigTests
{
    /// <summary>
    /// 测试默认配置创建
    /// </summary>
    [Fact]
    public void DefaultConfig_ShouldHaveSignalRIncrementalUpdateSettings()
    {
        // Arrange & Act
        var config = new ProgressBarConfig();
        
        // Assert
        Assert.NotNull(config.SignalRIncrementalUpdate);
    }

    /// <summary>
    /// 测试 SignalR 增量更新默认值
    /// </summary>
    [Fact]
    public void SignalRIncrementalUpdate_ShouldHaveCorrectDefaults()
    {
        // Arrange & Act
        var config = new ProgressBarConfig();
        var signalrConfig = config.SignalRIncrementalUpdate;
        
        // Assert - 功能开关
        Assert.True(signalrConfig.EnableIncrementalUpdate, "增量更新应默认启用");
        Assert.True(signalrConfig.EnableAttackTickUpdate, "攻击触发事件更新应默认启用");
        Assert.True(signalrConfig.EnableSkillCastUpdate, "技能施放事件更新应默认启用");
        Assert.False(signalrConfig.EnableDamageAppliedUpdate, "伤害应用事件更新应默认禁用（性能考虑）");
        
        // Assert - 预测和同步设置
        Assert.True(signalrConfig.ClientPredictionEnabled, "客户端预测应默认启用");
        Assert.Equal(500, signalrConfig.MaxPredictionAheadMs);
        Assert.Equal(100, signalrConfig.SyncThresholdMs);
        Assert.True(signalrConfig.ResetProgressOnMismatch, "不匹配时应重置进度");
    }

    /// <summary>
    /// 测试预测时间范围合理性
    /// </summary>
    [Fact]
    public void MaxPredictionAheadMs_ShouldBeReasonable()
    {
        // Arrange
        var config = new ProgressBarConfig();
        var signalrConfig = config.SignalRIncrementalUpdate;
        
        // Act & Assert
        // 预测时间应该在合理范围内（100ms - 1000ms）
        Assert.InRange(signalrConfig.MaxPredictionAheadMs, 100, 1000);
    }

    /// <summary>
    /// 测试同步阈值合理性
    /// </summary>
    [Fact]
    public void SyncThresholdMs_ShouldBeReasonable()
    {
        // Arrange
        var config = new ProgressBarConfig();
        var signalrConfig = config.SignalRIncrementalUpdate;
        
        // Act & Assert
        // 同步阈值应该小于最大预测时间
        Assert.True(signalrConfig.SyncThresholdMs < signalrConfig.MaxPredictionAheadMs);
        // 同步阈值应该在合理范围内（50ms - 500ms）
        Assert.InRange(signalrConfig.SyncThresholdMs, 50, 500);
    }

    /// <summary>
    /// 测试调试配置包含新的日志选项
    /// </summary>
    [Fact]
    public void DebugConfig_ShouldHaveSignalRLoggingOptions()
    {
        // Arrange & Act
        var config = new ProgressBarConfig();
        var debugConfig = config.Debug;
        
        // Assert
        Assert.False(debugConfig.LogSignalREvents, "SignalR 事件日志应默认关闭");
        Assert.False(debugConfig.LogIncrementalUpdates, "增量更新日志应默认关闭");
    }

    /// <summary>
    /// 测试可以独立控制各类事件更新
    /// </summary>
    [Theory]
    [InlineData(true, true, true, true)]    // 全部启用
    [InlineData(true, false, false, false)] // 仅启用总开关
    [InlineData(false, true, true, true)]   // 总开关禁用，其他启用（应被忽略）
    public void EventUpdates_CanBeControlledIndependently(
        bool enableIncremental, 
        bool enableAttack, 
        bool enableSkill, 
        bool enableDamage)
    {
        // Arrange
        var config = new SignalRIncrementalUpdateSettings
        {
            EnableIncrementalUpdate = enableIncremental,
            EnableAttackTickUpdate = enableAttack,
            EnableSkillCastUpdate = enableSkill,
            EnableDamageAppliedUpdate = enableDamage
        };
        
        // Act & Assert
        Assert.Equal(enableIncremental, config.EnableIncrementalUpdate);
        Assert.Equal(enableAttack, config.EnableAttackTickUpdate);
        Assert.Equal(enableSkill, config.EnableSkillCastUpdate);
        Assert.Equal(enableDamage, config.EnableDamageAppliedUpdate);
    }

    /// <summary>
    /// 测试配置完整性（所有必需属性都有默认值）
    /// </summary>
    [Fact]
    public void ProgressBarConfig_ShouldHaveAllRequiredProperties()
    {
        // Arrange & Act
        var config = new ProgressBarConfig();
        
        // Assert - 验证所有配置节都存在
        Assert.NotNull(config.ProgressBar);
        Assert.NotNull(config.JITPolling);
        Assert.NotNull(config.HPAnimation);
        Assert.NotNull(config.SignalRIncrementalUpdate);
        Assert.NotNull(config.Debug);
    }

    /// <summary>
    /// 测试客户端预测可以独立禁用
    /// </summary>
    [Fact]
    public void ClientPrediction_CanBeDisabled()
    {
        // Arrange
        var config = new SignalRIncrementalUpdateSettings
        {
            EnableIncrementalUpdate = true,
            ClientPredictionEnabled = false
        };
        
        // Act & Assert
        Assert.True(config.EnableIncrementalUpdate, "增量更新启用");
        Assert.False(config.ClientPredictionEnabled, "但客户端预测可以禁用");
    }

    /// <summary>
    /// 测试配置验证场景
    /// </summary>
    [Theory]
    [InlineData(100, 500, true)]   // 正常：阈值 < 预测时间
    [InlineData(500, 500, false)]  // 异常：阈值 = 预测时间
    [InlineData(600, 500, false)]  // 异常：阈值 > 预测时间
    public void SyncThreshold_ShouldBeLessThanMaxPrediction(
        int syncThresholdMs, 
        int maxPredictionMs, 
        bool isValid)
    {
        // Arrange
        var config = new SignalRIncrementalUpdateSettings
        {
            SyncThresholdMs = syncThresholdMs,
            MaxPredictionAheadMs = maxPredictionMs
        };
        
        // Act
        bool actualValid = config.SyncThresholdMs < config.MaxPredictionAheadMs;
        
        // Assert
        Assert.Equal(isValid, actualValid);
    }
}
