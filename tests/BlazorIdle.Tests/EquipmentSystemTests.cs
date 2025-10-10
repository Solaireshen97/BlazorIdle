using BlazorIdle.Server.Application.Equipment;
using System;
using System.Collections.Generic;
using Xunit;

namespace BlazorIdle.Tests;

/// <summary>
/// Step 5: 装备系统UI预留 - 数据模型验证测试
/// </summary>
public class EquipmentSystemTests
{
    [Fact]
    public void EquipmentSlotDto_HasCorrectStructure()
    {
        // Arrange & Act
        var slot = new EquipmentSlotDto
        {
            SlotType = "weapon",
            SlotName = "武器",
            Item = null,
            IsLocked = false
        };

        // Assert
        Assert.Equal("weapon", slot.SlotType);
        Assert.Equal("武器", slot.SlotName);
        Assert.Null(slot.Item);
        Assert.False(slot.IsLocked);
    }

    [Fact]
    public void GearInstanceDto_HasCorrectStructure()
    {
        // Arrange & Act
        var gear = new GearInstanceDto
        {
            Id = Guid.NewGuid(),
            DefinitionId = "iron_sword",
            Name = "铁剑",
            Icon = "⚔️",
            Rarity = "Common",
            Tier = 1,
            ItemLevel = 10,
            QualityScore = 50,
            Affixes = new List<AffixDto>(),
            SetId = null,
            Stats = new Dictionary<string, double>
            {
                { "attack", 25.0 },
                { "attackSpeed", 1.5 }
            }
        };

        // Assert
        Assert.NotEqual(Guid.Empty, gear.Id);
        Assert.Equal("iron_sword", gear.DefinitionId);
        Assert.Equal("铁剑", gear.Name);
        Assert.Equal("⚔️", gear.Icon);
        Assert.Equal("Common", gear.Rarity);
        Assert.Equal(1, gear.Tier);
        Assert.Equal(10, gear.ItemLevel);
        Assert.Equal(50, gear.QualityScore);
        Assert.Empty(gear.Affixes);
        Assert.Null(gear.SetId);
        Assert.Equal(2, gear.Stats.Count);
        Assert.Equal(25.0, gear.Stats["attack"]);
    }

    [Fact]
    public void AffixDto_HasCorrectStructure()
    {
        // Arrange & Act
        var affix = new AffixDto
        {
            Id = "crit_rating",
            Name = "暴击强化",
            Description = "暴击率 +5%",
            Value = 5.0
        };

        // Assert
        Assert.Equal("crit_rating", affix.Id);
        Assert.Equal("暴击强化", affix.Name);
        Assert.Equal("暴击率 +5%", affix.Description);
        Assert.Equal(5.0, affix.Value);
    }

    [Fact]
    public void EquipmentResponse_HasCorrectStructure()
    {
        // Arrange
        var characterId = Guid.NewGuid();

        // Act
        var response = new EquipmentResponse
        {
            CharacterId = characterId,
            CharacterName = "测试角色",
            Slots = new List<EquipmentSlotDto>
            {
                new() { SlotType = "weapon", SlotName = "武器", Item = null, IsLocked = false },
                new() { SlotType = "chest", SlotName = "胸甲", Item = null, IsLocked = false }
            },
            TotalStats = new Dictionary<string, double>
            {
                { "attack", 0 },
                { "armor", 0 }
            },
            TotalScore = 0
        };

        // Assert
        Assert.Equal(characterId, response.CharacterId);
        Assert.Equal("测试角色", response.CharacterName);
        Assert.Equal(2, response.Slots.Count);
        Assert.Equal(2, response.TotalStats.Count);
        Assert.Equal(0, response.TotalScore);
    }

    [Fact]
    public void EquipmentOperationResponse_CanIndicateSuccess()
    {
        // Arrange & Act
        var successResponse = new EquipmentOperationResponse
        {
            Success = true,
            ErrorMessage = null,
            Equipment = new EquipmentResponse
            {
                CharacterId = Guid.NewGuid(),
                CharacterName = "测试角色",
                Slots = new List<EquipmentSlotDto>(),
                TotalStats = new Dictionary<string, double>(),
                TotalScore = 0
            }
        };

        var failResponse = new EquipmentOperationResponse
        {
            Success = false,
            ErrorMessage = "装备槽位已锁定",
            Equipment = null
        };

        // Assert
        Assert.True(successResponse.Success);
        Assert.Null(successResponse.ErrorMessage);
        Assert.NotNull(successResponse.Equipment);

        Assert.False(failResponse.Success);
        Assert.Equal("装备槽位已锁定", failResponse.ErrorMessage);
        Assert.Null(failResponse.Equipment);
    }

    [Fact]
    public void EquipmentSlot_CanContainEquippedItem()
    {
        // Arrange
        var gear = new GearInstanceDto
        {
            Id = Guid.NewGuid(),
            DefinitionId = "legendary_sword",
            Name = "传说之剑",
            Icon = "⚔️",
            Rarity = "Legendary",
            Tier = 3,
            ItemLevel = 50,
            QualityScore = 150,
            Affixes = new List<AffixDto>
            {
                new() { Id = "crit", Name = "暴击", Description = "暴击率 +10%", Value = 10 },
                new() { Id = "lifesteal", Name = "吸血", Description = "生命偷取 +5%", Value = 5 }
            },
            SetId = "legendary_set",
            Stats = new Dictionary<string, double>
            {
                { "attack", 100 },
                { "critRate", 10 },
                { "lifesteal", 5 }
            }
        };

        // Act
        var slot = new EquipmentSlotDto
        {
            SlotType = "weapon",
            SlotName = "武器",
            Item = gear,
            IsLocked = false
        };

        // Assert
        Assert.NotNull(slot.Item);
        Assert.Equal("传说之剑", slot.Item!.Name);
        Assert.Equal("Legendary", slot.Item.Rarity);
        Assert.Equal(3, slot.Item.Tier);
        Assert.Equal(2, slot.Item.Affixes.Count);
        Assert.Equal("legendary_set", slot.Item.SetId);
        Assert.Equal(3, slot.Item.Stats.Count);
    }
}
