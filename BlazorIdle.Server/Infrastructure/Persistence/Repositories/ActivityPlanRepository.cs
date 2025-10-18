using BlazorIdle.Server.Application.Abstractions;
using BlazorIdle.Server.Domain.Activities;
using BlazorIdle.Server.Infrastructure.DatabaseOptimization.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace BlazorIdle.Server.Infrastructure.Persistence.Repositories;

/// <summary>
/// 活动计划仓储实现（支持缓存读取）
/// Activity plan repository implementation (with caching support)
/// 
/// 读写操作共享同一个 MemoryStateManager，确保数据一致性
/// Read and write operations share the same MemoryStateManager for data consistency
/// </summary>
public class ActivityPlanRepository : IActivityPlanRepository
{
    private readonly GameDbContext _db;
    private readonly IConfiguration _configuration;
    private readonly IMemoryStateManager<ActivityPlan>? _memoryStateManager;
    private readonly ILogger<ActivityPlanRepository>? _logger;

    public ActivityPlanRepository(
        GameDbContext db,
        IConfiguration configuration,
        IMemoryStateManager<ActivityPlan>? memoryStateManager = null,
        ILogger<ActivityPlanRepository>? logger = null)
    {
        _db = db;
        _configuration = configuration;
        _memoryStateManager = memoryStateManager;
        _logger = logger;
    }

    /// <summary>
    /// 按ID获取活动计划（支持缓存）
    /// Get activity plan by ID (with caching support)
    /// </summary>
    public async Task<ActivityPlan?> GetAsync(Guid id, CancellationToken ct = default)
    {
        var enableCaching = _configuration.GetValue<bool>(
            "CacheConfiguration:GlobalSettings:EnableReadCaching", true);
        
        if (enableCaching && _memoryStateManager != null)
        {
            // 使用缓存优先策略
            // 活动计划在写入时已使用 MemoryStateManager
            // 读取时使用同一个实例，确保读写一致
            return await _memoryStateManager.TryGetAsync(
                id,
                async (id, ct) => await _db.ActivityPlans
                    .FirstOrDefaultAsync(p => p.Id == id, ct),
                ct
            );
        }
        else
        {
            // 回退：直接查数据库
            return await _db.ActivityPlans.FirstOrDefaultAsync(p => p.Id == id, ct);
        }
    }

    /// <summary>
    /// 获取角色的所有活动计划（支持缓存）
    /// Get all activity plans for a character (with caching support)
    /// </summary>
    public async Task<List<ActivityPlan>> GetByCharacterAsync(Guid characterId, CancellationToken ct = default)
    {
        var enableCaching = _configuration.GetValue<bool>(
            "CacheConfiguration:GlobalSettings:EnableReadCaching", true);
        
        if (enableCaching && _memoryStateManager != null)
        {
            // 从缓存筛选
            var allCached = _memoryStateManager.GetAll();
            return allCached
                .Where(p => p.CharacterId == characterId)
                .OrderBy(p => p.SlotIndex)
                .ThenBy(p => p.CreatedAt)
                .ToList();
        }
        else
        {
            // 回退：直接查数据库
            return await _db.ActivityPlans
                .Where(p => p.CharacterId == characterId)
                .OrderBy(p => p.SlotIndex)
                .ThenBy(p => p.CreatedAt)
                .ToListAsync(ct);
        }
    }

    /// <summary>
    /// 获取角色指定槽位的活动计划（支持缓存）
    /// Get activity plans for a character's specific slot (with caching support)
    /// </summary>
    public async Task<List<ActivityPlan>> GetByCharacterAndSlotAsync(Guid characterId, int slotIndex, CancellationToken ct = default)
    {
        var enableCaching = _configuration.GetValue<bool>(
            "CacheConfiguration:GlobalSettings:EnableReadCaching", true);
        
        if (enableCaching && _memoryStateManager != null)
        {
            // 从缓存筛选
            var allCached = _memoryStateManager.GetAll();
            return allCached
                .Where(p => p.CharacterId == characterId && p.SlotIndex == slotIndex)
                .OrderBy(p => p.CreatedAt)
                .ToList();
        }
        else
        {
            // 回退：直接查数据库
            return await _db.ActivityPlans
                .Where(p => p.CharacterId == characterId && p.SlotIndex == slotIndex)
                .OrderBy(p => p.CreatedAt)
                .ToListAsync(ct);
        }
    }

    public async Task AddAsync(ActivityPlan plan, CancellationToken ct = default)
    {
        _db.ActivityPlans.Add(plan);
        
        // 检查是否启用内存缓冲
        var enableMemoryBuffering = _configuration.GetValue<bool>("Persistence:EnableMemoryBuffering", false);
        if (enableMemoryBuffering && _memoryStateManager != null)
        {
            // 使用内存缓冲：将实体添加到内存，标记为dirty
            _memoryStateManager.Add(plan);
            // 不立即调用 SaveChangesAsync，由 PersistenceCoordinator 批量保存
        }
        else
        {
            // 未启用内存缓冲：保持原有的立即保存行为
            await Persistence.DatabaseRetryPolicy.SaveChangesWithRetryAsync(_db, ct);
        }
    }

