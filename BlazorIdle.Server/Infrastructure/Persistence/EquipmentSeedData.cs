using BlazorIdle.Server.Domain.Equipment.Models;
using BlazorIdle.Server.Domain.Equipment.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace BlazorIdle.Server.Infrastructure.Persistence;

/// <summary>
/// 装备系统种子数据
/// 提供初始装备定义、词条定义和套装定义
/// </summary>
public static class EquipmentSeedData
{
    /// <summary>
    /// 为数据库添加装备系统种子数据
    /// </summary>
    public static void SeedEquipmentData(this ModelBuilder modelBuilder)
    {
        SeedAffixDefinitions(modelBuilder);
        SeedGearDefinitions(modelBuilder);
        SeedGearSets(modelBuilder);
    }

    /// <summary>
    /// 词条定义种子数据
    /// </summary>
    private static void SeedAffixDefinitions(ModelBuilder modelBuilder)
    {
        // 使用静态日期以避免每次构建模型时产生变化
        var now = new DateTime(2025, 10, 11, 0, 0, 0, DateTimeKind.Utc);
        
        var affixes = new List<Affix>
        {
            // 主属性词条
            new Affix 
            { 
                Id = "affix_strength", 
                Name = "力量", 
                StatType = StatType.Strength, 
                ModifierType = ModifierType.Flat,
                ValueMin = 5, 
                ValueMax = 20, 
                RarityWeight = 1.0,
                CreatedAt = now, 
                UpdatedAt = now 
            },
            new Affix 
            { 
                Id = "affix_agility", 
                Name = "敏捷", 
                StatType = StatType.Agility, 
                ModifierType = ModifierType.Flat,
                ValueMin = 5, 
                ValueMax = 20, 
                RarityWeight = 1.0,
                CreatedAt = now, 
                UpdatedAt = now 
            },
            new Affix 
            { 
                Id = "affix_intellect", 
                Name = "智力", 
                StatType = StatType.Intellect, 
                ModifierType = ModifierType.Flat,
                ValueMin = 5, 
                ValueMax = 20, 
                RarityWeight = 1.0,
                CreatedAt = now, 
                UpdatedAt = now 
            },
            new Affix 
            { 
                Id = "affix_stamina", 
                Name = "耐力", 
                StatType = StatType.Stamina, 
                ModifierType = ModifierType.Flat,
                ValueMin = 5, 
                ValueMax = 20, 
                RarityWeight = 1.0,
                CreatedAt = now, 
                UpdatedAt = now 
            },
            
            // 次级属性词条
            new Affix 
            { 
                Id = "affix_attack_power", 
                Name = "攻击强度", 
                StatType = StatType.AttackPower, 
                ModifierType = ModifierType.Flat,
                ValueMin = 10, 
                ValueMax = 50, 
                RarityWeight = 1.0,
                CreatedAt = now, 
                UpdatedAt = now 
            },
            new Affix 
            { 
                Id = "affix_crit_chance", 
                Name = "暴击率", 
                StatType = StatType.CritChance, 
                ModifierType = ModifierType.Percent,
                ValueMin = 0.01, 
                ValueMax = 0.05, 
                RarityWeight = 0.7,
                CreatedAt = now, 
                UpdatedAt = now 
            },
            new Affix 
            { 
                Id = "affix_spell_power", 
                Name = "法术强度", 
                StatType = StatType.SpellPower, 
                ModifierType = ModifierType.Flat,
                ValueMin = 10, 
                ValueMax = 50, 
                RarityWeight = 0.7,
                CreatedAt = now, 
                UpdatedAt = now 
            },
            new Affix 
            { 
                Id = "affix_haste", 
                Name = "急速", 
                StatType = StatType.Haste, 
                ModifierType = ModifierType.Percent,
                ValueMin = 0.02, 
                ValueMax = 0.10, 
                RarityWeight = 0.8,
                CreatedAt = now, 
                UpdatedAt = now 
            },
            new Affix 
            { 
                Id = "affix_armor", 
                Name = "护甲", 
                StatType = StatType.Armor, 
                ModifierType = ModifierType.Flat,
                ValueMin = 20, 
                ValueMax = 100, 
                RarityWeight = 1.0,
                CreatedAt = now, 
                UpdatedAt = now 
            },
            new Affix 
            { 
                Id = "affix_mastery", 
                Name = "精通等级", 
                StatType = StatType.MasteryRating, 
                ModifierType = ModifierType.Flat,
                ValueMin = 10, 
                ValueMax = 50, 
                RarityWeight = 0.8,
                CreatedAt = now, 
                UpdatedAt = now 
            },
            new Affix 
            { 
                Id = "affix_health", 
                Name = "生命值", 
                StatType = StatType.Health, 
                ModifierType = ModifierType.Flat,
                ValueMin = 50, 
                ValueMax = 200, 
                RarityWeight = 1.0,
                CreatedAt = now, 
                UpdatedAt = now 
            },
            new Affix 
            { 
                Id = "affix_dodge", 
                Name = "闪避等级", 
                StatType = StatType.DodgeRating, 
                ModifierType = ModifierType.Flat,
                ValueMin = 5, 
                ValueMax = 30, 
                RarityWeight = 0.9,
                CreatedAt = now, 
                UpdatedAt = now 
            }
        };

        modelBuilder.Entity<Affix>().HasData(affixes);
    }

