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
    
    // 实时战斗信息
    public int PlayerMaxHp { get; set; }
    public double PlayerHpPercent { get; set; } = 1.0;
    public List<EnemyHealthStatusDto> Enemies { get; set; } = new();
    public double? NextAttackAt { get; set; }
    public double? NextSpecialAt { get; set; }
    public double CurrentTime { get; set; }
    public PollingHint? PollingHint { get; set; }
}

// 敌人血量状态
public sealed class EnemyHealthStatusDto
{
    public string EnemyId { get; set; } = "";
    public string EnemyName { get; set; } = "";
    public int CurrentHp { get; set; }
    public int MaxHp { get; set; }
    public double HpPercent { get; set; }
    public bool IsDead { get; set; }
}

// 轮询提示信息（服务器建议的轮询策略）
public sealed class PollingHint
{
    public int SuggestedIntervalMs { get; set; }
    public double? NextSignificantEventAt { get; set; }
    public bool IsStable { get; set; }
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