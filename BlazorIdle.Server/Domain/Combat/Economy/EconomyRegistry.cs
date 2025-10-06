using System.Collections.Concurrent;

namespace BlazorIdle.Server.Domain.Economy;

public static class EconomyRegistry
{
    private static readonly ConcurrentDictionary<string, ItemDefinition> _items = new();
    private static readonly ConcurrentDictionary<string, LootTable> _lootTables = new();
    private static readonly ConcurrentDictionary<string, EnemyEconomy> _enemyEco = new();

    static EconomyRegistry()
    {
        // 示例物品
        AddItem(new ItemDefinition("mat_scrap", "Scrap"));
        AddItem(new ItemDefinition("mat_core", "Core"));
        AddItem(new ItemDefinition("gem_small", "Small Gem"));

        // 示例掉落表
        AddLoot(new LootTable("loot_common", new[]
        {
            new LootEntry { ItemId = "mat_scrap", DropChance = 0.50, QuantityMin = 1, QuantityMax = 2 },
            new LootEntry { ItemId = "gem_small", DropChance = 0.05, QuantityMin = 1, QuantityMax = 1 }
        }));

        AddLoot(new LootTable("loot_elite", new[]
        {
            new LootEntry { ItemId = "mat_core", DropChance = 0.25, QuantityMin = 1, QuantityMax = 1 },
            new LootEntry { ItemId = "gem_small", DropChance = 0.10, QuantityMin = 1, QuantityMax = 2 }
        }));

        // 敌人经济（示例，可按需调参）
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

    // 读取全部（可做管理界面）
    public static IReadOnlyDictionary<string, EnemyEconomy> AllEnemyEconomy => _enemyEco;
    public static IReadOnlyDictionary<string, LootTable> AllLootTables => _lootTables;
    public static IReadOnlyDictionary<string, ItemDefinition> AllItems => _items;
}