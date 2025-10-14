using BlazorIdle.Server.Config;
using BlazorIdle.Server.Services;
using BlazorIdle.Shared.Models;
using Microsoft.Extensions.Options;
using Xunit;

namespace BlazorIdle.Tests;

/// <summary>
/// 战斗消息系统单元测试
/// </summary>
public class BattleMessageTests
{
    [Fact]
    public void BattleMessageFormatter_FormatAttackStarted_ReplacesPlaceholders()
    {
        // Arrange
        var options = Options.Create(new BattleMessageOptions
        {
            AttackStartedTemplate = "{attacker} 开始攻击 {target}"
        });
        var formatter = new BattleMessageFormatter(options);

        // Act
        var message = formatter.FormatAttackStarted("玩家", "史莱姆");

        // Assert
        Assert.Equal("玩家 开始攻击 史莱姆", message);
    }

    [Fact]
    public void BattleMessageFormatter_FormatDamageDealt_WithoutCrit_ReplacesPlaceholders()
    {
        // Arrange
        var options = Options.Create(new BattleMessageOptions
        {
            DamageDealtTemplate = "{attacker} 对 {target} 造成 {damage} 点伤害{critSuffix}",
            CritSuffix = "（暴击）"
        });
        var formatter = new BattleMessageFormatter(options);

        // Act
        var message = formatter.FormatDamageDealt("玩家", "史莱姆", 100, isCrit: false);

        // Assert
        Assert.Equal("玩家 对 史莱姆 造成 100 点伤害", message);
    }

    [Fact]
    public void BattleMessageFormatter_FormatDamageDealt_WithCrit_IncludesCritSuffix()
    {
        // Arrange
        var options = Options.Create(new BattleMessageOptions
        {
            DamageDealtTemplate = "{attacker} 对 {target} 造成 {damage} 点伤害{critSuffix}",
            CritSuffix = "（暴击）"
        });
        var formatter = new BattleMessageFormatter(options);

        // Act
        var message = formatter.FormatDamageDealt("玩家", "史莱姆", 200, isCrit: true);

        // Assert
        Assert.Equal("玩家 对 史莱姆 造成 200 点伤害（暴击）", message);
    }

    [Fact]
    public void BattleMessageFormatter_FormatDamageReceived_ReplacesPlaceholders()
    {
        // Arrange
        var options = Options.Create(new BattleMessageOptions
        {
            DamageReceivedTemplate = "{target} 受到来自 {attacker} 的 {damage} 点伤害"
        });
        var formatter = new BattleMessageFormatter(options);

        // Act
        var message = formatter.FormatDamageReceived("玩家", "哥布林", 50);

        // Assert
        Assert.Equal("玩家 受到来自 哥布林 的 50 点伤害", message);
    }

    [Fact]
    public void BattleMessageFormatter_IsAttackStartedEnabled_ReturnsConfigValue()
    {
        // Arrange
        var options = Options.Create(new BattleMessageOptions
        {
            EnableAttackStartedEvent = true
        });
        var formatter = new BattleMessageFormatter(options);

        // Act & Assert
        Assert.True(formatter.IsAttackStartedEnabled);
    }

    [Fact]
    public void BattleMessageFormatter_GetPlayerName_ReturnsConfigValue()
    {
        // Arrange
        var options = Options.Create(new BattleMessageOptions
        {
            PlayerName = "测试玩家"
        });
        var formatter = new BattleMessageFormatter(options);

        // Act
        var playerName = formatter.GetPlayerName();

        // Assert
        Assert.Equal("测试玩家", playerName);
    }

    [Fact]
    public void AttackStartedEventDto_HasRequiredProperties()
    {
        // Arrange & Act
        var evt = new AttackStartedEventDto
        {
            BattleId = Guid.NewGuid(),
            EventTime = 5.0,
            EventType = "AttackStarted",
            AttackerName = "玩家",
            TargetName = "史莱姆",
            Message = "玩家 开始攻击 史莱姆"
        };

        // Assert
        Assert.NotEqual(Guid.Empty, evt.BattleId);
        Assert.Equal(5.0, evt.EventTime);
        Assert.Equal("AttackStarted", evt.EventType);
        Assert.Equal("玩家", evt.AttackerName);
        Assert.Equal("史莱姆", evt.TargetName);
        Assert.Equal("玩家 开始攻击 史莱姆", evt.Message);
    }

    [Fact]
    public void DamageAppliedEventDto_HasExtendedProperties()
    {
        // Arrange & Act
        var evt = new DamageAppliedEventDto
        {
            BattleId = Guid.NewGuid(),
            EventTime = 5.5,
            EventType = "DamageApplied",
            Source = "basic_attack",
            Damage = 150,
            IsCrit = true,
            TargetCurrentHp = 350,
            TargetMaxHp = 500,
            AttackerName = "玩家",
            TargetName = "哥布林",
            Message = "玩家 对 哥布林 造成 150 点伤害（暴击）"
        };

        // Assert
        Assert.Equal("basic_attack", evt.Source);
        Assert.Equal(150, evt.Damage);
        Assert.True(evt.IsCrit);
        Assert.Equal(350, evt.TargetCurrentHp);
        Assert.Equal(500, evt.TargetMaxHp);
        Assert.Equal("玩家", evt.AttackerName);
        Assert.Equal("哥布林", evt.TargetName);
        Assert.Contains("暴击", evt.Message);
    }

    [Fact]
    public void DamageReceivedEventDto_HasRequiredProperties()
    {
        // Arrange & Act
        var evt = new DamageReceivedEventDto
        {
            BattleId = Guid.NewGuid(),
            EventTime = 6.0,
            EventType = "DamageReceived",
            AttackerName = "哥布林",
            TargetName = "玩家",
            Damage = 30,
            TargetCurrentHp = 470,
            TargetMaxHp = 500,
            Message = "玩家 受到来自 哥布林 的 30 点伤害"
        };

        // Assert
        Assert.Equal("哥布林", evt.AttackerName);
        Assert.Equal("玩家", evt.TargetName);
        Assert.Equal(30, evt.Damage);
        Assert.Equal(470, evt.TargetCurrentHp);
        Assert.Equal(500, evt.TargetMaxHp);
        Assert.Contains("哥布林", evt.Message);
    }
}
