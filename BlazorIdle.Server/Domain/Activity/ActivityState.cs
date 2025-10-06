namespace BlazorIdle.Server.Domain.Activity;

/// <summary>
/// 活动计划状态机：Pending → Running → Completed/Cancelled
/// </summary>
public enum ActivityState
{
    /// <summary>等待执行</summary>
    Pending = 0,
    
    /// <summary>正在执行</summary>
    Running = 1,
    
    /// <summary>已完成</summary>
    Completed = 2,
    
    /// <summary>已取消</summary>
    Cancelled = 3
}
