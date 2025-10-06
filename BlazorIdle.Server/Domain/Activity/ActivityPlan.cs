namespace BlazorIdle.Server.Domain.Activity;

/// <summary>
/// 活动计划实体：包含类型、状态、限制、载荷等信息
/// </summary>
public sealed class ActivityPlan
{
    public Guid Id { get; init; } = Guid.NewGuid();
    
    /// <summary>所属角色ID</summary>
    public Guid CharacterId { get; init; }
    
    /// <summary>所属槽位索引（0-based）</summary>
    public int SlotIndex { get; set; }
    
    /// <summary>活动类型</summary>
    public ActivityType Type { get; init; }
    
    /// <summary>当前状态</summary>
    public ActivityState State { get; private set; } = ActivityState.Pending;
    
    /// <summary>限制规格</summary>
    public LimitSpec Limit { get; init; } = new InfiniteLimit();
    
    /// <summary>活动特定载荷（JSON序列化，如 {enemyId, dungeonId, ...}）</summary>
    public string PayloadJson { get; set; } = "{}";
    
    /// <summary>创建时间（UTC）</summary>
    public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;
    
    /// <summary>开始执行时间（UTC）</summary>
    public DateTime? StartedAtUtc { get; private set; }
    
    /// <summary>完成/取消时间（UTC）</summary>
    public DateTime? EndedAtUtc { get; private set; }
    
    /// <summary>当前进度</summary>
    public ActivityProgress Progress { get; private set; } = new();
    
    /// <summary>执行上下文（运行时数据，如 battleId）</summary>
    public Dictionary<string, object> RuntimeContext { get; init; } = new();
    
    /// <summary>
    /// 开始执行
    /// </summary>
    public void Start()
    {
        if (State != ActivityState.Pending)
            throw new InvalidOperationException($"Cannot start activity in state {State}");
        
        State = ActivityState.Running;
        StartedAtUtc = DateTime.UtcNow;
    }
    
    /// <summary>
    /// 标记为已完成
    /// </summary>
    public void Complete()
    {
        if (State != ActivityState.Running)
            throw new InvalidOperationException($"Cannot complete activity in state {State}");
        
        State = ActivityState.Completed;
        EndedAtUtc = DateTime.UtcNow;
    }
    
    /// <summary>
    /// 标记为已取消
    /// </summary>
    public void Cancel()
    {
        if (State == ActivityState.Completed)
            throw new InvalidOperationException("Cannot cancel completed activity");
        
        State = ActivityState.Cancelled;
        EndedAtUtc = DateTime.UtcNow;
    }
    
    /// <summary>
    /// 更新进度
    /// </summary>
    public void UpdateProgress(double simulatedSeconds, int completedCount)
    {
        Progress.SimulatedSeconds = simulatedSeconds;
        Progress.CompletedCount = completedCount;
    }
    
    /// <summary>
    /// 检查是否已达到限制
    /// </summary>
    public bool IsLimitReached()
    {
        return Limit.IsReached(Progress);
    }
}
