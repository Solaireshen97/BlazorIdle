using BlazorIdle.Client.Services;
using Xunit;

namespace BlazorIdle.Tests.Services;

/// <summary>
/// 装备对比服务测试
/// </summary>
public class EquipmentComparisonServiceTests
{
    private readonly EquipmentComparisonService _service;

    public EquipmentComparisonServiceTests()
    {
        _service = new EquipmentComparisonService();
    }

    [Fact]
    public void Compare_NoCurrentGear_ShouldReturnNewGearStats()
    {
        // Arrange
        var newGear = new GearInstanceDto
        {
            Id = Guid.NewGuid(),
            Name = "测试剑",
            QualityScore = 100,
            ItemLevel = 10,
            Stats = new Dictionary<string, double>
            {
                { "AttackPower", 50 },
                { "Strength", 10 }
            }
        };

        // Act
        var result = _service.Compare(null, newGear);

        // Assert
        Assert.True(result.IsUpgrade);
        Assert.Equal(100, result.QualityScoreDifference);
        Assert.Equal(10, result.ItemLevelDifference);
        Assert.Equal(50, result.StatDifferences["AttackPower"]);
        Assert.Equal(10, result.StatDifferences["Strength"]);
        Assert.True(result.OverallScore > 0);
    }

    [Fact]
    public void Compare_NewGearBetter_ShouldShowUpgrade()
    {
        // Arrange
        var currentGear = new GearInstanceDto
        {
            Id = Guid.NewGuid(),
            Name = "旧剑",
            QualityScore = 80,
            ItemLevel = 8,
            Stats = new Dictionary<string, double>
            {
                { "AttackPower", 40 },
                { "Strength", 8 }
            }
        };

        var newGear = new GearInstanceDto
        {
            Id = Guid.NewGuid(),
            Name = "新剑",
            QualityScore = 120,
            ItemLevel = 12,
            Stats = new Dictionary<string, double>
            {
                { "AttackPower", 60 },
                { "Strength", 12 },
                { "CritChance", 0.05 }  // 新增暴击
            }
        };

        // Act
        var result = _service.Compare(currentGear, newGear);

        // Assert
        Assert.True(result.IsUpgrade);
        Assert.Equal(40, result.QualityScoreDifference);
        Assert.Equal(4, result.ItemLevelDifference);
        Assert.Equal(20, result.StatDifferences["AttackPower"]);
        Assert.Equal(4, result.StatDifferences["Strength"]);
        Assert.Equal(0.05, result.StatDifferences["CritChance"]);
        Assert.True(result.OverallScore > 0);
    }

    [Fact]
    public void Compare_NewGearWorse_ShouldShowDowngrade()
    {
        // Arrange
        var currentGear = new GearInstanceDto
        {
            Id = Guid.NewGuid(),
            Name = "好剑",
            QualityScore = 150,
            ItemLevel = 15,
            Stats = new Dictionary<string, double>
            {
                { "AttackPower", 80 },
                { "Strength", 15 },
                { "CritChance", 0.08 }
            }
        };

        var newGear = new GearInstanceDto
        {
            Id = Guid.NewGuid(),
            Name = "差剑",
            QualityScore = 100,
            ItemLevel = 10,
            Stats = new Dictionary<string, double>
            {
                { "AttackPower", 50 },
                { "Strength", 10 }
            }
        };

        // Act
        var result = _service.Compare(currentGear, newGear);

        // Assert
        Assert.False(result.IsUpgrade);
        Assert.Equal(-50, result.QualityScoreDifference);
        Assert.Equal(-5, result.ItemLevelDifference);
        Assert.Equal(-30, result.StatDifferences["AttackPower"]);
        Assert.Equal(-5, result.StatDifferences["Strength"]);
        Assert.Equal(-0.08, result.StatDifferences["CritChance"]);
        Assert.True(result.OverallScore < 0);
    }

    [Fact]
    public void Compare_MixedChanges_ShouldCalculateCorrectly()
    {
        // Arrange
        var currentGear = new GearInstanceDto
        {
            Id = Guid.NewGuid(),
            Name = "旧装备",
            QualityScore = 100,
            ItemLevel = 10,
            Stats = new Dictionary<string, double>
            {
                { "AttackPower", 50 },
                { "Armor", 100 }
            }
        };

        var newGear = new GearInstanceDto
        {
            Id = Guid.NewGuid(),
            Name = "新装备",
            QualityScore = 110,
            ItemLevel = 11,
            Stats = new Dictionary<string, double>
            {
                { "AttackPower", 60 },  // +10
                { "Armor", 80 }         // -20
            }
        };

        // Act
        var result = _service.Compare(currentGear, newGear);

        // Assert
        Assert.Equal(10, result.QualityScoreDifference);
        Assert.Equal(10, result.StatDifferences["AttackPower"]);
        Assert.Equal(-20, result.StatDifferences["Armor"]);
    }

    [Fact]
    public void GetDifferenceDisplayText_PositiveValue_ShouldShowGreenArrow()
    {
        // Act
        var text = _service.GetDifferenceDisplayText("AttackPower", 10, true);

        // Assert
        Assert.Contains("green", text);
        Assert.Contains("↑", text);
        Assert.Contains("+10", text);
    }

    [Fact]
    public void GetDifferenceDisplayText_NegativeValue_ShouldShowRedArrow()
    {
        // Act
        var text = _service.GetDifferenceDisplayText("AttackPower", -10, true);

        // Assert
        Assert.Contains("red", text);
        Assert.Contains("↓", text);
        Assert.Contains("-10", text);
    }

    [Fact]
    public void GetDifferenceDisplayText_PercentageStat_ShouldShowPercent()
    {
        // Act
        var text = _service.GetDifferenceDisplayText("CritChance", 0.05, true);

        // Assert
        Assert.Contains("5.0%", text);
    }

    [Fact]
    public void GetDifferenceDisplayText_NoColor_ShouldNotIncludeHtml()
    {
        // Act
        var text = _service.GetDifferenceDisplayText("AttackPower", 10, false);

        // Assert
        Assert.DoesNotContain("<span", text);
        Assert.Equal("+10", text);
    }

    [Fact]
    public void GetStatDisplayName_ShouldReturnChineseName()
    {
        // Arrange & Act
        var names = new Dictionary<string, string>
        {
            { "AttackPower", _service.GetStatDisplayName("AttackPower") },
            { "CritChance", _service.GetStatDisplayName("CritChance") },
            { "Strength", _service.GetStatDisplayName("Strength") }
        };

        // Assert
        Assert.Equal("攻击力", names["AttackPower"]);
        Assert.Equal("暴击率", names["CritChance"]);
        Assert.Equal("力量", names["Strength"]);
    }
}
