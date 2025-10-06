namespace BlazorIdle.Server.Domain.Activity;

/// <summary>
/// 活动槽位：管理角色的活动计划队列
/// 每个角色有固定数量的槽位（3-5个），每个槽位可以排队多个计划
/// </summary>
public sealed class ActivitySlot
{
    /// <summary>槽位索引</summary>
    public int SlotIndex { get; init; }
    
    /// <summary>所属角色ID</summary>
    public Guid CharacterId { get; init; }
    
    /// <summary>当前正在执行的计划ID</summary>
    public Guid? CurrentPlanId { get; private set; }
    
    /// <summary>排队中的计划列表（按顺序执行）</summary>
    public List<Guid> QueuedPlanIds { get; init; } = new();
    
    /// <summary>
    /// 是否空闲（没有正在执行的计划）
    /// </summary>
    public bool IsIdle => CurrentPlanId is null;
    
    /// <summary>
    /// 开始执行计划
    /// </summary>
    public void StartPlan(Guid planId)
    {
        if (CurrentPlanId is not null)
            throw new InvalidOperationException($"Slot {SlotIndex} is already running plan {CurrentPlanId}");
        
        CurrentPlanId = planId;
    }
    
    /// <summary>
    /// 完成当前计划，自动衔接下一个
    /// </summary>
    public Guid? FinishCurrentAndGetNext()
    {
        CurrentPlanId = null;
        
        if (QueuedPlanIds.Count > 0)
        {
            var nextId = QueuedPlanIds[0];
            QueuedPlanIds.RemoveAt(0);
            return nextId;
        }
        
        return null;
    }
    
    /// <summary>
    /// 添加计划到队列
    /// </summary>
    public void EnqueuePlan(Guid planId)
    {
        QueuedPlanIds.Add(planId);
    }
    
    /// <summary>
    /// 移除队列中的计划
    /// </summary>
    public bool RemovePlan(Guid planId)
    {
        return QueuedPlanIds.Remove(planId);
    }
    
    /// <summary>
    /// 清空队列（不影响当前正在执行的计划）
    /// </summary>
    public void ClearQueue()
    {
        QueuedPlanIds.Clear();
    }
}
