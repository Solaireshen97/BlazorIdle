using System;
using System.Collections.Generic;

namespace BlazorIdle.Server.Application.Battles.Offline;

/// <summary>
/// 离线快进模拟结果
/// 包含模拟时长、收益统计和计划状态信息
/// </summary>
public sealed class OfflineFastForwardResult
{
    /// <summary>角色ID</summary>
    public Guid CharacterId { get; init; }
    
    /// <summary>计划ID</summary>
    public Guid PlanId { get; init; }
    
    /// <summary>实际模拟时长（秒）</summary>
    public double SimulatedSeconds { get; init; }
    
    /// <summary>计划是否已完成</summary>
    public bool PlanCompleted { get; init; }
    
    /// <summary>总伤害</summary>
    public long TotalDamage { get; init; }
    
    /// <summary>总击杀数</summary>
    public int TotalKills { get; init; }
    
    /// <summary>金币收益</summary>
    public long Gold { get; init; }
    
    /// <summary>经验收益</summary>
    public long Exp { get; init; }
    
    /// <summary>物品掉落（预期值）</summary>
    public Dictionary<string, double> Loot { get; init; } = new();
    
    /// <summary>更新后的已执行时长（秒）</summary>
    public double UpdatedExecutedSeconds { get; init; }
}
