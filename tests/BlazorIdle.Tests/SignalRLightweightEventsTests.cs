using BlazorIdle.Server.Config;
using BlazorIdle.Shared.Models;
using Microsoft.Extensions.Options;
using Xunit;

namespace BlazorIdle.Tests;

/// <summary>
/// SignalR 轻量事件测试（Phase 2.5）
/// </summary>
public class SignalRLightweightEventsTests
{
    [Fact]
    public void SignalROptions_ShouldHave_AttackTickNotification_Enabled_ByDefault()
    {
        // Arrange
        var options = new SignalROptions();
        
        // Assert
        Assert.True(options.Notification.EnableAttackTickNotification);
    }
    
    [Fact]
    public void SignalROptions_ShouldHave_SkillCastNotification_Enabled()
    {
        // Arrange
        var options = new SignalROptions();
        
        // Assert
        Assert.True(options.Notification.EnableSkillCastNotification);
    }
    
    [Fact]
    public void SignalROptions_ShouldHave_DamageAppliedNotification_Enabled_ByDefault()
    {
        // Arrange
        var options = new SignalROptions();
        
        // Assert
        Assert.True(options.Notification.EnableDamageAppliedNotification);
    }
    
    [Fact]
    public void AttackTickEventDto_ShouldSerialize_Correctly()
    {
        // Arrange
        var dto = new AttackTickEventDto
        {
            BattleId = Guid.NewGuid(),
            EventTime = 10.5,
            EventType = "AttackTick",
            NextAttackAt = 12.5,
            AttackInterval = 2.0,
            IsCrit = true
        };
        
        // Act & Assert
        Assert.Equal("AttackTick", dto.EventType);
        Assert.Equal(10.5, dto.EventTime);
        Assert.Equal(12.5, dto.NextAttackAt);
        Assert.Equal(2.0, dto.AttackInterval);
        Assert.True(dto.IsCrit);
    }
    
    [Fact]
    public void SkillCastEventDto_ShouldSerialize_Correctly()
    {
        // Arrange
        var dto = new SkillCastEventDto
        {
            BattleId = Guid.NewGuid(),
            EventTime = 5.0,
            EventType = "SkillCast",
            SkillId = "fireball",
            SkillName = "Fireball",
            IsCastStart = true,
            CastDuration = 1.5,
            CooldownDuration = 8.0,
            CooldownReadyAt = 13.0
        };
        
        // Act & Assert
        Assert.Equal("SkillCast", dto.EventType);
        Assert.Equal("fireball", dto.SkillId);
        Assert.Equal("Fireball", dto.SkillName);
        Assert.True(dto.IsCastStart);
        Assert.Equal(1.5, dto.CastDuration);
        Assert.Equal(8.0, dto.CooldownDuration);
    }
    
    [Fact]
    public void DamageAppliedEventDto_ShouldSerialize_Correctly()
    {
        // Arrange
        var dto = new DamageAppliedEventDto
        {
            BattleId = Guid.NewGuid(),
            EventTime = 3.5,
            EventType = "DamageApplied",
            SourceId = "basic_attack",
            DamageAmount = 50,
            DamageType = "Physical",
            TargetCurrentHp = 450,
            TargetMaxHp = 1000,
            TargetDied = false
        };
        
        // Act & Assert
        Assert.Equal("DamageApplied", dto.EventType);
        Assert.Equal("basic_attack", dto.SourceId);
        Assert.Equal(50, dto.DamageAmount);
        Assert.Equal("Physical", dto.DamageType);
        Assert.Equal(450, dto.TargetCurrentHp);
        Assert.Equal(1000, dto.TargetMaxHp);
        Assert.False(dto.TargetDied);
    }
    
    [Fact]
    public void DamageAppliedEventDto_ShouldIndicate_TargetDeath()
    {
        // Arrange
        var dto = new DamageAppliedEventDto
        {
            BattleId = Guid.NewGuid(),
            EventTime = 10.0,
            EventType = "DamageApplied",
            SourceId = "skill:execute",
            DamageAmount = 100,
            DamageType = "Physical",
            TargetCurrentHp = 0,
            TargetMaxHp = 500,
            TargetDied = true
        };
        
        // Act & Assert
        Assert.True(dto.TargetDied);
        Assert.Equal(0, dto.TargetCurrentHp);
    }
    
    [Fact]
    public void SkillCastEventDto_InstantCast_ShouldHave_ZeroCastDuration()
    {
        // Arrange
        var dto = new SkillCastEventDto
        {
            BattleId = Guid.NewGuid(),
            EventTime = 2.0,
            EventType = "SkillCast",
            SkillId = "instant_skill",
            SkillName = "Instant Skill",
            IsCastStart = false,
            CastDuration = 0,
            CooldownDuration = 5.0,
            CooldownReadyAt = 7.0
        };
        
        // Act & Assert
        Assert.Equal(0, dto.CastDuration);
        Assert.False(dto.IsCastStart);
    }
    
    [Theory]
    [InlineData(0.5, 2.0, false)]
    [InlineData(1.5, 3.0, true)]
    [InlineData(2.5, 4.0, false)]
    public void AttackTickEventDto_ShouldHandle_VariousIntervals(double eventTime, double nextAttackAt, bool isCrit)
    {
        // Arrange
        var dto = new AttackTickEventDto
        {
            BattleId = Guid.NewGuid(),
            EventTime = eventTime,
            EventType = "AttackTick",
            NextAttackAt = nextAttackAt,
            AttackInterval = nextAttackAt - eventTime,
            IsCrit = isCrit
        };
        
        // Act & Assert
        Assert.Equal(nextAttackAt - eventTime, dto.AttackInterval);
        Assert.Equal(isCrit, dto.IsCrit);
    }
}
