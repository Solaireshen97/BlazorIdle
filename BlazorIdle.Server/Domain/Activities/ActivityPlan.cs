using System;

namespace BlazorIdle.Server.Domain.Activities;

/// <summary>
/// 活动计划实体
/// 代表一个可执行的活动任务（如战斗、采集、制作等）
/// </summary>
public class ActivityPlan
{
    /// <summary>计划唯一标识</summary>
    public Guid Id { get; set; }
    
    /// <summary>所属角色ID</summary>
    public Guid CharacterId { get; set; }
    
    /// <summary>所属槽位索引（0-4）</summary>
    public int SlotIndex { get; set; }
    
    /// <summary>活动类型</summary>
    public ActivityType Type { get; set; }
    
    /// <summary>限制类型</summary>
    public LimitType LimitType { get; set; }
    
    /// <summary>限制值（当 LimitType=Duration 时表示秒数）</summary>
    public double? LimitValue { get; set; }
    
    /// <summary>活动状态</summary>
    public ActivityState State { get; set; }
    
    /// <summary>计划创建时间</summary>
    public DateTime CreatedAt { get; set; }
    
    /// <summary>开始执行时间</summary>
    public DateTime? StartedAt { get; set; }
    
    /// <summary>完成时间</summary>
    public DateTime? CompletedAt { get; set; }
    
    /// <summary>活动配置数据（JSON 格式，存储特定类型活动的参数）</summary>
    public string PayloadJson { get; set; } = "{}";
    
    /// <summary>关联的战斗ID（如果是战斗类活动）</summary>
    public Guid? BattleId { get; set; }
    
    /// <summary>已执行时长（秒）</summary>
    public double ExecutedSeconds { get; set; }

    /// <summary>战斗状态快照（JSON格式，用于离线/在线无缝切换）</summary>
    public string? BattleStateJson { get; set; }

    /// <summary>
    /// 检查计划是否已完成（基于限制条件）
    /// </summary>
    public bool IsLimitReached()
    {
        if (LimitType == LimitType.Infinite)
            return false;
            
        if (LimitType == LimitType.Duration && LimitValue.HasValue)
            return ExecutedSeconds >= LimitValue.Value;
            
        return false;
    }
}
