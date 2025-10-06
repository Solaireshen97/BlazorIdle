using BlazorIdle.Server.Domain.Activity;

namespace BlazorIdle.Server.Application.Activities;

/// <summary>
/// 活动执行器接口：不同类型活动（战斗、采集、制作）的执行逻辑
/// </summary>
public interface IActivityExecutor
{
    /// <summary>
    /// 支持的活动类型
    /// </summary>
    ActivityType SupportedType { get; }
    
    /// <summary>
    /// 开始执行活动
    /// </summary>
    Task<ActivityExecutionContext> StartAsync(ActivityPlan plan, CancellationToken ct = default);
    
    /// <summary>
    /// 推进活动执行（周期性调用）
    /// </summary>
    Task AdvanceAsync(ActivityPlan plan, ActivityExecutionContext context, CancellationToken ct = default);
    
    /// <summary>
    /// 停止活动执行
    /// </summary>
    Task StopAsync(ActivityPlan plan, ActivityExecutionContext context, CancellationToken ct = default);
    
    /// <summary>
    /// 检查活动是否完成（达到限制条件）
    /// </summary>
    Task<bool> CheckCompletionAsync(ActivityPlan plan, ActivityExecutionContext context, CancellationToken ct = default);
}

/// <summary>
/// 活动执行上下文：存储执行过程中的运行时数据
/// </summary>
public sealed class ActivityExecutionContext
{
    /// <summary>活动计划ID</summary>
    public Guid PlanId { get; init; }
    
    /// <summary>底层执行ID（如 battleId, gatherSessionId 等）</summary>
    public Guid? UnderlyingExecutionId { get; set; }
    
    /// <summary>开始时间（wall clock）</summary>
    public DateTime StartedAtUtc { get; init; } = DateTime.UtcNow;
    
    /// <summary>最后更新时间（wall clock）</summary>
    public DateTime LastUpdatedAtUtc { get; set; } = DateTime.UtcNow;
    
    /// <summary>自定义上下文数据</summary>
    public Dictionary<string, object> Data { get; init; } = new();
}
