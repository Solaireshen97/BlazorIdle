using System;
using System.Collections.Generic;

namespace BlazorIdle.Server.Application.Battles.Offline;

/// <summary>
/// 战斗状态快照，用于在离线/在线切换时保持战斗连续性
/// </summary>
public sealed class BattleState
{
    /// <summary>敌人血量状态列表（按遇敌组顺序）</summary>
    public List<EnemyHealthState> Enemies { get; init; } = new();
    
    /// <summary>当前波次索引（地下城模式）</summary>
    public int WaveIndex { get; init; }
    
    /// <summary>已完成的轮数（地下城循环模式）</summary>
    public int RunCount { get; init; }
    
    /// <summary>快照时间戳</summary>
    public double SnapshotAtSeconds { get; init; }
}

/// <summary>
/// 单个敌人的血量状态
/// </summary>
public sealed class EnemyHealthState
{
    /// <summary>敌人ID</summary>
    public string EnemyId { get; init; } = "dummy";
    
    /// <summary>当前血量</summary>
    public int CurrentHp { get; init; }
    
    /// <summary>最大血量</summary>
    public int MaxHp { get; init; }
    
    /// <summary>是否已死亡</summary>
    public bool IsDead { get; init; }
    
    /// <summary>击杀时间（如果已死亡）</summary>
    public double? KillTime { get; init; }
    
    /// <summary>过量伤害</summary>
    public int Overkill { get; init; }
}
