using BlazorIdle.Server.Application.Abstractions;
using BlazorIdle.Server.Domain.Activities;
using BlazorIdle.Server.Domain.Characters;
using BlazorIdle.Server.Domain.Combat.Enemies;
using BlazorIdle.Server.Domain.Combat.Rng;
using BlazorIdle.Server.Domain.Economy;
using BlazorIdle.Server.Domain.Equipment.Services;
using BlazorIdle.Server.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
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
    private readonly EquipmentStatsIntegration _equipmentStats;
    private readonly ILogger<OfflineSettlementService> _logger;
    private readonly Func<Guid, CancellationToken, Task<ActivityPlan?>>? _tryStartNextPlan;
    private readonly Func<Guid, CancellationToken, Task<Guid>>? _startPlan;

    public OfflineSettlementService(
        ICharacterRepository characters, 
        BattleSimulator simulator,
        IActivityPlanRepository plans,
        OfflineFastForwardEngine engine,
        GameDbContext db,
        EquipmentStatsIntegration equipmentStats,
        ILogger<OfflineSettlementService> logger,
        Func<Guid, CancellationToken, Task<ActivityPlan?>>? tryStartNextPlan = null,
        Func<Guid, CancellationToken, Task<Guid>>? startPlan = null)
    {
        _characters = characters;
        _simulator = simulator;
        _plans = plans;
        _engine = engine;
        _db = db;
        _equipmentStats = equipmentStats;
        _logger = logger;
        _tryStartNextPlan = tryStartNextPlan;
        _startPlan = startPlan;
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
        {
            _logger.LogWarning("角色不存在，CharacterId={CharacterId}", characterId);
            throw new InvalidOperationException("Character not found");
        }

        // 1. 计算离线时长
        var offlineSeconds = CalculateOfflineDuration(character);
        
        _logger.LogInformation(
            "离线检查开始，CharacterId={CharacterId}, OfflineSeconds={OfflineSeconds}, LastSeenAt={LastSeenAt}",
            characterId, offlineSeconds, character.LastSeenAtUtc);

        if (offlineSeconds <= 0)
        {
            // 更新心跳时间
            character.LastSeenAtUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);

            _logger.LogDebug("无离线时间，CharacterId={CharacterId}", characterId);

            return new OfflineCheckResult
            {
                HasOfflineTime = false,
                OfflineSeconds = 0
            };
        }

        // 2. 查找离线时正在运行或暂停的计划
        var runningPlan = await _plans.GetRunningPlanAsync(characterId, ct);
        
        // 如果没有运行中的计划，检查是否有暂停的计划
        if (runningPlan is null)
        {
            // 查找暂停的计划（需要通过仓储查询）
            var allPlans = await _db.ActivityPlans
                .Where(p => p.CharacterId == characterId && p.State == ActivityState.Paused)
                .OrderByDescending(p => p.StartedAt)
                .ToListAsync(ct);
            
            runningPlan = allPlans.FirstOrDefault();
        }
        
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

        // 3. 记录计划是否处于暂停状态（用于后续恢复战斗）
        bool wasPaused = runningPlan.State == ActivityState.Paused;
        
        // 4. 使用 OfflineFastForwardEngine 快进模拟（保持无感继承效果）
        // FastForward 可以处理 Running 或 Paused 状态的计划
        var result = _engine.FastForward(character, runningPlan, offlineSeconds);

        // 5. 更新计划状态（已在 FastForward 中完成，但需要持久化）
        await _plans.UpdateAsync(runningPlan, ct);

        // 6. 更新角色时间戳
        character.LastSeenAtUtc = DateTime.UtcNow;
        character.LastOfflineSettledAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        // 7. 如果计划完成，尝试启动下一个（实现自动衔接）
        // 如果计划未完成，需要重新启动战斗以恢复 BattleId 和战斗状态
        Guid? nextPlanId = null;
        bool nextPlanStarted = false;
        
        if (result.PlanCompleted && _tryStartNextPlan is not null)
        {
            // 计划已完成，启动下一个待执行的计划
            var nextPlan = await _tryStartNextPlan(characterId, ct);
            if (nextPlan is not null)
            {
                nextPlanId = nextPlan.Id;
                nextPlanStarted = true;
            }
        }
        else if (!result.PlanCompleted && _startPlan is not null)
        {
            // 计划未完成，需要重新启动战斗以恢复 BattleId
            // 战斗状态已经在 FastForward 中更新到 BattleStateJson，StartPlanAsync 会自动加载并恢复
            // 注意：此时 runningPlan 的状态可能是 Paused 或 Running
            // StartPlanAsync 可以处理 Paused 状态的计划，但不能处理 Running 状态
            // 所以如果是 Running 状态（没有 BattleId），需要先改为 Paused
            if (runningPlan.State == ActivityState.Running && !runningPlan.BattleId.HasValue)
            {
                runningPlan.State = ActivityState.Paused;
                await _plans.UpdateAsync(runningPlan, ct);
            }
            
            try
            {
                await _startPlan(runningPlan.Id, ct);
                // 重新加载计划以获取更新后的 BattleId
                runningPlan = await _plans.GetAsync(runningPlan.Id, ct);
            }
            catch (Exception)
            {
                // 如果启动失败，计划会保持当前状态（Paused 或 Running，但 BattleId=null）
                // 用户可以手动点击恢复按钮来重试
            }
        }

        _logger.LogInformation(
            "离线结算完成，CharacterId={CharacterId}, OfflineSeconds={OfflineSeconds}, PlanCompleted={PlanCompleted}, Gold={Gold}, Exp={Exp}, TotalDamage={TotalDamage}",
            characterId, offlineSeconds, result.PlanCompleted, result.Gold, result.Exp, result.TotalDamage);

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
        var attrs = new PrimaryAttributes(c.Strength, c.Agility, c.Intellect, c.Stamina);
        // 使用装备集成服务构建包含装备加成的完整属性
        var stats = await _equipmentStats.BuildStatsWithEquipmentAsync(characterId, profession, attrs);

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
            // Phase 6: 应用强化掉落倍率
            var finalDropMultiplier = d.DropChanceMultiplier * d.EnhancedDropMultiplier;
            ctx = new EconomyContext
            {
                GoldMultiplier = d.GoldMultiplier,
                ExpMultiplier = d.ExpMultiplier,
                DropChanceMultiplier = finalDropMultiplier,
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