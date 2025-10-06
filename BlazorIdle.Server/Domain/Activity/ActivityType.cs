namespace BlazorIdle.Server.Domain.Activity;

/// <summary>
/// 活动类型：战斗、采集、制作等
/// </summary>
public enum ActivityType
{
    /// <summary>战斗</summary>
    Combat = 0,
    
    /// <summary>采集（未来扩展）</summary>
    Gather = 1,
    
    /// <summary>制作/生产（未来扩展）</summary>
    Craft = 2
}
