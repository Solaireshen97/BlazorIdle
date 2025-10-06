using System;
using System.Collections.Generic;
using System.Linq;

namespace BlazorIdle.Server.Domain.Economy;

public sealed class EconomyValidationIssue
{
    public string Severity { get; init; } = "error"; // "error" | "warning"
    public string Code { get; init; } = "";
    public string Message { get; init; } = "";
}

public static class EconomyValidator
{
    public static IReadOnlyList<EconomyValidationIssue> ValidateAll()
    {
        var issues = new List<EconomyValidationIssue>();

        // 校验物品重复（按 Id）
        var itemDup = EconomyRegistry.AllItems
            .GroupBy(kv => kv.Key, StringComparer.Ordinal)
            .Where(g => g.Count() > 1)
            .ToList();
        foreach (var g in itemDup)
        {
            issues.Add(new EconomyValidationIssue
            {
                Severity = "warning",
                Code = "ITEM_DUPLICATE",
                Message = $"Item '{g.Key}' defined {g.Count()} times."
            });
        }

        // 校验掉落表
        foreach (var (tableId, table) in EconomyRegistry.AllLootTables)
        {
            if (table.Entries is null || table.Entries.Count == 0)
            {
                issues.Add(new EconomyValidationIssue
                {
                    Severity = "warning",
                    Code = "LOOT_EMPTY",
                    Message = $"LootTable '{tableId}' has no entries."
                });
                continue;
            }

            var idx = 0;
            foreach (var e in table.Entries)
            {
                if (!EconomyRegistry.TryGetItem(e.ItemId, out _))
                {
                    issues.Add(new EconomyValidationIssue
                    {
                        Severity = "error",
                        Code = "ITEM_MISSING",
                        Message = $"LootTable '{tableId}' entry[{idx}] references missing ItemId '{e.ItemId}'."
                    });
                }
                if (e.DropChance < 0 || e.DropChance > 1)
                {
                    issues.Add(new EconomyValidationIssue
                    {
                        Severity = "error",
                        Code = "DROP_CHANCE_RANGE",
                        Message = $"LootTable '{tableId}' entry[{idx}] DropChance={e.DropChance} is out of [0,1]."
                    });
                }
                if (e.QuantityMin < 0 || e.QuantityMax < e.QuantityMin)
                {
                    issues.Add(new EconomyValidationIssue
                    {
                        Severity = "error",
                        Code = "QUANTITY_RANGE",
                        Message = $"LootTable '{tableId}' entry[{idx}] QuantityMin={e.QuantityMin}, QuantityMax={e.QuantityMax} invalid."
                    });
                }
                if (e.Rolls < 1)
                {
                    issues.Add(new EconomyValidationIssue
                    {
                        Severity = "error",
                        Code = "ROLLS_RANGE",
                        Message = $"LootTable '{tableId}' entry[{idx}] Rolls={e.Rolls} must be >= 1."
                    });
                }
                idx++;
            }

            // 警告：同一表内 ItemId 重复（允许，但提示）
            var dupItems = table.Entries
                .GroupBy(x => x.ItemId, StringComparer.Ordinal)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();
            foreach (var itemId in dupItems)
            {
                issues.Add(new EconomyValidationIssue
                {
                    Severity = "warning",
                    Code = "LOOT_ITEM_DUP_IN_TABLE",
                    Message = $"LootTable '{tableId}' has duplicate ItemId '{itemId}' entries."
                });
            }
        }

        // 校验敌人经济引用
        foreach (var (enemyId, eco) in EconomyRegistry.AllEnemyEconomy)
        {
            if (eco.Gold < 0 || eco.Exp < 0)
            {
                issues.Add(new EconomyValidationIssue
                {
                    Severity = "error",
                    Code = "ENEMY_NEG_REWARD",
                    Message = $"EnemyEconomy '{enemyId}' has negative Gold/Exp."
                });
            }
            if (eco.LootTableId is not null && !EconomyRegistry.TryGetLootTable(eco.LootTableId, out _))
            {
                issues.Add(new EconomyValidationIssue
                {
                    Severity = "error",
                    Code = "LOOT_TABLE_MISSING",
                    Message = $"EnemyEconomy '{enemyId}' references missing LootTable '{eco.LootTableId}'."
                });
            }
        }

        return issues;
    }
}