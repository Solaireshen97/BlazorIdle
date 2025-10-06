using BlazorIdle.Server.Domain.Activity;
using Xunit;

namespace BlazorIdle.Tests;

public class ActivityPlanTests
{
    [Fact]
    public void ActivityPlan_StateMachine_TransitionFromPendingToRunning()
    {
        // Arrange
        var plan = new ActivityPlan
        {
            CharacterId = Guid.NewGuid(),
            Type = ActivityType.Combat,
            Limit = new DurationLimit(60.0)
        };

        // Act
        plan.Start();

        // Assert
        Assert.Equal(ActivityState.Running, plan.State);
        Assert.NotNull(plan.StartedAtUtc);
    }

    [Fact]
    public void ActivityPlan_StateMachine_TransitionFromRunningToCompleted()
    {
        // Arrange
        var plan = new ActivityPlan
        {
            CharacterId = Guid.NewGuid(),
            Type = ActivityType.Combat,
            Limit = new DurationLimit(60.0)
        };
        plan.Start();

        // Act
        plan.Complete();

        // Assert
        Assert.Equal(ActivityState.Completed, plan.State);
        Assert.NotNull(plan.EndedAtUtc);
    }

    [Fact]
    public void ActivityPlan_StateMachine_CannotStartIfNotPending()
    {
        // Arrange
        var plan = new ActivityPlan
        {
            CharacterId = Guid.NewGuid(),
            Type = ActivityType.Combat,
            Limit = new DurationLimit(60.0)
        };
        plan.Start();

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => plan.Start());
    }

    [Fact]
    public void ActivityPlan_CannotCancelCompletedPlan()
    {
        // Arrange
        var plan = new ActivityPlan
        {
            CharacterId = Guid.NewGuid(),
            Type = ActivityType.Combat,
            Limit = new DurationLimit(60.0)
        };
        plan.Start();
        plan.Complete();

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => plan.Cancel());
    }

    [Fact]
    public void DurationLimit_ReachesLimit()
    {
        // Arrange
        var limit = new DurationLimit(60.0);
        var progress = new ActivityProgress
        {
            SimulatedSeconds = 70.0,
            CompletedCount = 0
        };

        // Act
        var reached = limit.IsReached(progress);

        // Assert
        Assert.True(reached);
    }

    [Fact]
    public void DurationLimit_NotReached()
    {
        // Arrange
        var limit = new DurationLimit(60.0);
        var progress = new ActivityProgress
        {
            SimulatedSeconds = 50.0,
            CompletedCount = 0
        };

        // Act
        var reached = limit.IsReached(progress);

        // Assert
        Assert.False(reached);
    }

    [Fact]
    public void CountLimit_ReachesLimit()
    {
        // Arrange
        var limit = new CountLimit(10);
        var progress = new ActivityProgress
        {
            SimulatedSeconds = 0,
            CompletedCount = 15
        };

        // Act
        var reached = limit.IsReached(progress);

        // Assert
        Assert.True(reached);
    }

    [Fact]
    public void CountLimit_NotReached()
    {
        // Arrange
        var limit = new CountLimit(10);
        var progress = new ActivityProgress
        {
            SimulatedSeconds = 0,
            CompletedCount = 5
        };

        // Act
        var reached = limit.IsReached(progress);

        // Assert
        Assert.False(reached);
    }

    [Fact]
    public void InfiniteLimit_NeverReaches()
    {
        // Arrange
        var limit = new InfiniteLimit();
        var progress = new ActivityProgress
        {
            SimulatedSeconds = 10000.0,
            CompletedCount = 10000
        };

        // Act
        var reached = limit.IsReached(progress);

        // Assert
        Assert.False(reached);
    }

    [Fact]
    public void ActivitySlot_EnqueueAndDequeue()
    {
        // Arrange
        var slot = new ActivitySlot
        {
            SlotIndex = 0,
            CharacterId = Guid.NewGuid()
        };
        var plan1Id = Guid.NewGuid();
        var plan2Id = Guid.NewGuid();
        var plan3Id = Guid.NewGuid();

        // Act
        slot.StartPlan(plan1Id); // Start first plan
        slot.EnqueuePlan(plan2Id); // Queue second plan
        slot.EnqueuePlan(plan3Id); // Queue third plan
        
        var nextId = slot.FinishCurrentAndGetNext(); // Finish first, get second

        // Assert
        Assert.Null(slot.CurrentPlanId); // Current should be null
        Assert.Equal(plan2Id, nextId); // Next should be plan2
        Assert.Single(slot.QueuedPlanIds); // One plan left in queue
        Assert.Equal(plan3Id, slot.QueuedPlanIds[0]); // That plan should be plan3
    }

    [Fact]
    public void ActivitySlot_CannotStartIfNotIdle()
    {
        // Arrange
        var slot = new ActivitySlot
        {
            SlotIndex = 0,
            CharacterId = Guid.NewGuid()
        };
        var plan1Id = Guid.NewGuid();
        var plan2Id = Guid.NewGuid();
        slot.StartPlan(plan1Id);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => slot.StartPlan(plan2Id));
    }
}
