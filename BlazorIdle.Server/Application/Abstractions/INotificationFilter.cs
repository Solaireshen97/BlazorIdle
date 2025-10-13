namespace BlazorIdle.Server.Application.Abstractions;

/// <summary>
/// 通知过滤器接口
/// 用于在发送通知前进行自定义过滤逻辑
/// </summary>
public interface INotificationFilter
{
    /// <summary>
    /// 过滤器名称
    /// </summary>
    string Name { get; }

    /// <summary>
    /// 过滤器优先级（数字越小优先级越高）
    /// </summary>
    int Priority { get; }

    /// <summary>
    /// 判断是否应该发送通知
    /// </summary>
    /// <param name="context">过滤上下文</param>
    /// <returns>如果应该发送返回 true，否则返回 false</returns>
    bool ShouldNotify(NotificationFilterContext context);
}

/// <summary>
/// 通知过滤上下文
/// </summary>
public sealed class NotificationFilterContext
{
    /// <summary>
    /// 战斗 ID
    /// </summary>
    public Guid BattleId { get; set; }

    /// <summary>
    /// 事件类型
    /// </summary>
    public string EventType { get; set; } = string.Empty;

    /// <summary>
    /// 事件数据（可选）
    /// </summary>
    public object? EventData { get; set; }

    /// <summary>
    /// 附加元数据
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();

    /// <summary>
    /// 添加元数据
    /// </summary>
    public void AddMetadata(string key, object value)
    {
        Metadata[key] = value;
    }

    /// <summary>
    /// 获取元数据
    /// </summary>
    public T? GetMetadata<T>(string key)
    {
        if (Metadata.TryGetValue(key, out var value) && value is T typedValue)
        {
            return typedValue;
        }
        return default;
    }
}
