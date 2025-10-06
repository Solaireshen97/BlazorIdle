using BlazorIdle.Server.Domain.Combat.Rng;

namespace BlazorIdle.Server.Domain.Economy;

public static class EconomyCalculator
{
    // 期望值计算：不消耗 RNG，线性相加，稳定可复现
    public static RewardSummary ComputeExpected(IDictionary<string, int> killCounts)
    {
        var sum = new RewardSummary();

        foreach (var (killKey, cnt) in killCounts)
        {
            // 只处理 kill.{enemyId}
            if (!killKey.StartsWith("kill.", StringComparison.Ordinal)) continue;
            var enemyId = killKey.Substring("kill.".Length);
            if (!EconomyRegistry.TryGetEnemyEconomy(enemyId, out var eco)) continue;

            sum.Gold += (long)eco.Gold * cnt;
            sum.Exp += (long)eco.Exp * cnt;

            if (eco.LootTableId is not null && EconomyRegistry.TryGetLootTable(eco.LootTableId, out var table))
            {
                foreach (var e in table.Entries)
                {
                    // 期望件数 = 次数 * 掉落概率 * 平均数量
                    var avgQty = (e.QuantityMin + e.QuantityMax) / 2.0;
                    var expected = cnt * e.DropChance * avgQty;
                    if (!sum.Items.ContainsKey(e.ItemId)) sum.Items[e.ItemId] = 0;
                    sum.Items[e.ItemId] += expected;
                }
            }
        }

        return sum;
    }

    // 可选：采样计算（如需实际掉落，使用经济 RNG：基于种子派生，避免污染战斗 RNG）
    public static RewardSummary ComputeSampled(IDictionary<string, int> killCounts, ulong battleSeed, ulong salt = 0xEC00_1234UL)
    {
        var sum = new RewardSummary();
        var rng = new RngContext(Hash64(battleSeed ^ salt));

        foreach (var (killKey, cnt) in killCounts)
        {
            if (!killKey.StartsWith("kill.", StringComparison.Ordinal)) continue;
            var enemyId = killKey.Substring("kill.".Length);
            if (!EconomyRegistry.TryGetEnemyEconomy(enemyId, out var eco)) continue;

            sum.Gold += (long)eco.Gold * cnt;
            sum.Exp += (long)eco.Exp * cnt;

            if (eco.LootTableId is not null && EconomyRegistry.TryGetLootTable(eco.LootTableId, out var table))
            {
                for (int i = 0; i < cnt; i++)
                {
                    foreach (var e in table.Entries)
                    {
                        if (rng.NextDouble() <= e.DropChance)
                        {
                            var qty = e.QuantityMin == e.QuantityMax
                                ? e.QuantityMin
                                : (int)(e.QuantityMin + rng.NextDouble() * (e.QuantityMax - e.QuantityMin + 1));
                            if (!sum.Items.ContainsKey(e.ItemId)) sum.Items[e.ItemId] = 0;
                            sum.Items[e.ItemId] += qty;
                        }
                    }
                }
            }
        }

        return sum;
    }

    private static ulong Hash64(ulong x)
    {
        // 简易 xorshift64* 派生
        x ^= x >> 12; x ^= x << 25; x ^= x >> 27;
        return x * 2685821657736338717UL;
    }
}