    /// <summary>
    /// 装备定义种子数据
    /// </summary>
    private static void SeedGearDefinitions(ModelBuilder modelBuilder)
    {
        // 使用静态日期以避免每次构建模型时产生变化
        var now = new DateTime(2025, 10, 11, 0, 0, 0, DateTimeKind.Utc);
        
        var gearDefinitions = new List<GearDefinition>
        {
            // 武器 - 主手剑
            new GearDefinition
            {
                Id = "weapon_iron_sword",
                Name = "铁剑",
                Icon = "⚔️",
                Slot = EquipmentSlot.MainHand,
                ArmorType = ArmorType.None,
                WeaponType = WeaponType.Sword,
                RequiredLevel = 1,
                BaseStats = new Dictionary<StatType, StatRange>
                {
                    { StatType.AttackPower, new StatRange { Min = 10, Max = 15 } }
                },
                AllowedAffixPool = new List<string> 
                { 
                    "affix_attack_power", "affix_crit_chance", "affix_strength", "affix_agility" 
                },
                RarityWeights = new Dictionary<Rarity, double>
                {
                    { Rarity.Common, 50.0 },
                    { Rarity.Rare, 30.0 },
                    { Rarity.Epic, 15.0 },
                    { Rarity.Legendary, 5.0 }
                },
                SetId = null,
                CreatedAt = now,
                UpdatedAt = now
            },
            
            // 盾牌
            new GearDefinition
            {
                Id = "shield_iron",
                Name = "铁盾",
                Icon = "🛡️",
                Slot = EquipmentSlot.OffHand,
                ArmorType = ArmorType.None,
                WeaponType = WeaponType.Shield,
                RequiredLevel = 1,
                BaseStats = new Dictionary<StatType, StatRange>
                {
                    { StatType.Armor, new StatRange { Min = 30, Max = 50 } },
                    { StatType.Stamina, new StatRange { Min = 5, Max = 10 } }
                },
                AllowedAffixPool = new List<string> 
                { 
                    "affix_armor", "affix_stamina", "affix_health" 
                },
                RarityWeights = new Dictionary<Rarity, double>
                {
                    { Rarity.Common, 50.0 },
                    { Rarity.Rare, 30.0 },
                    { Rarity.Epic, 15.0 },
                    { Rarity.Legendary, 5.0 }
                },
                SetId = null,
                CreatedAt = now,
                UpdatedAt = now
            },
            
            // 头盔 - 布甲
            new GearDefinition
            {
                Id = "helm_cloth_basic",
                Name = "布甲头盔",
                Icon = "🪖",
                Slot = EquipmentSlot.Head,
                ArmorType = ArmorType.Cloth,
                WeaponType = WeaponType.None,
                RequiredLevel = 1,
                BaseStats = new Dictionary<StatType, StatRange>
                {
                    { StatType.Armor, new StatRange { Min = 5, Max = 10 } },
                    { StatType.Intellect, new StatRange { Min = 3, Max = 8 } }
                },
                AllowedAffixPool = new List<string> 
                { 
                    "affix_intellect", "affix_stamina", "affix_armor" 
                },
                RarityWeights = new Dictionary<Rarity, double>
                {
                    { Rarity.Common, 50.0 },
                    { Rarity.Rare, 30.0 },
                    { Rarity.Epic, 15.0 },
                    { Rarity.Legendary, 5.0 }
                },
                SetId = "set_mage_basic",
                CreatedAt = now,
                UpdatedAt = now
            },
            
            // 胸甲 - 板甲
            new GearDefinition
            {
                Id = "chest_plate_basic",
                Name = "板甲胸甲",
                Icon = "🧥",
                Slot = EquipmentSlot.Chest,
                ArmorType = ArmorType.Plate,
                WeaponType = WeaponType.None,
                RequiredLevel = 1,
                BaseStats = new Dictionary<StatType, StatRange>
                {
                    { StatType.Armor, new StatRange { Min = 40, Max = 60 } },
                    { StatType.Strength, new StatRange { Min = 5, Max = 15 } }
                },
                AllowedAffixPool = new List<string> 
                { 
                    "affix_strength", "affix_stamina", "affix_armor", "affix_health" 
                },
                RarityWeights = new Dictionary<Rarity, double>
                {
                    { Rarity.Common, 50.0 },
                    { Rarity.Rare, 30.0 },
                    { Rarity.Epic, 15.0 },
                    { Rarity.Legendary, 5.0 }
                },
                SetId = "set_warrior_basic",
                CreatedAt = now,
                UpdatedAt = now
            },
            
            // 腰带
            new GearDefinition
            {
                Id = "belt_leather",
                Name = "皮革腰带",
                Icon = "⚖️",
                Slot = EquipmentSlot.Waist,
                ArmorType = ArmorType.Leather,
                WeaponType = WeaponType.None,
                RequiredLevel = 1,
                BaseStats = new Dictionary<StatType, StatRange>
                {
                    { StatType.Armor, new StatRange { Min = 15, Max = 25 } },
                    { StatType.Agility, new StatRange { Min = 3, Max = 8 } }
                },
                AllowedAffixPool = new List<string> 
                { 
                    "affix_agility", "affix_stamina", "affix_armor" 
                },
                RarityWeights = new Dictionary<Rarity, double>
                {
                    { Rarity.Common, 50.0 },
                    { Rarity.Rare, 30.0 },
                    { Rarity.Epic, 15.0 },
                    { Rarity.Legendary, 5.0 }
                },
                SetId = null,
                CreatedAt = now,
                UpdatedAt = now
            },
            
            // 戒指
            new GearDefinition
            {
                Id = "ring_basic",
                Name = "普通戒指",
                Icon = "💍",
                Slot = EquipmentSlot.Finger1,
                ArmorType = ArmorType.None,
                WeaponType = WeaponType.None,
                RequiredLevel = 1,
                BaseStats = new Dictionary<StatType, StatRange>
                {
                    { StatType.AttackPower, new StatRange { Min = 5, Max = 10 } }
                },
                AllowedAffixPool = new List<string> 
                { 
                    "affix_attack_power", "affix_crit_chance", "affix_haste" 
                },
                RarityWeights = new Dictionary<Rarity, double>
                {
                    { Rarity.Common, 40.0 },
                    { Rarity.Rare, 35.0 },
                    { Rarity.Epic, 20.0 },
                    { Rarity.Legendary, 5.0 }
                },
                SetId = null,
                CreatedAt = now,
                UpdatedAt = now
            }
        };

        modelBuilder.Entity<GearDefinition>().HasData(gearDefinitions);
    }

