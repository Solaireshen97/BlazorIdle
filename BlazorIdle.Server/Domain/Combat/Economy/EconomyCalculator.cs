using System;
using System.Collections.Generic;
using BlazorIdle.Server.Domain.Combat.Rng;

namespace BlazorIdle.Server.Domain.Economy;

public sealed class EconomyContext
{
    // 倍率（默认 1）
    public double GoldMultiplier { get; init; } = 1.0;
    public double ExpMultiplier { get; init; } = 1.0;
    public double DropChanceMultiplier { get; init; } = 1.0;

    // 整轮完成奖励（每 run 叠加）
    public int RunCompletedCount { get; init; } = 0;
    public int RunRewardGold { get; init; } = 0;
    public int RunRewardExp { get; init; } = 0;
    public string? RunRewardLootTableId { get; init; }
    public int RunRewardLootRolls { get; init; } = 0;

    // 经济 RNG 用于 sampled
    public ulong? Seed { get; init; }
    public ulong Salt { get; init; } = 0xEC00_1234UL;
}

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

    public static RewardSummary ComputeExpectedWithContext(IDictionary<string, int> killCounts, EconomyContext ctx)
    {
        var baseSum = ComputeExpected(killCounts); // 使用已有期望逻辑（含 Rolls）

        // 奖励倍率（金币/经验直接乘；掉落用“概率倍率”近似为期望件数乘数）
        var sum = new RewardSummary
        {
            Gold = (long)Math.Round(baseSum.Gold * ctx.GoldMultiplier),
            Exp = (long)Math.Round(baseSum.Exp * ctx.ExpMultiplier)
        };

        foreach (var (item, qty) in baseSum.Items)
        {
            var adj = qty * ctx.DropChanceMultiplier;
            sum.Items[item] = adj;
        }

        // 整轮奖励（期望）：固定 Gold/Exp + Loot 表按期望计算
        if (ctx.RunCompletedCount > 0)
        {
            sum.Gold += (long)(ctx.RunRewardGold * ctx.RunCompletedCount);
            sum.Exp += (long)(ctx.RunRewardExp * ctx.RunCompletedCount);

            if (!string.IsNullOrWhiteSpace(ctx.RunRewardLootTableId) &&
                EconomyRegistry.TryGetLootTable(ctx.RunRewardLootTableId!, out var table))
            {
                foreach (var e in table.Entries)
                {
                    var rollsTotal = Math.Max(0, ctx.RunRewardLootRolls) * ctx.RunCompletedCount;
                    if (rollsTotal <= 0) continue;
                    var avgQty = (e.QuantityMin + e.QuantityMax) / 2.0;
                    var expected = rollsTotal * e.DropChance * avgQty;
                    if (!sum.Items.ContainsKey(e.ItemId)) sum.Items[e.ItemId] = 0;
                    sum.Items[e.ItemId] += expected;
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

    // 抽样：考虑 Rolls、倍率（概率倍率通过重复试验实现）与整轮奖励（按 rolls*runCount 抽样）
    public static RewardSummary ComputeSampledWithContext(IDictionary<string, int> killCounts, EconomyContext ctx)
    {
        if (ctx.Seed is null) throw new InvalidOperationException("Sampled computation requires EconomyContext.Seed.");

        var rng = new RngContext(Hash64(ctx.Seed.Value ^ ctx.Salt));
        var sum = new RewardSummary();

        // 敌人击杀掉落（抽样）
        foreach (var (tag, cnt) in killCounts)
        {
            if (!tag.StartsWith("kill.", StringComparison.Ordinal)) continue;
            var enemyId = tag["kill.".Length..];
            if (!EconomyRegistry.TryGetEnemyEconomy(enemyId, out var eco)) continue;

            // 金币/经验倍率
            sum.Gold += (long)Math.Round(eco.Gold * ctx.GoldMultiplier) * cnt;
            sum.Exp += (long)Math.Round(eco.Exp * ctx.ExpMultiplier) * cnt;

            if (eco.LootTableId is not null && EconomyRegistry.TryGetLootTable(eco.LootTableId, out var table))
            {
                for (int i = 0; i < cnt; i++)
                {
                    foreach (var e in table.Entries)
                    {
                        var rolls = Math.Max(1, e.Rolls);
                        for (int r = 0; r < rolls; r++)
                        {
                            var chance = Math.Clamp(e.DropChance * ctx.DropChanceMultiplier, 0.0, 1.0);
                            if (rng.NextDouble() <= chance)
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

        // 整轮奖励（抽样）
        if (ctx.RunCompletedCount > 0)
        {
            sum.Gold += (long)(ctx.RunRewardGold * ctx.RunCompletedCount);
            sum.Exp += (long)(ctx.RunRewardExp * ctx.RunCompletedCount);

            if (!string.IsNullOrWhiteSpace(ctx.RunRewardLootTableId) &&
                EconomyRegistry.TryGetLootTable(ctx.RunRewardLootTableId!, out var table))
            {
                var rollsTotal = Math.Max(0, ctx.RunRewardLootRolls) * ctx.RunCompletedCount;
                for (int r = 0; r < rollsTotal; r++)
                {
                    foreach (var e in table.Entries)
                    {
                        var chance = Math.Clamp(e.DropChance * ctx.DropChanceMultiplier, 0.0, 1.0);
                        if (rng.NextDouble() <= chance)
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

        return sum;
    }

    private static ulong Hash64(ulong x)
    {
        x ^= x >> 12; x ^= x << 25; x ^= x >> 27;
        return x * 2685821657736338717UL;
    }
}