namespace BlazorIdle.Server.Domain.Activities;

/// <summary>
/// 活动限制类型
/// </summary>
public enum LimitType
{
    /// <summary>时长限制（秒）</summary>
    Duration = 1,
    
    /// <summary>无限制（一直执行直到手动停止）</summary>
    Infinite = 2,
    
    // 预留未来扩展
    // Count = 3,  // 击杀数量限制
}
