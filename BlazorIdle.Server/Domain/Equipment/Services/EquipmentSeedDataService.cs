using BlazorIdle.Server.Application.Abstractions;
using BlazorIdle.Server.Domain.Equipment.Models;
using BlazorIdle.Server.Domain.Equipment.ValueObjects;

namespace BlazorIdle.Server.Domain.Equipment.Services;

/// <summary>
/// 装备系统种子数据服务 - 提供初始的装备定义和词条配置
/// </summary>
public class EquipmentSeedDataService
{
    private readonly IGearDefinitionRepository _gearDefinitionRepo;
    private readonly IAffixRepository _affixRepo;

    public EquipmentSeedDataService(
        IGearDefinitionRepository gearDefinitionRepo,
        IAffixRepository affixRepo)
    {
        _gearDefinitionRepo = gearDefinitionRepo;
        _affixRepo = affixRepo;
    }

    /// <summary>
    /// 初始化种子数据（如果数据库为空）
    /// </summary>
    public async Task SeedDataAsync(CancellationToken ct = default)
    {
        // 检查是否已有数据
        var existingGear = await _gearDefinitionRepo.GetAllAsync(ct);
        if (existingGear.Any())
        {
            return; // 已有数据，跳过初始化
        }

        // 创建词条定义
        await SeedAffixesAsync(ct);

        // 创建装备定义
        await SeedGearDefinitionsAsync(ct);
    }

    private async Task SeedAffixesAsync(CancellationToken ct)
    {
        var affixes = new List<Affix>
        {
            // 力量类词条
            new Affix
            {
                Id = "str_minor",
                Name = "微量力量",
                StatType = StatType.Strength,
                ModifierType = ModifierType.Flat,
                ValueMin = 1,
                ValueMax = 3,
                RarityWeight = 1.0
            },
            new Affix
            {
                Id = "str_moderate",
                Name = "中量力量",
                StatType = StatType.Strength,
                ModifierType = ModifierType.Flat,
                ValueMin = 4,
                ValueMax = 8,
                RarityWeight = 0.5
            },
            
            // 攻击强度词条
            new Affix
            {
                Id = "ap_minor",
                Name = "微量攻击强度",
                StatType = StatType.AttackPower,
                ModifierType = ModifierType.Flat,
                ValueMin = 5,
                ValueMax = 15,
                RarityWeight = 1.0
            },
            new Affix
            {
                Id = "ap_moderate",
                Name = "中量攻击强度",
                StatType = StatType.AttackPower,
                ModifierType = ModifierType.Flat,
                ValueMin = 16,
                ValueMax = 30,
                RarityWeight = 0.5
            },
            
            // 暴击率词条
            new Affix
            {
                Id = "crit_minor",
                Name = "微量暴击",
                StatType = StatType.CritChance,
                ModifierType = ModifierType.Flat,
                ValueMin = 0.02,
                ValueMax = 0.04,
                RarityWeight = 0.8
            },
            
            // 急速词条
            new Affix
            {
                Id = "haste_minor",
                Name = "微量急速",
                StatType = StatType.Haste,
                ModifierType = ModifierType.Flat,
                ValueMin = 0.03,
                ValueMax = 0.06,
                RarityWeight = 0.8
            }
        };

        foreach (var affix in affixes)
        {
            await _affixRepo.CreateAsync(affix, ct);
        }
    }

    private async Task SeedGearDefinitionsAsync(CancellationToken ct)
    {
        var gearDefinitions = new List<GearDefinition>
        {
            // 战士铁剑
            new GearDefinition
            {
                Id = "sword_iron",
                Name = "铁剑",
                Icon = "⚔️",
                Slot = EquipmentSlot.MainHand,
                ArmorType = ArmorType.None,
                WeaponType = WeaponType.Sword,
                RequiredLevel = 1,
                BaseAttackSpeed = 2.6,
                BaseDamageMin = 10,
                BaseDamageMax = 18,
                BaseStats = new Dictionary<StatType, StatRange>
                {
                    { StatType.AttackPower, new StatRange(5, 10) }
                },
                AllowedAffixPool = new List<string> { "str_minor", "ap_minor", "crit_minor", "haste_minor" },
                RarityWeights = new Dictionary<Rarity, double>
                {
                    { Rarity.Common, 0.70 },
                    { Rarity.Rare, 0.25 },
                    { Rarity.Epic, 0.05 }
                }
            },
            
            // 布甲头盔
            new GearDefinition
            {
                Id = "cloth_hood",
                Name = "布质兜帽",
                Icon = "🪖",
                Slot = EquipmentSlot.Head,
                ArmorType = ArmorType.Cloth,
                WeaponType = WeaponType.None,
                RequiredLevel = 1,
                BaseArmor = 10,
                BaseStats = new Dictionary<StatType, StatRange>
                {
                    { StatType.Intellect, new StatRange(2, 5) }
                },
                AllowedAffixPool = new List<string> { "ap_minor", "crit_minor" },
                RarityWeights = new Dictionary<Rarity, double>
                {
                    { Rarity.Common, 0.70 },
                    { Rarity.Rare, 0.30 }
                }
            },
            
            // 皮甲胸甲
            new GearDefinition
            {
                Id = "leather_chest",
                Name = "皮革胸甲",
                Icon = "🛡️",
                Slot = EquipmentSlot.Chest,
                ArmorType = ArmorType.Leather,
                WeaponType = WeaponType.None,
                RequiredLevel = 1,
                BaseArmor = 30,
                BaseStats = new Dictionary<StatType, StatRange>
                {
                    { StatType.Agility, new StatRange(3, 7) },
                    { StatType.Stamina, new StatRange(5, 10) }
                },
                AllowedAffixPool = new List<string> { "str_minor", "ap_minor", "haste_minor" },
                RarityWeights = new Dictionary<Rarity, double>
                {
                    { Rarity.Common, 0.60 },
                    { Rarity.Rare, 0.35 },
                    { Rarity.Epic, 0.05 }
                }
            }
        };

        foreach (var gear in gearDefinitions)
        {
            await _gearDefinitionRepo.CreateAsync(gear, ct);
        }
    }
}
