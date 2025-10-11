using BlazorIdle.Server.Application.Abstractions;
using BlazorIdle.Server.Application.Equipment;
using BlazorIdle.Server.Domain.Equipment.Models;
using BlazorIdle.Server.Domain.Equipment.ValueObjects;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace BlazorIdle.Tests.Equipment.Services;

public class GearGenerationServiceTests
{
    private readonly Mock<IGearDefinitionRepository> _defRepo;
    private readonly Mock<IAffixRepository> _affixRepo;
    private readonly Mock<ILogger<GearGenerationService>> _logger;
    private readonly GearGenerationService _service;

    public GearGenerationServiceTests()
    {
        _defRepo = new Mock<IGearDefinitionRepository>();
        _affixRepo = new Mock<IAffixRepository>();
        _logger = new Mock<ILogger<GearGenerationService>>();
        _service = new GearGenerationService(_defRepo.Object, _affixRepo.Object, _logger.Object);
    }

    [Fact]
    public async Task GenerateAsync_WithValidDefinition_ShouldGenerateGear()
    {
        // Arrange
        var definition = new GearDefinition
        {
            Id = "sword_iron",
            Name = "Iron Sword",
            Slot = EquipmentSlot.MainHand,
            RequiredLevel = 1,
            BaseStats = new Dictionary<StatType, StatRange>
            {
                { StatType.AttackPower, new StatRange { Min = 10, Max = 15 } }
            },
            RarityWeights = new Dictionary<Rarity, double>
            {
                { Rarity.Common, 1.0 }
            }
        };

        _defRepo.Setup(r => r.GetByIdAsync("sword_iron", It.IsAny<CancellationToken>()))
            .ReturnsAsync(definition);
        
        _affixRepo.Setup(r => r.GetBySlotAsync(EquipmentSlot.MainHand, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Affix>());

        // Act
        var gear = await _service.GenerateAsync("sword_iron", 10, new Random(42));

        // Assert
        Assert.NotNull(gear);
        Assert.Equal("sword_iron", gear.DefinitionId);
        Assert.Equal(Rarity.Common, gear.Rarity);
        Assert.Equal(1, gear.TierLevel);
        Assert.True(gear.ItemLevel >= 10); // Character level + bonus
        Assert.NotEmpty(gear.RolledStats);
        Assert.Contains(StatType.AttackPower, gear.RolledStats.Keys);
    }

    [Fact]
    public async Task GenerateAsync_WithNonexistentDefinition_ShouldThrow()
    {
        // Arrange
        _defRepo.Setup(r => r.GetByIdAsync("nonexistent", It.IsAny<CancellationToken>()))
            .ReturnsAsync((GearDefinition?)null);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.GenerateAsync("nonexistent", 10));
    }
}
