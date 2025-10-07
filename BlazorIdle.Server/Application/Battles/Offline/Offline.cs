using BlazorIdle.Server.Application.Abstractions;
using BlazorIdle.Server.Domain.Activities;
using BlazorIdle.Server.Domain.Characters;
using BlazorIdle.Server.Domain.Combat.Enemies;
using BlazorIdle.Server.Domain.Combat.Rng;
using BlazorIdle.Server.Domain.Economy;
using BlazorIdle.Server.Infrastructure.Persistence;
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
/// 离线检查结果
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
    private readonly IActivityPlanRepository _plans;
    private readonly BattleSimulator _simulator;
    private readonly OfflineFastForwardEngine _engine;
    private readonly GameDbContext _db;

    public OfflineSettlementService(
        ICharacterRepository characters, 
        IActivityPlanRepository plans,
        BattleSimulator simulator,
        OfflineFastForwardEngine engine,
        GameDbContext db)
    {
        _characters = characters;
        _plans = plans;
        _simulator = simulator;
        _engine = engine;
        _db = db;
    }

    /// <summary>
    /// 检查并结算离线收益（用户登录时自动调用）
    /// </summary>
    public async Task<OfflineCheckResult> CheckAndSettleAsync(
        Guid characterId,
        double maxOfflineSeconds = 43200, // 12小时上限
        CancellationToken ct = default)
    {
        // 1. 获取角色
        var character = await _characters.GetAsync(characterId, ct);
        if (character is null)
            throw new InvalidOperationException("Character not found");

        // 2. 计算离线时长
        if (character.LastSeenAtUtc is null)
        {
            // 首次登录，设置LastSeenAtUtc并返回
            character.LastSeenAtUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
            
            return new OfflineCheckResult
            {
                HasOfflineTime = false,
                OfflineSeconds = 0,
                HasRunningPlan = false
            };
        }

        var offlineSeconds = (DateTime.UtcNow - character.LastSeenAtUtc.Value).TotalSeconds;
        
        // 3. 如果离线时长 <= 0，无离线
        if (offlineSeconds <= 0)
        {
            return new OfflineCheckResult
            {
                HasOfflineTime = false,
                OfflineSeconds = 0,
                HasRunningPlan = false
            };
        }

        // 4. 查找离线时正在运行的计划
        var runningPlan = await _plans.GetRunningPlanAsync(characterId, ct);
        
        // 5. 如果没有运行计划，直接返回
        if (runningPlan is null)
        {
            // 更新LastSeenAtUtc
            character.LastSeenAtUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
            
            return new OfflineCheckResult
            {
                HasOfflineTime = true,
                OfflineSeconds = offlineSeconds,
                HasRunningPlan = false
            };
        }

        // 6. 使用OfflineFastForwardEngine进行快进
        var result = _engine.FastForward(
            character, 
            runningPlan, 
            offlineSeconds, 
            maxOfflineSeconds
        );

        // 7. 更新计划状态到数据库
        await _plans.UpdateAsync(runningPlan, ct);

        // 8. 如果计划完成，尝试启动下一个Pending计划
        ActivityPlan? nextPlan = null;
        if (result.PlanCompleted)
        {
            nextPlan = await _plans.GetNextPendingPlanAsync(characterId, ct);
            
            // 注意：这里不直接启动下一个计划，因为需要ActivityPlanService
            // 实际启动需要在Controller或更高层完成
            // 这里只返回下一个计划的ID供调用者处理
        }

        // 9. 更新LastSeenAtUtc
        character.LastSeenAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        // 10. 返回结算结果
        return new OfflineCheckResult
        {
            HasOfflineTime = true,
            OfflineSeconds = offlineSeconds,
            HasRunningPlan = true,
            Settlement = result,
            PlanCompleted = result.PlanCompleted,
            NextPlanStarted = false, // 实际启动需要在外部完成
            NextPlanId = nextPlan?.Id
        };
    }

    /// <summary>
    /// 应用离线结算，实际发放收益
    /// </summary>
    public async Task ApplySettlementAsync(
        Guid characterId,
        OfflineFastForwardResult settlement,
        CancellationToken ct = default)
    {
        var character = await _characters.GetAsync(characterId, ct);
        if (character is null)
            throw new InvalidOperationException("Character not found");

        // 更新角色金币和经验
        character.Gold += settlement.Gold;
        character.Experience += settlement.Exp;

        // TODO: 发放物品到背包（如果实现了背包系统）
        // if (settlement.LootSampled.Count > 0)
        // {
        //     await _inventory.AddItemsAsync(characterId, settlement.LootSampled, ct);
        // }

        // 保存角色数据
        await _db.SaveChangesAsync(ct);
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