    /// <summary>
    /// 套装定义种子数据
    /// </summary>
    private static void SeedGearSets(ModelBuilder modelBuilder)
    {
        // 使用静态日期以避免每次构建模型时产生变化
        var now = new DateTime(2025, 10, 11, 0, 0, 0, DateTimeKind.Utc);
        
        var gearSets = new List<GearSet>
        {
            new GearSet
            {
                Id = "set_warrior_basic",
                Name = "战士基础套装",
                Pieces = new List<string> 
                { 
                    "chest_plate_basic" 
                    // 可以添加更多套装件
                },
                Bonuses = new Dictionary<int, List<StatModifier>>
                {
                    { 2, new List<StatModifier> 
                        { 
                            new StatModifier(StatType.Strength, ModifierType.Flat, 10) 
                        } 
                    },
                    { 4, new List<StatModifier> 
                        { 
                            new StatModifier(StatType.AttackPower, ModifierType.Flat, 30),
                            new StatModifier(StatType.Health, ModifierType.Flat, 100)
                        } 
                    }
                },
                CreatedAt = now,
                UpdatedAt = now
            },
            
            new GearSet
            {
                Id = "set_mage_basic",
                Name = "法师基础套装",
                Pieces = new List<string> 
                { 
                    "helm_cloth_basic" 
                },
                Bonuses = new Dictionary<int, List<StatModifier>>
                {
                    { 2, new List<StatModifier> 
                        { 
                            new StatModifier(StatType.Intellect, ModifierType.Flat, 10) 
                        } 
                    },
                    { 4, new List<StatModifier> 
                        { 
                            new StatModifier(StatType.AttackPower, ModifierType.Flat, 25),
                            new StatModifier(StatType.CritChance, ModifierType.Percent, 0.03)
                        } 
                    }
                },
                CreatedAt = now,
                UpdatedAt = now
            }
        };

        modelBuilder.Entity<GearSet>().HasData(gearSets);
    }
}
