using BlazorIdle.Server.Application.Abstractions;
using BlazorIdle.Server.Domain.Activities;
using BlazorIdle.Server.Domain.Characters;
using BlazorIdle.Server.Domain.Combat.Enemies;
using BlazorIdle.Server.Domain.Combat.Rng;
using BlazorIdle.Server.Domain.Economy;
using BlazorIdle.Server.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace BlazorIdle.Server.Application.Battles.Offline;

public sealed class OfflineSettleResult
{
    public Guid CharacterId { get; init; }
    public double SimulatedSeconds { get; init; }
    public long TotalDamage { get; init; }
    public int TotalKills { get; init; }
    public string Mode { get; init; } = "continuous";
    public string EnemyId { get; init; } = "dummy";
    public int EnemyCount { get; init; } = 1;
    public string? DungeonId { get; init; }

    public string DropMode { get; init; } = "expected";
    public long Gold { get; init; }
    public long Exp { get; init; }
    public Dictionary<string, double> LootExpected { get; init; } = new();
    public Dictionary<string, int> LootSampled { get; init; } = new();
}

/// <summary>
/// 离线检查结果（用于登录时自动检测）
/// </summary>
public sealed class OfflineCheckResult
{
    public bool HasOfflineTime { get; init; }
    public double OfflineSeconds { get; init; }
    public bool HasRunningPlan { get; init; }
    public OfflineFastForwardResult? Settlement { get; init; }
    public bool PlanCompleted { get; init; }
    public bool NextPlanStarted { get; init; }
    public Guid? NextPlanId { get; init; }
}

public sealed class OfflineSettlementService
{
    private readonly ICharacterRepository _characters;
    private readonly BattleSimulator _simulator;
    private readonly IActivityPlanRepository _plans;
    private readonly OfflineFastForwardEngine _engine;
    private readonly GameDbContext _db;
    private readonly Func<Guid, CancellationToken, Task<ActivityPlan?>>? _tryStartNextPlan;

    public OfflineSettlementService(
        ICharacterRepository characters, 
        BattleSimulator simulator,
        IActivityPlanRepository plans,
        OfflineFastForwardEngine engine,
        GameDbContext db,
        Func<Guid, CancellationToken, Task<ActivityPlan?>>? tryStartNextPlan = null)
    {
        _characters = characters;
        _simulator = simulator;
        _plans = plans;
        _engine = engine;
        _db = db;
        _tryStartNextPlan = tryStartNextPlan;
    }

    /// <summary>
    /// 用户登录时自动检测并结算离线收益（不立即发放，返回结算结果供前端展示）
    /// </summary>
    public async Task<OfflineCheckResult> CheckAndSettleAsync(
        Guid characterId,
        CancellationToken ct = default)
    {
        var character = await _characters.GetAsync(characterId, ct);
        if (character is null)
            throw new InvalidOperationException("Character not found");

        // 1. 计算离线时长
        var offlineSeconds = CalculateOfflineDuration(character);
        if (offlineSeconds <= 0)
        {
            // 更新心跳时间
            character.LastSeenAtUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);

            return new OfflineCheckResult
            {
                HasOfflineTime = false,
                OfflineSeconds = 0
            };
        }

        // 2. 查找离线时正在运行的计划
        var runningPlan = await _plans.GetRunningPlanAsync(characterId, ct);
        if (runningPlan is null)
        {
            // 没有活动计划，仅更新LastSeenAt
            character.LastSeenAtUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);

