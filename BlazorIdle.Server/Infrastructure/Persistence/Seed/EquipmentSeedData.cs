using BlazorIdle.Server.Domain.Equipment.Models;
using BlazorIdle.Server.Domain.Equipment.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace BlazorIdle.Server.Infrastructure.Persistence.Seed;

/// <summary>
/// 装备系统初始数据种子
/// </summary>
public static class EquipmentSeedData
{
    /// <summary>
    /// 种子装备定义数据
    /// </summary>
    public static async Task SeedEquipmentDefinitionsAsync(GameDbContext context)
    {
        // 检查是否已有数据
        if (await context.GearDefinitions.AnyAsync())
        {
            return; // 已有数据，跳过种子
        }

        var definitions = new List<GearDefinition>
        {
            // 基础武器
            CreateWeaponDefinition("iron_sword", "铁剑", "⚔️", EquipmentSlot.MainHand, WeaponType.Sword, 1),
            CreateWeaponDefinition("iron_dagger", "铁匕首", "🗡️", EquipmentSlot.MainHand, WeaponType.Dagger, 1),
            CreateWeaponDefinition("iron_axe", "铁斧", "🪓", EquipmentSlot.MainHand, WeaponType.Axe, 1),
            CreateWeaponDefinition("wooden_bow", "木弓", "🏹", EquipmentSlot.TwoHand, WeaponType.Bow, 1),
            CreateWeaponDefinition("wooden_staff", "木杖", "🪄", EquipmentSlot.TwoHand, WeaponType.Staff, 1),
            CreateWeaponDefinition("wooden_shield", "木盾", "🛡️", EquipmentSlot.OffHand, WeaponType.Shield, 1),

            // 基础头盔
            CreateArmorDefinition("cloth_hood", "布质兜帽", "🎓", EquipmentSlot.Head, ArmorType.Cloth, 1),
            CreateArmorDefinition("leather_cap", "皮革帽", "🧢", EquipmentSlot.Head, ArmorType.Leather, 1),
            CreateArmorDefinition("iron_helm", "铁盔", "⛑️", EquipmentSlot.Head, ArmorType.Mail, 1),

            // 基础胸甲
            CreateArmorDefinition("cloth_robe", "布袍", "🥼", EquipmentSlot.Chest, ArmorType.Cloth, 1),
            CreateArmorDefinition("leather_vest", "皮革背心", "🦺", EquipmentSlot.Chest, ArmorType.Leather, 1),
            CreateArmorDefinition("iron_breastplate", "铁胸甲", "🛡️", EquipmentSlot.Chest, ArmorType.Mail, 1),

            // 基础腿甲
            CreateArmorDefinition("cloth_pants", "布裤", "👖", EquipmentSlot.Legs, ArmorType.Cloth, 1),
            CreateArmorDefinition("leather_pants", "皮革护腿", "🦵", EquipmentSlot.Legs, ArmorType.Leather, 1),
            CreateArmorDefinition("iron_greaves", "铁护腿", "🦿", EquipmentSlot.Legs, ArmorType.Mail, 1),

            // 基础鞋子
            CreateArmorDefinition("cloth_shoes", "布鞋", "👞", EquipmentSlot.Feet, ArmorType.Cloth, 1),
            CreateArmorDefinition("leather_boots", "皮革靴", "👢", EquipmentSlot.Feet, ArmorType.Leather, 1),
            CreateArmorDefinition("iron_boots", "铁靴", "🥾", EquipmentSlot.Feet, ArmorType.Mail, 1),

            // 基础饰品
            CreateJewelryDefinition("bronze_ring", "青铜戒指", "💍", EquipmentSlot.Finger1, 1),
            CreateJewelryDefinition("bronze_amulet", "青铜护身符", "📿", EquipmentSlot.Neck, 1),
            CreateJewelryDefinition("simple_trinket", "简易饰品", "🔮", EquipmentSlot.Trinket1, 1),
        };

        context.GearDefinitions.AddRange(definitions);
        await context.SaveChangesAsync();
    }

    private static GearDefinition CreateWeaponDefinition(
        string id, string name, string icon, 
        EquipmentSlot slot, WeaponType weaponType, int level)
    {
        var baseAttackPower = level * 10 + 10;
        
        return new GearDefinition
        {
            Id = id,
            Name = name,
            Icon = icon,
            Slot = slot,
            ArmorType = ArmorType.None,
            WeaponType = weaponType,
            RequiredLevel = level,
            BaseStats = new Dictionary<StatType, StatRange>
            {
                { StatType.AttackPower, new StatRange(baseAttackPower * 0.8, baseAttackPower * 1.2) }
            },
            AllowedAffixPool = new List<string> 
            { 
                "affix_crit", "affix_haste", "affix_strength", "affix_agility" 
            },
            RarityWeights = new Dictionary<Rarity, double>
            {
                { Rarity.Common, 50.0 },
                { Rarity.Rare, 30.0 },
                { Rarity.Epic, 15.0 },
                { Rarity.Legendary, 5.0 }
            },
            TierMultipliers = new Dictionary<int, double>
            {
                { 1, 0.8 },
                { 2, 1.0 },
                { 3, 1.2 }
            }
        };
    }

