using BlazorIdle.Server.Domain.Equipment.Models;
using BlazorIdle.Server.Domain.Equipment.Services;
using BlazorIdle.Server.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BlazorIdle.Tests.Equipment.Services;

/// <summary>
/// 装备管理服务测试
/// </summary>
public class EquipmentServiceTests : IDisposable
{
    private readonly GameDbContext _context;
    private readonly EquipmentService _service;

    public EquipmentServiceTests()
    {
        var options = new DbContextOptionsBuilder<GameDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new GameDbContext(options);
        var validator = new EquipmentValidator();
        _service = new EquipmentService(_context, validator);
    }

    [Fact]
    public async Task EquipAsync_ValidGear_ShouldEquipSuccessfully()
    {
        // Arrange
        var characterId = Guid.NewGuid();
        await CreateTestCharacterAsync(characterId);
        var (definition, gear) = CreateTestGear(characterId, EquipmentSlot.Head);
        
        await _context.Set<GearDefinition>().AddAsync(definition);
        await _context.Set<GearInstance>().AddAsync(gear);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.EquipAsync(characterId, gear.Id);

        // Assert
        Assert.True(result.IsSuccess);
        
        var updatedGear = await _context.Set<GearInstance>().FindAsync(gear.Id);
        Assert.NotNull(updatedGear);
        Assert.True(updatedGear!.IsEquipped);
        Assert.Equal(EquipmentSlot.Head, updatedGear.SlotType);
        Assert.Equal(characterId, updatedGear.CharacterId);
    }

    [Fact]
    public async Task EquipAsync_NonExistentGear_ShouldFail()
    {
        // Arrange
        var characterId = Guid.NewGuid();
        var nonExistentGearId = Guid.NewGuid();

        // Act
        var result = await _service.EquipAsync(characterId, nonExistentGearId);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("不存在", result.Message);
    }

    [Fact]
    public async Task EquipAsync_GearOwnedByOtherCharacter_ShouldFail()
    {
        // Arrange
        var characterId1 = Guid.NewGuid();
        var characterId2 = Guid.NewGuid();
        var (definition, gear) = CreateTestGear(characterId1, EquipmentSlot.Head);
        
        await _context.Set<GearDefinition>().AddAsync(definition);
        await _context.Set<GearInstance>().AddAsync(gear);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.EquipAsync(characterId2, gear.Id);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("其他角色", result.Message);
    }

    [Fact]
    public async Task EquipAsync_AlreadyEquipped_ShouldFail()
    {
        // Arrange
        var characterId = Guid.NewGuid();
        var (definition, gear) = CreateTestGear(characterId, EquipmentSlot.Head);
        gear.IsEquipped = true;
        gear.SlotType = EquipmentSlot.Head;
        
        await _context.Set<GearDefinition>().AddAsync(definition);
        await _context.Set<GearInstance>().AddAsync(gear);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.EquipAsync(characterId, gear.Id);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("已装备", result.Message);
    }

    [Fact]
    public async Task EquipAsync_TwoHandWeapon_ShouldUnequipMainHandAndOffHand()
    {
        // Arrange
        var characterId = Guid.NewGuid();
        await CreateTestCharacterAsync(characterId);
        var (mainHandDef, mainHandGear) = CreateTestGear(characterId, EquipmentSlot.MainHand);
        var (offHandDef, offHandGear) = CreateTestGear(characterId, EquipmentSlot.OffHand);
        var (twoHandDef, twoHandGear) = CreateTestGear(characterId, EquipmentSlot.TwoHand);
        
        // 先装备主手和副手
        mainHandGear.IsEquipped = true;
        mainHandGear.SlotType = EquipmentSlot.MainHand;
        offHandGear.IsEquipped = true;
        offHandGear.SlotType = EquipmentSlot.OffHand;
        
        await _context.Set<GearDefinition>().AddRangeAsync(mainHandDef, offHandDef, twoHandDef);
        await _context.Set<GearInstance>().AddRangeAsync(mainHandGear, offHandGear, twoHandGear);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.EquipAsync(characterId, twoHandGear.Id);

        // Assert
        Assert.True(result.IsSuccess);
        
        var updatedTwoHand = await _context.Set<GearInstance>().FindAsync(twoHandGear.Id);
        Assert.True(updatedTwoHand!.IsEquipped);
        
        var updatedMainHand = await _context.Set<GearInstance>().FindAsync(mainHandGear.Id);
        Assert.False(updatedMainHand!.IsEquipped);
        
        var updatedOffHand = await _context.Set<GearInstance>().FindAsync(offHandGear.Id);
        Assert.False(updatedOffHand!.IsEquipped);
    }

