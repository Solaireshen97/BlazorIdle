using BlazorIdle.Models;
using Xunit;

namespace BlazorIdle.Tests;

/// <summary>
/// 进度条事件同步配置测试（Phase 2.5）
/// </summary>
public class ProgressBarEventSyncTests
{
    [Fact]
    public void ProgressBarSettings_ShouldHave_AttackTickSync_Enabled_ByDefault()
    {
        // Arrange
        var settings = new ProgressBarSettings();
        
        // Assert
        Assert.True(settings.EnableSyncOnAttackTick);
    }
    
    [Fact]
    public void ProgressBarSettings_ShouldHave_SkillCastSync_Enabled_ByDefault()
    {
        // Arrange
        var settings = new ProgressBarSettings();
        
        // Assert
        Assert.True(settings.EnableSyncOnSkillCast);
    }
    
    [Fact]
    public void ProgressBarSettings_ShouldHave_DamageAppliedSync_Enabled_ByDefault()
    {
        // Arrange
        var settings = new ProgressBarSettings();
        
        // Assert
        Assert.True(settings.EnableSyncOnDamageApplied);
    }
    
    [Fact]
    public void ProgressBarConfig_ShouldHave_AllSections()
    {
        // Arrange
        var config = new ProgressBarConfig();
        
        // Assert
        Assert.NotNull(config.ProgressBar);
        Assert.NotNull(config.JITPolling);
        Assert.NotNull(config.HPAnimation);
        Assert.NotNull(config.Debug);
    }
    
    [Fact]
    public void ProgressBarSettings_ShouldAllow_DisablingEventSync()
    {
        // Arrange
        var settings = new ProgressBarSettings
        {
            EnableSyncOnAttackTick = false,
            EnableSyncOnSkillCast = false,
            EnableSyncOnDamageApplied = false
        };
        
        // Assert
        Assert.False(settings.EnableSyncOnAttackTick);
        Assert.False(settings.EnableSyncOnSkillCast);
        Assert.False(settings.EnableSyncOnDamageApplied);
    }
    
    [Fact]
    public void JITPollingSettings_ShouldHave_ReasonableDefaults()
    {
        // Arrange
        var settings = new JITPollingSettings();
        
        // Assert
        Assert.True(settings.EnableJITPolling);
        Assert.Equal(150, settings.TriggerWindowMs);
        Assert.Equal(100, settings.MinPredictionTimeMs);
        Assert.Equal(1, settings.MaxJITAttemptsPerCycle);
        Assert.True(settings.AdaptivePollingEnabled);
    }
    
    [Theory]
    [InlineData(0.1, 100.0, true)]
    [InlineData(0.05, 50.0, true)]
    [InlineData(1.0, 10.0, true)]
    public void ProgressBarSettings_ShouldAccept_ValidIntervalRanges(double minInterval, double maxInterval, bool enableLooping)
    {
        // Arrange
        var settings = new ProgressBarSettings
        {
            MinIntervalForLooping = minInterval,
            MaxIntervalForLooping = maxInterval,
            EnableLoopingProgress = enableLooping
        };
        
        // Assert
        Assert.Equal(minInterval, settings.MinIntervalForLooping);
        Assert.Equal(maxInterval, settings.MaxIntervalForLooping);
        Assert.Equal(enableLooping, settings.EnableLoopingProgress);
        Assert.True(settings.MaxIntervalForLooping > settings.MinIntervalForLooping);
    }
    
    [Fact]
    public void HPAnimationSettings_ShouldHave_ConsistentTransitionTimes()
    {
        // Arrange
        var settings = new HPAnimationSettings();
        
        // Assert
        Assert.True(settings.EnableSmoothTransition);
        Assert.Equal(120, settings.TransitionDurationMs);
        Assert.Equal(120, settings.PlayerHPTransitionMs);
        Assert.Equal(120, settings.EnemyHPTransitionMs);
        Assert.Equal("linear", settings.TransitionTimingFunction);
    }
    
    [Fact]
    public void DebugSettings_ShouldBe_Disabled_ByDefault()
    {
        // Arrange
        var settings = new DebugSettings();
        
        // Assert
        Assert.False(settings.LogProgressCalculations);
        Assert.False(settings.LogJITPollingEvents);
        Assert.False(settings.ShowProgressDebugInfo);
    }
}
