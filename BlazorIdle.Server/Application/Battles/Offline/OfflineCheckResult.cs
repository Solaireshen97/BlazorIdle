using System;

namespace BlazorIdle.Server.Application.Battles.Offline;

/// <summary>
/// 离线检查结果
/// 用于前端展示离线收益预览
/// </summary>
public sealed class OfflineCheckResult
{
    /// <summary>是否有离线时间</summary>
    public bool HasOfflineTime { get; init; }
    
    /// <summary>离线总时长（秒）</summary>
    public double OfflineSeconds { get; init; }
    
    /// <summary>离线时是否有运行中的计划</summary>
    public bool HasRunningPlan { get; init; }
    
    /// <summary>结算结果（如果有运行计划）</summary>
    public OfflineFastForwardResult? Settlement { get; init; }
    
    /// <summary>计划是否完成</summary>
    public bool PlanCompleted { get; init; }
    
    /// <summary>是否启动了下一个计划</summary>
    public bool NextPlanStarted { get; init; }
    
    /// <summary>下一个计划ID（如果自动启动）</summary>
    public Guid? NextPlanId { get; init; }
}
