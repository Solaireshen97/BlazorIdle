using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace BlazorIdle.Server.Domain.Economy;

public static class EconomyRegistry
{
    private static readonly ConcurrentDictionary<string, ItemDefinition> _items = new(StringComparer.Ordinal);
    private static readonly ConcurrentDictionary<string, LootTable> _lootTables = new(StringComparer.Ordinal);
    private static readonly ConcurrentDictionary<string, EnemyEconomy> _enemyEco = new(StringComparer.Ordinal);

    static EconomyRegistry()
    {
        // 示例物品
        AddItem(new ItemDefinition("mat_scrap", "Scrap"));
        AddItem(new ItemDefinition("mat_core", "Core"));
        AddItem(new ItemDefinition("gem_small", "Small Gem"));

        // 示例掉落表（演示 Rolls 字段，默认 1，不影响旧数据）
        AddLoot(new LootTable("loot_common", new[]
        {
            new LootEntry { ItemId = "mat_scrap", DropChance = 0.50, QuantityMin = 1, QuantityMax = 2, Rolls = 1 },
            new LootEntry { ItemId = "gem_small",  DropChance = 0.05, QuantityMin = 1, QuantityMax = 1, Rolls = 1 }
        }));

        AddLoot(new LootTable("loot_elite", new[]
        {
            new LootEntry { ItemId = "mat_core",  DropChance = 0.25, QuantityMin = 1, QuantityMax = 1, Rolls = 1 },
            new LootEntry { ItemId = "gem_small", DropChance = 0.10, QuantityMin = 1, QuantityMax = 2, Rolls = 2 } // 精英多 roll 例子
        }));

        // 装备掉落表
        RegisterGearLootTables();

        // 敌人经济（示例）
        AddEnemy(new EnemyEconomy(enemyId: "paper", gold: 3, exp: 2, lootTableId: "loot_common"));
        AddEnemy(new EnemyEconomy(enemyId: "dummy", gold: 1, exp: 1, lootTableId: "loot_common"));
        AddEnemy(new EnemyEconomy(enemyId: "tank", gold: 15, exp: 10, lootTableId: "loot_elite"));
    }
    
