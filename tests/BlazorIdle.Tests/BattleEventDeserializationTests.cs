using System.Text.Json;
using BlazorIdle.Shared.Models;
using Xunit;

namespace BlazorIdle.Tests;

/// <summary>
/// 测试战斗事件 JSON 反序列化
/// </summary>
public class BattleEventDeserializationTests
{
    [Fact]
    public void AttackStartedEventDto_Deserialize_Success()
    {
        // Arrange
        var json = """
        {
            "BattleId": "123e4567-e89b-12d3-a456-426614174000",
            "EventTime": 10.5,
            "EventType": "AttackStarted",
            "AttackerName": "勇士",
            "TargetName": "哥布林",
            "Message": "勇士 开始攻击 哥布林"
        }
        """;
        
        var jsonElement = JsonSerializer.Deserialize<JsonElement>(json);

        // Act
        var result = jsonElement.Deserialize<AttackStartedEventDto>();

        // Assert
        Assert.NotNull(result);
        Assert.Equal("AttackStarted", result.EventType);
        Assert.Equal("勇士", result.AttackerName);
        Assert.Equal("哥布林", result.TargetName);
        Assert.Equal("勇士 开始攻击 哥布林", result.Message);
        Assert.Equal(10.5, result.EventTime);
    }

    [Fact]
    public void DamageAppliedEventDto_Deserialize_Success()
    {
        // Arrange
        var json = """
        {
            "BattleId": "123e4567-e89b-12d3-a456-426614174000",
            "EventTime": 10.5,
            "EventType": "DamageApplied",
            "Source": "basic_attack",
            "Damage": 150,
            "IsCrit": true,
            "TargetCurrentHp": 350,
            "TargetMaxHp": 500,
            "AttackerName": "勇士",
            "TargetName": "哥布林",
            "Message": "勇士 对 哥布林 造成 150 点暴击伤害"
        }
        """;
        
        var jsonElement = JsonSerializer.Deserialize<JsonElement>(json);

        // Act
        var result = jsonElement.Deserialize<DamageAppliedEventDto>();

        // Assert
        Assert.NotNull(result);
        Assert.Equal("DamageApplied", result.EventType);
        Assert.Equal("basic_attack", result.Source);
        Assert.Equal(150, result.Damage);
        Assert.True(result.IsCrit);
        Assert.Equal(350, result.TargetCurrentHp);
        Assert.Equal(500, result.TargetMaxHp);
        Assert.Equal("勇士", result.AttackerName);
        Assert.Equal("哥布林", result.TargetName);
    }

    [Fact]
    public void DamageReceivedEventDto_Deserialize_Success()
    {
        // Arrange
        var json = """
        {
            "BattleId": "123e4567-e89b-12d3-a456-426614174000",
            "EventTime": 11.0,
            "EventType": "DamageReceived",
            "AttackerName": "哥布林",
            "TargetName": "勇士",
            "Damage": 50,
            "TargetCurrentHp": 450,
            "TargetMaxHp": 500,
            "Message": "勇士 受到 哥布林 的 50 点伤害"
        }
        """;
        
        var jsonElement = JsonSerializer.Deserialize<JsonElement>(json);

        // Act
        var result = jsonElement.Deserialize<DamageReceivedEventDto>();

        // Assert
        Assert.NotNull(result);
        Assert.Equal("DamageReceived", result.EventType);
        Assert.Equal("哥布林", result.AttackerName);
        Assert.Equal("勇士", result.TargetName);
        Assert.Equal(50, result.Damage);
        Assert.Equal(450, result.TargetCurrentHp);
        Assert.Equal(500, result.TargetMaxHp);
    }

    [Fact]
    public void AttackTickEventDto_Deserialize_Success()
    {
        // Arrange
        var json = """
        {
            "BattleId": "123e4567-e89b-12d3-a456-426614174000",
            "EventTime": 10.0,
            "EventType": "AttackTick",
            "NextTriggerAt": 12.5,
            "Interval": 2.5
        }
        """;
        
        var jsonElement = JsonSerializer.Deserialize<JsonElement>(json);

        // Act
        var result = jsonElement.Deserialize<AttackTickEventDto>();

        // Assert
        Assert.NotNull(result);
        Assert.Equal("AttackTick", result.EventType);
        Assert.Equal(12.5, result.NextTriggerAt);
        Assert.Equal(2.5, result.Interval);
    }

    [Fact]
    public void EventType_Property_IsAccessible()
    {
        // Arrange
        var json = """
        {
            "BattleId": "123e4567-e89b-12d3-a456-426614174000",
            "EventTime": 10.5,
            "EventType": "AttackStarted",
            "AttackerName": "勇士",
            "TargetName": "哥布林",
            "Message": "测试消息"
        }
        """;
        
        var jsonElement = JsonSerializer.Deserialize<JsonElement>(json);

        // Act
        var hasEventType = jsonElement.TryGetProperty("EventType", out var eventTypeProperty);
        var eventType = eventTypeProperty.GetString();

        // Assert
        Assert.True(hasEventType);
        Assert.Equal("AttackStarted", eventType);
    }
}
