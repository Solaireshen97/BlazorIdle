using BlazorIdle.Server.Domain.Activities;
using System;
using Xunit;

namespace BlazorIdle.Tests;

public class ActivityPlanTests
{
    [Fact]
    public void ActivityPlan_IsLimitReached_ReturnsFalseForInfinite()
    {
        // Arrange
        var plan = new ActivityPlan
        {
            Id = Guid.NewGuid(),
            CharacterId = Guid.NewGuid(),
            LimitType = LimitType.Infinite,
            ExecutedSeconds = 1000.0
        };

        // Act
        var result = plan.IsLimitReached();

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ActivityPlan_IsLimitReached_ReturnsTrueWhenDurationExceeded()
    {
        // Arrange
        var plan = new ActivityPlan
        {
            Id = Guid.NewGuid(),
            CharacterId = Guid.NewGuid(),
            LimitType = LimitType.Duration,
            LimitValue = 100.0,
            ExecutedSeconds = 150.0
        };

        // Act
        var result = plan.IsLimitReached();

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void ActivityPlan_IsLimitReached_ReturnsFalseWhenDurationNotExceeded()
    {
        // Arrange
        var plan = new ActivityPlan
        {
            Id = Guid.NewGuid(),
            CharacterId = Guid.NewGuid(),
            LimitType = LimitType.Duration,
            LimitValue = 100.0,
            ExecutedSeconds = 50.0
        };

        // Act
        var result = plan.IsLimitReached();

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ActivityPlan_IsLimitReached_ReturnsTrueWhenDurationExactlyReached()
    {
        // Arrange
        var plan = new ActivityPlan
        {
            Id = Guid.NewGuid(),
            CharacterId = Guid.NewGuid(),
            LimitType = LimitType.Duration,
            LimitValue = 100.0,
            ExecutedSeconds = 100.0
        };

        // Act
        var result = plan.IsLimitReached();

        // Assert
        Assert.True(result);
    }

    [Theory]
    [InlineData(ActivityState.Pending)]
    [InlineData(ActivityState.Running)]
    [InlineData(ActivityState.Completed)]
    [InlineData(ActivityState.Cancelled)]
    public void ActivityPlan_StateTransitions_AreValid(ActivityState state)
    {
        // Arrange
        var plan = new ActivityPlan
        {
            Id = Guid.NewGuid(),
            CharacterId = Guid.NewGuid(),
            State = state
        };

        // Act & Assert
        Assert.Equal(state, plan.State);
    }

    [Theory]
    [InlineData(ActivityType.Combat)]
    [InlineData(ActivityType.Dungeon)]
    public void ActivityPlan_ActivityTypes_AreValid(ActivityType type)
    {
        // Arrange
        var plan = new ActivityPlan
        {
            Id = Guid.NewGuid(),
            CharacterId = Guid.NewGuid(),
            Type = type
        };

        // Act & Assert
        Assert.Equal(type, plan.Type);
    }
}
