using System;
using System.Collections.Generic;
using BlazorIdle.Shared.Models; // 新增：引用共享枚举 Profession

namespace BlazorIdle.Client.Services;

// Step 状态返回（服务器的 StepBattleStatusDto 映射）
public sealed class StepStatusResponse
{
    public Guid Id { get; set; }
    public Guid CharacterId { get; set; }

    // 修改：string -> Profession（与服务端一致）
    public Profession Profession { get; set; }

    public string EnemyId { get; set; } = "dummy";
    public int EnemyCount { get; set; }
    public double SimulatedSeconds { get; set; }
    public double TargetSeconds { get; set; }
    public bool Completed { get; set; }
    public int TotalDamage { get; set; }
    public double Dps { get; set; }
    public int SegmentCount { get; set; }
    public string Seed { get; set; } = "0";
    public long SeedIndexStart { get; set; }
    public long SeedIndexEnd { get; set; }
    public bool Killed { get; set; }
    public double? KillTimeSeconds { get; set; }
    public int OverkillDamage { get; set; }
    public Guid? PersistedBattleId { get; set; }

    public string? Mode { get; set; }
    public int? WaveIndex { get; set; }
    public int? RunCount { get; set; }
    public string? DungeonId { get; set; }

    // 经济
    public string? DropMode { get; set; } // "expected" | "sampled"
    public long Gold { get; set; }
    public long Exp { get; set; }
    public Dictionary<string, double> LootExpected { get; set; } = new();
    public Dictionary<string, int> LootSampled { get; set; } = new();
}

// 同步摘要返回（/api/battles/{id}/summary）
public sealed class BattleSummaryResponse
{
    public Guid Id { get; set; }
    public Guid CharacterId { get; set; }
    public int TotalDamage { get; set; }
    public double DurationSeconds { get; set; }
    public double Dps { get; set; }
    public int SegmentCount { get; set; }

    // 页面用到的字段
    public double AttackIntervalSeconds { get; set; }
    public double SpecialIntervalSeconds { get; set; }

    public string? EnemyId { get; set; }
    public string? EnemyName { get; set; }

    public bool Killed { get; set; }
    public double? KillTimeSeconds { get; set; }
    public int OverkillDamage { get; set; }

    public string? Seed { get; set; }
    public long SeedIndexStart { get; set; }
    public long SeedIndexEnd { get; set; }

    // 经济 + 地城
    public string? DropMode { get; set; }
    public long Gold { get; set; }
    public long Exp { get; set; }
    public Dictionary<string, double> LootExpected { get; set; } = new();
    public Dictionary<string, int> LootSampled { get; set; } = new();

    public string? DungeonId { get; set; }
    public int? DungeonRuns { get; set; }
}

// 离线结算返回（/api/offline/settle）
public sealed class OfflineSettleResponse
{
    public Guid CharacterId { get; set; }
    public double SimulatedSeconds { get; set; }
    public long TotalDamage { get; set; }
    public int TotalKills { get; set; }
    public string Mode { get; set; } = "continuous";
    public string EnemyId { get; set; } = "dummy";
    public int EnemyCount { get; set; } = 1;
    public string? DungeonId { get; set; }

    public string DropMode { get; set; } = "expected";
    public long Gold { get; set; }
    public long Exp { get; set; }
    public Dictionary<string, double> LootExpected { get; set; } = new();
    public Dictionary<string, int> LootSampled { get; set; } = new();
}

// 离线战斗相关 DTOs
/// <summary>
/// 离线快进结果
/// </summary>
public sealed class OfflineFastForwardResult
{
    public Guid CharacterId { get; init; }
    public Guid PlanId { get; init; }
    public double SimulatedSeconds { get; init; }      // 实际模拟时长
    public bool PlanCompleted { get; init; }           // 计划是否完成
    public long TotalDamage { get; init; }
    public int TotalKills { get; init; }
    public long Gold { get; init; }                    // 金币收益
    public long Exp { get; init; }                     // 经验收益
    public Dictionary<string, double> LootExpected { get; init; } = new();
    public Dictionary<string, int> LootSampled { get; init; } = new();
    public double UpdatedExecutedSeconds { get; init; } // 更新后的已执行时长
    public string DropMode { get; init; } = "expected";
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

/// <summary>
/// 应用离线结算请求
/// </summary>
public sealed class ApplySettlementRequest
{
    public Guid CharacterId { get; set; }
    public OfflineFastForwardResult Settlement { get; set; } = null!;
}

// 背包物品（/api/inventory/{characterId}）
public sealed class InventoryItemDto
{
    public string ItemId { get; set; } = "";
    public int Quantity { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class InventoryResponse
{
    public Guid CharacterId { get; set; }
    public string CharacterName { get; set; } = "";
    public long Gold { get; set; }
    public long Experience { get; set; }
    public List<InventoryItemDto> Items { get; set; } = new();
}