            return new OfflineCheckResult
            {
                HasOfflineTime = true,
                OfflineSeconds = offlineSeconds,
                HasRunningPlan = false
            };
        }

        // 3. 使用 OfflineFastForwardEngine 快进模拟（保持无感继承效果）
        var result = _engine.FastForward(character, runningPlan, offlineSeconds);

        // 4. 更新计划状态（已在 FastForward 中完成，但需要持久化）
        await _plans.UpdateAsync(runningPlan, ct);

        // 5. 更新角色时间戳
        character.LastSeenAtUtc = DateTime.UtcNow;
        character.LastOfflineSettledAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        // 6. 如果计划完成，尝试启动下一个（实现自动衔接）
        Guid? nextPlanId = null;
        bool nextPlanStarted = false;
        if (result.PlanCompleted && _tryStartNextPlan is not null)
        {
            var nextPlan = await _tryStartNextPlan(characterId, ct);
            if (nextPlan is not null)
            {
                nextPlanId = nextPlan.Id;
                nextPlanStarted = true;
            }
        }

        return new OfflineCheckResult
        {
            HasOfflineTime = true,
            OfflineSeconds = offlineSeconds,
            HasRunningPlan = true,
            Settlement = result,
            PlanCompleted = result.PlanCompleted,
            NextPlanStarted = nextPlanStarted,
            NextPlanId = nextPlanId
        };
    }

    /// <summary>
    /// 应用离线结算，实际发放收益到角色（前端确认后调用）
    /// </summary>
    public async Task ApplySettlementAsync(
        Guid characterId,
        OfflineFastForwardResult settlement,
        CancellationToken ct = default)
    {
        var character = await _db.Characters.FindAsync(new object[] { characterId }, ct);
        if (character is null)
            throw new InvalidOperationException("Character not found");

        // 发放金币和经验
        character.Gold += settlement.Gold;
        character.Experience += settlement.Exp;
        
        // 发放物品（如果有背包系统）
        // TODO: 当背包系统完善后，添加物品发放逻辑
        // if (settlement.LootSampled.Any())
        // {
        //     foreach (var (itemId, quantity) in settlement.LootSampled.Where(kv => kv.Value > 0))
        //     {
        //         var existing = await _db.InventoryItems
        //             .FirstOrDefaultAsync(i => i.CharacterId == characterId && i.ItemId == itemId, ct);
        //         if (existing != null)
        //         {
        //             existing.Quantity += quantity;
        //             existing.UpdatedAt = DateTime.UtcNow;
        //         }
        //         else
        //         {
        //             _db.InventoryItems.Add(new InventoryItem
        //             {
        //                 Id = Guid.NewGuid(),
        //                 CharacterId = characterId,
        //                 ItemId = itemId,
        //                 Quantity = quantity,
        //                 CreatedAt = DateTime.UtcNow,
        //                 UpdatedAt = DateTime.UtcNow
        //             });
        //         }
        //     }
        // }

        await _db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// 计算离线时长（秒）
    /// </summary>
    private double CalculateOfflineDuration(Character character)
    {
        if (!character.LastSeenAtUtc.HasValue)
            return 0;

        var now = DateTime.UtcNow;
        var lastSeen = character.LastSeenAtUtc.Value;
        return (now - lastSeen).TotalSeconds;
    }

    // 新增 dropMode: "expected" | "sampled"
    public async Task<OfflineSettleResult> SimulateAsync(
        Guid characterId,
        TimeSpan offlineDuration,
        string? mode = "continuous",
        string? enemyId = "dummy",
        int enemyCount = 1,
        string? dungeonId = null,
        ulong? seed = null,
        string? dropMode = "expected",
        CancellationToken ct = default)
    {
        var c = await _characters.GetAsync(characterId, ct) ?? throw new InvalidOperationException("Character not found");

        var profession = c.Profession;
        var baseStats = ProfessionBaseStatsRegistry.Resolve(profession);
        var attrs = new PrimaryAttributes(c.Strength, c.Agility, c.Intellect, c.Stamina);
        var derived = StatsBuilder.BuildDerived(profession, attrs);
        var stats = StatsBuilder.Combine(baseStats, derived);

        var enemyDef = EnemyRegistry.Resolve(enemyId);
        var seconds = Math.Max(1.0, offlineDuration.TotalSeconds);
        ulong finalSeed = seed ?? DeriveSeed(characterId);

        // 使用 BattleSimulator 统一创建和执行
        var config = new BattleSimulator.BattleConfig
        {
            BattleId = Guid.NewGuid(),
            CharacterId = characterId,
            Profession = profession,
            Stats = stats,
            Seed = finalSeed,
            EnemyDef = enemyDef,
            EnemyCount = Math.Max(1, enemyCount),
            Mode = mode ?? "continuous",
            DungeonId = dungeonId
        };

        var rb = _simulator.CreateRunningBattle(config, seconds);
        rb.FastForwardTo(seconds);

        long totalDamage = 0;
        int kills = 0;
        int runCompleted = 0;
        var killCount = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var s in rb.Segments)
        {
            if (s.TagCounters.TryGetValue("dungeon_run_complete", out var rc)) runCompleted += rc;
            foreach (var (tag, val) in s.TagCounters)
            {
                if (!tag.StartsWith("kill.", StringComparison.Ordinal)) continue;
                if (!killCount.ContainsKey(tag)) killCount[tag] = 0;
                killCount[tag] += val;
            }
        }

        // 构建 ctx
        var ctx = new EconomyContext
        {
            GoldMultiplier = 1.0,
            ExpMultiplier = 1.0,
            DropChanceMultiplier = 1.0,
            RunCompletedCount = runCompleted,
            Seed = finalSeed
        };

        if (!string.IsNullOrWhiteSpace(dungeonId))
        {
            var d = DungeonRegistry.Resolve(dungeonId!);
            ctx = new EconomyContext
            {
                GoldMultiplier = d.GoldMultiplier,
                ExpMultiplier = d.ExpMultiplier,
                DropChanceMultiplier = d.DropChanceMultiplier,
                RunCompletedCount = runCompleted,
                RunRewardGold = d.RunRewardGold,
                RunRewardExp = d.RunRewardExp,
                RunRewardLootTableId = d.RunRewardLootTableId,
                RunRewardLootRolls = d.RunRewardLootRolls,
                Seed = finalSeed
            };
        }

        var dm = (dropMode ?? "expected").Trim().ToLowerInvariant();
        long gold; long exp; Dictionary<string, double> lootExp = new(); Dictionary<string, int> lootSmp = new();
        if (dm == "sampled")
        {
            var r = EconomyCalculator.ComputeSampledWithContext(killCount, ctx);
            gold = r.Gold; exp = r.Exp;
            lootSmp = r.Items.ToDictionary(kv => kv.Key, kv => (int)Math.Round(kv.Value));
            dm = "sampled";
        }
        else
        {
            var r = EconomyCalculator.ComputeExpectedWithContext(killCount, ctx);
            gold = r.Gold; exp = r.Exp;
            lootExp = r.Items;
            dm = "expected";
        }

        return new OfflineSettleResult
        {
            CharacterId = characterId,
            SimulatedSeconds = seconds,
            TotalDamage = totalDamage,
            TotalKills = kills,
            Mode = mode ?? "continuous",
            EnemyId = enemyDef.Id,
            EnemyCount = Math.Max(1, enemyCount),
            DungeonId = dungeonId,
            DropMode = dm,
            Gold = gold,
            Exp = exp,
            LootExpected = lootExp,
            LootSampled = lootSmp
        };
    }

    private static ulong DeriveSeed(Guid characterId)
    {
        var baseRng = RngContext.FromGuid(characterId);
        baseRng.Skip(4);
        ulong salt = (ulong)DateTime.UtcNow.Ticks;
        return RngContext.Hash64(baseRng.NextUInt64() ^ salt);
    }
}