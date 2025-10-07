using BlazorIdle.Server.Domain.Activities;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace BlazorIdle.Server.Application.Abstractions;

/// <summary>
/// 活动计划仓储接口
/// </summary>
public interface IActivityPlanRepository
{
    /// <summary>根据ID获取活动计划</summary>
    Task<ActivityPlan?> GetAsync(Guid id, CancellationToken ct = default);
    
    /// <summary>获取角色的所有活动计划</summary>
    Task<List<ActivityPlan>> GetByCharacterAsync(Guid characterId, CancellationToken ct = default);
    
    /// <summary>获取角色指定槽位的活动计划队列</summary>
    Task<List<ActivityPlan>> GetByCharacterAndSlotAsync(Guid characterId, int slotIndex, CancellationToken ct = default);
    
    /// <summary>添加活动计划</summary>
    Task AddAsync(ActivityPlan plan, CancellationToken ct = default);
    
    /// <summary>更新活动计划</summary>
    Task UpdateAsync(ActivityPlan plan, CancellationToken ct = default);
    
    /// <summary>删除活动计划</summary>
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    
    /// <summary>获取角色当前正在运行的活动计划</summary>
    Task<ActivityPlan?> GetRunningPlanAsync(Guid characterId, CancellationToken ct = default);
}
