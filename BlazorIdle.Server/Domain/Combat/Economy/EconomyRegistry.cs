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

        // 装备物品（使用 gear: 前缀标识）
        AddItem(new ItemDefinition("gear:iron_sword", "Iron Sword (Gear)"));
        AddItem(new ItemDefinition("gear:iron_dagger", "Iron Dagger (Gear)"));
        AddItem(new ItemDefinition("gear:cloth_robe", "Cloth Robe (Gear)"));
        AddItem(new ItemDefinition("gear:leather_vest", "Leather Vest (Gear)"));

        // 示例掉落表（演示 Rolls 字段，默认 1，不影响旧数据）
        AddLoot(new LootTable("loot_common", new[]
        {
            new LootEntry { ItemId = "mat_scrap", DropChance = 0.50, QuantityMin = 1, QuantityMax = 2, Rolls = 1 },
            new LootEntry { ItemId = "gem_small",  DropChance = 0.05, QuantityMin = 1, QuantityMax = 1, Rolls = 1 },
            new LootEntry { ItemId = "gear:iron_sword", DropChance = 0.08, QuantityMin = 1, QuantityMax = 1, Rolls = 1 }
        }));

        AddLoot(new LootTable("loot_elite", new[]
        {
            new LootEntry { ItemId = "mat_core",  DropChance = 0.25, QuantityMin = 1, QuantityMax = 1, Rolls = 1 },
            new LootEntry { ItemId = "gem_small", DropChance = 0.10, QuantityMin = 1, QuantityMax = 2, Rolls = 2 }, // 精英多 roll 例子
            new LootEntry { ItemId = "gear:cloth_robe", DropChance = 0.15, QuantityMin = 1, QuantityMax = 1, Rolls = 1 },
            new LootEntry { ItemId = "gear:leather_vest", DropChance = 0.12, QuantityMin = 1, QuantityMax = 1, Rolls = 1 }
        }));

        // 敌人经济（示例）
        AddEnemy(new EnemyEconomy(enemyId: "paper", gold: 3, exp: 2, lootTableId: "loot_common"));
        AddEnemy(new EnemyEconomy(enemyId: "dummy", gold: 1, exp: 1, lootTableId: "loot_common"));
        AddEnemy(new EnemyEconomy(enemyId: "tank", gold: 15, exp: 10, lootTableId: "loot_elite"));
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