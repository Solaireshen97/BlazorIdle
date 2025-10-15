using BlazorIdle.Server.Domain.Characters;
using BlazorIdle.Server.Domain.Equipment.Models;
using BlazorIdle.Server.Domain.Equipment.Services;
using BlazorIdle.Server.Infrastructure.Persistence;
using BlazorIdle.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BlazorIdle.Tests.Equipment;

/// <summary>
/// 职业装备限制集成测试
/// 验证职业-装备兼容性验证在实际装备操作中正确生效
/// </summary>
public class ProfessionRestrictionIntegrationTests : IDisposable
{
    private readonly GameDbContext _context;
    private readonly EquipmentService _equipmentService;
    private readonly EquipmentValidator _validator;

    public ProfessionRestrictionIntegrationTests()
    {
        var options = new DbContextOptionsBuilder<GameDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new GameDbContext(options);
        _validator = new EquipmentValidator();
        var logger = NullLogger<EquipmentService>.Instance;
        _equipmentService = new EquipmentService(_context, _validator, logger);
    }

    [Fact]
    public async Task Warrior_CanEquip_PlateArmor()
    {
        // Arrange
        var characterId = Guid.NewGuid();
        await CreateCharacterAsync(characterId, Profession.Warrior, level: 10);
        
        var (definition, gear) = CreateGear(
            characterId, 
            EquipmentSlot.Chest, 
            ArmorType.Plate, 
            requiredLevel: 1
        );
        
        await _context.Set<GearDefinition>().AddAsync(definition);
        await _context.Set<GearInstance>().AddAsync(gear);
        await _context.SaveChangesAsync();

        // Act
        var result = await _equipmentService.EquipAsync(characterId, gear.Id);

        // Assert
        Assert.True(result.IsSuccess, $"战士应该能装备板甲。错误: {result.Message}");
        
        var updatedGear = await _context.Set<GearInstance>().FindAsync(gear.Id);
        Assert.True(updatedGear?.IsEquipped);
    }

    [Fact]
    public async Task Ranger_CannotEquip_PlateArmor()
    {
        // Arrange
        var characterId = Guid.NewGuid();
        await CreateCharacterAsync(characterId, Profession.Ranger, level: 10);
        
        var (definition, gear) = CreateGear(
            characterId, 
            EquipmentSlot.Chest, 
            ArmorType.Plate, 
            requiredLevel: 1
        );
        
        await _context.Set<GearDefinition>().AddAsync(definition);
        await _context.Set<GearInstance>().AddAsync(gear);
        await _context.SaveChangesAsync();

        // Act
        var result = await _equipmentService.EquipAsync(characterId, gear.Id);

        // Assert
        Assert.False(result.IsSuccess, "游侠不应该能装备板甲");
        Assert.Contains("无法装备", result.Message);
        
        var updatedGear = await _context.Set<GearInstance>().FindAsync(gear.Id);
        Assert.False(updatedGear?.IsEquipped ?? false);
    }

    [Fact]
    public async Task Ranger_CanEquip_MailArmor()
    {
        // Arrange
        var characterId = Guid.NewGuid();
        await CreateCharacterAsync(characterId, Profession.Ranger, level: 10);
        
        var (definition, gear) = CreateGear(
            characterId, 
            EquipmentSlot.Chest, 
            ArmorType.Mail, 
            requiredLevel: 1
        );
        
        await _context.Set<GearDefinition>().AddAsync(definition);
        await _context.Set<GearInstance>().AddAsync(gear);
        await _context.SaveChangesAsync();

        // Act
        var result = await _equipmentService.EquipAsync(characterId, gear.Id);

        // Assert
        Assert.True(result.IsSuccess, $"游侠应该能装备锁甲。错误: {result.Message}");
        
        var updatedGear = await _context.Set<GearInstance>().FindAsync(gear.Id);
        Assert.True(updatedGear?.IsEquipped);
    }

    [Fact]
    public async Task Warrior_CanEquip_Sword()
    {
        // Arrange
        var characterId = Guid.NewGuid();
        await CreateCharacterAsync(characterId, Profession.Warrior, level: 10);
        
        var (definition, gear) = CreateGear(
            characterId, 
            EquipmentSlot.MainHand, 
            ArmorType.None,
            weaponType: WeaponType.Sword,
            requiredLevel: 1
        );
        
        await _context.Set<GearDefinition>().AddAsync(definition);
        await _context.Set<GearInstance>().AddAsync(gear);
        await _context.SaveChangesAsync();

        // Act
        var result = await _equipmentService.EquipAsync(characterId, gear.Id);

        // Assert
        Assert.True(result.IsSuccess, $"战士应该能装备剑。错误: {result.Message}");
    }