    [Fact]
    public async Task UnequipAsync_EquippedGear_ShouldUnequipSuccessfully()
    {
        // Arrange
        var characterId = Guid.NewGuid();
        var (definition, gear) = CreateTestGear(characterId, EquipmentSlot.Head);
        gear.IsEquipped = true;
        gear.SlotType = EquipmentSlot.Head;
        
        await _context.Set<GearDefinition>().AddAsync(definition);
        await _context.Set<GearInstance>().AddAsync(gear);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.UnequipAsync(characterId, EquipmentSlot.Head);

        // Assert
        Assert.True(result.IsSuccess);
        
        var updatedGear = await _context.Set<GearInstance>().FindAsync(gear.Id);
        Assert.NotNull(updatedGear);
        Assert.False(updatedGear!.IsEquipped);
        Assert.Null(updatedGear.SlotType);
    }

    [Fact]
    public async Task UnequipAsync_EmptySlot_ShouldSucceedWithMessage()
    {
        // Arrange
        var characterId = Guid.NewGuid();

        // Act
        var result = await _service.UnequipAsync(characterId, EquipmentSlot.Head);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Contains("没有装备", result.Message);
    }

    [Fact]
    public async Task GetEquippedGearAsync_ShouldReturnAllEquippedGear()
    {
        // Arrange
        var characterId = Guid.NewGuid();
        var (headDef, headGear) = CreateTestGear(characterId, EquipmentSlot.Head);
        var (chestDef, chestGear) = CreateTestGear(characterId, EquipmentSlot.Chest);
        
        headGear.IsEquipped = true;
        headGear.SlotType = EquipmentSlot.Head;
        chestGear.IsEquipped = true;
        chestGear.SlotType = EquipmentSlot.Chest;
        
        await _context.Set<GearDefinition>().AddRangeAsync(headDef, chestDef);
        await _context.Set<GearInstance>().AddRangeAsync(headGear, chestGear);
        await _context.SaveChangesAsync();

        // Act
        var equipped = await _service.GetEquippedGearAsync(characterId);

        // Assert
        Assert.Equal(2, equipped.Count);
        Assert.Contains(equipped, g => g.SlotType == EquipmentSlot.Head);
        Assert.Contains(equipped, g => g.SlotType == EquipmentSlot.Chest);
    }

    [Fact]
    public async Task GetEquippedGearInSlotAsync_ExistingGear_ShouldReturnGear()
    {
        // Arrange
        var characterId = Guid.NewGuid();
        var (definition, gear) = CreateTestGear(characterId, EquipmentSlot.Head);
        gear.IsEquipped = true;
        gear.SlotType = EquipmentSlot.Head;
        
        await _context.Set<GearDefinition>().AddAsync(definition);
        await _context.Set<GearInstance>().AddAsync(gear);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetEquippedGearInSlotAsync(characterId, EquipmentSlot.Head);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(gear.Id, result!.Id);
    }

    [Fact]
    public async Task GetEquippedGearInSlotAsync_EmptySlot_ShouldReturnNull()
    {
        // Arrange
        var characterId = Guid.NewGuid();

        // Act
        var result = await _service.GetEquippedGearInSlotAsync(characterId, EquipmentSlot.Head);

        // Assert
        Assert.Null(result);
    }

    private (GearDefinition, GearInstance) CreateTestGear(Guid characterId, EquipmentSlot slot)
    {
        var definition = new GearDefinition
        {
            Id = $"test_{slot}_{Guid.NewGuid()}",
            Name = $"测试装备 {slot}",
            Slot = slot,
            ArmorType = ArmorType.Plate,
            WeaponType = WeaponType.None,
            RequiredLevel = 1
        };

        var gear = new GearInstance
        {
            Id = Guid.NewGuid(),
            DefinitionId = definition.Id,
            CharacterId = characterId,
            Rarity = Rarity.Common,
            TierLevel = 1,
            ItemLevel = 10,
            IsEquipped = false,
            Definition = definition
        };

        return (definition, gear);
    }

    private async Task CreateTestCharacterAsync(Guid characterId, BlazorIdle.Shared.Models.Profession profession = BlazorIdle.Shared.Models.Profession.Warrior, int level = 50)
    {
        var character = new BlazorIdle.Server.Domain.Characters.Character
        {
            Id = characterId,
            Name = "TestCharacter",
            Profession = profession,
            Level = level,
            Strength = 10,
            Agility = 10,
            Intellect = 10,
            Stamina = 10
        };

        await _context.Characters.AddAsync(character);
        await _context.SaveChangesAsync();
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
