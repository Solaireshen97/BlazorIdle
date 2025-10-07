namespace BlazorIdle.Server.Domain.Activities;

/// <summary>
/// 活动类型枚举
/// 当前实现：Combat（持续战斗）、Dungeon（地下城）
/// 未来扩展：Gather（采集）、Craft（制作）等
/// </summary>
public enum ActivityType
{
    /// <summary>持续战斗（Continuous Combat）</summary>
    Combat = 1,
    
    /// <summary>地下城战斗（Dungeon Combat）</summary>
    Dungeon = 2,
    
    // 预留未来扩展
    // Gather = 3,
    // Craft = 4,
}