    public async Task UpdateAsync(ActivityPlan plan, CancellationToken ct = default)
    {
        _db.ActivityPlans.Update(plan);
        
        // 检查是否启用内存缓冲
        var enableMemoryBuffering = _configuration.GetValue<bool>("Persistence:EnableMemoryBuffering", false);
        if (enableMemoryBuffering && _memoryStateManager != null)
        {
            // 使用内存缓冲：更新内存中的实体，标记为dirty
            _memoryStateManager.Update(plan);
            // 不立即调用 SaveChangesAsync，由 PersistenceCoordinator 批量保存
        }
        else
        {
            // 未启用内存缓冲：保持原有的立即保存行为
            await Persistence.DatabaseRetryPolicy.SaveChangesWithRetryAsync(_db, ct);
        }
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var plan = await GetAsync(id, ct);
        if (plan is not null)
        {
            _db.ActivityPlans.Remove(plan);
            
            // 检查是否启用内存缓冲
            var enableMemoryBuffering = _configuration.GetValue<bool>("Persistence:EnableMemoryBuffering", false);
            if (enableMemoryBuffering && _memoryStateManager != null)
            {
                // 使用内存缓冲：从内存中移除
                _memoryStateManager.Remove(id);
                // 标记 EF Core 实体为删除状态，但不立即保存
                // 由 PersistenceCoordinator 批量保存删除操作
            }
            else
            {
                // 未启用内存缓冲：保持原有的立即保存行为
                await Persistence.DatabaseRetryPolicy.SaveChangesWithRetryAsync(_db, ct);
            }
        }
    }

    /// <summary>
    /// 获取角色正在运行的活动计划（支持缓存）
    /// Get running activity plan for a character (with caching support)
    /// </summary>
    public async Task<ActivityPlan?> GetRunningPlanAsync(Guid characterId, CancellationToken ct = default)
    {
        var enableCaching = _configuration.GetValue<bool>(
            "CacheConfiguration:GlobalSettings:EnableReadCaching", true);
        
        if (enableCaching && _memoryStateManager != null)
        {
            // 从缓存筛选
            var allCached = _memoryStateManager.GetAll();
            return allCached
                .FirstOrDefault(p => p.CharacterId == characterId && p.State == ActivityState.Running);
        }
        else
        {
            // 回退：直接查数据库
            return await _db.ActivityPlans
                .Where(p => p.CharacterId == characterId && p.State == ActivityState.Running)
                .FirstOrDefaultAsync(ct);
        }
    }

    /// <summary>
    /// 获取角色的下一个待执行计划（支持缓存）
    /// Get next pending plan for a character (with caching support)
    /// </summary>
    public async Task<ActivityPlan?> GetNextPendingPlanAsync(Guid characterId, CancellationToken ct = default)
    {
        var enableCaching = _configuration.GetValue<bool>(
            "CacheConfiguration:GlobalSettings:EnableReadCaching", true);
        
        if (enableCaching && _memoryStateManager != null)
        {
            // 从缓存筛选
            var allCached = _memoryStateManager.GetAll();
            return allCached
                .Where(p => p.CharacterId == characterId && p.State == ActivityState.Pending)
                .OrderBy(p => p.SlotIndex)
                .ThenBy(p => p.CreatedAt)
                .FirstOrDefault();
        }
        else
        {
            // 回退：直接查数据库
            return await _db.ActivityPlans
                .Where(p => p.CharacterId == characterId && p.State == ActivityState.Pending)
                .OrderBy(p => p.SlotIndex)
                .ThenBy(p => p.CreatedAt)
                .FirstOrDefaultAsync(ct);
        }
    }

    /// <summary>
    /// 获取所有正在运行的活动计划（支持缓存）
    /// Get all running activity plans (with caching support)
    /// </summary>
    public async Task<List<ActivityPlan>> GetAllRunningPlansAsync(CancellationToken ct = default)
    {
        var enableCaching = _configuration.GetValue<bool>(
            "CacheConfiguration:GlobalSettings:EnableReadCaching", true);
        
        if (enableCaching && _memoryStateManager != null)
        {
            // 从缓存筛选
            var allCached = _memoryStateManager.GetAll();
            return allCached
                .Where(p => p.State == ActivityState.Running)
                .ToList();
        }
        else
        {
            // 回退：直接查数据库
            return await _db.ActivityPlans
                .Where(p => p.State == ActivityState.Running)
                .ToListAsync(ct);
        }
    }
}
