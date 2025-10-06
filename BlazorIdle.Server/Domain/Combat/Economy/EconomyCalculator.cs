using System;
using System.Collections.Generic;
using BlazorIdle.Server.Domain.Combat.Rng;

namespace BlazorIdle.Server.Domain.Economy;

public static class EconomyCalculator
{
    // 期望值：考虑 Rolls，且不消耗战斗 RNG
    public static RewardSummary ComputeExpected(IDictionary<string, int> killCounts)
    {
        var sum = new RewardSummary();

        foreach (var (tag, cnt) in killCounts)
        {
            if (!tag.StartsWith("kill.", StringComparison.Ordinal)) continue;
            var enemyId = tag["kill.".Length..];
            if (!EconomyRegistry.TryGetEnemyEconomy(enemyId, out var eco)) continue;

            sum.Gold += (long)eco.Gold * cnt;
            sum.Exp += (long)eco.Exp * cnt;

            if (eco.LootTableId is not null && EconomyRegistry.TryGetLootTable(eco.LootTableId, out var table))
            {
                foreach (var e in table.Entries)
                {
                    var rolls = Math.Max(1, e.Rolls);
                    var avgQty = (e.QuantityMin + e.QuantityMax) / 2.0;
                    var expectedPerKill = rolls * e.DropChance * avgQty;
                    if (!sum.Items.ContainsKey(e.ItemId)) sum.Items[e.ItemId] = 0;
                    sum.Items[e.ItemId] += cnt * expectedPerKill;
                }
            }
        }

        return sum;
    }

    // 抽样掉落：考虑 Rolls，用经济专用 RNG（由 battleSeed 派生），不影响战斗 RNG
    public static RewardSummary ComputeSampled(IDictionary<string, int> killCounts, ulong battleSeed, ulong salt = 0xEC00_1234UL)
    {
        var sum = new RewardSummary();
        var rng = new RngContext(Hash64(battleSeed ^ salt));

        foreach (var (tag, cnt) in killCounts)
        {
            if (!tag.StartsWith("kill.", StringComparison.Ordinal)) continue;
            var enemyId = tag["kill.".Length..];
            if (!EconomyRegistry.TryGetEnemyEconomy(enemyId, out var eco)) continue;

            sum.Gold += (long)eco.Gold * cnt;
            sum.Exp += (long)eco.Exp * cnt;

            if (eco.LootTableId is not null && EconomyRegistry.TryGetLootTable(eco.LootTableId, out var table))
            {
                for (int i = 0; i < cnt; i++)
                {
                    foreach (var e in table.Entries)
                    {
                        var rolls = Math.Max(1, e.Rolls);
                        for (int r = 0; r < rolls; r++)
                        {
                            if (rng.NextDouble() <= e.DropChance)
                            {
                                var maxInc = e.QuantityMax - e.QuantityMin + 1;
                                var q = e.QuantityMin + (maxInc <= 1 ? 0 : (int)(rng.NextDouble() * maxInc));
                                if (!sum.Items.ContainsKey(e.ItemId)) sum.Items[e.ItemId] = 0;
                                sum.Items[e.ItemId] += q;
                            }
                        }
                    }
                }
            }
        }

        return sum;
    }

    private static ulong Hash64(ulong x)
    {
        x ^= x >> 12; x ^= x << 25; x ^= x >> 27;
        return x * 2685821657736338717UL;
    }
}