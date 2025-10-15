using System;
using System.Collections.Generic;
using BlazorIdle.Server.Domain.Combat.Rng;

namespace BlazorIdle.Server.Domain.Economy;

/// <summary>
/// 经济上下文 - 存储奖励倍率和地下城奖励信息
/// </summary>
/// <remarks>
/// <para><strong>用途</strong>：</para>
/// <list type="bullet">
/// <item>奖励倍率：金币、经验、掉落概率的全局倍率</item>
/// <item>地下城奖励：完成地下城后的固定奖励和掉落表</item>
/// <item>RNG种子：用于掉落计算的独立随机数种子</item>
/// </list>
/// 
/// <para><strong>应用场景</strong>：</para>
/// <list type="bullet">
/// <item>Buff加成：双倍金币卡、经验加成等</item>
/// <item>活动加成：限时活动掉落率提升</item>
/// <item>地下城奖励：完成后给予固定金币、经验和抽取掉落表</item>
/// </list>
/// </remarks>
public sealed class EconomyContext
{
    /// <summary>金币倍率（默认1.0）</summary>
    public double GoldMultiplier { get; set; } = 1.0;
    
    /// <summary>经验倍率（默认1.0）</summary>
    public double ExpMultiplier { get; set; } = 1.0;
    
    /// <summary>掉落概率倍率（默认1.0）- 近似为期望掉落数的倍率</summary>
    public double DropChanceMultiplier { get; set; } = 1.0;

    /// <summary>已完成的地下城轮数</summary>
    public int RunCompletedCount { get; set; } = 0;
    
    /// <summary>每轮地下城完成后的固定金币奖励</summary>
    public int RunRewardGold { get; set; } = 0;
    
    /// <summary>每轮地下城完成后的固定经验奖励</summary>
    public int RunRewardExp { get; set; } = 0;
    
    /// <summary>每轮地下城完成后抽取的掉落表ID</summary>
    public string? RunRewardLootTableId { get; set; }
    
    /// <summary>每轮地下城完成后抽取掉落表的次数</summary>
    public int RunRewardLootRolls { get; set; } = 0;

    /// <summary>掉落计算的随机数种子（可选，用于可重现的掉落）</summary>
    public ulong? Seed { get; set; }
    
    /// <summary>随机数盐值（用于增加随机性）</summary>
    public ulong Salt { get; set; } = 0xEC00_1234UL;
}

/// <summary>
/// 经济计算器 - 计算战斗奖励（金币、经验、掉落）
/// </summary>
/// <remarks>
/// <para><strong>设计理念</strong>：</para>
/// <list type="bullet">
/// <item>两种模式：期望模式（快速预览）和采样模式（实际掉落）</item>
/// <item>击杀统计：根据战斗中的击杀计数计算奖励</item>
/// <item>掉落表：支持多次抽取（Rolls）和数量范围（Min/Max）</item>
/// <item>独立RNG：掉落使用独立的随机数生成器，不影响战斗RNG</item>
/// </list>
/// 
/// <para><strong>核心职责</strong>：</para>
/// <list type="number">
/// <item>计算期望奖励：不消耗RNG，用于快速预览和统计</item>
/// <item>采样实际掉落：消耗RNG，生成实际掉落物品</item>
/// <item>应用奖励倍率：根据EconomyContext中的倍率调整奖励</item>
/// <item>处理地下城奖励：完成轮数后的额外固定奖励和掉落</item>
/// </list>
/// 
/// <para><strong>期望值计算</strong>：</para>
/// <code>
/// 期望金币 = Σ(敌人金币 × 击杀数) × 金币倍率
/// 期望经验 = Σ(敌人经验 × 击杀数) × 经验倍率
/// 期望物品 = Σ(抽取次数 × 掉落概率 × 平均数量 × 击杀数) × 掉落倍率
/// </code>
/// 
/// <para><strong>采样模式</strong>：</para>
/// <code>
/// 对每个击杀：
///   对掉落表中的每个条目：
///     进行Rolls次抽取：
///       if (随机数 &lt; 掉落概率 × 掉落倍率):
///         数量 = 随机(Min, Max)
///         添加物品
/// </code>
/// </remarks>
public static class EconomyCalculator
{
    /// <summary>
    /// 计算期望奖励（不消耗RNG）
    /// </summary>
    /// <param name="killCounts">击杀统计字典，格式："kill.{敌人ID}" → 击杀数</param>
    /// <returns>期望奖励汇总（金币、经验、物品期望数量）</returns>
    /// <remarks>
    /// <para><strong>计算方法</strong>：</para>
    /// <list type="number">
    /// <item>遍历击杀统计，提取敌人ID</item>
    /// <item>从经济注册表获取敌人的金币、经验和掉落表</item>
    /// <item>累加金币和经验</item>
    /// <item>对掉落表中的每个条目，计算期望掉落：抽取次数 × 掉落概率 × 平均数量</item>
    /// </list>
    /// 
    /// <para><strong>用途</strong>：</para>
    /// 快速预览战斗奖励，不实际生成掉落物品，适合用于前端显示"预计收益"。
    /// </remarks>
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