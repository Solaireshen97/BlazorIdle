using BlazorIdle.Server.Domain.Activity;
using System.Collections.Concurrent;

namespace BlazorIdle.Server.Application.Activities;

/// <summary>
/// 活动协调器：管理所有活动计划的执行、状态转换和自动衔接
/// </summary>
public sealed class ActivityCoordinator
{
    private readonly ConcurrentDictionary<Guid, ActivityPlan> _plans = new();
    private readonly ConcurrentDictionary<Guid, ActivityExecutionContext> _contexts = new();
    private readonly ConcurrentDictionary<Guid, List<ActivitySlot>> _characterSlots = new();
    
    private readonly Dictionary<ActivityType, IActivityExecutor> _executors = new();
    
    private readonly int _slotsPerCharacter;
    
    public ActivityCoordinator(IEnumerable<IActivityExecutor> executors, int slotsPerCharacter = 3)
    {
        _slotsPerCharacter = Math.Max(1, Math.Min(5, slotsPerCharacter));
        
        foreach (var executor in executors)
        {
            _executors[executor.SupportedType] = executor;
        }
    }
    
    /// <summary>
    /// 创建活动计划并添加到指定槽位
    /// </summary>
    public ActivityPlan CreatePlan(
        Guid characterId,
        int slotIndex,
        ActivityType type,
        LimitSpec limit,
        string payloadJson)
    {
        if (slotIndex < 0 || slotIndex >= _slotsPerCharacter)
            throw new ArgumentException($"Invalid slot index: {slotIndex}. Must be 0-{_slotsPerCharacter - 1}");
        
        var plan = new ActivityPlan
        {
            CharacterId = characterId,
            SlotIndex = slotIndex,
            Type = type,
            Limit = limit,
            PayloadJson = payloadJson
        };
        
        _plans[plan.Id] = plan;
        
        // 确保角色槽位存在
        var slots = EnsureCharacterSlots(characterId);
        var slot = slots[slotIndex];
        
        // 如果槽位空闲，立即设为当前计划；否则加入队列
        if (slot.IsIdle)
        {
            slot.StartPlan(plan.Id);
        }
        else
        {
            slot.EnqueuePlan(plan.Id);
        }
        
        return plan;
    }
    
    /// <summary>
    /// 获取活动计划
    /// </summary>
    public ActivityPlan? GetPlan(Guid planId)
    {
        _plans.TryGetValue(planId, out var plan);
        return plan;
    }
    
    /// <summary>
    /// 获取角色的所有槽位
    /// </summary>
    public List<ActivitySlot> GetCharacterSlots(Guid characterId)
    {
        return EnsureCharacterSlots(characterId);
    }
    
    /// <summary>
    /// 获取指定槽位的当前计划和队列
    /// </summary>
    public (ActivityPlan? current, List<ActivityPlan> queued) GetSlotPlans(Guid characterId, int slotIndex)
    {
        var slots = EnsureCharacterSlots(characterId);
        if (slotIndex < 0 || slotIndex >= slots.Count)
            return (null, new List<ActivityPlan>());
        
        var slot = slots[slotIndex];
        
        ActivityPlan? current = null;
        if (slot.CurrentPlanId.HasValue && _plans.TryGetValue(slot.CurrentPlanId.Value, out var c))
            current = c;
        
        var queued = new List<ActivityPlan>();
        foreach (var qid in slot.QueuedPlanIds)
        {
            if (_plans.TryGetValue(qid, out var q))
                queued.Add(q);
        }
        
        return (current, queued);
    }
    
    /// <summary>
    /// 取消活动计划
    /// </summary>
    public async Task<bool> CancelPlanAsync(Guid planId, CancellationToken ct = default)
    {
        if (!_plans.TryGetValue(planId, out var plan))
            return false;
        
        // 如果正在运行，停止执行
        if (plan.State == ActivityState.Running)
        {
            if (_contexts.TryGetValue(planId, out var context))
            {
                if (_executors.TryGetValue(plan.Type, out var executor))
                {
                    await executor.StopAsync(plan, context, ct);
                }
                _contexts.TryRemove(planId, out _);
            }
            
            // 从槽位移除
            var slots = EnsureCharacterSlots(plan.CharacterId);
            var slot = slots[plan.SlotIndex];
            if (slot.CurrentPlanId == planId)
            {
                // 完成当前计划并尝试启动下一个
                var nextId = slot.FinishCurrentAndGetNext();
                if (nextId.HasValue)
                {
                    _ = Task.Run(() => TryStartPlanAsync(nextId.Value, ct), ct);
                }
            }
        }
        else if (plan.State == ActivityState.Pending)
        {
            // 从队列移除
            var slots = EnsureCharacterSlots(plan.CharacterId);
            var slot = slots[plan.SlotIndex];
            slot.RemovePlan(planId);
        }
        
        plan.Cancel();
        return true;
    }
    
