namespace BlazorIdle.Server.Infrastructure.SignalR.Models;

/// <summary>
/// 消息优先级枚举
/// 用于SignalR消息调度，确保重要消息优先发送
/// </summary>
public enum MessagePriority
{
    /// <summary>
    /// 低优先级 - 用于不紧急的通知消息
    /// 例如：系统公告、活动提醒等
    /// </summary>
    Low = 0,

    /// <summary>
    /// 普通优先级（默认） - 用于常规游戏更新
    /// 例如：背包更新、经验增加、资源变化等
    /// </summary>
    Normal = 1,

    /// <summary>
    /// 高优先级 - 用于实时游戏状态更新
    /// 例如：战斗帧数据、技能释放、伤害事件等
    /// </summary>
    High = 2,

    /// <summary>
    /// 关键优先级 - 用于紧急事件和错误通知
    /// 例如：连接中断警告、账号安全提醒、严重错误等
    /// </summary>
    Critical = 3
}
