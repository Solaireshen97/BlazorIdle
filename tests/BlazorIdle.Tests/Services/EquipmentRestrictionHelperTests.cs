using BlazorIdle.Client.Services;
using BlazorIdle.Shared.Models;
using Xunit;

namespace BlazorIdle.Tests.Services;

/// <summary>
/// 装备限制辅助服务测试
/// </summary>
public class EquipmentRestrictionHelperTests
{
    private readonly EquipmentRestrictionHelper _helper;

    public EquipmentRestrictionHelperTests()
    {
        _helper = new EquipmentRestrictionHelper();
    }

    [Fact]
    public void CheckRestrictions_WarriorCanEquipPlate_ShouldPass()
    {
        // Arrange
        var gear = new GearInstanceDto
        {
            Id = Guid.NewGuid(),
            Name = "板甲胸甲",
            ItemLevel = 10,
            ArmorType = "Plate"
        };

        // Act
        var result = _helper.CheckRestrictions(gear, Profession.Warrior, 15);

        // Assert
        Assert.True(result.CanEquip);
        Assert.Empty(result.Reasons);
    }

    [Fact]
    public void CheckRestrictions_RangerCannotEquipPlate_ShouldFail()
    {
        // Arrange
        var gear = new GearInstanceDto
        {
            Id = Guid.NewGuid(),
            Name = "板甲胸甲",
            ItemLevel = 10,
            ArmorType = "Plate"
        };

        // Act
        var result = _helper.CheckRestrictions(gear, Profession.Ranger, 15);

        // Assert
        Assert.False(result.CanEquip);
        Assert.NotEmpty(result.Reasons);
        Assert.Contains("战士", result.Reasons[0]);
    }

    [Fact]
    public void CheckRestrictions_LevelTooLow_ShouldFail()
    {
        // Arrange
        var gear = new GearInstanceDto
        {
            Id = Guid.NewGuid(),
            Name = "高级剑",
            ItemLevel = 20,
            WeaponType = "Sword"
        };

        // Act
        var result = _helper.CheckRestrictions(gear, Profession.Warrior, 10);

        // Assert
        Assert.False(result.CanEquip);
        Assert.Equal(20, result.RequiredLevel);
        Assert.Contains("需要等级 20", result.Reasons[0]);
    }

    [Fact]
    public void CheckRestrictions_WarriorCanEquipShield_ShouldPass()
    {
        // Arrange
        var gear = new GearInstanceDto
        {
            Id = Guid.NewGuid(),
            Name = "战士盾牌",
            ItemLevel = 10,
            WeaponType = "Shield"
        };

        // Act
        var result = _helper.CheckRestrictions(gear, Profession.Warrior, 15);

        // Assert
        Assert.True(result.CanEquip);
        Assert.Empty(result.Reasons);
    }

    [Fact]
    public void CheckRestrictions_RangerCannotEquipShield_ShouldFail()
    {
        // Arrange
        var gear = new GearInstanceDto
        {
            Id = Guid.NewGuid(),
            Name = "盾牌",
            ItemLevel = 10,
            WeaponType = "Shield"
        };

        // Act
        var result = _helper.CheckRestrictions(gear, Profession.Ranger, 15);

        // Assert
        Assert.False(result.CanEquip);
        Assert.Contains("战士", result.Reasons[0]);
    }

    [Fact]
    public void CheckRestrictions_RangerCanEquipBow_ShouldPass()
    {
        // Arrange
        var gear = new GearInstanceDto
        {
            Id = Guid.NewGuid(),
            Name = "猎弓",
            ItemLevel = 10,
            WeaponType = "Bow"
        };

        // Act
        var result = _helper.CheckRestrictions(gear, Profession.Ranger, 15);

        // Assert
        Assert.True(result.CanEquip);
        Assert.Empty(result.Reasons);
    }

    [Fact]
    public void CheckRestrictions_MultipleRestrictions_ShouldListAll()
    {
        // Arrange
        var gear = new GearInstanceDto
        {
            Id = Guid.NewGuid(),
            Name = "高级板甲",
            ItemLevel = 30,
            ArmorType = "Plate"
        };

        // Act
        var result = _helper.CheckRestrictions(gear, Profession.Ranger, 10);

        // Assert
        Assert.False(result.CanEquip);
        Assert.Equal(2, result.Reasons.Count); // 等级不足 + 职业不符
        Assert.Contains(result.Reasons, r => r.Contains("等级"));
        Assert.Contains(result.Reasons, r => r.Contains("职业"));
    }

    [Fact]
    public void GetRestrictionColor_CanEquip_ShouldReturnGreen()
    {
        // Act
        var color = _helper.GetRestrictionColor(true);

        // Assert
        Assert.Equal("#4caf50", color);
    }

    [Fact]
    public void GetRestrictionColor_CannotEquip_ShouldReturnRed()
    {
        // Act
        var color = _helper.GetRestrictionColor(false);

        // Assert
        Assert.Equal("#f44336", color);
    }

    [Fact]
    public void GetRestrictionIcon_CanEquip_ShouldReturnCheckmark()
    {
        // Act
        var icon = _helper.GetRestrictionIcon(true);

        // Assert
        Assert.Equal("✓", icon);
    }

    [Fact]
    public void GetRestrictionIcon_CannotEquip_ShouldReturnCross()
    {
        // Act
        var icon = _helper.GetRestrictionIcon(false);

        // Assert
        Assert.Equal("✗", icon);
    }

    [Fact]
    public void GenerateRestrictionTooltip_CanEquip_ShouldShowPositiveMessage()
    {
        // Arrange
        var result = new EquipmentRestrictionHelper.RestrictionCheckResult
        {
            CanEquip = true
        };

        // Act
        var tooltip = _helper.GenerateRestrictionTooltip(result);

        // Assert
        Assert.Contains("✓", tooltip);
        Assert.Contains("可以装备", tooltip);
    }

    [Fact]
    public void GenerateRestrictionTooltip_CannotEquip_ShouldListReasons()
    {
        // Arrange
        var result = new EquipmentRestrictionHelper.RestrictionCheckResult
        {
            CanEquip = false,
            Reasons = new List<string> { "需要等级 20", "需要职业: 战士" }
        };

        // Act
        var tooltip = _helper.GenerateRestrictionTooltip(result);

        // Assert
        Assert.Contains("✗", tooltip);
        Assert.Contains("无法装备", tooltip);
        Assert.Contains("需要等级 20", tooltip);
        Assert.Contains("需要职业: 战士", tooltip);
    }
}
