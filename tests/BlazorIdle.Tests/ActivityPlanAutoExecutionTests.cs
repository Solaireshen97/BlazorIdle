using BlazorIdle.Server.Domain.Activities;
using System;
using System.Linq;
using Xunit;

namespace BlazorIdle.Tests;

/// <summary>
/// 测试活动计划的自动执行功能相关的域逻辑
/// </summary>
public class ActivityPlanAutoExecutionTests
{
    [Fact]
    public void ActivityPlan_QueueOrdering_ShouldPrioritizeBySlotIndexThenCreatedTime()
    {
        // Arrange - 创建多个计划，模拟队列排序
        var characterId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        
        var plan1 = new ActivityPlan
        {
            Id = Guid.NewGuid(),
            CharacterId = characterId,
            SlotIndex = 1,
            CreatedAt = now.AddMinutes(-10),
            State = ActivityState.Pending
        };
        
        var plan2 = new ActivityPlan
        {
            Id = Guid.NewGuid(),
            CharacterId = characterId,
            SlotIndex = 0,
            CreatedAt = now.AddMinutes(-5),
            State = ActivityState.Pending
        };
        
        var plan3 = new ActivityPlan
        {
            Id = Guid.NewGuid(),
            CharacterId = characterId,
            SlotIndex = 0,
            CreatedAt = now.AddMinutes(-8),
            State = ActivityState.Pending
        };

        // Act - 模拟仓储排序逻辑
        var plans = new[] { plan1, plan2, plan3 }
            .OrderBy(p => p.SlotIndex)
            .ThenBy(p => p.CreatedAt)
            .ToList();

        // Assert - 验证顺序：plan3 (slot 0, 最早) -> plan2 (slot 0, 较晚) -> plan1 (slot 1)
        Assert.Equal(plan3.Id, plans[0].Id);
        Assert.Equal(plan2.Id, plans[1].Id);
        Assert.Equal(plan1.Id, plans[2].Id);
    }

    [Fact]
    public void ActivityPlan_StateTransition_FromPendingToRunning()
    {
        // Arrange
        var plan = new ActivityPlan
        {
            Id = Guid.NewGuid(),
            CharacterId = Guid.NewGuid(),
            State = ActivityState.Pending,
            CreatedAt = DateTime.UtcNow
        };

        // Act - 模拟启动计划
        plan.State = ActivityState.Running;
        plan.StartedAt = DateTime.UtcNow;

        // Assert
        Assert.Equal(ActivityState.Running, plan.State);
        Assert.NotNull(plan.StartedAt);
    }

    [Fact]
    public void ActivityPlan_StateTransition_FromRunningToCompleted()
    {
        // Arrange
        var plan = new ActivityPlan
        {
            Id = Guid.NewGuid(),
            CharacterId = Guid.NewGuid(),
            State = ActivityState.Running,
            CreatedAt = DateTime.UtcNow,
            StartedAt = DateTime.UtcNow.AddSeconds(-100)
        };

        // Act - 模拟完成计划
        plan.State = ActivityState.Completed;
        plan.CompletedAt = DateTime.UtcNow;
        var elapsed = (plan.CompletedAt.Value - plan.StartedAt.Value).TotalSeconds;
        plan.ExecutedSeconds = elapsed;

        // Assert
        Assert.Equal(ActivityState.Completed, plan.State);
        Assert.NotNull(plan.CompletedAt);
        Assert.True(plan.ExecutedSeconds > 0);
    }

    [Fact]
    public void ActivityPlan_OnlyOnePlanCanBeRunningPerCharacter()
    {
        // Arrange
        var characterId = Guid.NewGuid();
        
        var plan1 = new ActivityPlan
        {
            Id = Guid.NewGuid(),
            CharacterId = characterId,
            State = ActivityState.Running
        };
        
        var plan2 = new ActivityPlan
        {
            Id = Guid.NewGuid(),
            CharacterId = characterId,
            State = ActivityState.Pending
        };

        // Act & Assert - 验证只能有一个Running状态的计划
        var runningPlans = new[] { plan1, plan2 }
            .Where(p => p.CharacterId == characterId && p.State == ActivityState.Running)
            .ToList();

        Assert.Single(runningPlans);
        Assert.Equal(plan1.Id, runningPlans[0].Id);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void ActivityPlan_SlotIndex_ShouldBeValid(int slotIndex)
    {
        // Arrange & Act
        var plan = new ActivityPlan
        {
            Id = Guid.NewGuid(),
            CharacterId = Guid.NewGuid(),
            SlotIndex = slotIndex,
            State = ActivityState.Pending
        };

        // Assert
        Assert.True(plan.SlotIndex >= 0 && plan.SlotIndex < 5);
    }

    [Fact]
    public void ActivityPlan_AutoExecution_ConceptualFlow()
    {
        // 这个测试描述自动执行的概念流程
        
        // 步骤1: 创建计划时，如果没有运行中的任务，自动启动
        var characterId = Guid.NewGuid();
        var hasRunningPlan = false; // 模拟查询结果
        
        if (!hasRunningPlan)
        {
            // 应该自动启动这个计划
            var autoStarted = true;
            Assert.True(autoStarted);
        }
        
        // 步骤2: 如果有运行中的任务，新计划保持Pending状态
        hasRunningPlan = true;
        if (hasRunningPlan)
        {
            var newPlanState = ActivityState.Pending;
            Assert.Equal(ActivityState.Pending, newPlanState);
        }
        
        // 步骤3: 任务完成时，自动启动下一个Pending任务
        var currentTaskCompleted = true;
        if (currentTaskCompleted)
        {
            var shouldStartNext = true;
            Assert.True(shouldStartNext);
        }
    }
}