    private static GearDefinition CreateArmorDefinition(
        string id, string name, string icon,
        EquipmentSlot slot, ArmorType armorType, int level)
    {
        var baseArmor = level * 5 + 5;
        var baseStamina = level * 2 + 2;

        return new GearDefinition
        {
            Id = id,
            Name = name,
            Icon = icon,
            Slot = slot,
            ArmorType = armorType,
            WeaponType = WeaponType.None,
            RequiredLevel = level,
            BaseStats = new Dictionary<StatType, StatRange>
            {
                { StatType.Armor, new StatRange(baseArmor * 0.8, baseArmor * 1.2) },
                { StatType.Stamina, new StatRange(baseStamina * 0.8, baseStamina * 1.2) }
            },
            AllowedAffixPool = new List<string> 
            { 
                "affix_armor", "affix_stamina", "affix_strength", "affix_intellect" 
            },
            RarityWeights = new Dictionary<Rarity, double>
            {
                { Rarity.Common, 50.0 },
                { Rarity.Rare, 30.0 },
                { Rarity.Epic, 15.0 },
                { Rarity.Legendary, 5.0 }
            },
            TierMultipliers = new Dictionary<int, double>
            {
                { 1, 0.8 },
                { 2, 1.0 },
                { 3, 1.2 }
            }
        };
    }

    private static GearDefinition CreateJewelryDefinition(
        string id, string name, string icon,
        EquipmentSlot slot, int level)
    {
        var baseStat = level * 3 + 3;

        return new GearDefinition
        {
            Id = id,
            Name = name,
            Icon = icon,
            Slot = slot,
            ArmorType = ArmorType.None,
            WeaponType = WeaponType.None,
            RequiredLevel = level,
            BaseStats = new Dictionary<StatType, StatRange>
            {
                { StatType.Strength, new StatRange(baseStat * 0.8, baseStat * 1.2) },
                { StatType.Agility, new StatRange(baseStat * 0.8, baseStat * 1.2) },
                { StatType.Intellect, new StatRange(baseStat * 0.8, baseStat * 1.2) }
            },
            AllowedAffixPool = new List<string> 
            { 
                "affix_crit", "affix_haste", "affix_all_stats" 
            },
            RarityWeights = new Dictionary<Rarity, double>
            {
                { Rarity.Common, 40.0 },
                { Rarity.Rare, 35.0 },
                { Rarity.Epic, 20.0 },
                { Rarity.Legendary, 5.0 }
            },
            TierMultipliers = new Dictionary<int, double>
            {
                { 1, 0.8 },
                { 2, 1.0 },
                { 3, 1.2 }
            }
        };
    }

    /// <summary>
    /// 种子词条定义数据
    /// </summary>
    public static async Task SeedAffixDefinitionsAsync(GameDbContext context)
    {
        // 检查是否已有数据
        if (await context.Affixes.AnyAsync())
        {
            return;
        }

        var affixes = new List<Affix>
        {
            new Affix
            {
                Id = "affix_crit",
                Name = "暴击",
                StatType = StatType.CritRating,
                ModifierType = ModifierType.Flat,
                ValueMin = 5,
                ValueMax = 15
            },
            new Affix
            {
                Id = "affix_haste",
                Name = "急速",
                StatType = StatType.Haste,
                ModifierType = ModifierType.Flat,
                ValueMin = 5,
                ValueMax = 15
            },
            new Affix
            {
                Id = "affix_strength",
                Name = "力量",
                StatType = StatType.Strength,
                ModifierType = ModifierType.Flat,
                ValueMin = 3,
                ValueMax = 10
            },
            new Affix
            {
                Id = "affix_agility",
                Name = "敏捷",
                StatType = StatType.Agility,
                ModifierType = ModifierType.Flat,
                ValueMin = 3,
                ValueMax = 10
            },
            new Affix
            {
                Id = "affix_intellect",
                Name = "智力",
                StatType = StatType.Intellect,
                ModifierType = ModifierType.Flat,
                ValueMin = 3,
                ValueMax = 10
            },
            new Affix
            {
                Id = "affix_stamina",
                Name = "耐力",
                StatType = StatType.Stamina,
                ModifierType = ModifierType.Flat,
                ValueMin = 5,
                ValueMax = 15
            },
            new Affix
            {
                Id = "affix_armor",
                Name = "护甲",
                StatType = StatType.Armor,
                ModifierType = ModifierType.Flat,
                ValueMin = 10,
                ValueMax = 30
            },
            new Affix
            {
                Id = "affix_all_stats",
                Name = "全属性",
                StatType = StatType.Strength, // 代表性属性
                ModifierType = ModifierType.Flat,
                ValueMin = 2,
                ValueMax = 5
            }
        };

        context.Affixes.AddRange(affixes);
        await context.SaveChangesAsync();
    }
}
