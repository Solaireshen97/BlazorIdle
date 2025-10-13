using BlazorIdle.Server.Application.Battles.Step;
using BlazorIdle.Server.Domain.Characters;
using BlazorIdle.Server.Domain.Combat.Enemies;
using BlazorIdle.Shared.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using Xunit;

namespace BlazorIdle.Tests;

public class PollingHintTests
{
    [Fact]
    public void GetStatus_ReturnsPollingHint_WhenBattleIsRunning()
    {
        // Arrange
        var coordinator = TestHelpers.CreateCoordinator();
        var characterId = Guid.NewGuid();
        var stats = new CharacterStats
        {
            AttackPower = 10
        };

        var battleId = coordinator.Start(
            characterId: characterId,
            profession: Profession.Warrior,
            stats: stats,
            seconds: 30.0,
            seed: 12345UL,
            enemyId: "dummy",
            enemyCount: 1,
            stamina: 10
        );

        // Act
        var (found, status) = coordinator.GetStatus(battleId);

        // Assert
        Assert.True(found);
        Assert.NotNull(status);
        Assert.NotNull(status.PollingHint);
        Assert.True(status.PollingHint.SuggestedIntervalMs > 0);
        Assert.True(status.PollingHint.SuggestedIntervalMs <= 5000);
    }

    [Fact]
    public void GetStatus_ReturnsStablePollingHint_ForHealthyPlayer()
    {
        // Arrange
        var coordinator = TestHelpers.CreateCoordinator();
        var characterId = Guid.NewGuid();
        var stats = new CharacterStats
        {
            AttackPower = 10
        };

        var battleId = coordinator.Start(
            characterId: characterId,
            profession: Profession.Warrior,
            stats: stats,
            seconds: 30.0,
            seed: 12345UL,
            enemyId: "dummy",
            enemyCount: 1,
            stamina: 10
        );

        // Act - advance a bit but not too much
        coordinator.AdvanceAll(maxEventsPerBattle: 10, maxSliceSeconds: 0.1);
        var (found, status) = coordinator.GetStatus(battleId);

        // Assert
        Assert.True(found);
        Assert.NotNull(status.PollingHint);
        
        // For a healthy player (HP > 50%), should suggest 2000ms (stable)
        if (status.PlayerHpPercent >= 0.5 && !status.PlayerIsDead && !status.Completed)
        {
            Assert.Equal(2000, status.PollingHint.SuggestedIntervalMs);
            Assert.True(status.PollingHint.IsStable);
        }
    }

    [Fact]
    public void GetStatus_ReturnsSlowestPollingHint_ForCompletedBattle()
    {
        // Arrange
        var coordinator = TestHelpers.CreateCoordinator();
        var characterId = Guid.NewGuid();
        var stats = new CharacterStats
        {
            AttackPower = 100 // High stats to complete quickly
        };

        var battleId = coordinator.Start(
            characterId: characterId,
            profession: Profession.Warrior,
            stats: stats,
            seconds: 5.0, // Short duration
            seed: 12345UL,
            enemyId: "dummy",
            enemyCount: 1,
            stamina: 100
        );

        // Act - advance until completion
        for (int i = 0; i < 20; i++)
        {
            coordinator.AdvanceAll(maxEventsPerBattle: 100, maxSliceSeconds: 1.0);
            var (_, checkStatus) = coordinator.GetStatus(battleId);
            if (checkStatus.Completed)
                break;
        }

        var (found, status) = coordinator.GetStatus(battleId);

        // Assert
        Assert.True(found);
        Assert.NotNull(status.PollingHint);
        
        // For completed battle, should suggest 5000ms (slowest)
        if (status.Completed)
        {
            Assert.Equal(5000, status.PollingHint.SuggestedIntervalMs);
            Assert.True(status.PollingHint.IsStable);
        }
    }

    [Fact]
    public void GetStatus_PollingHintContainsNextSignificantEvent()
    {
        // Arrange
        var coordinator = TestHelpers.CreateCoordinator();
        var characterId = Guid.NewGuid();
        var stats = new CharacterStats
        {
            AttackPower = 10
        };

        var battleId = coordinator.Start(
            characterId: characterId,
            profession: Profession.Warrior,
            stats: stats,
            seconds: 30.0,
            seed: 12345UL,
            enemyId: "dummy",
            enemyCount: 1,
            stamina: 10
        );

        // Act
        coordinator.AdvanceAll(maxEventsPerBattle: 10, maxSliceSeconds: 0.5);
        var (found, status) = coordinator.GetStatus(battleId);

        // Assert
        Assert.True(found);
        Assert.NotNull(status.PollingHint);
        
        // NextSignificantEventAt should be set if there are upcoming attacks
        if (!status.Completed && (status.NextAttackAt.HasValue || status.NextSpecialAt.HasValue))
        {
            Assert.NotNull(status.PollingHint.NextSignificantEventAt);
            Assert.True(status.PollingHint.NextSignificantEventAt >= status.CurrentTime);
        }
    }
}