    /// <summary>
    /// 推进所有活动（由 HostedService 周期性调用）
    /// </summary>
    public async Task AdvanceAllAsync(CancellationToken ct = default)
    {
        var runningPlans = _plans.Values.Where(p => p.State == ActivityState.Running).ToList();
        
        foreach (var plan in runningPlans)
        {
            if (ct.IsCancellationRequested)
                break;
            
            try
            {
                await AdvancePlanAsync(plan, ct);
            }
            catch (Exception)
            {
                // TODO: 记录日志
                // 继续处理其他活动
            }
        }
        
        // 尝试启动等待中的计划
        await TryStartPendingPlansAsync(ct);
    }
    
    /// <summary>
    /// 推进单个活动
    /// </summary>
    private async Task AdvancePlanAsync(ActivityPlan plan, CancellationToken ct)
    {
        if (!_contexts.TryGetValue(plan.Id, out var context))
            return;
        
        if (!_executors.TryGetValue(plan.Type, out var executor))
            return;
        
        // 推进执行
        await executor.AdvanceAsync(plan, context, ct);
        
        // 检查是否完成
        var completed = await executor.CheckCompletionAsync(plan, context, ct);
        
        if (completed)
        {
            // 停止并标记完成
            await executor.StopAsync(plan, context, ct);
            plan.Complete();
            _contexts.TryRemove(plan.Id, out _);
            
            // 从槽位移除并尝试启动下一个
            var slots = EnsureCharacterSlots(plan.CharacterId);
            var slot = slots[plan.SlotIndex];
            
            if (slot.CurrentPlanId == plan.Id)
            {
                var nextId = slot.FinishCurrentAndGetNext();
                if (nextId.HasValue)
                {
                    await TryStartPlanAsync(nextId.Value, ct);
                }
            }
        }
    }
    
    /// <summary>
    /// 尝试启动等待中的计划
    /// </summary>
    private async Task TryStartPendingPlansAsync(CancellationToken ct)
    {
        foreach (var (characterId, slots) in _characterSlots)
        {
            foreach (var slot in slots)
            {
                if (slot.IsIdle && slot.QueuedPlanIds.Count > 0)
                {
                    var nextId = slot.QueuedPlanIds[0];
                    slot.QueuedPlanIds.RemoveAt(0);
                    slot.StartPlan(nextId);
                    
                    await TryStartPlanAsync(nextId, ct);
                }
            }
        }
    }
    
    /// <summary>
    /// 启动单个计划
    /// </summary>
    private async Task TryStartPlanAsync(Guid planId, CancellationToken ct)
    {
        if (!_plans.TryGetValue(planId, out var plan))
            return;
        
        if (plan.State != ActivityState.Pending)
            return;
        
        if (!_executors.TryGetValue(plan.Type, out var executor))
            return;
        
        try
        {
            // 开始执行
            var context = await executor.StartAsync(plan, ct);
            _contexts[plan.Id] = context;
            
            plan.Start();
        }
        catch (Exception)
        {
            // TODO: 记录日志
            // 如果启动失败，标记为取消
            plan.Cancel();
            
            // 从槽位移除并尝试下一个
            var slots = EnsureCharacterSlots(plan.CharacterId);
            var slot = slots[plan.SlotIndex];
            
            if (slot.CurrentPlanId == planId)
            {
                var nextId = slot.FinishCurrentAndGetNext();
                if (nextId.HasValue)
                {
                    await TryStartPlanAsync(nextId.Value, ct);
                }
            }
        }
    }
    
    /// <summary>
    /// 确保角色槽位存在
    /// </summary>
    private List<ActivitySlot> EnsureCharacterSlots(Guid characterId)
    {
        return _characterSlots.GetOrAdd(characterId, cid =>
        {
            var slots = new List<ActivitySlot>();
            for (int i = 0; i < _slotsPerCharacter; i++)
            {
                slots.Add(new ActivitySlot
                {
                    SlotIndex = i,
                    CharacterId = cid
                });
            }
            return slots;
        });
    }
    
    /// <summary>
    /// 清理已完成的计划（可由定时任务调用）
    /// </summary>
    public int PruneCompletedPlans(TimeSpan olderThan)
    {
        var cutoff = DateTime.UtcNow - olderThan;
        int removed = 0;
        
        var toRemove = _plans.Values
            .Where(p => (p.State == ActivityState.Completed || p.State == ActivityState.Cancelled)
                        && p.EndedAtUtc.HasValue
                        && p.EndedAtUtc.Value < cutoff)
            .Select(p => p.Id)
            .ToList();
        
        foreach (var id in toRemove)
        {
            if (_plans.TryRemove(id, out _))
                removed++;
        }
        
        return removed;
    }
}
