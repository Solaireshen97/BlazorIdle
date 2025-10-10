using System;
using System.Collections.Generic;
using BlazorIdle.Shared.Models; // 新增：引用共享枚举 Profession

namespace BlazorIdle.Client.Services;

// Buff状态（与服务器的 BuffStatusDto 映射）
public sealed class BuffStatusDto
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public int Stacks { get; set; }
    public int MaxStacks { get; set; }
    public double RemainingSeconds { get; set; }
    public bool IsDebuff { get; set; }
}

// 技能状态（与服务器的 SkillStatusDto 映射 - Step 4: 技能系统UI）
public sealed class SkillStatusDto
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public int SlotIndex { get; set; }
    public int Priority { get; set; }
    public bool IsReady { get; set; }
    public double CooldownRemaining { get; set; }
    public int CurrentCharges { get; set; }
    public int MaxCharges { get; set; }
    public int BaseDamage { get; set; }
    public string? CostResourceId { get; set; }
    public int CostAmount { get; set; }
}

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
    
    // 轮询提示
    public PollingHint? PollingHint { get; set; }
    
    // Buff状态
    public List<BuffStatusDto> PlayerBuffs { get; set; } = new();
    public List<BuffStatusDto> EnemyBuffs { get; set; } = new();
    
    // 技能状态（Step 4: 技能系统UI）
    public List<SkillStatusDto> Skills { get; set; } = new();
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

// 轮询提示（服务器建议的轮询间隔）
public sealed class PollingHint
{
    /// <summary>建议的轮询间隔（毫秒）</summary>
    public int SuggestedIntervalMs { get; set; }
    
    /// <summary>下次重要事件发生的时间（战斗时间，秒）</summary>
    public double? NextSignificantEventAt { get; set; }
    
    /// <summary>战斗状态是否稳定（true表示可以使用较长轮询间隔）</summary>
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

// 装备系统DTO（Step 5: 装备系统UI预留）
/// <summary>装备槽位信息</summary>
public sealed class EquipmentSlotDto
{
    public string SlotType { get; set; } = "";
    public string SlotName { get; set; } = "";
    public GearInstanceDto? Item { get; set; }
    public bool IsLocked { get; set; }
}

/// <summary>装备实例</summary>
public sealed class GearInstanceDto
{
    public Guid Id { get; set; }
    public string DefinitionId { get; set; } = "";
    public string Name { get; set; } = "";
    public string Icon { get; set; } = "⚔️";
    public string Rarity { get; set; } = "Common";
    public int Tier { get; set; } = 1;
    public int ItemLevel { get; set; }
    public int QualityScore { get; set; }
    public List<AffixDto> Affixes { get; set; } = new();
    public string? SetId { get; set; }
    public Dictionary<string, double> Stats { get; set; } = new();
}

/// <summary>装备词条</summary>
public sealed class AffixDto
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public double Value { get; set; }
}

/// <summary>装备栏响应</summary>
public sealed class EquipmentResponse
{
    public Guid CharacterId { get; set; }
    public string CharacterName { get; set; } = "";
    public List<EquipmentSlotDto> Slots { get; set; } = new();
    public Dictionary<string, double> TotalStats { get; set; } = new();
    public int TotalScore { get; set; }
}

/// <summary>装备操作请求</summary>
public sealed class EquipItemRequest
{
    public Guid ItemId { get; set; }
}

/// <summary>装备操作响应</summary>
public sealed class EquipmentOperationResponse
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public EquipmentResponse? Equipment { get; set; }
}