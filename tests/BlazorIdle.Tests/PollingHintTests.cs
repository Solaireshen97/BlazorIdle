using BlazorIdle.Server.Application.Battles.Step;
using Xunit;

namespace BlazorIdle.Tests;

/// <summary>
/// Step 1.3 单元测试：PollingHint 字段验证
/// 验证服务器端轮询提示功能的数据结构
/// </summary>
public class PollingHintTests
{
    [Fact]
    public void PollingHint_Initialization_ShouldSetCorrectValues()
    {
        // Arrange & Act
        var hint = new PollingHint
        {
            SuggestedIntervalMs = 1000,
            NextSignificantEventAt = 15.5,
            IsStable = true
        };

        // Assert
        Assert.Equal(1000, hint.SuggestedIntervalMs);
        Assert.Equal(15.5, hint.NextSignificantEventAt);
        Assert.True(hint.IsStable);
    }

    [Fact]
    public void PollingHint_DefaultValues_ShouldBeZeroOrNull()
    {
        // Arrange & Act
        var hint = new PollingHint();

        // Assert
        Assert.Equal(0, hint.SuggestedIntervalMs);
        Assert.Null(hint.NextSignificantEventAt);
        Assert.False(hint.IsStable);
    }

    [Fact]
    public void StepBattleStatusDto_PollingHint_ShouldBeOptional()
    {
        // Arrange & Act
        var status = new StepBattleStatusDto
        {
            Id = Guid.NewGuid(),
            SimulatedSeconds = 10.0,
            PollingHint = null // 可选字段，可以为 null
        };

        // Assert
        Assert.Null(status.PollingHint);
    }

    [Fact]
    public void StepBattleStatusDto_WithPollingHint_ShouldRetainValues()
    {
        // Arrange
        var battleId = Guid.NewGuid();
        var hint = new PollingHint
        {
            SuggestedIntervalMs = 500,
            NextSignificantEventAt = 20.0,
            IsStable = false
        };

        // Act
        var status = new StepBattleStatusDto
        {
            Id = battleId,
            SimulatedSeconds = 15.0,
            PollingHint = hint
        };

        // Assert
        Assert.NotNull(status.PollingHint);
        Assert.Equal(500, status.PollingHint.SuggestedIntervalMs);
        Assert.Equal(20.0, status.PollingHint.NextSignificantEventAt);
        Assert.False(status.PollingHint.IsStable);
    }

    [Fact]
    public void PollingHint_BackwardCompatibility_NullHintShouldNotBreakExistingCode()
    {
        // Arrange
        var status = new StepBattleStatusDto
        {
            Id = Guid.NewGuid(),
            SimulatedSeconds = 5.0,
            Completed = false
        };

        // Act - 模拟旧代码不设置 PollingHint
        var hint = status.PollingHint; // 应该是 null

        // Assert - 旧代码可以正常工作，不会因为 null 而崩溃
        Assert.Null(hint);
        Assert.Equal(5.0, status.SimulatedSeconds);
    }

    [Fact]
    public void PollingHint_SuggestedIntervalMs_ShouldSupportDifferentScenarios()
    {
        // 测试不同战斗状态的建议轮询间隔
        
        // 激烈战斗 - 短间隔
        var aggressiveHint = new PollingHint { SuggestedIntervalMs = 500 };
        Assert.Equal(500, aggressiveHint.SuggestedIntervalMs);
        
        // 稳定战斗 - 中等间隔
        var stableHint = new PollingHint { SuggestedIntervalMs = 1000, IsStable = true };
        Assert.Equal(1000, stableHint.SuggestedIntervalMs);
        Assert.True(stableHint.IsStable);
        
        // 闲置状态 - 长间隔
        var idleHint = new PollingHint { SuggestedIntervalMs = 5000, IsStable = true };
        Assert.Equal(5000, idleHint.SuggestedIntervalMs);
    }
}
