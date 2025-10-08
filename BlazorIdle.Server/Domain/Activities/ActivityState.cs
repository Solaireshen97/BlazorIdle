namespace BlazorIdle.Server.Domain.Activities;

/// <summary>
/// 活动计划状态
/// 状态机：Pending → Running → Paused/Completed/Cancelled
/// </summary>
public enum ActivityState
{
    /// <summary>待执行（已创建但未开始）</summary>
    Pending = 0,
    
    /// <summary>执行中</summary>
    Running = 1,
    
    /// <summary>已完成（正常结束）</summary>
    Completed = 2,
    
    /// <summary>已取消（用户主动停止）</summary>
    Cancelled = 3,
    
    /// <summary>已暂停（玩家离线导致暂停，等待恢复）</summary>
    Paused = 4
}