    [Fact]
    public async Task Warrior_CanEquip_Shield()
    {
        // Arrange
        var characterId = Guid.NewGuid();
        await CreateCharacterAsync(characterId, Profession.Warrior, level: 10);
        
        var (definition, gear) = CreateGear(
            characterId, 
            EquipmentSlot.OffHand, 
            ArmorType.None,
            weaponType: WeaponType.Shield,
            requiredLevel: 1
        );
        
        await _context.Set<GearDefinition>().AddAsync(definition);
        await _context.Set<GearInstance>().AddAsync(gear);
        await _context.SaveChangesAsync();

        // Act
        var result = await _equipmentService.EquipAsync(characterId, gear.Id);

        // Assert
        Assert.True(result.IsSuccess, $"战士应该能装备盾牌。错误: {result.Message}");
    }

    [Fact]
    public async Task Ranger_CannotEquip_Shield()
    {
        // Arrange
        var characterId = Guid.NewGuid();
        await CreateCharacterAsync(characterId, Profession.Ranger, level: 10);
        
        var (definition, gear) = CreateGear(
            characterId, 
            EquipmentSlot.OffHand, 
            ArmorType.None,
            weaponType: WeaponType.Shield,
            requiredLevel: 1
        );
        
        await _context.Set<GearDefinition>().AddAsync(definition);
        await _context.Set<GearInstance>().AddAsync(gear);
        await _context.SaveChangesAsync();

        // Act
        var result = await _equipmentService.EquipAsync(characterId, gear.Id);

        // Assert
        Assert.False(result.IsSuccess, "游侠不应该能装备盾牌");
        Assert.Contains("无法装备", result.Message);
    }

    [Fact]
    public async Task Character_CannotEquip_HighLevelGear()
    {
        // Arrange
        var characterId = Guid.NewGuid();
        await CreateCharacterAsync(characterId, Profession.Warrior, level: 5);
        
        var (definition, gear) = CreateGear(
            characterId, 
            EquipmentSlot.Chest, 
            ArmorType.Plate, 
            requiredLevel: 10
        );
        
        await _context.Set<GearDefinition>().AddAsync(definition);
        await _context.Set<GearInstance>().AddAsync(gear);
        await _context.SaveChangesAsync();

        // Act
        var result = await _equipmentService.EquipAsync(characterId, gear.Id);

        // Assert
        Assert.False(result.IsSuccess, "角色不应该能装备高等级装备");
        Assert.Contains("等级", result.Message);
    }

    [Fact]
    public async Task Character_CanEquip_SameLevelGear()
    {
        // Arrange
        var characterId = Guid.NewGuid();
        await CreateCharacterAsync(characterId, Profession.Warrior, level: 10);
        
        var (definition, gear) = CreateGear(
            characterId, 
            EquipmentSlot.Chest, 
            ArmorType.Plate, 
            requiredLevel: 10
        );
        
        await _context.Set<GearDefinition>().AddAsync(definition);
        await _context.Set<GearInstance>().AddAsync(gear);
        await _context.SaveChangesAsync();

        // Act
        var result = await _equipmentService.EquipAsync(characterId, gear.Id);

        // Assert
        Assert.True(result.IsSuccess, "角色应该能装备同等级装备");
    }

    private async Task CreateCharacterAsync(Guid characterId, Profession profession, int level)
    {
        var character = new Character
        {
            Id = characterId,
            Name = $"Test{profession}",
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

    private (GearDefinition, GearInstance) CreateGear(
        Guid characterId, 
        EquipmentSlot slot, 
        ArmorType armorType, 
        WeaponType weaponType = WeaponType.None,
        int requiredLevel = 1)
    {
        var definition = new GearDefinition
        {
            Id = $"test_{slot}_{Guid.NewGuid()}",
            Name = $"测试装备 {slot}",
            Slot = slot,
            ArmorType = armorType,
            WeaponType = weaponType,
            RequiredLevel = requiredLevel
        };

        var gear = new GearInstance
        {
            Id = Guid.NewGuid(),
            DefinitionId = definition.Id,
            CharacterId = characterId,
            Rarity = Rarity.Common,
            TierLevel = 1,
            ItemLevel = requiredLevel,
            IsEquipped = false,
            Definition = definition
        };

        return (definition, gear);
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