    /// <summary>
    /// 注册装备掉落表
    /// </summary>
    private static void RegisterGearLootTables()
    {
        // 普通怪物装备掉落表（低稀有度装备）
        AddLoot(new LootTable("loot_gear_common", new[]
        {
            new LootEntry 
            { 
                ItemId = "weapon_iron_sword",
                ItemType = ItemType.Gear,
                GearDefinitionId = "weapon_iron_sword",
                DropChance = 0.08,
                QuantityMin = 1,
                QuantityMax = 1,
                Rolls = 1,
                Note = "普通怪物-铁剑"
            },
            new LootEntry 
            { 
                ItemId = "weapon_dagger",
                ItemType = ItemType.Gear,
                GearDefinitionId = "weapon_dagger",
                DropChance = 0.08,
                QuantityMin = 1,
                QuantityMax = 1,
                Rolls = 1,
                Note = "普通怪物-匕首"
            },
            new LootEntry 
            { 
                ItemId = "shield_iron",
                ItemType = ItemType.Gear,
                GearDefinitionId = "shield_iron",
                DropChance = 0.05,
                QuantityMin = 1,
                QuantityMax = 1,
                Rolls = 1,
                Note = "普通怪物-铁盾"
            },
            new LootEntry 
            { 
                ItemId = "belt_leather",
                ItemType = ItemType.Gear,
                GearDefinitionId = "belt_leather",
                DropChance = 0.10,
                QuantityMin = 1,
                QuantityMax = 1,
                Rolls = 1,
                Note = "普通怪物-皮腰带"
            },
            new LootEntry 
            { 
                ItemId = "ring_basic",
                ItemType = ItemType.Gear,
                GearDefinitionId = "ring_basic",
                DropChance = 0.06,
                QuantityMin = 1,
                QuantityMax = 1,
                Rolls = 1,
                Note = "普通怪物-戒指"
            }
        }));
        
        // 精英怪物装备掉落表（中等稀有度装备）
        AddLoot(new LootTable("loot_gear_elite", new[]
        {
            new LootEntry 
            { 
                ItemId = "helm_cloth_basic",
                ItemType = ItemType.Gear,
                GearDefinitionId = "helm_cloth_basic",
                DropChance = 0.15,
                QuantityMin = 1,
                QuantityMax = 1,
                Rolls = 1,
                Note = "精英怪物-布甲头盔"
            },
            new LootEntry 
            { 
                ItemId = "chest_leather_basic",
                ItemType = ItemType.Gear,
                GearDefinitionId = "chest_leather_basic",
                DropChance = 0.12,
                QuantityMin = 1,
                QuantityMax = 1,
                Rolls = 1,
                Note = "精英怪物-皮甲胸甲"
            },
            new LootEntry 
            { 
                ItemId = "chest_mail_basic",
                ItemType = ItemType.Gear,
                GearDefinitionId = "chest_mail_basic",
                DropChance = 0.12,
                QuantityMin = 1,
                QuantityMax = 1,
                Rolls = 1,
                Note = "精英怪物-锁甲胸甲"
            },
            new LootEntry 
            { 
                ItemId = "neck_basic",
                ItemType = ItemType.Gear,
                GearDefinitionId = "neck_basic",
                DropChance = 0.10,
                QuantityMin = 1,
                QuantityMax = 1,
                Rolls = 1,
                Note = "精英怪物-项链"
            }
        }));
        
        // Boss装备掉落表（高稀有度装备）
        AddLoot(new LootTable("loot_gear_boss", new[]
        {
            new LootEntry 
            { 
                ItemId = "weapon_twohand_sword",
                ItemType = ItemType.Gear,
                GearDefinitionId = "weapon_twohand_sword",
                DropChance = 0.25,
                QuantityMin = 1,
                QuantityMax = 1,
                Rolls = 1,
                Note = "Boss-双手大剑"
            },
            new LootEntry 
            { 
                ItemId = "weapon_staff",
                ItemType = ItemType.Gear,
                GearDefinitionId = "weapon_staff",
                DropChance = 0.25,
                QuantityMin = 1,
                QuantityMax = 1,
                Rolls = 1,
                Note = "Boss-法杖"
            },
            new LootEntry 
            { 
                ItemId = "chest_plate_basic",
                ItemType = ItemType.Gear,
                GearDefinitionId = "chest_plate_basic",
                DropChance = 0.20,
                QuantityMin = 1,
                QuantityMax = 1,
                Rolls = 1,
                Note = "Boss-板甲胸甲"
            },
            new LootEntry 
            { 
                ItemId = "trinket_basic",
                ItemType = ItemType.Gear,
                GearDefinitionId = "trinket_basic",
                DropChance = 0.15,
                QuantityMin = 1,
                QuantityMax = 1,
                Rolls = 1,
                Note = "Boss-饰品"
            }
        }));
    }

    public static void AddItem(ItemDefinition def) => _items[def.Id] = def;
    public static void AddLoot(LootTable table) => _lootTables[table.Id] = table;
    public static void AddEnemy(EnemyEconomy eco) => _enemyEco[eco.EnemyId] = eco;

    public static bool TryGetEnemyEconomy(string enemyId, out EnemyEconomy eco) => _enemyEco.TryGetValue(enemyId, out eco!);
    public static bool TryGetLootTable(string id, out LootTable table) => _lootTables.TryGetValue(id, out table!);
    public static bool TryGetItem(string id, out ItemDefinition item) => _items.TryGetValue(id, out item!);

    public static IReadOnlyDictionary<string, EnemyEconomy> AllEnemyEconomy => _enemyEco;
    public static IReadOnlyDictionary<string, LootTable> AllLootTables => _lootTables;
    public static IReadOnlyDictionary<string, ItemDefinition> AllItems => _items;
}