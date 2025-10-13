using BlazorIdle.Server.Application.Abstractions;

namespace BlazorIdle.Server.Services;

/// <summary>
/// 通知过滤器管道
/// 按优先级顺序执行多个过滤器
/// </summary>
public sealed class NotificationFilterPipeline
{
    private readonly List<INotificationFilter> _filters = new();
    private readonly ILogger<NotificationFilterPipeline> _logger;

    public NotificationFilterPipeline(
        IEnumerable<INotificationFilter> filters,
        ILogger<NotificationFilterPipeline> logger)
    {
        _logger = logger;
        
        // 按优先级排序
        _filters.AddRange(filters.OrderBy(f => f.Priority));
    }

    /// <summary>
    /// 执行过滤器管道
    /// </summary>
    /// <param name="context">过滤上下文</param>
    /// <returns>如果所有过滤器都通过返回 true，否则返回 false</returns>
    public bool Execute(NotificationFilterContext context)
    {
        foreach (var filter in _filters)
        {
            try
            {
                if (!filter.ShouldNotify(context))
                {
                    _logger.LogDebug(
                        "通知被过滤器 {FilterName} 阻止: BattleId={BattleId}, EventType={EventType}",
                        filter.Name,
                        context.BattleId,
                        context.EventType
                    );
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "过滤器 {FilterName} 执行失败，默认允许通知: BattleId={BattleId}, EventType={EventType}",
                    filter.Name,
                    context.BattleId,
                    context.EventType
                );
                // 过滤器异常时默认允许通知，避免影响正常功能
            }
        }

        return true;
    }

    /// <summary>
    /// 获取已注册的过滤器数量
    /// </summary>
    public int FilterCount => _filters.Count;

    /// <summary>
    /// 获取所有过滤器名称
    /// </summary>
    public IEnumerable<string> GetFilterNames()
    {
        return _filters.Select(f => f.Name);
    }
}
