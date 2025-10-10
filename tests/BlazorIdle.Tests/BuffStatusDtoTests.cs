using BlazorIdle.Server.Application.Battles.Step;
using BlazorIdle.Server.Domain.Characters;
using BlazorIdle.Shared.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace BlazorIdle.Tests;

/// <summary>
/// Buff状态DTO测试 - Step 3: Buff状态显示
/// 验证 BuffStatusDto 的提取和格式化功能
/// </summary>
public class BuffStatusDtoTests
{
    [Fact]
    public void BuffStatusDto_ShouldHaveCorrectStructure()
    {
        // Arrange & Act
        var buffDto = new BuffStatusDto
        {
            Id = "test_buff",
            Name = "测试Buff",
            Icon = "✨",
            Stacks = 3,
            MaxStacks = 5,
            RemainingSeconds = 10.5,
            IsDebuff = false,
            Source = "测试技能"
        };

        // Assert
        Assert.Equal("test_buff", buffDto.Id);
        Assert.Equal("测试Buff", buffDto.Name);
        Assert.Equal("✨", buffDto.Icon);
        Assert.Equal(3, buffDto.Stacks);
        Assert.Equal(5, buffDto.MaxStacks);
        Assert.Equal(10.5, buffDto.RemainingSeconds);
        Assert.False(buffDto.IsDebuff);
        Assert.Equal("测试技能", buffDto.Source);
    }

    [Fact]
    public void StepBattleStatusDto_ShouldSupportEmptyBuffLists()
    {
        // Arrange & Act
        var statusDto = new StepBattleStatusDto
        {
            Id = Guid.NewGuid(),
            CharacterId = Guid.NewGuid(),
            Profession = Profession.Warrior,
            PlayerBuffs = new List<BuffStatusDto>(),
            EnemyBuffs = new List<BuffStatusDto>()
        };

        // Assert
        Assert.NotNull(statusDto.PlayerBuffs);
        Assert.NotNull(statusDto.EnemyBuffs);
        Assert.Empty(statusDto.PlayerBuffs);
        Assert.Empty(statusDto.EnemyBuffs);
    }

    [Fact]
    public void BuffStatusDto_ShouldSupportMultipleBuffs()
    {
        // Arrange
        var buffs = new List<BuffStatusDto>
        {
            new BuffStatusDto 
            { 
                Id = "warrior_expose_armor",
                Name = "破甲",
                Icon = "🛡️",
                Stacks = 5,
                MaxStacks = 10,
                RemainingSeconds = 8.5,
                IsDebuff = false
            },
            new BuffStatusDto 
            { 
                Id = "warrior_precision",
                Name = "精准",
                Icon = "⚡",
                Stacks = 2,
                MaxStacks = 3,
                RemainingSeconds = 12.0,
                IsDebuff = false
            }
        };

        // Act & Assert
        Assert.Equal(2, buffs.Count);
        Assert.All(buffs, buff => Assert.False(buff.IsDebuff));
        Assert.Contains(buffs, b => b.Name == "破甲");
        Assert.Contains(buffs, b => b.Name == "精准");
    }
}
