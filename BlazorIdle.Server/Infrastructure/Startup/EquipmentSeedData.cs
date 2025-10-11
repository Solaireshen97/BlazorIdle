using BlazorIdle.Server.Domain.Equipment.Models;
using BlazorIdle.Server.Domain.Equipment.ValueObjects;
using BlazorIdle.Server.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BlazorIdle.Server.Infrastructure.Startup;

/// <summary>
/// 装备系统种子数据服务
/// 初始化装备定义、词条定义等基础数据
/// </summary>
public static class EquipmentSeedData
{
    /// <summary>
    /// 初始化装备系统种子数据
    /// </summary>
    public static async Task InitializeAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<GameDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<GameDbContext>>();

        try
        {
            // 确保数据库已创建
            await context.Database.EnsureCreatedAsync();

            // 检查是否已有数据
            if (await context.GearDefinitions.AnyAsync())
            {
                logger.LogInformation("Equipment seed data already exists, skipping initialization");
                return;
            }

            logger.LogInformation("Initializing equipment seed data...");

            // 1. 创建词条定义
            await SeedAffixesAsync(context);

            // 2. 创建装备定义
            await SeedGearDefinitionsAsync(context);

            // 3. 创建套装定义
            await SeedGearSetsAsync(context);

            await context.SaveChangesAsync();
            logger.LogInformation("Equipment seed data initialized successfully");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error initializing equipment seed data");
            throw;
        }
    }

    private static async Task SeedAffixesAsync(GameDbContext context)
    {
        var affixes = new List<Affix>
        {
            // 基础属性词条
            new Affix
            {
                Id = "affix_str",
                Name = "力量",
                StatType = StatType.Strength,
                ModifierType = ModifierType.Flat,
                ValueMin = 5,
                ValueMax = 15,
                RarityWeight = 1.0,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new Affix
            {
                Id = "affix_agi",
                Name = "敏捷",
                StatType = StatType.Agility,
                ModifierType = ModifierType.Flat,
                ValueMin = 5,
                ValueMax = 15,
                RarityWeight = 1.0,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new Affix
            {
                Id = "affix_int",
                Name = "智力",
                StatType = StatType.Intellect,
                ModifierType = ModifierType.Flat,
                ValueMin = 5,
                ValueMax = 15,
                RarityWeight = 1.0,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new Affix
            {
                Id = "affix_sta",
                Name = "耐力",
                StatType = StatType.Stamina,
                ModifierType = ModifierType.Flat,
                ValueMin = 8,
                ValueMax = 20,
                RarityWeight = 1.0,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            // 战斗属性词条
            new Affix
            {
                Id = "affix_ap",
                Name = "攻击强度",
                StatType = StatType.AttackPower,
                ModifierType = ModifierType.Flat,
                ValueMin = 10,
                ValueMax = 30,
                RarityWeight = 0.8,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new Affix
            {
                Id = "affix_crit",
                Name = "暴击",
                StatType = StatType.CritRating,
                ModifierType = ModifierType.Flat,
                ValueMin = 5,
                ValueMax = 15,
                RarityWeight = 0.6,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new Affix
            {
                Id = "affix_haste",
                Name = "急速",
                StatType = StatType.Haste,
                ModifierType = ModifierType.Flat,
                ValueMin = 5,
                ValueMax = 15,
                RarityWeight = 0.7,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new Affix
            {
                Id = "affix_armor",
                Name = "护甲",
                StatType = StatType.Armor,
                ModifierType = ModifierType.Flat,
                ValueMin = 20,
                ValueMax = 50,
                RarityWeight = 0.8,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }
        };

        await context.Affixes.AddRangeAsync(affixes);
    }

    private static async Task SeedGearDefinitionsAsync(GameDbContext context)
    {
        var definitions = new List<GearDefinition>
        {
            // 武器
            new GearDefinition
            {
                Id = "sword_iron",
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
                    "affix_str", "affix_ap", "affix_crit", "affix_haste"
                },
                RarityWeights = new Dictionary<Rarity, double>
                {
                    { Rarity.Common, 0.60 },
                    { Rarity.Rare, 0.30 },
                    { Rarity.Epic, 0.08 },
                    { Rarity.Legendary, 0.02 }
                },
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new GearDefinition
            {
                Id = "sword_steel",
                Name = "精钢剑",
                Icon = "⚔️",
                Slot = EquipmentSlot.MainHand,
                ArmorType = ArmorType.None,
                WeaponType = WeaponType.Sword,
                RequiredLevel = 10,
                BaseStats = new Dictionary<StatType, StatRange>
                {
                    { StatType.AttackPower, new StatRange { Min = 20, Max = 30 } }
                },
                AllowedAffixPool = new List<string>
                {
                    "affix_str", "affix_ap", "affix_crit", "affix_haste"
                },
                RarityWeights = new Dictionary<Rarity, double>
                {
                    { Rarity.Common, 0.50 },
                    { Rarity.Rare, 0.35 },
                    { Rarity.Epic, 0.12 },
                    { Rarity.Legendary, 0.03 }
                },
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            // 护甲
            new GearDefinition
            {
                Id = "chest_leather",
                Name = "皮革胸甲",
                Icon = "🛡️",
                Slot = EquipmentSlot.Chest,
                ArmorType = ArmorType.Leather,
                WeaponType = WeaponType.None,
                RequiredLevel = 1,
                BaseStats = new Dictionary<StatType, StatRange>
                {
                    { StatType.Armor, new StatRange { Min = 20, Max = 30 } },
                    { StatType.Stamina, new StatRange { Min = 5, Max = 10 } }
                },
                AllowedAffixPool = new List<string>
                {
                    "affix_agi", "affix_sta", "affix_armor"
                },
                RarityWeights = new Dictionary<Rarity, double>
                {
                    { Rarity.Common, 0.60 },
                    { Rarity.Rare, 0.30 },
                    { Rarity.Epic, 0.08 },
                    { Rarity.Legendary, 0.02 }
                },
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new GearDefinition
            {
                Id = "helm_cloth",
                Name = "布甲头盔",
                Icon = "🎩",
                Slot = EquipmentSlot.Head,
                ArmorType = ArmorType.Cloth,
                WeaponType = WeaponType.None,
                RequiredLevel = 1,
                BaseStats = new Dictionary<StatType, StatRange>
                {
                    { StatType.Armor, new StatRange { Min = 10, Max = 15 } },
                    { StatType.Intellect, new StatRange { Min = 5, Max = 10 } }
                },
                AllowedAffixPool = new List<string>
                {
                    "affix_int", "affix_sta"
                },
                RarityWeights = new Dictionary<Rarity, double>
                {
                    { Rarity.Common, 0.60 },
                    { Rarity.Rare, 0.30 },
                    { Rarity.Epic, 0.08 },
                    { Rarity.Legendary, 0.02 }
                },
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }
        };

        await context.GearDefinitions.AddRangeAsync(definitions);
    }

    private static async Task SeedGearSetsAsync(GameDbContext context)
    {
        var sets = new List<GearSet>
        {
            new GearSet
            {
                Id = "set_warrior",
                Name = "战士套装",
                Pieces = new List<string>
                {
                    "chest_warrior", "helm_warrior", "legs_warrior", "boots_warrior"
                },
                Bonuses = new Dictionary<int, List<StatModifier>>
                {
                    {
                        2, new List<StatModifier>
                        {
                            new StatModifier
                            {
                                StatType = StatType.Strength,
                                ModifierType = ModifierType.Flat,
                                Value = 10
                            }
                        }
                    },
                    {
                        4, new List<StatModifier>
                        {
                            new StatModifier
                            {
                                StatType = StatType.AttackPower,
                                ModifierType = ModifierType.Flat,
                                Value = 50
                            },
                            new StatModifier
                            {
                                StatType = StatType.Stamina,
                                ModifierType = ModifierType.Flat,
                                Value = 20
                            }
                        }
                    }
                },
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }
        };

        await context.GearSets.AddRangeAsync(sets);
    }
}
